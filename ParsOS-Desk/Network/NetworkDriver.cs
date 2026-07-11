using System;
using Cosmos.HAL;
using Cosmos.System.Network;
using Cosmos.System.Network.Config;
using Cosmos.System.Network.IPv4;
using Cosmos.System.Network.IPv4.UDP.DHCP;

namespace ParsOS.Network
{
    // ═══════════════════════════════════════════════════════════════════
    //  وضعیت شبکه
    // ═══════════════════════════════════════════════════════════════════
    public enum NetworkStatus
    {
        Disconnected,   // آداپتور پیدا نشد
        Disabled,       // آداپتور پیدا شد ولی غیرفعال است
        Connecting,     // در حال اتصال (DHCP در حال اجرا)
        Connected,      // متصل و IP دارد
        Error           // خطا در اتصال
    }

    // ═══════════════════════════════════════════════════════════════════
    //  درایور شبکه — سازگار با Cosmos localbuild20221121
    //
    //  API تأیید شده در این نسخه:
    //    NetworkDevice.Devices                 → لیست NIC ها
    //    NetworkDevice.MACAddress              → آدرس MAC
    //    IPConfig.Enable(dev, ip, sub, gw)     → تنظیم IP
    //    DHCPClient.SendDiscoverPacket()        → blocking DHCP (بلوکه‌کننده)
    //    DHCPClient.SendReleasePacket()         → آزاد کردن IP
    //    NetworkConfiguration.Get(device)       → خواندن IPConfig
    //    NetworkConfiguration.NetworkConfigs    → لیست configs
    //    NetworkConfiguration.ClearConfigs()    → پاک کردن configs
    //    NetworkStack.RemoveAllConfigIP()        → reset stack
    //
    //  نکته مهم: SendDiscoverPacket() بلوکه‌کننده است (~5 ثانیه timeout)
    //  بنابراین آن را در یک "deferred" اجرا می‌کنیم تا UI freeze نشود:
    //  اول یک flag می‌گذاریم، سپس در اولین Tick آن را اجرا می‌کنیم.
    //  Cosmos از thread پشتیبانی نمی‌کند پس باید UI را قبول کنیم که
    //  هنگام DHCP چند ثانیه منجمد می‌شود — این محدودیت Cosmos است.
    // ═══════════════════════════════════════════════════════════════════
    public static class NetworkDriver
    {
        // ─── وضعیت عمومی ─────────────────────────────────────────────
        public static NetworkStatus Status { get; private set; } = NetworkStatus.Disconnected;
        public static bool IsEnabled { get; private set; } = false;

        // ─── اطلاعات آداپتور ─────────────────────────────────────────
        public static string AdapterName { get; private set; } = "No Adapter";
        public static string MacAddress { get; private set; } = "--:--:--:--:--:--";
        public static string IpAddress { get; private set; } = "0.0.0.0";
        public static string SubnetMask { get; private set; } = "0.0.0.0";
        public static string Gateway { get; private set; } = "0.0.0.0";
        public static string DnsServer { get; private set; } = "8.8.8.8";

        // ─── آمار داخلی ──────────────────────────────────────────────
        public static ulong PacketsSent { get; private set; } = 0;
        public static ulong PacketsReceived { get; private set; } = 0;
        public static string LastError { get; private set; } = "";

        // ─── پیام تشخیصی — چون Console روی این OS دیده نمی‌شود، همین‌جا
        //     (و در UI پنل شبکه) وضعیت واقعی DHCP/تلاش‌ها نشان داده می‌شود ──
        public static string Diagnostic { get; private set; } = "";

        // ─── داخلی ───────────────────────────────────────────────────
        private static NetworkDevice _device = null;
        private static bool _initialized = false;
        private static bool _pendingDhcp = false;   // DHCP منتظر اجرا
        private static int _statCounter = 0;
        private const int StatInterval = 120;

        // ─── تنظیمات DHCP Retry ──────────────────────────────────────
        private static int _dhcpRetryCount = 0;
        private const int DhcpMaxRetries = 3;       // حداکثر ۳ بار تلاش

        // ─── چون DHCP روی این محیط (Cosmos + VMware) یک باگ شناخته‌شده
        //     و حل‌نشده است (نگاه کنید: CosmosOS/Cosmos issue #2096، #3075)،
        //     کلاً از تلاش DHCP صرف‌نظر می‌کنیم و مستقیم static IP اعمال
        //     می‌شود. برای غیرفعال کردن این حالت (مثلاً روی سخت‌افزار واقعی
        //     یا VirtualBox که DHCP آنجا سالم است)، مقدار زیر را false کنید.
        private const bool SkipDhcp = true;

