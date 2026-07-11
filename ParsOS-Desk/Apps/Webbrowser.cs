using Cosmos.System;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using Cosmos.System.Network.Config;
using Cosmos.System.Network.IPv4;
using Cosmos.System.Network.IPv4.TCP;
using Cosmos.System.Network.IPv4.UDP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using ParsOS.GUI;
using ParsOS.Network;

namespace ParsOS.Apps
{
    // ═══════════════════════════════════════════════════════════════════
    //  HtmlElement — نمایانگر یک عنصر HTML پارس‌شده
    // ═══════════════════════════════════════════════════════════════════
    public class HtmlElement
    {
        public string Tag;       // "h1","p","a","b","br","img","ul","li",...
        public string Text;      // محتوای متنی
        public string Href;      // مخصوص <a>
        public string Src;       // مخصوص <img>
        public bool IsBold;
        public bool IsItalic;
        public Color FgColor;
        public bool HasColor;

        public HtmlElement(string tag, string text)
        {
            Tag = tag;
            Text = text;
            FgColor = Color.White;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  SimpleHtmlParser — پارسر HTML بسیار سبک برای Cosmos
    //  پشتیبانی: h1-h6, p, a, b, strong, i, em, br, ul, ol, li,
    //             div, span, title, body, html, head, img, hr, code
    // ═══════════════════════════════════════════════════════════════════
    public static class SimpleHtmlParser
    {
        public static List<HtmlElement> Parse(string html)
        {
            var elements = new List<HtmlElement>();
            if (string.IsNullOrEmpty(html)) return elements;

            int i = 0;
            bool inBold = false;
            bool inItalic = false;
            bool inHead = false;
            bool inScript = false;
            bool inStyle = false;
            string pageTitle = "";

            while (i < html.Length)
            {
                if (html[i] == '<')
                {
                    // پیدا کردن انتهای تگ
                    int end = html.IndexOf('>', i);
                    if (end < 0) break;

                    string tagContent = html.Substring(i + 1, end - i - 1).Trim();
                    i = end + 1;

                    bool isClose = tagContent.StartsWith("/");
                    if (isClose) tagContent = tagContent.Substring(1).Trim();

                    // نام تگ (بدون attributes)
                    string tagName = tagContent.Split(' ')[0].ToLower();

                    // پردازش تگ‌های خاص
                    if (tagName == "script") { inScript = !isClose; continue; }
                    if (tagName == "style") { inStyle = !isClose; continue; }
                    if (tagName == "head") { inHead = !isClose; continue; }
                    if (inScript || inStyle || inHead) continue;

                    if (tagName == "b" || tagName == "strong") { inBold = !isClose; continue; }
                    if (tagName == "i" || tagName == "em") { inItalic = !isClose; continue; }

                    if (tagName == "br")
                    { elements.Add(new HtmlElement("br", "")); continue; }

                    if (tagName == "hr")
                    { elements.Add(new HtmlElement("hr", "")); continue; }

                    // تگ‌هایی که محتوا دارند
                    if (!isClose)
                    {
                        string href = ExtractAttr(tagContent, "href");
                        string src = ExtractAttr(tagContent, "src");
                        string colorAttr = ExtractAttr(tagContent, "color");

                        var el = new HtmlElement(tagName, "");
                        el.Href = href;
                        el.Src = src;
                        el.IsBold = inBold;
                        el.IsItalic = inItalic;

                        if (!string.IsNullOrEmpty(colorAttr))
                        {
                            el.FgColor = ParseColor(colorAttr);
                            el.HasColor = true;
                        }

                        // برای تگ‌های بلوک، محتوا را از داخل تگ بخوان
                        switch (tagName)
                        {
                            case "h1":
                            case "h2":
                            case "h3":
                            case "h4":
                            case "h5":
                            case "h6":
                            case "p":
                            case "li":
                            case "a":
                            case "div":
                            case "span":
                            case "code":
                            case "title":
                            case "td":
                            case "th":
                                // محتوای بین تگ باز و بسته
                                string closeTag = "</" + tagName;
                                int closeIdx = html.IndexOf(closeTag, i, StringComparison.OrdinalIgnoreCase);
                                if (closeIdx >= 0)
                                {
                                    // محتوای خام (ممکن است شامل تگ‌های inline باشد)
                                    string inner = html.Substring(i, closeIdx - i);
                                    el.Text = StripInlineTags(inner);
                                    i = html.IndexOf('>', closeIdx) + 1;
                                    if (i <= 0) i = closeIdx + closeTag.Length + 1;
                                }
                                if (tagName == "title") pageTitle = el.Text;
                                else elements.Add(el);
                                break;

                            case "img":
                                elements.Add(el);
                                break;

                            case "ul":
                            case "ol":
                                elements.Add(el);
                                break;
                        }
                    }
                }
                else
                {
                    // متن خارج از تگ
                    int nextTag = html.IndexOf('<', i);
                    string text;
                    if (nextTag < 0) { text = html.Substring(i); i = html.Length; }
                    else { text = html.Substring(i, nextTag - i); i = nextTag; }

                    text = DecodeEntities(text.Trim());
                    if (!string.IsNullOrEmpty(text))
                    {
                        var el = new HtmlElement("text", text);
                        el.IsBold = inBold;
                        el.IsItalic = inItalic;
                        elements.Add(el);
                    }
                }
            }

            // اگر عنوان پیدا شد اول اضافه کن
            if (!string.IsNullOrEmpty(pageTitle))
                elements.Insert(0, new HtmlElement("page-title", pageTitle));

            return elements;
        }

        // ─── استخراج attribute از تگ ─────────────────────────────────
        private static string ExtractAttr(string tag, string attr)
        {
            string search = attr + "=\"";
            int idx = tag.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                search = attr + "='";
                idx = tag.IndexOf(search, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return "";
                idx += search.Length;
                int end = tag.IndexOf('\'', idx);
                return end < 0 ? "" : tag.Substring(idx, end - idx);
            }
            idx += search.Length;
            int endQ = tag.IndexOf('"', idx);
            return endQ < 0 ? "" : tag.Substring(idx, endQ - idx);
        }

        // ─── حذف تگ‌های inline از محتوا ─────────────────────────────
        private static string StripInlineTags(string html)
        {
            var sb = new StringBuilder();
            int i = 0;
            while (i < html.Length)
            {
                if (html[i] == '<')
                {
                    int end = html.IndexOf('>', i);
                    if (end < 0) break;
                    i = end + 1;
                }
                else sb.Append(html[i++]);
            }
            return DecodeEntities(sb.ToString().Trim());
        }

        // ─── decode entities ─────────────────────────────────────────
        private static string DecodeEntities(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("&amp;", "&");
            s = s.Replace("&lt;", "<");
            s = s.Replace("&gt;", ">");
            s = s.Replace("&quot;", "\"");
            s = s.Replace("&nbsp;", " ");
            s = s.Replace("&#39;", "'");
            return s;
        }

        // ─── parse رنگ ساده ─────────────────────────────────────────
        private static Color ParseColor(string colorStr)
        {
            if (colorStr.StartsWith("#") && colorStr.Length == 7)
            {
                try
                {
                    int r = Convert.ToInt32(colorStr.Substring(1, 2), 16);
                    int g = Convert.ToInt32(colorStr.Substring(3, 2), 16);
                    int b = Convert.ToInt32(colorStr.Substring(5, 2), 16);
                    return Color.FromArgb(r, g, b);
                }
                catch { }
            }
            switch (colorStr.ToLower())
            {
                case "red": return Color.FromArgb(220, 60, 60);
                case "green": return Color.FromArgb(60, 200, 60);
                case "blue": return Color.FromArgb(60, 120, 230);
                case "yellow": return Color.FromArgb(220, 200, 50);
                case "white": return Color.White;
                case "gray": case "grey": return Color.FromArgb(160, 160, 160);
                case "orange": return Color.FromArgb(230, 140, 40);
            }
            return Color.White;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HttpClient — کلاینت HTTP ساده برای Cosmos TCP
    //  فقط GET / بدون TLS پشتیبانی می‌کند (Cosmos محدودیت دارد)
    // ═══════════════════════════════════════════════════════════════════
    public static class SimpleHttpClient
    {
        // ─── پارس URL ─────────────────────────────────────────────────
        public static bool ParseUrl(string url, out string host, out int port, out string path)
        {
            host = ""; path = "/"; port = 80;
            if (string.IsNullOrEmpty(url)) return false;

            if (url.StartsWith("http://"))
                url = url.Substring(7);
            else if (url.StartsWith("https://"))
            {
                // HTTPS بدون TLS — هشدار می‌دهیم ولی تلاش می‌کنیم
                url = url.Substring(8);
                port = 443;
            }

            int slash = url.IndexOf('/');
            if (slash >= 0)
            {
                path = url.Substring(slash);
                host = url.Substring(0, slash);
            }
            else
            {
                host = url;
            }

            int colon = host.IndexOf(':');
            if (colon >= 0)
            {
                int.TryParse(host.Substring(colon + 1), out port);
                host = host.Substring(0, colon);
            }
            if (string.IsNullOrEmpty(path)) path = "/";
            return !string.IsNullOrEmpty(host);
        }

        // ─── جدول hosts داخلی (v2) ───────────────────────────────────────
        // نکته‌ی کلیدی بعد از بررسی CosmosOS/CosmosHttp (پکیج HTTP رسمی
        // خود اکوسیستم Cosmos که AuraOS هم برای wget از آن استفاده می‌کند):
        // حتی آن پروژه هم روی resolve خودکار hostname داخل Cosmos حساب باز
        // نمی‌کند — در مثال رسمی‌اش کاربر مستقیماً IP را می‌دهد
        // (request.IP = "34.223.124.45"; request.Domain = "neverssl.com";)
        // چون DNS-over-UDP داخل Cosmos روی هایپروایزرهای مختلف (QEMU با
        // 10.0.2.3، VirtualBox، VMware که هرکدام آدرس DNS proxy متفاوتی
        // دارند) به‌شدت غیرقابل‌اعتماد است؛ حتی DnsClient رسمی خود Cosmos
        // هم روی برخی پاسخ‌ها کرش می‌کند (ایشوی #3143 در ریپوی Cosmos،
        // بسته‌شده به‌عنوان "not planned"). پس به‌جای شرط‌بندی روی حدس زدن
        // آدرس DNS این هایپروایزر یا آن یکی، همان استراتژی که در عمل روی
        // Cosmos جواب می‌دهد را پیاده می‌کنیم: چند سایت پرکاربرد را با IP
        // از پیش شناخته‌شده (بدون هیچ DNS query‌ای) در دسترس می‌گذاریم، و
        // DNS واقعی هم به‌عنوان تلاش بهترین‌تلاش (best-effort) برای بقیه‌ی
        // آدرس‌ها باقی می‌ماند. با AddHostEntry می‌توانید موارد بیشتری اضافه
        // کنید (مثلاً IP سرور تست محلی خودتان).
        private static readonly List<(string Host, string Ip)> _hostsTable = new List<(string, string)>
        {
            ("neverssl.com", "34.223.124.45"), // همان IP در مثال رسمی CosmosHttp — تضمین‌شده کار می‌کند
            ("example.com",  "93.184.216.34"),
        };

        public static void AddHostEntry(string host, string ip)
        {
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(ip)) return;
            for (int i = 0; i < _hostsTable.Count; i++)
                if (_hostsTable[i].Host == host) { _hostsTable[i] = (host, ip); return; }
            _hostsTable.Add((host, ip));
        }

        // ─── DNS Resolve ───────────────────────────────────────────────
        // اگر host خودش IP باشد مستقیم parse می‌شود (سریع‌ترین حالت).
        // در غیر این صورت یک DNS query با UDP به سرور DNS شبکه می‌فرستیم.
        private static Cosmos.System.Network.IPv4.Address ResolveHost(string host, out string error)
        {
            error = "";
            // حالت ۱: IP مستقیم
            var direct = TryParseIp(host);
            if (direct != null) return direct;

            // حالت ۱.۵: جدول hosts داخلی — بدون هیچ DNS query‌ای، همیشه کار می‌کند
            for (int i = 0; i < _hostsTable.Count; i++)
                if (_hostsTable[i].Host == host) return TryParseIp(_hostsTable[i].Ip);

            // حالت ۲: کش — اگر قبلاً resolve شده دوباره کوئری نزن
            for (int i = 0; i < _dnsCache.Count; i++)
                if (_dnsCache[i].Host == host) return _dnsCache[i].Ip;

            // حالت ۳: DNS query واقعی روی UDP (best-effort) — به‌ترتیب چند
            // سرور را امتحان می‌کنیم. این دیگر تنها راه اتصال نیست (جدول
            // hosts بالا آن نقش را برای سایت‌های پرکاربرد پوشش می‌دهد) پس
            // شکست آن دیگر یعنی «مرورگر خراب است»، بلکه یعنی «این یک
            // hostname خاص روی این شبکه resolve نشد — از IP مستقیم یا
            // AddHostEntry استفاده کنید».
            Cosmos.System.Network.IPv4.Address resolved = null;
            var triedServers = new List<string>(5);
            string[] fallbackChain = { NetworkDriver.DnsServer, "10.0.2.3", "8.8.8.8", "1.1.1.1", "9.9.9.9" };
            var errBuilder = new StringBuilder();
            for (int fi = 0; fi < fallbackChain.Length; fi++)
            {
                string dnsSrv = fallbackChain[fi];
                if (string.IsNullOrEmpty(dnsSrv) || triedServers.Contains(dnsSrv)) continue;
                triedServers.Add(dnsSrv);

                string attemptErr;
                resolved = DnsResolve(host, dnsSrv, out attemptErr);
                if (resolved != null) { error = ""; break; }

                if (errBuilder.Length > 0) errBuilder.Append(" / ");
                errBuilder.Append(dnsSrv).Append(": ").Append(attemptErr);
            }
            if (resolved == null) error = errBuilder.ToString();
            if (resolved != null)
            {
                _dnsCache.Add((host, resolved));
                if (_dnsCache.Count > 16) _dnsCache.RemoveAt(0); // کش محدود
            }
            return resolved;
        }

        private static Cosmos.System.Network.IPv4.Address TryParseIp(string host)
        {
            var parts = host.Split('.');
            if (parts.Length == 4)
            {
                byte b0, b1, b2, b3;
                if (byte.TryParse(parts[0], out b0) &&
                    byte.TryParse(parts[1], out b1) &&
                    byte.TryParse(parts[2], out b2) &&
                    byte.TryParse(parts[3], out b3))
                    return new Cosmos.System.Network.IPv4.Address(b0, b1, b2, b3);
            }
            return null;
        }

        // کش ساده DNS — host به IP، عمر برنامه (List به‌جای Dictionary، طبق الگوی پروژه)
        private static readonly List<(string Host, Cosmos.System.Network.IPv4.Address Ip)> _dnsCache
            = new List<(string, Cosmos.System.Network.IPv4.Address)>();

        // شمارنده برای انتخاب پورت مبدأ ephemeral در هر DNS query (هرگز صفر نیست)
        private static int _udpPortCounter = 0;

        // همان الگو برای اتصال TCP — رجوع کنید به توضیح داخل Get()
        private static int _tcpPortCounter = 0;

        // ─── DNS Query دستی روی UDP (پروتکل ساده RFC1035، فقط رکورد A) ──
        private static Cosmos.System.Network.IPv4.Address DnsResolve(string host, string dnsServer, out string error)
        {
            error = "";
            UdpClient udp = null;
            try
            {
                var dnsServerIp = TryParseIp(dnsServer);
                if (dnsServerIp == null)
                {
                    error = "DNS server address invalid";
                    return null;
                }

                System.Console.WriteLine("[DNS] Querying " + host + " via " + dnsServer);

                // نکته مهم: پورت ۰ به‌عنوان local port معتبر نیست — خیلی از
                // NAT/Gatewayها (از جمله DNS proxy داخلی VMware NAT) پکت با
                // source port برابر صفر را silently drop می‌کنند. حتی وقتی
                // DnsServer درست تنظیم شده (مثلاً همان Gateway)، اگر پورت
                // مبدأ ۰ باشد پاسخ هرگز برنمی‌گردد و همیشه timeout می‌خوریم.
                // پس یک پورت ephemeral واقعی (۴۹۱۵۲-۶۵۵۳۵) انتخاب می‌کنیم.
                _udpPortCounter++;
                int srcPort = 49152 + (_udpPortCounter % 16000);

                // ─── باگ قبلی: اگر بین Connect/Send/Receive استثنایی رخ
                // می‌داد، udp.Close() هرگز فراخوانی نمی‌شد و پورت ephemeral
                // برای همیشه bound باقی می‌ماند. بعد از چندین تلاش ناموفق
                // (که در شبکه‌های محدود زیاد پیش می‌آید)، پورت‌های جدید با
                // پورت‌های leak‌شده‌ی قبلی تصادم می‌کردند و new UdpClient یا
                // Connect با یک استثنای عمومی شکست می‌خورد که به‌عنوان همان
                // پیام «Cannot resolve hostname» به کاربر نمایش داده می‌شد.
                // الان با try/finally تضمین می‌کنیم Close همیشه صدا زده شود.
                udp = new UdpClient(srcPort);
                udp.Connect(dnsServerIp, 53);

                // ساخت DNS query packet (Header + Question، فقط نوع A)
                byte[] query = BuildDnsQuery(host, out ushort txId);
                udp.Send(query);

                // انتظار پاسخ با timeout — با یک بار retransmit در نیمه‌ی راه.
                // شبکه‌های NAT/emulator گاهی تک‌بسته را drop می‌کنند؛ قبلاً
                // فقط یک‌بار پکت می‌رفت و اگر همان یکی گم می‌شد کل query
                // با «DNS timeout» شکست می‌خورد حتی اگر سرور کاملاً سالم بود.
                // ارسال مجدد همان query (همان txId) این نقطه‌ی شکست تک‌بسته‌ای
                // را برطرف می‌کند، بدون این‌که هزینه‌ی حافظه‌ای داشته باشد.
                // با اضافه شدن 10.0.2.3 به زنجیره، ممکن است تا ۵ سرور امتحان شود؛
                // برای این‌که در بدترین حالت (همه timeout) کاربر خیلی معطل نشود،
                // سقف هر سرور را از ۴ به ۲.۵ ثانیه کاهش می‌دهیم (بدترین حالت کل
                // ~۱۲.۵ ثانیه به‌جای ۲۰ ثانیه) و retransmit را زودتر می‌فرستیم.
                var source = new EndPoint(Cosmos.System.Network.IPv4.Address.Zero, 0);
                int waited = 0;
                bool resent = false;
                while (waited < 2500)
                {
                    byte[] resp = udp.NonBlockingReceive(ref source);
                    if (resp != null && resp.Length > 12)
                    {
                        var ip = ParseDnsResponse(resp, txId);
                        if (ip == null) error = "DNS: no A record found";
                        return ip;
                    }
                    if (!resent && waited >= 1200)
                    {
                        resent = true;
                        try { udp.Send(query); } catch { }
                    }
                    System.Threading.Thread.Sleep(20);
                    waited += 20;
                }
                error = "DNS timeout";
                System.Console.WriteLine("[DNS] Timeout waiting for reply from " + dnsServer);
                return null;
            }
            catch (Exception ex)
            {
                error = "DNS error: " + ex.Message;
                System.Console.WriteLine("[DNS] Exception for " + host + " via " + dnsServer + ": " + ex.Message);
                return null;
            }
            finally
            {
                try { udp?.Close(); } catch { }
            }
        }

        // ─── ساخت DNS Query packet (RFC1035) ────────────────────────────
        private static byte[] BuildDnsQuery(string host, out ushort txId)
        {
            txId = (ushort)(new Random().Next(1, 65535));
            var labels = host.Split('.');

            int qnameLen = 1; // null terminator
            for (int i = 0; i < labels.Length; i++) qnameLen += labels[i].Length + 1;

            byte[] packet = new byte[12 + qnameLen + 4]; // header + question
            int p = 0;

            // ─── Header (12 bytes) ───────────────────────────────────
            packet[p++] = (byte)(txId >> 8); packet[p++] = (byte)(txId & 0xFF);
            packet[p++] = 0x01; packet[p++] = 0x00; // flags: standard query, recursion desired
            packet[p++] = 0x00; packet[p++] = 0x01; // QDCOUNT = 1
            packet[p++] = 0x00; packet[p++] = 0x00; // ANCOUNT = 0
            packet[p++] = 0x00; packet[p++] = 0x00; // NSCOUNT = 0
            packet[p++] = 0x00; packet[p++] = 0x00; // ARCOUNT = 0

            // ─── Question: QNAME ──────────────────────────────────────
            for (int i = 0; i < labels.Length; i++)
            {
                packet[p++] = (byte)labels[i].Length;
                for (int j = 0; j < labels[i].Length; j++)
                    packet[p++] = (byte)labels[i][j];
            }
            packet[p++] = 0x00; // null terminator

            // QTYPE = A (1), QCLASS = IN (1)
            packet[p++] = 0x00; packet[p++] = 0x01;
            packet[p++] = 0x00; packet[p++] = 0x01;

            return packet;
        }

        // ─── پارس پاسخ DNS — فقط اولین رکورد A را برمی‌گرداند ──────────
        private static Cosmos.System.Network.IPv4.Address ParseDnsResponse(byte[] resp, ushort expectedTxId)
        {
            if (resp.Length < 12) return null;

            ushort txId = (ushort)((resp[0] << 8) | resp[1]);
            if (txId != expectedTxId) return null;

            int flags = (resp[2] << 8) | resp[3];
            if ((flags & 0x000F) != 0) return null; // RCODE != 0 → خطا

            int qdCount = (resp[4] << 8) | resp[5];
            int anCount = (resp[6] << 8) | resp[7];
            if (anCount < 1) return null;

            int pos = 12;
            // پرش از روی Question section
            for (int q = 0; q < qdCount; q++)
            {
                pos = SkipDnsName(resp, pos);
                pos += 4; // QTYPE + QCLASS
            }

            // پارس Answer records تا یک A record پیدا شود
            for (int a = 0; a < anCount && pos < resp.Length; a++)
            {
                pos = SkipDnsName(resp, pos);
                if (pos + 10 > resp.Length) return null;

                int type = (resp[pos] << 8) | resp[pos + 1];
                int rdLength = (resp[pos + 8] << 8) | resp[pos + 9];
                pos += 10;

                if (type == 1 && rdLength == 4) // نوع A، IPv4
                {
                    return new Cosmos.System.Network.IPv4.Address(
                        resp[pos], resp[pos + 1], resp[pos + 2], resp[pos + 3]);
                }
                pos += rdLength;
            }
            return null;
        }

        // ─── رد شدن از روی یک DNS name (با پشتیبانی compression pointer) ──
        private static int SkipDnsName(byte[] data, int pos)
        {
            while (pos < data.Length)
            {
                int len = data[pos];
                if (len == 0) { pos++; break; }
                if ((len & 0xC0) == 0xC0) { pos += 2; break; } // compression pointer
                pos += len + 1;
            }
            return pos;
        }

        // ─── GET Request ─────────────────────────────────────────────
        public static string Get(string url, out string error, int timeoutMs = 8000)
        {
            error = "";
            if (!ParseUrl(url, out string host, out int port, out string path))
            { error = "Invalid URL"; return null; }

            if (NetworkDriver.Status != NetworkStatus.Connected)
            { error = "Network not connected"; return null; }

            var ip = ResolveHost(host, out string resolveErr);
            if (ip == null)
            { error = "Cannot resolve hostname: " + host + (string.IsNullOrEmpty(resolveErr) ? "" : " (" + resolveErr + ")"); return null; }

            // نکته کلیدی (باگ نشت حافظه/سوکت): قبلاً tcp.Close() فقط در مسیر
            // موفقیت فراخوانی می‌شد. اگر tcp.Connect(...) شکست می‌خورد (بسیار
            // رایج — سرور در دسترس نیست، پورت بسته، یا هر خطای دیگر) یا هر
            // استثنایی حین ارسال/دریافت رخ می‌داد، اجرا مستقیم به catch
            // می‌پرید و Close() هرگز صدا زده نمی‌شد — یعنی هر تلاش ناموفق
            // (که هنگام تست همین حالاست: DNS/اتصال مدام fail می‌شود) یک
            // TcpClient/سوکت را برای همیشه leak می‌کرد. این دقیقاً همان
            // الگوی باگی بود که در DnsResolve با try/finally حل شد؛ همان
            // الگو اینجا هم اعمال می‌شود.
            TcpClient tcp = null;
            try
            {
                // نکته مهم (همان باگ VMware NAT که در DnsResolve فیکس شد):
                // پورت مبدأ ۰ رندوم نیست — Cosmos واقعاً با source port صفر
                // SYN می‌فرستد و روی NAT شرکت‌هایی مثل VMware این پکت بی‌صدا
                // drop می‌شود، یعنی Connect همیشه با «Failed to open TCP
                // connection!» شکست می‌خورد، حتی وقتی شبکه کاملاً وصل است.
                // پس مثل UDP یک پورت ephemeral واقعی انتخاب می‌کنیم.
                _tcpPortCounter++;
                int localPort = 49152 + (_tcpPortCounter % 16000);
                tcp = new TcpClient(localPort);

                // Connect — blocking با timeout داخلی 5 ثانیه
                tcp.Connect(ip, port);

                // ارسال HTTP GET request
                string request =
                    "GET " + path + " HTTP/1.0\r\n" +
                    "Host: " + host + "\r\n" +
                    "Connection: close\r\n" +
                    "User-Agent: Xagros/1.0\r\n" +
                    "\r\n";

                byte[] reqBytes = Encoding.ASCII.GetBytes(request);
                tcp.Send(reqBytes);

                // دریافت پاسخ با NonBlockingReceive + polling
                var sb = new StringBuilder();
                int totalReceived = 0;
                int maxBytes = 65536; // حداکثر 64KB
                var source = new EndPoint(Address.Zero, 0);

                int waited = 0;
                while (waited < timeoutMs && totalReceived < maxBytes)
                {
                    if (!tcp.IsConnected())
                        break; // سرور اتصال را بست

                    byte[] data = tcp.NonBlockingReceive(ref source);
                    if (data != null && data.Length > 0)
                    {
                        string chunk = Encoding.ASCII.GetString(data);
                        sb.Append(chunk);
                        totalReceived += data.Length;
                        waited = 0;
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(20);
                        waited += 20;
                    }
                }

                string response = sb.ToString();

                // جدا کردن header از body
                int headerEnd = response.IndexOf("\r\n\r\n");
                if (headerEnd >= 0)
                    return response.Substring(headerEnd + 4);

                return response;
            }
            catch (Exception ex)
            {
                error = "Connection error: " + ex.Message;
                return null;
            }
            finally
            {
                // حالا در هر مسیر خروجی (موفقیت، timeout، یا استثنا) بسته می‌شود
                try { tcp?.Close(); } catch { }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WebBrowserState — وضعیت کامل مرورگر برای هر پنجره
    // ═══════════════════════════════════════════════════════════════════
    public class WebBrowserState
    {
        // ─── نوار آدرس ────────────────────────────────────────────────
        public string AddressBarText = "http://";
        public bool AddressBarFocused = false;
        public int AddressBarCursorPos = 7;

        // ─── محتوا ────────────────────────────────────────────────────
        public List<HtmlElement> Elements = new List<HtmlElement>();
        public int ScrollOffset = 0;           // اسکرول عمودی (px)
        public int ContentHeight = 0;          // ارتفاع کل محتوا

        // ─── وضعیت بارگذاری ───────────────────────────────────────────
        public enum PageState { Idle, Loading, Loaded, Error }
        public PageState State = PageState.Idle;
        public string StatusText = "Ready";
        public string CurrentUrl = "";
        public string ErrorMessage = "";

        // ─── تاریخچه ──────────────────────────────────────────────────
        public List<string> History = new List<string>();
        public int HistoryIndex = -1;

        // ─── لینک‌های قابل کلیک (موقعیت رندر) ─────────────────────────
        public List<(int X, int Y, int W, int H, string Url)> Links
            = new List<(int, int, int, int, string)>();

        // ─── صفحه خوش‌آمدگویی ─────────────────────────────────────────
        public bool ShowWelcome = true;

        // ─── بافر layout مخصوص همین پنجره (رفع باگ shared-static-buffer) ──
        internal List<WebBrowserApp.LayoutItem> LayoutBuf = new List<WebBrowserApp.LayoutItem>(128);

        // ─── کش صفحه‌ی خوش‌آمدگویی (رفع نشت حافظه) ──────────────────────
        // قبلاً BuildWelcomePage() هر فریم (هر بار ComputeLayout صدا زده
        // می‌شد) یک List<HtmlElement> و چند HtmlElement تازه می‌ساخت، حتی
        // وقتی هیچ‌چیزی عوض نشده بود — یعنی تا وقتی کاربر روی صفحه‌ی
        // خوش‌آمدگویی بود (حالت پیش‌فرض هر پنجره‌ی جدید مرورگر)، هر فریم
        // زباله‌ی جدید تولید می‌شد. حالا فقط وقتی متن وضعیت شبکه واقعاً
        // عوض شود (اتصال/قطع شبکه) دوباره ساخته می‌شود.
        internal List<HtmlElement> WelcomeCache = null;
        internal string WelcomeCacheNetStatus = null;

        // ─── کش dirty-check برای ComputeLayout (رفع نشت حافظه اصلی) ────────
        // ComputeLayout قبلاً هر فریم (هر بار پنجره redraw می‌شد) اجرا می‌شد،
        // و برای هر خط از هر پاراگراف word-wrap شده روی صفحه، LayoutWrappedText
        // یک text.Substring() تازه می‌ساخت — یعنی برای هر صفحه‌ی متنی معمولی
        // (نه فقط صفحه‌ی خوش‌آمدگویی) هر فریم مقدار زیادی زباله‌ی string تولید
        // می‌شد، حتی وقتی هیچ‌چیزی روی صفحه عوض نشده بود. حالا اگر منبع
        // عناصر، اسکرول، اندازه‌ی محتوا و اندازه‌ی پنجره از آخرین بار عوض
        // نشده باشند، ComputeLayout بلافاصله برمی‌گردد و از state.LayoutBuf
        // قبلی (که دست‌نخورده مانده) استفاده می‌شود — بدون هیچ allocation.
        internal object LayoutCacheElementsRef = null;
        internal int LayoutCacheScroll = int.MinValue;
        internal int LayoutCacheContentY = int.MinValue;
        internal int LayoutCacheContentH = int.MinValue;
        internal int LayoutCacheW = int.MinValue;
        internal WebBrowserState.PageState LayoutCacheState = WebBrowserState.PageState.Idle;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WebBrowserApp — مرورگر وب برای Xagros OS
    //
    //  معماری:
    //    ┌─────────────────────────────────────────┐
    //    │ Toolbar: [←][→][⟳]  [address bar]  [Go] │
    //    ├─────────────────────────────────────────┤
    //    │ Content Area: HTML Renderer              │
    //    │   - Text, Headings, Links, Lists         │
    //    │   - Scroll با mouse wheel                │
    //    ├─────────────────────────────────────────┤
    //    │ Status Bar: وضعیت | URL                  │
    //    └─────────────────────────────────────────┘
    // ═══════════════════════════════════════════════════════════════════
    public static class WebBrowserApp
    {
        public const string ContentFlag = "WEB_BROWSER_APP";

        // ─── ثابت‌های Layout ──────────────────────────────────────────
        private const int ToolbarH = 36;
        private const int StatusBarH = 20;
        private const int AddressBarX = 70;     // بعد از دکمه‌های nav
        private const int AddressBarMargin = 50; // فضای دکمه Go در سمت راست
        private const int AddressBarH = 24;
        private const int ScrollStep = 18;
        private const int MaxElements = 512;     // حداکثر عناصر نمایشی

        // ─── رنگ‌های packed (یک‌بار محاسبه، طبق الگوی بقیه‌ی سیستم) ──
        private static readonly int ColToolbar = RenderSystem.ToInt(Color.FromArgb(28, 28, 45));
        private static readonly int ColAddrBar = RenderSystem.ToInt(Color.FromArgb(40, 40, 62));
        private static readonly int ColAddrBarFocus = RenderSystem.ToInt(Color.FromArgb(50, 55, 80));
        private static readonly int ColAddrBorder = RenderSystem.ToInt(Color.FromArgb(60, 60, 100));
        private static readonly int ColAddrBorderFocus = RenderSystem.ToInt(Color.FromArgb(100, 120, 220));
        private static readonly int ColGoBtn = RenderSystem.ToInt(Color.FromArgb(60, 120, 200));
        private static readonly int ColNavBtn = RenderSystem.ToInt(Color.FromArgb(45, 45, 70));
        private static readonly int ColNavBtnHover = RenderSystem.ToInt(Color.FromArgb(70, 70, 110));
        private static readonly int ColStatusBar = RenderSystem.ToInt(Color.FromArgb(18, 18, 30));
        private static readonly int ColLink = RenderSystem.ToInt(Color.FromArgb(100, 160, 255));
        private static readonly int ColHr = RenderSystem.ToInt(Color.FromArgb(70, 70, 110));
        private static readonly int ColDivider = RenderSystem.ToInt(Color.FromArgb(50, 50, 80));
        private static readonly int ColCodeBg = RenderSystem.ToInt(Color.FromArgb(30, 30, 48));
        private static readonly int ColListBullet = RenderSystem.ToInt(Color.FromArgb(120, 140, 220));
        private static readonly int ColScrollThumb = RenderSystem.ToInt(Color.FromArgb(80, 80, 130));
        private static readonly int ColScrollTrack = RenderSystem.ToInt(Color.FromArgb(28, 28, 44));
        private static readonly int ColImgBox = RenderSystem.ToInt(Color.FromArgb(60, 60, 100));
        private static readonly int ColWhite = RenderSystem.ToInt(Color.White);

        // ─── همان رنگ‌ها به شکل Color (نه packed int) — برای پارامتر bgColor
        // در canvas.DrawTtf که آنتی‌الیاسینگ خاکستری واقعی TtfFont را فعال
        // می‌کند. فقط برای پس‌زمینه‌هایی که یکدست و از قبل مشخص‌اند (طبق
        // هشدار خود TtfFont.cs — روی گرادیان/تصویر این کار اشتباه است).
        private static readonly Color BgAddrBar = Color.FromArgb(40, 40, 62);
        private static readonly Color BgAddrBarFocus = Color.FromArgb(50, 55, 80);
        private static readonly Color BgGoBtn = Color.FromArgb(60, 120, 200);
        private static readonly Color BgNavBtn = Color.FromArgb(45, 45, 70); // تقریب حالت غیر-hover
        private static readonly Color BgStatusBar = Color.FromArgb(18, 18, 30);
        private static readonly Color BgCode = Color.FromArgb(30, 30, 48);

        // ─── Pen ها — فقط برای DrawTexts (بعد از Flush، مستقیم روی Canvas) ──
        private static readonly Pen _penWhite = new Pen(Color.White);
        private static readonly Pen _penTextPrimary = new Pen(Color.FromArgb(225, 225, 235));
        private static readonly Pen _penLink = new Pen(Color.FromArgb(100, 160, 255));
        private static readonly Pen _penH1 = new Pen(Color.FromArgb(240, 240, 255));
        private static readonly Pen _penH2 = new Pen(Color.FromArgb(210, 220, 255));
        private static readonly Pen _penH3 = new Pen(Color.FromArgb(190, 200, 240));
        private static readonly Pen _penStatus = new Pen(Color.FromArgb(160, 165, 200));
        private static readonly Pen _penCodeText = new Pen(Color.FromArgb(140, 210, 160));
        private static readonly Pen _penImgLabel = new Pen(Color.FromArgb(150, 150, 180));

        // ─── state map ────────────────────────────────────────────────
        // توجه: از Dictionary استفاده نمی‌کنیم چون GetHashCode/Equals پیش‌فرض
        // در محیط Cosmos AOT گاهی مشکل‌ساز است. به‌جای آن از یک لیست ساده
        // با جستجوی خطی (reference equality) استفاده می‌کنیم — دقیقاً مثل
        // الگوی Windows (List<WindowInfo>) که در بقیه‌ی پروژه استفاده شده.
        private static readonly List<WindowInfo> _stateKeys = new List<WindowInfo>();
        private static readonly List<WebBrowserState> _stateValues = new List<WebBrowserState>();

        public static WebBrowserState GetOrCreateState(WindowInfo w)
        {
            for (int i = 0; i < _stateKeys.Count; i++)
            {
                if (ReferenceEquals(_stateKeys[i], w))
                    return _stateValues[i];
            }
            var newState = new WebBrowserState();
            _stateKeys.Add(w);
            _stateValues.Add(newState);
            return newState;
        }

        public static void CleanupState(WindowInfo w)
        {
            for (int i = 0; i < _stateKeys.Count; i++)
            {
                if (ReferenceEquals(_stateKeys[i], w))
                {
                    _stateKeys.RemoveAt(i);
                    _stateValues.RemoveAt(i);
                    return;
                }
            }
        }

        // ─── صفحه خوش‌آمدگویی (نمایش در حالت اولیه) ─────────────────
        // رفع نشت حافظه: فقط وقتی متن وضعیت شبکه تغییر کند دوباره ساخته
        // می‌شود؛ در غیر این صورت همان نمونه‌ی کش‌شده در WebBrowserState
        // برگردانده می‌شود (بدون هیچ allocation جدید).
        private static List<HtmlElement> GetWelcomePage(WebBrowserState state)
        {
            string netStatus = NetworkDriver.Status == NetworkStatus.Connected
                ? "متصل — IP: " + NetworkDriver.IpAddress
                : "قطع — شبکه را از Settings فعال کنید";

            if (state.WelcomeCache != null && state.WelcomeCacheNetStatus == netStatus)
                return state.WelcomeCache;

            var els = new List<HtmlElement>(9);
            els.Add(new HtmlElement("h1", "ParsOS Web Browser"));
            els.Add(new HtmlElement("p", "Welcom to NovaSearch"));
            els.Add(new HtmlElement("hr", ""));
            els.Add(new HtmlElement("h2", "How to use:"));
            els.Add(new HtmlElement("li", "http://neverssl.com یا http://example.com را امتحان کنید — این دو بدون نیاز به DNS همیشه کار می‌کنند."));
            els.Add(new HtmlElement("li", "برای هر آدرس دیگری می‌توانید مستقیماً IP وارد کنید، مثلاً: http://192.168.1.10/index.html"));
            els.Add(new HtmlElement("li", "Cosmos از DNS پشتیبانی محدودی دارد — اگر hostname دیگری resolve نشد، از IP مستقیم استفاده کنید."));
            els.Add(new HtmlElement("hr", ""));
            els.Add(new HtmlElement("h2", "Network status:"));
            els.Add(new HtmlElement("p", netStatus));

            state.WelcomeCache = els;
            state.WelcomeCacheNetStatus = netStatus;
            return els;
        }

        // ═══════════════════════════════════════════════════════════════
        //  Navigate — بارگذاری URL
        // ═══════════════════════════════════════════════════════════════
        public static void Navigate(WebBrowserState state, string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "http://" + url;

            state.AddressBarText = url;
            state.State = WebBrowserState.PageState.Loading;
            state.StatusText = "Connecting...";
            state.ShowWelcome = false;
            state.ScrollOffset = 0;
            state.Links.Clear();

            string html = SimpleHttpClient.Get(url, out string error);
            if (html == null)
            {
                state.State = WebBrowserState.PageState.Error;
                state.ErrorMessage = error;
                state.StatusText = "Error: " + error;
                state.Elements = new List<HtmlElement>
                {
                    new HtmlElement("h1", "Error"),
                    new HtmlElement("p", error),
                    new HtmlElement("p", "URL: " + url)
                };
            }
            else
            {
                state.Elements = SimpleHtmlParser.Parse(html);
                if (state.Elements.Count == 0)
                    state.Elements.Add(new HtmlElement("p", "(صفحه خالی یا قابل پارس نیست)"));
                state.State = WebBrowserState.PageState.Loaded;
                state.CurrentUrl = url;
                state.StatusText = "Done — " + url;

                // تاریخچه
                if (state.HistoryIndex >= 0 && state.HistoryIndex < state.History.Count - 1)
                    state.History.RemoveRange(state.HistoryIndex + 1,
                        state.History.Count - state.HistoryIndex - 1);
                state.History.Add(url);
                state.HistoryIndex = state.History.Count - 1;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HandleClick — کلیک‌های درون پنجره مرورگر
        // ═══════════════════════════════════════════════════════════════
        public static bool HandleClick(WindowInfo w, int mx, int my)
        {
            var state = GetOrCreateState(w);
            int th = WindowInfo.TitleH;
            int contentY = w.Y + th + ToolbarH;
            int toolbarY = w.Y + th;

            // ─── کلیک روی Toolbar ─────────────────────────────────────
            if (my >= toolbarY && my < contentY)
            {
                // دکمه Back
                if (mx >= w.X + 4 && mx < w.X + 26 && my >= toolbarY + 6 && my < toolbarY + 30)
                {
                    GoBack(state);
                    return true;
                }
                // دکمه Forward
                if (mx >= w.X + 30 && mx < w.X + 52 && my >= toolbarY + 6 && my < toolbarY + 30)
                {
                    GoForward(state);
                    return true;
                }
                // دکمه Refresh
                if (mx >= w.X + 56 && mx < w.X + 70 && my >= toolbarY + 6 && my < toolbarY + 30)
                {
                    if (!string.IsNullOrEmpty(state.CurrentUrl))
                        Navigate(state, state.CurrentUrl);
                    return true;
                }

                // کلیک روی Address Bar
                int addrX = w.X + AddressBarX;
                int addrW = w.W - AddressBarX - AddressBarMargin;
                if (mx >= addrX && mx < addrX + addrW && my >= toolbarY + 6 && my < toolbarY + 30)
                {
                    state.AddressBarFocused = true;
                    state.AddressBarCursorPos = state.AddressBarText.Length;
                    return true;
                }
                else
                {
                    state.AddressBarFocused = false;
                }

                // دکمه Go
                int goBtnX = w.X + w.W - AddressBarMargin + 4;
                if (mx >= goBtnX && mx < w.X + w.W - 4 && my >= toolbarY + 6 && my < toolbarY + 30)
                {
                    Navigate(state, state.AddressBarText);
                    return true;
                }
                return true;
            }

            // ─── کلیک روی Content ─────────────────────────────────────
            if (my >= contentY && my < w.Y + w.H - StatusBarH)
            {
                state.AddressBarFocused = false;
                // بررسی کلیک روی لینک
                foreach (var link in state.Links)
                {
                    if (mx >= link.X && mx <= link.X + link.W &&
                        my >= link.Y && my <= link.Y + link.H)
                    {
                        string href = link.Url;
                        // resolve relative URL
                        if (href.StartsWith("/") && !string.IsNullOrEmpty(state.CurrentUrl))
                        {
                            SimpleHttpClient.ParseUrl(state.CurrentUrl, out string host, out int port, out _);
                            href = "http://" + host + (port != 80 ? ":" + port : "") + href;
                        }
                        Navigate(state, href);
                        return true;
                    }
                }
            }

            return false;
        }

        // ─── پیمایش تاریخچه ──────────────────────────────────────────
        private static void GoBack(WebBrowserState state)
        {
            if (state.HistoryIndex > 0)
            {
                state.HistoryIndex--;
                Navigate(state, state.History[state.HistoryIndex]);
                // بعد از navigate HistoryIndex دوباره آخر می‌رود — اصلاح:
                state.HistoryIndex = Math.Max(0, state.HistoryIndex - 1);
            }
        }

        private static void GoForward(WebBrowserState state)
        {
            if (state.HistoryIndex < state.History.Count - 1)
            {
                state.HistoryIndex++;
                Navigate(state, state.History[state.HistoryIndex]);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HandleKeyboard — ورودی کیبورد برای Address Bar
        // ═══════════════════════════════════════════════════════════════
        public static bool HandleKeyboard(WindowInfo w)
        {
            var state = GetOrCreateState(w);
            if (!state.AddressBarFocused) return false;
            if (!Cosmos.System.KeyboardManager.KeyAvailable) return false;

            var key = Cosmos.System.KeyboardManager.ReadKey();

            if (key.Key == ConsoleKeyEx.Enter)
            {
                Navigate(state, state.AddressBarText);
                return true;
            }
            if (key.Key == ConsoleKeyEx.Backspace && state.AddressBarText.Length > 0)
            {
                state.AddressBarText = state.AddressBarText.Substring(0,
                    state.AddressBarText.Length - 1);
                if (state.AddressBarCursorPos > state.AddressBarText.Length)
                    state.AddressBarCursorPos = state.AddressBarText.Length;
                return true;
            }
            if (key.Key == ConsoleKeyEx.UpArrow)
            {
                state.ScrollOffset = Math.Max(0, state.ScrollOffset - ScrollStep);
                return true;
            }
            if (key.Key == ConsoleKeyEx.DownArrow)
            {
                int maxScroll = Math.Max(0, state.ContentHeight - 200);
                state.ScrollOffset = Math.Min(maxScroll, state.ScrollOffset + ScrollStep);
                return true;
            }
            if (key.KeyChar != '\0' && key.KeyChar != '\n' && key.KeyChar != '\r')
            {
                if (state.AddressBarText.Length < 256)
                {
                    state.AddressBarText += key.KeyChar;
                    state.AddressBarCursorPos = state.AddressBarText.Length;
                    return true;
                }
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════
        //  معماری رندر — هماهنگ با بقیه‌ی سیستم (RenderSystem + back-buffer)
        //
        //  مثل بقیه‌ی برنامه‌ها (Settings/...)، رسم به دو پاس تقسیم می‌شود:
        //    DrawShapes()  → قبل از Flush، با RenderSystem روی back-buffer
        //                    (مستطیل‌ها، خطوط، دایره‌ها، اسکرول‌بار، نشانگرها)
        //    DrawTexts()   → بعد از Flush، فقط Canvas.DrawString
        //                    (تمام رشته‌های متنی، چون فونت روی Canvas کار می‌کند)
        //
        //  این دو پاس باید کاملاً هم‌راستا باشند، پس مختصات هر المان طی
        //  ComputeLayout یک‌بار محاسبه و در هر دو پاس مصرف می‌شود — بدون
        //  محاسبه‌ی دوباره و بدون احتمال ناهماهنگی.
        // ═══════════════════════════════════════════════════════════════

        // ─── یک آیتم layout محاسبه‌شده (مشترک بین دو پاس) ──────────────
        // internal شد (قبلاً private) چون حالا باید از WebBrowserState هم
        // در دسترس باشد — به دلیل باگ زیر:
        internal struct LayoutItem
        {
            public string Kind;     // "h1","h2","h3","text","link","li","hr","code","img","cursor"
            public int X, Y, W, H;
            public string Text;
            public bool HasColor;
            public Color FgColor;
            public string Href;
        }

        // ─── باگ واقعی که اینجا بود (نه فقط نشت حافظه) ──────────────────
        // قبلاً _layoutBuf یک فیلد static مشترک بین همه‌ی پنجره‌های مرورگر
        // بود. اما رندر در دو پاس جدا انجام می‌شود: ابتدا DrawWindowShapes
        // برای «همه‌ی» پنجره‌ها اجرا می‌شود (که ComputeLayout را صدا می‌زند و
        // _layoutBuf را پر می‌کند)، بعد یک بار Flush، و بعد در یک حلقه‌ی
        // کاملاً جدا DrawWindowTexts برای «همه‌ی» پنجره‌ها اجرا می‌شود (که از
        // همان _layoutBuf برای رسم متن استفاده می‌کند). یعنی وقتی بیش از یک
        // پنجره‌ی مرورگر همزمان باز باشد، تا زمانی که نوبت به پاس متن هر
        // پنجره می‌رسید، _layoutBuf از قبل توسط آخرین پنجره‌ای که در پاس
        // شکل‌ها پردازش شده بازنویسی شده بود — یعنی همه‌ی پنجره‌های مرورگر
        // به‌جز آخری، متنِ (و مختصاتِ) پنجره‌ی دیگری را نشان می‌دادند.
        // راه‌حل: هر WebBrowserState بافر layout مخصوص به خودش را دارد.

        // ═══════════════════════════════════════════════════════════════
        //  DrawShapes — فراخوانی از DrawWindowContentShapes (قبل از Flush)
        // ═══════════════════════════════════════════════════════════════
        public static void DrawShapes(WindowInfo w, int mouseX, int mouseY)
        {
            // محافظت در برابر فریز/کرش هنگام انیمیشن باز/بسته شدن پنجره
            if (w.W < 100 || w.H < 80) return;

            var state = GetOrCreateState(w);
            int th = WindowInfo.TitleH;
            int toolbarY = w.Y + th;
            int contentY = toolbarY + ToolbarH;
            int contentH = w.H - th - ToolbarH - StatusBarH;
            if (contentH < 10) return;

            // ─── محاسبه‌ی layout یک‌بار برای این فریم ──────────────────
            ComputeLayout(w, state, contentY, contentH);

            // ─── Toolbar (شکل‌ها) ──────────────────────────────────────
            DrawToolbarShapes(w, state, toolbarY, mouseX, mouseY);

            // ─── محتوا (شکل‌ها: خطوط زیر تیتر، bullet، کادر کد و ...) ──
            for (int i = 0; i < state.LayoutBuf.Count; i++)
            {
                var item = state.LayoutBuf[i];
                switch (item.Kind)
                {
                    case "h1-rule":
                        RenderSystem.HLine(item.X, item.Y, item.W, ColHr);
                        break;
                    case "link-rule":
                        RenderSystem.HLine(item.X, item.Y, item.W, ColLink);
                        break;
                    case "li-bullet":
                        RenderSystem.FilledCircle(item.X, item.Y, 3, ColListBullet);
                        break;
                    case "hr":
                        RenderSystem.HLine(item.X, item.Y, item.W, ColHr);
                        break;
                    case "code-bg":
                        RenderSystem.FillRoundRect(item.X, item.Y, item.W, item.H, 3, ColCodeBg);
                        break;
                    case "img-box":
                        RenderSystem.DrawRect(item.X, item.Y, item.W, item.H, ColImgBox);
                        break;
                }
            }

            // ─── Status Bar (شکل) ──────────────────────────────────────
            int sbY = w.Y + w.H - StatusBarH;
            RenderSystem.Fill(w.X, sbY, w.W, StatusBarH, ColStatusBar);
            RenderSystem.HLine(w.X, sbY, w.W, ColDivider);

            // ─── Scrollbar (شکل) ────────────────────────────────────────
            if (state.ContentHeight > contentH)
                DrawScrollbarShapes(w, state, contentY, contentH);
        }

        // ═══════════════════════════════════════════════════════════════
        //  DrawTexts — فراخوانی از DrawWindowContentTexts (بعد از Flush)
        // ═══════════════════════════════════════════════════════════════
        // توجه: قبلاً این متد PCScreenFont (فونت داخلی/bitmap کاسموس) می‌گرفت
        // که هیچ گلیف فارسی/عربی ندارد و شکل‌دهی حروف (initial/medial/final)
        // یا BiDi هم انجام نمی‌دهد — دقیقاً همان چیزی که باعث می‌شد صفحه
        // خوش‌آمدگویی (و هر متن فارسی دیگر در مرورگر) با کاراکترهای عجیب و
        // نامرتبط نمایش داده شود. حالا از TtfFont (فونت Vazir پروژه) استفاده
        // می‌کنیم که هم گلیف فارسی دارد و هم از طریق canvas.DrawTtf مسیر
        // DrawAuto (شکل‌دهی + BiDi) را طی می‌کند — همان چیزی که NotepadApp
        // از قبل استفاده می‌کرد.
        public static void DrawTexts(WindowInfo w, WindowCanvas canvas, TtfFont font)
        {
            if (w.W < 100 || w.H < 80) return;

            var state = GetOrCreateState(w);
            int th = WindowInfo.TitleH;
            int toolbarY = w.Y + th;
            int contentY = toolbarY + ToolbarH;
            int contentH = w.H - th - ToolbarH - StatusBarH;
            if (contentH < 10) return;

            // ─── Toolbar (متن‌ها) ──────────────────────────────────────
            DrawToolbarTexts(w, state, toolbarY, canvas, font);

            // ─── محتوا (متن‌ها) ─────────────────────────────────────────
            if (state.State == WebBrowserState.PageState.Loading && !state.ShowWelcome)
            {
                int cx = w.X + w.W / 2 - 40;
                int cy = contentY + contentH / 2 - 8;
                canvas.DrawTtf(font, "Loading...", _penStatus, cx, cy, Theme.WindowBg);
            }
            else
            {
                for (int i = 0; i < state.LayoutBuf.Count; i++)
                {
                    var item = state.LayoutBuf[i];
                    Pen pen;
                    Color bg = Theme.WindowBg; // پس‌زمینه‌ی پیش‌فرض محتوا
                    switch (item.Kind)
                    {
                        case "h1": pen = _penH1; break;
                        case "h2": pen = _penH2; break;
                        case "h3": pen = _penH3; break;
                        case "link": pen = _penLink; break;
                        case "li": pen = _penTextPrimary; break;
                        case "code": pen = _penCodeText; bg = BgCode; break;
                        case "img-label": pen = _penImgLabel; break;
                        case "text":
                            pen = item.HasColor ? GetColorPen(item.FgColor) : _penTextPrimary;
                            break;
                        default: pen = null; break;
                    }
                    if (pen != null && !string.IsNullOrEmpty(item.Text))
                        canvas.DrawTtf(font, item.Text, pen, item.X, item.Y, bg);
                }
            }

            // ─── Status Bar (متن) ──────────────────────────────────────
            int sbY = w.Y + w.H - StatusBarH;
            string status = state.StatusText ?? "";
            int maxChars = Math.Max(1, (w.W / 8) - 2);
            if (status.Length > maxChars)
                status = status.Substring(0, Math.Max(0, maxChars - 3)) + "...";
            canvas.DrawTtf(font, status, _penStatus, w.X + 4, sbY + 3, BgStatusBar);
        }

        // ─── کش پن‌های رنگی سفارشی برای المان‌های <font color> ─────────
        // تا از new Pen در حلقه‌ی رندر (که در Cosmos costly است) پرهیز شود
        private static readonly List<(int Argb, Pen P)> _colorPenCache = new List<(int, Pen)>(8);
        private static Pen GetColorPen(Color c)
        {
            int argb = c.ToArgb();
            for (int i = 0; i < _colorPenCache.Count; i++)
                if (_colorPenCache[i].Argb == argb) return _colorPenCache[i].P;
            var p = new Pen(c);
            if (_colorPenCache.Count < 32) _colorPenCache.Add((argb, p));
            return p;
        }

        // ═══════════════════════════════════════════════════════════════
        //  ComputeLayout — محاسبه‌ی یک‌بار مختصات تمام المان‌ها
        //  هم برای DrawShapes و هم DrawTexts استفاده می‌شود (بدون تکرار)
        // ═══════════════════════════════════════════════════════════════
        private static void ComputeLayout(WindowInfo w, WebBrowserState state, int contentY, int contentH)
        {
            List<HtmlElement> elements;
            if (state.ShowWelcome || state.Elements.Count == 0)
                elements = GetWelcomePage(state);
            else if (state.State == WebBrowserState.PageState.Loading)
            { state.ContentHeight = 0; return; } // فقط متن Loading نشان داده می‌شود
            else
                elements = state.Elements;

            // ─── dirty-check: اگر هیچ‌چیز مؤثر بر layout عوض نشده، از نتیجه‌ی
            // قبلی (همچنان در state.LayoutBuf/Links) استفاده کن — رفع نشت
            // حافظه‌ی اصلی مرورگر (ر.ک. توضیح روی فیلدهای LayoutCache*)
            if (ReferenceEquals(elements, state.LayoutCacheElementsRef) &&
                state.ScrollOffset == state.LayoutCacheScroll &&
                contentY == state.LayoutCacheContentY &&
                contentH == state.LayoutCacheContentH &&
                w.W == state.LayoutCacheW &&
                state.State == state.LayoutCacheState)
            {
                return; // layout قبلی هنوز معتبر است
            }

            state.LayoutBuf.Clear();
            state.Links.Clear();

            int x = w.X + 12;
            int y = contentY + 8 - state.ScrollOffset;
            int maxW = Math.Max(8, w.W - 24);
            int clipBottom = contentY + contentH;
            int totalY = 8;

            for (int ei = 0; ei < elements.Count && ei < MaxElements; ei++)
            {
                var el = elements[ei];
                int lineH = 18;

                switch (el.Tag)
                {
                    case "page-title":
                        continue;

                    case "h1":
                        {
                            int ty = y + 8;
                            if (ty >= contentY && ty < clipBottom)
                            {
                                state.LayoutBuf.Add(new LayoutItem { Kind = "h1", X = x, Y = ty, Text = el.Text });
                                state.LayoutBuf.Add(new LayoutItem { Kind = "h1-rule", X = x, Y = ty + 16, W = maxW });
                            }
                            lineH = 28;
                            break;
                        }

                    case "h2":
                        {
                            int ty = y + 6;
                            if (ty >= contentY && ty < clipBottom)
                                state.LayoutBuf.Add(new LayoutItem { Kind = "h2", X = x, Y = ty, Text = el.Text });
                            lineH = 26;
                            break;
                        }

                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        {
                            int ty = y + 4;
                            if (ty >= contentY && ty < clipBottom)
                                state.LayoutBuf.Add(new LayoutItem { Kind = "h3", X = x, Y = ty, Text = el.Text });
                            lineH = 22;
                            break;
                        }

                    case "p":
                    case "div":
                    case "text":
                        {
                            if (y >= contentY && y < clipBottom)
                            {
                                lineH = LayoutWrappedText(state, el.Text, x, y, maxW, el.HasColor, el.FgColor) + 2;
                            }
                            else
                            {
                                int estCharsPerLine = Math.Max(1, maxW / 8);
                                int lines = (el.Text.Length + estCharsPerLine - 1) / estCharsPerLine;
                                if (lines < 1) lines = 1;
                                lineH = lines * 16 + 2;
                            }
                            break;
                        }

                    case "a":
                        {
                            if (y >= contentY && y < clipBottom)
                            {
                                int linkW = Math.Min(el.Text.Length * 8, maxW);
                                state.LayoutBuf.Add(new LayoutItem { Kind = "link", X = x, Y = y, Text = el.Text });
                                state.LayoutBuf.Add(new LayoutItem { Kind = "link-rule", X = x, Y = y + 14, W = linkW });
                                if (!string.IsNullOrEmpty(el.Href))
                                    state.Links.Add((x, y, linkW, 16, el.Href));
                            }
                            lineH = 18;
                            break;
                        }

                    case "li":
                        {
                            if (y >= contentY && y < clipBottom)
                            {
                                state.LayoutBuf.Add(new LayoutItem { Kind = "li-bullet", X = x + 6, Y = y + 8 });
                                state.LayoutBuf.Add(new LayoutItem { Kind = "li", X = x + 16, Y = y, Text = el.Text });
                            }
                            lineH = 18;
                            break;
                        }

                    case "br":
                        lineH = 8;
                        break;

                    case "hr":
                        if (y >= contentY && y < clipBottom)
                            state.LayoutBuf.Add(new LayoutItem { Kind = "hr", X = x, Y = y + 4, W = maxW });
                        lineH = 12;
                        break;

                    case "code":
                        if (y >= contentY && y < clipBottom)
                        {
                            state.LayoutBuf.Add(new LayoutItem { Kind = "code-bg", X = x, Y = y, W = maxW, H = 16 });
                            state.LayoutBuf.Add(new LayoutItem { Kind = "code", X = x + 4, Y = y, Text = el.Text });
                        }
                        lineH = 20;
                        break;

                    case "img":
                        if (y >= contentY && y < clipBottom)
                        {
                            state.LayoutBuf.Add(new LayoutItem { Kind = "img-box", X = x, Y = y, W = 60, H = 40 });
                            state.LayoutBuf.Add(new LayoutItem { Kind = "img-label", X = x + 16, Y = y + 12, Text = "[img]" });
                        }
                        lineH = 48;
                        break;

                    default:
                        lineH = 4;
                        break;
                }

                y += lineH;
                totalY += lineH;
            }

            state.ContentHeight = totalY + 16;

            // ذخیره‌ی کلیدهای کش برای فریم بعدی
            state.LayoutCacheElementsRef = elements;
            state.LayoutCacheScroll = state.ScrollOffset;
            state.LayoutCacheContentY = contentY;
            state.LayoutCacheContentH = contentH;
            state.LayoutCacheW = w.W;
            state.LayoutCacheState = state.State;
        }

        // ─── Word Wrap — فقط محاسبه‌ی layout، بدون رسم مستقیم ──────────
        // خروجی مستقیماً به state.LayoutBuf اضافه می‌شود؛ ارتفاع کل را برمی‌گرداند
        private static int LayoutWrappedText(WebBrowserState state, string text, int x, int y, int maxW, bool hasColor, Color fg)
        {
            if (string.IsNullOrEmpty(text)) return 16;

            int charsPerLine = Math.Max(1, maxW / 8);

            int totalH = 0;
            int lineY = y;
            int pos = 0;
            int safety = 0;
            int maxIterations = text.Length + 8; // محافظت سخت در برابر infinite loop

            while (pos < text.Length && safety < maxIterations)
            {
                safety++;
                int len = Math.Min(charsPerLine, text.Length - pos);
                if (len < 1) len = 1;

                if (pos + len < text.Length && len > 1)
                {
                    int lastSpace = text.LastIndexOf(' ', pos + len - 1, len);
                    if (lastSpace > pos) len = lastSpace - pos + 1;
                }
                if (len < 1) len = 1;

                state.LayoutBuf.Add(new LayoutItem
                {
                    Kind = "text",
                    X = x,
                    Y = lineY,
                    Text = text.Substring(pos, len),
                    HasColor = hasColor,
                    FgColor = fg
                });

                lineY += 16;
                totalH += 16;
                pos += len; // همیشه حداقل ۱ پیشروی — تضمین پایان حلقه
            }
            if (totalH == 0) totalH = 16;
            return totalH;
        }

        // ─── Toolbar: شکل‌ها (قبل از Flush) ─────────────────────────────
        private static void DrawToolbarShapes(WindowInfo w, WebBrowserState state,
                                              int toolbarY, int mouseX, int mouseY)
        {
            RenderSystem.Fill(w.X, toolbarY, w.W, ToolbarH, ColToolbar);
            RenderSystem.HLine(w.X, toolbarY + ToolbarH - 1, w.W, ColDivider);

            bool hovBack = mouseX >= w.X + 4 && mouseX < w.X + 26 &&
                           mouseY >= toolbarY + 6 && mouseY < toolbarY + 30;
            bool hovFwd = mouseX >= w.X + 30 && mouseX < w.X + 52 &&
                          mouseY >= toolbarY + 6 && mouseY < toolbarY + 30;
            bool hovRef = mouseX >= w.X + 56 && mouseX < w.X + 70 &&
                          mouseY >= toolbarY + 6 && mouseY < toolbarY + 30;

            RenderSystem.FillRoundRect(w.X + 4, toolbarY + 6, 22, 24, 4, hovBack ? ColNavBtnHover : ColNavBtn);
            RenderSystem.FillRoundRect(w.X + 30, toolbarY + 6, 22, 24, 4, hovFwd ? ColNavBtnHover : ColNavBtn);
            RenderSystem.FillRoundRect(w.X + 56, toolbarY + 6, 14, 24, 3, hovRef ? ColNavBtnHover : ColNavBtn);

            int addrX = w.X + AddressBarX;
            int addrW = w.W - AddressBarX - AddressBarMargin;
            int addrCol = state.AddressBarFocused ? ColAddrBarFocus : ColAddrBar;
            RenderSystem.FillRoundRect(addrX, toolbarY + 6, addrW, AddressBarH, 4, addrCol);
            RenderSystem.DrawRect(addrX, toolbarY + 6, addrW, AddressBarH,
                state.AddressBarFocused ? ColAddrBorderFocus : ColAddrBorder);

            if (state.AddressBarFocused)
            {
                string addrText = state.AddressBarText ?? "";
                int maxChars = Math.Max(1, (addrW - 8) / 8);
                int shown = Math.Min(addrText.Length, maxChars);
                int curX = addrX + 4 + shown * 8;
                RenderSystem.VLine(curX, toolbarY + 8, AddressBarH - 4, ColWhite);
            }

            int goBtnX = w.X + w.W - AddressBarMargin + 4;
            int goBtnW = AddressBarMargin - 8;
            RenderSystem.FillRoundRect(goBtnX, toolbarY + 6, goBtnW, AddressBarH, 4, ColGoBtn);
        }

        // ─── Toolbar: متن‌ها (بعد از Flush) ─────────────────────────────
        private static void DrawToolbarTexts(WindowInfo w, WebBrowserState state,
                                             int toolbarY, WindowCanvas canvas, TtfFont font)
        {
            // نکته: حروف نویگیشن روی دکمه‌ای رسم می‌شوند که رنگش با hover عوض
            // می‌شود (BgNavBtn/ColNavBtnHover در DrawToolbarShapes) اما این پاس
            // متن مختصات موس را ندارد، پس از رنگ حالت غیر-hover تقریب می‌زنیم؛
            // در حالت hover حاشیه‌ی خیلی جزئی رنگ ممکن است دیده شود که بی‌ضرر
            // است — خیلی بهتر از نداشتن AA اصلاً.
            canvas.DrawTtf(font, "<", _penWhite, w.X + 10, toolbarY + 11, BgNavBtn);
            canvas.DrawTtf(font, ">", _penWhite, w.X + 36, toolbarY + 11, BgNavBtn);
            canvas.DrawTtf(font, "R", _penWhite, w.X + 59, toolbarY + 11, BgNavBtn);

            int addrX = w.X + AddressBarX;
            int addrW = w.W - AddressBarX - AddressBarMargin;
            string addrText = state.AddressBarText ?? "";
            int maxChars = Math.Max(1, (addrW - 8) / 8);
            if (addrText.Length > maxChars && maxChars > 3)
                addrText = addrText.Substring(addrText.Length - maxChars + 1);
            Color addrBg = state.AddressBarFocused ? BgAddrBarFocus : BgAddrBar;
            canvas.DrawTtf(font, addrText, _penWhite, addrX + 4, toolbarY + 10, addrBg);

            int goBtnX = w.X + w.W - AddressBarMargin + 4;
            int goBtnW = AddressBarMargin - 8;
            canvas.DrawTtf(font, "Go", _penWhite, goBtnX + Math.Max(0, (goBtnW - 16) / 2), toolbarY + 10, BgGoBtn);
        }

        // ─── Scrollbar: فقط شکل (قبل از Flush) ─────────────────────────
        private static void DrawScrollbarShapes(WindowInfo w, WebBrowserState state,
                                                int contentY, int contentH)
        {
            int sbX = w.X + w.W - 10;
            int sbH = contentH;

            RenderSystem.Fill(sbX, contentY, 8, sbH, ColScrollTrack);

            float ratio = (float)state.ScrollOffset / Math.Max(1, state.ContentHeight);
            float sizeRatio = (float)contentH / Math.Max(1, state.ContentHeight);
            int thumbH = Math.Max(20, (int)(sbH * sizeRatio));
            int thumbY = contentY + (int)(sbH * ratio);
            if (thumbY + thumbH > contentY + sbH)
                thumbY = contentY + sbH - thumbH;

            RenderSystem.FillRoundRect(sbX + 1, thumbY, 6, thumbH, 3, ColScrollThumb);
        }
    }
}