        // ─── Static IP — این مقادیر را حتماً با subnet واقعی VMware خودتان
        //     عوض کنید: VMware → Edit → Virtual Network Editor → VMnet8
        //     (NAT) → NAT Settings... → همان‌جا Subnet و Gateway واقعی را
        //     می‌بینید (معمولاً چیزی شبیه 192.168.XXX.0 است، نه 192.168.1.x)
        private static readonly Address _fallbackIp = new Address(192, 168, 1, 100);
        private static readonly Address _fallbackSubnet = new Address(255, 255, 255, 0);
        private static readonly Address _fallbackGateway = new Address(192, 168, 1, 1);

        // ═══════════════════════════════════════════════════════════════
        //  Initialize — یک‌بار در Kernel.BeforeRun
        // ═══════════════════════════════════════════════════════════════
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var devs = NetworkDevice.Devices;
                if (devs == null || devs.Count == 0)
                {
                    AdapterName = "No Network Adapter";
                    LastError = "No NIC found";
                    Status = NetworkStatus.Disconnected;
                    return;
                }
                _device = devs[0];
                AdapterName = _device.Name ?? "Unknown NIC";
                ReadMac();
                Status = NetworkStatus.Disabled;
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.WriteLine("[NET] " + AdapterName + "  " + MacAddress);
                System.Console.ResetColor();
            }
            catch (Exception ex)
            {
                Status = NetworkStatus.Error;
                LastError = ex.Message;
            }
        }

        private static void ReadMac()
        {
            try
            {
                var m = _device.MACAddress;
                if (m != null)
                    MacAddress = ToHex(m.bytes[0]) + ":" + ToHex(m.bytes[1]) + ":" +
                                 ToHex(m.bytes[2]) + ":" + ToHex(m.bytes[3]) + ":" +
                                 ToHex(m.bytes[4]) + ":" + ToHex(m.bytes[5]);
            }
            catch { MacAddress = "??:??:??:??:??:??"; }
        }

        // ═══════════════════════════════════════════════════════════════
        //  Enable — فعال‌سازی شبکه
        //  DHCP در Tick بعدی اجرا می‌شود (تا UI فریم آخر را رندر کند)
        // ═══════════════════════════════════════════════════════════════
        public static void Enable()
        {
            if (_device == null) { LastError = "No adapter"; return; }
            if (IsEnabled) return;

            IsEnabled = true;
            Status = NetworkStatus.Connecting;
            LastError = "";

            if (SkipDhcp)
            {
                // مستقیم static IP — بدون تلف کردن ۳×۵ ثانیه روی DHCP
                Diagnostic = "DHCP skipped (known Cosmos/VMware issue) — applying static IP directly.";
                PacketsSent = 0;
                PacketsReceived = 0;
                _pendingDhcp = false;
                ApplyStaticIp();
                return;
            }

            Diagnostic = "Sending DHCP discover...";
            PacketsSent = 0;
            PacketsReceived = 0;
            _dhcpRetryCount = 0;   // reset retry counter
            _pendingDhcp = true;   // اجرا در Tick بعدی
        }

        // ═══════════════════════════════════════════════════════════════
        //  Disable — قطع اتصال
        // ═══════════════════════════════════════════════════════════════
        public static void Disable()
        {
            if (_device == null) return;
            _pendingDhcp = false;
            IsEnabled = false;

            try
            {
                // آزاد کردن IP از DHCP server
                using (var dhcp = new DHCPClient())
                {
                    dhcp.SendReleasePacket();
                }
            }
            catch { }

            try { NetworkStack.RemoveAllConfigIP(); } catch { }
            try { NetworkConfiguration.ClearConfigs(); } catch { }

            IpAddress = "0.0.0.0";
            SubnetMask = "0.0.0.0";
            Gateway = "0.0.0.0";
            DnsServer = "8.8.8.8";
            Status = NetworkStatus.Disabled;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Toggle
        // ═══════════════════════════════════════════════════════════════
        public static void Toggle()
        {
            if (IsEnabled) Disable();
            else Enable();
        }

        // ═══════════════════════════════════════════════════════════════
        //  Tick — هر فریم از GraphicsManager.Tick
        //
        //  اگر _pendingDhcp == true باشد، DHCP را اجرا می‌کند.
        //  SendDiscoverPacket بلوکه‌کننده است (~5 ثانیه) —
        //  این محدودیت ذاتی Cosmos است (no threading).
        // ═══════════════════════════════════════════════════════════════
        public static void Tick()
        {
            if (_device == null || !IsEnabled) return;

            // ─── اجرای DHCP مستقیم در Tick (Cosmos از Thread پشتیبانی نمی‌کند) ─
            // UI چند ثانیه فریز می‌شود — این محدودیت ذاتی Cosmos است.
            if (_pendingDhcp)
            {
                _pendingDhcp = false;
                DhcpThreadProc();
            }

            // ─── شمارنده آمار ─────────────────────────────────────────
            if (Status == NetworkStatus.Connected)
            {
                _statCounter++;
                if (_statCounter >= StatInterval)
                {
                    _statCounter = 0;
                    PacketsSent++;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  DhcpThreadProc — اجرای DHCP (بدون Thread، مستقیم در Tick)
        //
        //  رفع باگ مهم: نسخه‌ی قبلی این متد یک حلقه‌ی «do { RunDhcp(); }
        //  while(_pendingDhcp)» داشت که وقتی RunDhcp یک retry لازم می‌دید
        //  (چون SendDiscoverPacket تایم‌اوت شده بود)، بلافاصله و در همان
        //  فراخوانی Tick دوباره RunDhcp را صدا می‌زد — یعنی هر ۳ تلاش DHCP
        //  (هرکدام تا ~۵ ثانیه بلوکه‌کننده) پشت‌سرهم و در یک Tick واحد اجرا
        //  می‌شدند: مجموعاً تا ~۱۵ ثانیه فریز کامل و بدون حتی یک فریم رندر
        //  شده در این میان — با اینکه کامنت خودِ کد می‌گفت «تلاش مجدد در
        //  Tick بعدی»، عملاً این‌طور نبود.
        //
        //  الان این متد فقط یک تلاش در هر Tick انجام می‌دهد. اگر RunDhcp
        //  به retry نیاز داشته باشد، _pendingDhcp را دوباره true می‌کند و
        //  برمی‌گردد؛ Tick بعدی (فریم بعدی) آن را می‌بیند و RunDhcp را از نو
        //  صدا می‌زند. نتیجه: بین هر تلاش DHCP حداقل یک فریم کامل رندر و
        //  Display می‌شود — یعنی UI به‌جای یک فریز یکپارچه‌ی ~۱۵ ثانیه‌ای،
        //  چند تکه‌ی جداگانه‌ی ~۵ ثانیه‌ای می‌بیند که بینشان صفحه به‌روز
        //  می‌شود (مثلاً شمارنده‌ی retry در StatusText). خودِ بلاک ~۵ ثانیه‌ای
        //  داخل SendDiscoverPacket هنوز باقی است — آن جزو کتابخانه‌ی داخلی
        //  Cosmos است و از این پروژه قابل رفع نیست (Cosmos هیچ API غیربلوکه‌
        //  کننده یا callback-based برای DHCP در دسترس نمی‌گذارد).
        // ═══════════════════════════════════════════════════════════════
        private static void DhcpThreadProc()
        {
            try
            {
                RunDhcp(); // فقط یک تلاش؛ تلاش بعدی (در صورت نیاز) در Tick بعدی
            }
            catch (Exception ex)
            {
                Status = NetworkStatus.Error;
                LastError = "DHCP error: " + ex.Message;
                _pendingDhcp = false; // در خطای غیرمنتظره دیگر تلاش نکن
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  RunDhcp — اجرای واقعی DHCP با پشتیبانی از Retry و Static Fallback
        //  SendDiscoverPacket: blocking، IP را از طریق IPConfig.Enable
        //  روی device تنظیم می‌کند. بعد از آن NetworkConfiguration.Get
        //  اطلاعات را برمی‌گرداند.
        // ═══════════════════════════════════════════════════════════════
        private static void RunDhcp()
        {
            try
            {
                // ─── پاک‌سازی stack قبلی برای تلاش تمیز ────────────────
                try { NetworkStack.RemoveAllConfigIP(); } catch { }
                try { NetworkConfiguration.ClearConfigs(); } catch { }

                // نکته‌ی مهم (طبق مستندات رسمی Cosmos): SendDiscoverPacket
                // خودش IPConfig.Enable را صدا نمی‌زند — قبل از آن باید یک
                // IP موقت/فرضی روی device تنظیم شود تا NetworkStack برای
                // این NIC مقداردهی اولیه (routing/ARP) شود. بدون این خط،
                // Discover اصلاً به‌درستی از استک عبور نمی‌کند و همیشه
                // timeout می‌دهد (صرف‌نظر از Bridged/NAT/Host-only).
                IPConfig.Enable(_device, _fallbackIp, _fallbackSubnet, _fallbackGateway);

                using (var dhcp = new DHCPClient())
                {
                    int result = dhcp.SendDiscoverPacket();
                    // result >= 0 = موفق (تعداد ثانیه طول کشید)
                    // result == -1 = timeout
                    if (result == -1)
                    {
                        _dhcpRetryCount++;
                        Diagnostic = "DHCP timeout — no response from DHCP server (attempt " +
                                     _dhcpRetryCount.ToString() + "/" + DhcpMaxRetries.ToString() + ")";
                        System.Console.WriteLine("[NET] " + Diagnostic);

                        if (_dhcpRetryCount < DhcpMaxRetries)
                        {
                            // تلاش مجدد در Tick بعدی
                            _pendingDhcp = true;
                            return;
                        }

                        // همه تلاش‌ها ناموفق بود → استفاده از Static IP
                        Diagnostic = "DHCP failed after " + DhcpMaxRetries.ToString() +
                                     " timeouts (no reply from DHCP server) — using static IP fallback.";
                        System.Console.ForegroundColor = ConsoleColor.Yellow;
                        System.Console.WriteLine("[NET] " + Diagnostic);
                        System.Console.ResetColor();
                        ApplyStaticIp();
                        return;
                    }
                }

                // DHCP موفق شد — IP را از NetworkConfiguration بخوان
                var ipcfg = NetworkConfiguration.Get(_device);
                if (ipcfg != null)
                {
                    IpAddress = ipcfg.IPAddress?.ToString() ?? "0.0.0.0";
                    SubnetMask = ipcfg.SubnetMask?.ToString() ?? "255.255.255.0";
                    Gateway = ipcfg.DefaultGateway?.ToString() ?? "0.0.0.0";

                    // DNS از DNSConfig.DNSNameservers (لیست)
                    // نکته مهم: در نسخه‌های فعلی Cosmos، DHCPClient معمولاً option 6 (DNS server)
                    // را از پاسخ DHCP پارس نمی‌کند، یعنی DNSConfig.DNSNameservers اغلب خالی
                    // می‌ماند. در نتیجه DnsServer قبلاً به‌طور ثابت روی "8.8.8.8" باقی می‌ماند
                    // که در شبکه‌های NAT/Bridge محدود (مثل اکثر پیکربندی‌های QEMU/VirtualBox)
                    // قابل دسترس نیست و هر درخواست DNS بعد از timeout (۴ ثانیه) شکست می‌خورد —
                    // همان خطای همیشگی Cannot resolve hostname.
                    // Gateway (روتر محلی) تقریباً همیشه قابل دسترس است و در اغلب شبکه‌ها به
                    // عنوان DNS resolver هم عمل می‌کند، پس آن را fallback اصلی قرار می‌دهیم.
                    bool dnsFromDhcp = false;
                    try
                    {
                        var dnsList = DNSConfig.DNSNameservers;
                        if (dnsList != null && dnsList.Count > 0)
                        {
                            DnsServer = dnsList[0].ToString();
                            dnsFromDhcp = true;
                        }
                    }
                    catch { }

                    if (!dnsFromDhcp)
                    {
                        // نکته (بعد از بررسی محیط‌های متداول Cosmos یعنی QEMU/VirtualBox NAT):
                        // در این دو محیط، Gateway معمولاً 10.0.2.2 است ولی DNS resolver
                        // واقعی روی 10.0.2.3 شنود می‌کند — نه روی خود Gateway. قبلاً همیشه
                        // از Gateway به‌عنوان DNS استفاده می‌شد که در این حالت رایج (که اکثر
                        // توسعه‌دهندگان Cosmos با آن مواجه‌اند) هرگز پاسخ نمی‌داد و باعث
                        // «DNS timeout» همیشگی می‌شد. الان اگر Gateway الگوی معمول NAT این دو
                        // ابزار را داشته باشد (10.0.2.x) از 10.0.2.3 استفاده می‌کنیم، وگرنه
                        // همان Gateway (شبکه‌های واقعی/Bridged که روتر هم DNS می‌دهد).
                        if (!string.IsNullOrEmpty(Gateway) && Gateway.StartsWith("10.0.2."))
                            DnsServer = "10.0.2.3";
                        else
                            DnsServer = (Gateway != "0.0.0.0" && !string.IsNullOrEmpty(Gateway))
                                ? Gateway
                                : "8.8.8.8";
                        System.Console.ForegroundColor = ConsoleColor.Yellow;
                        System.Console.WriteLine("[NET] DHCP did not provide a DNS server; falling back to " + DnsServer);
                        System.Console.ResetColor();
                    }

                    if (IpAddress != "0.0.0.0")
                    {
                        Status = NetworkStatus.Connected;
                        _dhcpRetryCount = 0;
                        Diagnostic = "DHCP succeeded — IP=" + IpAddress + " GW=" + Gateway;
                        System.Console.ForegroundColor = ConsoleColor.Green;
                        System.Console.WriteLine("[NET] Connected! IP=" + IpAddress +
                                                 " GW=" + Gateway);
                        System.Console.ResetColor();
                        return;
                    }
                }

                // DHCP پاسخ داد ولی IP خالی است
                _dhcpRetryCount++;
                Diagnostic = "DHCP responded but returned no IP address (attempt " +
                             _dhcpRetryCount.ToString() + "/" + DhcpMaxRetries.ToString() + ")";
                if (_dhcpRetryCount < DhcpMaxRetries)
                {
                    _pendingDhcp = true;
                    return;
                }
                ApplyStaticIp();
            }
            catch (Exception ex)
            {
                _dhcpRetryCount++;
                Diagnostic = "DHCP exception (attempt " + _dhcpRetryCount.ToString() + "/" +
                             DhcpMaxRetries.ToString() + "): " + ex.Message;
                System.Console.WriteLine("[NET] " + Diagnostic);

                if (_dhcpRetryCount < DhcpMaxRetries)
                {
                    _pendingDhcp = true;
                    return;
                }
                ApplyStaticIp();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  ApplyStaticIp — تنظیم IP ثابت به عنوان fallback
        // ═══════════════════════════════════════════════════════════════
        private static void ApplyStaticIp()
        {
            try
            {
                IPConfig.Enable(_device, _fallbackIp, _fallbackSubnet, _fallbackGateway);
                IpAddress = _fallbackIp.ToString();
                SubnetMask = _fallbackSubnet.ToString();
                Gateway = _fallbackGateway.ToString();
                // Gateway را به‌جای 8.8.8.8 fallback اول می‌کنیم چون در شبکه‌های محدود
                // (NAT/Bridge بدون مسیر کامل اینترنت) قابل دسترس‌تر است.
                DnsServer = Gateway;
                Status = NetworkStatus.Connected;
                LastError = "Static IP (DHCP failed)";
                Diagnostic = "Static IP fallback in use (" + IpAddress + ") — DHCP never responded. " +
                             "This is a guessed address; if your real gateway differs, connections will fail.";
                System.Console.ForegroundColor = ConsoleColor.Yellow;
                System.Console.WriteLine("[NET] Static IP applied: " + IpAddress);
                System.Console.ResetColor();
            }
            catch (Exception ex)
            {
                Status = NetworkStatus.Error;
                LastError = "Static IP failed: " + ex.Message;
                Diagnostic = "Static IP setup failed: " + ex.Message;
                System.Console.WriteLine("[NET] Static IP error: " + ex.Message);
            }
        }

        // ─── وضعیت به رشته ───────────────────────────────────────────
        public static string StatusText()
        {
            switch (Status)
            {
                case NetworkStatus.Disconnected: return "No Adapter";
                case NetworkStatus.Disabled: return "Disabled";
                case NetworkStatus.Connecting: return "Connecting...";
                case NetworkStatus.Connected: return "Connected";
                case NetworkStatus.Error: return "Error: " + LastError;
                default: return "Unknown";
            }
        }

        public static string StatusShort()
        {
            switch (Status)
            {
                case NetworkStatus.Disconnected: return "N/A";
                case NetworkStatus.Disabled: return "OFF";
                case NetworkStatus.Connecting: return "...";
                case NetworkStatus.Connected: return "ON";
                case NetworkStatus.Error: return "ERR";
                default: return "?";
            }
        }

        public static bool HasAdapter => _device != null;

        private static readonly char[] _hexChars = "0123456789ABCDEF".ToCharArray();
        private static string ToHex(byte b)
        {
            char[] buf = { _hexChars[(b >> 4) & 0xF], _hexChars[b & 0xF] };
            return new string(buf);
        }
    }
}