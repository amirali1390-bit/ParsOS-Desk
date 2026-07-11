using Cosmos.System;
using Cosmos.System.FileSystem;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using IL2CPU.API.Attribs;
using System;
using System.Drawing;
using ParsOS.GUI;
using ParsOS.Network;
using Sys = Cosmos.System;

namespace ParsOS
{
    public class Kernel : Sys.Kernel
    {
        public static string Version { get; set; } = "1.0.0";

        // ─── فونت‌ها ────────────────────────────────────────────────────────
        // PCScreenFont (fallback) — همیشه موجود
        public static PCScreenFont DefaultFont { get; set; }

        // فونت TTF Vazir — برای نمایش فارسی و متن بهتر
        // در BeforeRun لود می‌شود؛ اگر ناموفق بود null می‌ماند
        public static TtfFont VazirFont { get; private set; }
        public static TtfFont VazirFontSm { get; private set; }  // اندازه کوچک‌تر (taskbar)

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Fonts.Vazir.ttf")]
        static byte[] VazirFontBytes;

        // ─── تصاویر Embedded ────────────────────────────────────────────────
        // wallpaper PNG — اولویت اول
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Images.Wallpaper1.png")]
        public static byte[] WallpaperPngByte;
        public static PngImage WallpaperPng;

        // wallpaper BMP — fallback اگر PNG موجود نبود
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Images.Wallpaper.png")]
        public static byte[] WallpaperByte;
        public static Bitmap Wallpaper;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Images.Wallpaper1.png")]
        public static byte[] Wal1Byte;
        public static PngImage Wallpaper1;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Images.Wallpaper2.png")]
        public static byte[] Wal2Byte;
        public static PngImage Wallpaper2;

        // ─── تصاویر اضافی برای انتخاب والپیپر ─────────────────────────────
        // برای اضافه کردن تصویر جدید:
        //   ۱. فایل را در پروژه زیر Assets\Images\ قرار دهید
        //   ۲. یک خط ManifestResourceStream زیر اضافه کنید
        //   ۳. آن را در GetWallpaperAssets() اضافه کنید
        //
        // مثال:
        //   [ManifestResourceStream(ResourceName = "Xagros.Assets.Images.Nature.png")]
        //   static byte[] _wpNatureBytes;
        //
        // ─────────────────────────────────────────────────────────────────────
        // کش آرایه assets — یک‌بار ساخته می‌شود، هرگز دوباره alloc نمی‌شود
        private static (string FileName, byte[] Data)[] _cachedAssets;
        public static (string FileName, byte[] Data)[] GetWallpaperAssetsPublic()
        {
            if (_cachedAssets == null)
                _cachedAssets = new (string, byte[])[]
                {
            ("Wallpaper.png",  WallpaperPngByte),
            ("Wallpaper1.png", Wal1Byte),
            ("Wallpaper2.png", Wal2Byte),
                };
            return _cachedAssets;
        }

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Images.Cursor.bmp")]
        static byte[] CursorByte;
        public static Bitmap CursorBitmap;

        // ─── لوگوی صفحه بوت ─────────────────────────────────────────────
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Logo.Logo.png")]
        public static byte[] LogoPngByte;
        public static PngImage LogoPng;


        // ─── آیکون‌های برنامه‌ها (48×48) ─────────────────────────────────
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Icons.Settings.bmp")]
        static byte[] IconSettingsByte;
        public static Bitmap IconSettings;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Icons.Notepad.bmp")]
        static byte[] IconNotepadByte;
        public static Bitmap IconNotepad;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Icons.FileExploler.bmp")]
        static byte[] IconFilesByte;
        public static Bitmap IconFiles;

        // آیکون پوشه ۲۰×۲۰ برای لیست File Explorer
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Icons.Folder.bmp")]
        static byte[] IconFolder20Byte;
        public static Bitmap IconFolder20;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Icons.Terminal.bmp")]
        static byte[] IconTerminalByte;
        public static Bitmap IconTerminal;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Icons.WebBrowser.bmp")]
        static byte[] IconBrowserByte;
        public static Bitmap IconBrowser;

        // آیکون موزیک پلیر — فایل را زیر Assets\Icons\MusicPlayer.bmp قرار دهید
        // و Build Action آن را روی Embedded Resource بگذارید (۴۸×۴۸، دقیقاً
        // مثل بقیه‌ی آیکون‌های بالا). اگر فایل موجود نباشد، بارگذاری بی‌خطر
        // شکست می‌خورد و آیکون null می‌ماند — تسک‌بار در این حالت به‌جای
        // بیت‌مپ، حرف اول نام برنامه را نشان می‌دهد (رفتار fallback موجود).
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Icons.MusicPlayer.bmp")]
        static byte[] IconMusicPlayerByte;
        public static Bitmap IconMusicPlayer;

        // ─── اپ‌های .pap که همراه سیستم‌عامل embedded می‌شوند ──────────────
        // فایل‌ها را زیر Assets\Apps\Calculator\ بگذارید و Build Action‌شان
        // را روی Embedded Resource بگذارید (دقیقاً مثل بقیه‌ی asset های بالا).
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Apps.Calculator.app.wsm")]
        static byte[] CalcAppWasmBytes;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Apps.Calculator.manifest.cfg")]
        static byte[] CalcManifestBytes;

        // ─── آهنگ‌های MP3 embedded برای MusicPlayerApp ─────────────────────
        // فایل‌ها را زیر Assets\Music\ بگذارید (Build Action = Embedded Resource،
        // دقیقاً مثل بقیه‌ی asset های بالا). اسم فایل و پراپرتی‌ها را با
        // آهنگ‌های واقعی خودتان جایگزین کنید — این‌ها فقط اسکلت/مسیر هستند.
        //
        // مثال اضافه‌کردن یک آهنگ جدید:
        //   [ManifestResourceStream(ResourceName = "Xagros.Assets.Music.Track1.mp3")]
        //   static byte[] Track1Mp3Bytes;
        // و بعد پایین در LoadEmbeddedMusic() یک خط AddEmbeddedTrack اضافه کنید.
        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Music.audio-in-my-head-by-bedroom.mp3")]
        static byte[] Track1Mp3Bytes;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Music.XaniarKhosravi-YeSetareh.mp3")]
        static byte[] Track2Mp3Bytes;

        [ManifestResourceStream(ResourceName = "ParsOS.Assets.Music.Youna-BavarKardam(320).mp3")]
        static byte[] Track3Mp3Bytes;

        public string CurrentPath { get; set; } = @"0:\";

        // ═══════════════════════════════════════════════════════════════════
        //  BeforeRun
        // ═══════════════════════════════════════════════════════════════════
        protected override void BeforeRun()
        {
            System.Console.Clear();
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine($"ParsOS v{Version} - Loading...");

            DefaultFont = PCScreenFont.Default;

            // ─── لود فونت Vazir TTF ────────────────────────────────────────
            LoadVazirFont();

            // ─── لود تصاویر ───────────────────────────────────────────────
            LoadBitmaps();

            // ─── فایل‌سیستم ───────────────────────────────────────────────
            SetupFileSystem();

            // ─── بارگذاری والپیپر ذخیره‌شده ────────────────────────────────
            // اگر کاربر قبلاً والپیپر انتخاب کرده، همان را لود کن
            LoadSavedWallpaper();

            // ─── صدای استارت ──────────────────────────────────────────────
            Music.StartUpSound();

            // ─── اندازه‌گیری RAM کل ────────────────────────────────────────────
            DetectTotalRAM();

            // ─── راه‌اندازی GUI ────────────────────────────────────────────
            GraphicsManager.Initialize();
            NetworkDriver.Initialize();

            // ─── لود آهنگ‌های MP3 embedded برای MusicPlayerApp ──────────────
            LoadEmbeddedMusic();
        }

        // ─── ثبت آهنگ‌های embedded در MusicPlayerApp ────────────────────────
        // اسم/آرتیست‌ها را با اطلاعات واقعی آهنگ‌های خودتان جایگزین کنید.
        private void LoadEmbeddedMusic()
        {
            try
            {
                AddEmbeddedTrack("In My Head", "Bedroom", Track1Mp3Bytes);
                AddEmbeddedTrack("yesetare", "Xaniar Khosravi", Track2Mp3Bytes);
                AddEmbeddedTrack("bavarkardam", "Youna", Track3Mp3Bytes);

                System.Console.WriteLine("[Kernel] Embedded music loaded: " +
                    ParsOS.GUI.MusicPlayerApp.Tracks.Count + " track(s)");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[Kernel] LoadEmbeddedMusic error: " + e.Message);
            }
        }

        private void AddEmbeddedTrack(string title, string artist, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                System.Console.WriteLine("[Kernel] Music track missing/empty: " + title);
                return;
            }
            ParsOS.GUI.MusicPlayerApp.AddTrack(title, artist, data);
        }

        // ─── بارگذاری والپیپر ذخیره‌شده از تنظیمات ───────────────────────
        // فرمت ذخیره: "embed:0" برای embedded، "file:0:\Assets\Images\x.png" برای فایل دیسک
        private void LoadSavedWallpaper()
        {
            try
            {
                string saved = LoadSetting("wallpaper", "");
                if (string.IsNullOrEmpty(saved)) return;

                System.Console.WriteLine("Loading saved wallpaper: " + saved);

                if (saved.StartsWith("embed:"))
                {
                    // والپیپر embedded
                    int idx = int.Parse(saved.Substring(6));
                    var assets = GetWallpaperAssetsPublic();
                    if (idx < 0 || idx >= assets.Length) return;
                    byte[] data = assets[idx].Data;
                    if (data == null) return;
                    string extU = System.IO.Path.GetExtension(assets[idx].FileName).ToUpper();
                    if (extU == ".PNG")
                    {
                        var decoded = PngDecoder.Decode(data);
                        if (decoded != null && decoded.IsValid)
                        { WallpaperPng = decoded; Wallpaper = null; }
                    }
                    else
                    {
                        WallpaperPng = null;
                        Wallpaper = new Bitmap(data);
                    }
                }
                else if (saved.StartsWith("file:"))
                {
                    // والپیپر از فایل دیسک
                    string path = saved.Substring(5);
                    if (!System.IO.File.Exists(path)) return;
                    byte[] data = System.IO.File.ReadAllBytes(path);
                    string extU = System.IO.Path.GetExtension(path).ToUpper();
                    if (extU == ".PNG")
                    {
                        var decoded = PngDecoder.Decode(data);
                        if (decoded != null && decoded.IsValid)
                        { WallpaperPng = decoded; Wallpaper = null; data = null; }
                    }
                    else
                    {
                        WallpaperPng = null;
                        Wallpaper = new Bitmap(data);
                    }
                }
                System.Console.WriteLine("Saved wallpaper loaded OK.");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("LoadSavedWallpaper error: " + e.Message);
            }
        }

        private void DetectTotalRAM()
        {
            try { _totalRAM_MB = Cosmos.Core.CPU.GetAmountOfRAM(); System.Console.WriteLine($"Total RAM: {_totalRAM_MB} MB"); }
            catch { _totalRAM_MB = 256; }
        }

        // ─── آستانه‌های مدیریت حافظه ────────────────────────────────────────
        // مقادیر پایه — در CheckMemoryPressure با درصد RAM کل override می‌شوند
        private const ulong MemCriticalMB = 120;     // fallback: 120 MB → GC فوری
        private const ulong MemRestartMB = 148;      // fallback: 148 MB → ری‌استارت
        private int _memCheckCounter = 0;
        private const int MemCheckInterval = 45;
        private int _criticalStreak = 0;
        private const int CriticalStreakLimit = 5;   // 5 × 45 تیک ≈ ~5 ثانیه
        // RAM کل سیستم (یک‌بار در BeforeRun)
        private static ulong _totalRAM_MB = 0;

        // ═══════════════════════════════════════════════════════════════════
        //  Run
        // ═══════════════════════════════════════════════════════════════════
        protected override void Run()
        {
            try
            {
                // ─── نگهبان حافظه ────────────────────────────────────────
                _memCheckCounter++;
                if (_memCheckCounter >= MemCheckInterval)
                {
                    _memCheckCounter = 0;
                    CheckMemoryPressure();
                }

                // ─── مسیریابی صفحه نمایش (LockScreen یا Desktop) ─────────
                if (LockScreen.IsActive)
                {
                    // اگر لاک‌اسکرین فعال است، فقط فریم‌های آن را رندر کن
                    LockScreen.Tick(GraphicsManager.Canvas, GraphicsManager.Width, GraphicsManager.Height);
                }
                else
                {
                    // وقتی IsActive برابر false شد (انیمیشن لاک‌اسکرین تمام شد)، وارد دسکتاپ شو
                    GraphicsManager.Tick();
                }
            }
            catch (Exception e)
            {
                BlueScreen(e.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  بررسی و مدیریت فشار حافظه
        // ═══════════════════════════════════════════════════════════════════
        private void CheckMemoryPressure()
        {
            try
            {
                ulong usedMB = Cosmos.Core.GCImplementation.GetUsedRAM() / (1024UL * 1024UL);

                // ─── آستانه‌های کاملاً درصدی بر اساس RAM کل ──────────────────
                // مقادیر fallback فقط وقتی RAM کل ناشناخته است (totalRAM=0)
                ulong effectiveWarn = _totalRAM_MB > 0 ? (_totalRAM_MB * 60UL) / 100UL : MemCriticalMB;  // 60%
                ulong effectiveCritical = _totalRAM_MB > 0 ? (_totalRAM_MB * 75UL) / 100UL : MemCriticalMB;  // 75%
                ulong effectiveRestart = _totalRAM_MB > 0 ? (_totalRAM_MB * 92UL) / 100UL : MemRestartMB;   // 92%

                if (usedMB >= effectiveRestart)
                {
                    _criticalStreak++;
                    if (_criticalStreak >= CriticalStreakLimit)
                    {
                        // حافظه چند بار متوالی بحرانی بود — GC نهایی و ری‌استارت
                        Cosmos.Core.Memory.Heap.Collect();
                        System.Threading.Thread.Sleep(200);

                        ulong afterGC = Cosmos.Core.GCImplementation.GetUsedRAM() / (1024UL * 1024UL);
                        if (afterGC >= effectiveRestart - 8)
                        {
                            // GC کمکی نکرد → ری‌استارت هوشمند
                            try
                            {
                                var c = GraphicsManager.Canvas;
                                if (c != null)
                                {
                                    int W = GraphicsManager.Width;
                                    int H = GraphicsManager.Height;
                                    c.Clear(Color.FromArgb(0, 0, 160));
                                    var wp = new Pen(Color.White);
                                    string pctStr = _totalRAM_MB > 0
                                        ? " (" + ((usedMB * 100) / _totalRAM_MB).ToString() + "% used)"
                                        : "";
                                    c.DrawString("Low Memory" + pctStr + " — Restarting...", DefaultFont, wp, W / 2 - 100, H / 2 - 10);
                                    c.Display();
                                }
                            }
                            catch { }
                            Sys.Power.Reboot();
                        }
                        else
                        {
                            _criticalStreak = 0; // GC موفق بود
                        }
                    }
                    else
                    {
                        // GC اضطراری
                        Cosmos.Core.Memory.Heap.Collect();
                    }
                }
                else if (usedMB >= effectiveCritical)
                {
                    // ۷۵٪: منطقه خطر — GC فوری اما هنوز ری‌استارت نه
                    Cosmos.Core.Memory.Heap.Collect();
                    _criticalStreak = 0;
                }
                else if (usedMB >= effectiveWarn)
                {
                    // ۶۰٪: هشدار — GC پیشگیرانه سبک
                    Cosmos.Core.Memory.Heap.Collect();
                    _criticalStreak = 0;
                }
                else
                {
                    _criticalStreak = 0;
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  لود فونت Vazir
        // ═══════════════════════════════════════════════════════════════════
        private void LoadVazirFont()
        {
            try
            {
                if (VazirFontBytes == null || VazirFontBytes.Length < 64)
                {
                    System.Console.ForegroundColor = ConsoleColor.Yellow;
                    System.Console.WriteLine("Vazir font not embedded — using PCScreenFont fallback.");
                    System.Console.ResetColor();
                    return;
                }

                // اندازه عادی: 16px (برای عنوان پنجره، متن UI)
                VazirFont = TtfFont.Load(VazirFontBytes, 16);

                // اندازه کوچک: 13px (برای taskbar، status bar)
                VazirFontSm = TtfFont.Load(VazirFontBytes, 13);

                if (VazirFont != null)
                {
                    System.Console.ForegroundColor = ConsoleColor.Green;
                    System.Console.WriteLine($"Vazir TTF loaded OK. ({VazirFontBytes.Length / 1024} KB)");
                }
                else
                {
                    System.Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine("Vazir TTF load FAILED — using PCScreenFont fallback.");
                }
                System.Console.ResetColor();
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Vazir font exception: " + e.Message);
                VazirFont = null;
                VazirFontSm = null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  لود Bitmapها
        // ═══════════════════════════════════════════════════════════════════
        private void LoadBitmaps()
        {
            // ─── Wallpaper: PNG اولویت دارد، BMP به عنوان fallback ────────────
            WallpaperPng = null;
            Wallpaper = null;
            System.Console.WriteLine("Loading wallpaper...");
            try
            {
                if (WallpaperPngByte?.Length > 0)
                {
                    System.Console.WriteLine($"PNG bytes: {WallpaperPngByte.Length}");
                    WallpaperPng = PngDecoder.Decode(WallpaperPngByte);
                    if (WallpaperPng != null && WallpaperPng.IsValid)
                        System.Console.WriteLine($"Wallpaper PNG OK ({WallpaperPng.Width}x{WallpaperPng.Height})");
                    else
                    {
                        System.Console.WriteLine("PNG decode failed, trying BMP...");
                        WallpaperPng = null;
                    }
                }
                else
                {
                    System.Console.WriteLine("No PNG embedded, trying BMP...");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("PNG exception: " + ex.Message);
                WallpaperPng = null;
            }

            if (WallpaperPng == null)
            {
                try
                {
                    if (WallpaperByte?.Length > 0)
                    {
                        Wallpaper = new Bitmap(WallpaperByte);
                        System.Console.WriteLine("Wallpaper BMP loaded OK.");
                    }
                    else System.Console.WriteLine("No BMP embedded either.");
                }
                catch { Wallpaper = null; System.Console.WriteLine("BMP load failed."); }
            }

            try { if (CursorByte?.Length > 0) CursorBitmap = new Bitmap(CursorByte); }
            catch { CursorBitmap = null; }

            try { if (IconSettingsByte != null) IconSettings = new Bitmap(IconSettingsByte); } catch { }
            try { if (IconNotepadByte != null) IconNotepad = new Bitmap(IconNotepadByte); } catch { }
            try { if (IconFilesByte != null) IconFiles = new Bitmap(IconFilesByte); } catch { }
            try { if (IconTerminalByte != null) IconTerminal = new Bitmap(IconTerminalByte); } catch { }
            try { if (IconFolder20Byte != null) IconFolder20 = new Bitmap(IconFolder20Byte); } catch { }
            try { if (IconBrowserByte != null) IconBrowser = new Bitmap(IconBrowserByte); } catch { }
            try { if (IconMusicPlayerByte != null) IconMusicPlayer = new Bitmap(IconMusicPlayerByte); } catch { }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  فایل‌سیستم
        // ═══════════════════════════════════════════════════════════════════
        private void SetupFileSystem()
        {
            try
            {
                var fs = new CosmosVFS();
                Sys.FileSystem.VFS.VFSManager.RegisterVFS(fs);
                System.Console.WriteLine("FS OK - Free: " + fs.GetAvailableFreeSpace(CurrentPath));

                // ─── ساخت پوشه Assets\Images (بدون نوشتن فایل — فقط ساختار) ─
                EnsureAssetDirectories();
            }
            catch (Exception e)
            {
                System.Console.WriteLine("FS Error: " + e.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ساخت پوشه‌های لازم در VFS — بدون نوشتن فایل‌های سنگین
        //
        //  چرا فایل‌ها را نمی‌نویسیم؟
        //  نوشتن فایل‌های بزرگ (مثلاً Wallpaper.png = 1.4MB) روی FAT32 در
        //  Cosmos در هنگام بوت باعث هنگ کردن سیستم می‌شود.
        //
        //  راه‌حل: تصاویر والپیپر از embed استفاده می‌شوند (مثل قبل) و
        //  کاربر می‌تواند تصاویر کوچک‌تر را خودش روی دیسک قرار دهد.
        //  لیست Settings هر فایلی که در این پوشه باشد را نشان می‌دهد.
        // ═══════════════════════════════════════════════════════════════════
        private void EnsureAssetDirectories()
        {
            try
            {
                if (!System.IO.Directory.Exists(@"0:\Assets"))
                    System.IO.Directory.CreateDirectory(@"0:\Assets");
                if (!System.IO.Directory.Exists(@"0:\Assets\Images"))
                    System.IO.Directory.CreateDirectory(@"0:\Assets\Images");
                if (!System.IO.Directory.Exists(@"0:\Settings"))
                    System.IO.Directory.CreateDirectory(@"0:\Settings");
                // ─── پوشه‌ی نصب برنامه‌های .pap ──────────────────────────
                ParsOS.Apps.Packaging.PapPackage.EnsureDirectories();
                SeedEmbeddedApps();
                System.Console.WriteLine(@"Dir OK: 0:\Assets\Images, 0:\Settings, 0:\Apps\Installed");
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Dir create error: " + e.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ⛔ غیرفعال شده — سیستم نصب/اجرای برنامه‌های .pap (PapAppRuntime)
        //  پایدار نبود (نشتی حافظه‌ی پیوسته حین اجرا) و کاملاً بسته شده.
        //  دیگر هیچ برنامه‌ی embedded ای موقع بوت seed/نصب نمی‌شود.
        // ═══════════════════════════════════════════════════════════════════
        private void SeedEmbeddedApps()
        {
            // عمداً خالی — قبلاً اینجا SeedOneApp("calcapp2", ...) صدا زده می‌شد.
        }

        private void SeedOneApp(string appId, byte[] manifestBytes, byte[] wasmBytes)
        {
            try
            {
                System.Console.WriteLine("Seed check " + appId + ": manifest=" +
                    (manifestBytes == null ? "NULL" : manifestBytes.Length.ToString()) +
                    " wasm=" + (wasmBytes == null ? "NULL" : wasmBytes.Length.ToString()));

                if (manifestBytes == null || wasmBytes == null) return;

                string dir = ParsOS.Apps.Packaging.PapPackage.AppDir(appId);
                string manifestPath = ParsOS.Apps.Packaging.PapPackage.ManifestPath(appId);
                string wasmPath = ParsOS.Apps.Packaging.PapPackage.WasmPath(appId);

                // به‌جای چک‌کردن فقط وجود پوشه (که با یک نصبِ نصفه‌ونیمه هم true
                // می‌شد و دیگر هیچ‌وقت دوباره تلاش نمی‌کرد)، هر دو فایل را چک کن
                bool alreadyComplete = System.IO.File.Exists(manifestPath) && System.IO.File.Exists(wasmPath);
                if (alreadyComplete) return;

                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllBytes(manifestPath, manifestBytes);
                System.Console.WriteLine("manifest written");

                System.IO.File.WriteAllBytes(wasmPath, wasmBytes);
                System.Console.WriteLine("wasm written");

                System.Console.WriteLine("Seeded embedded app: " + appId);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("SeedOneApp error (" + appId + "): " + e.Message);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ذخیره و بارگذاری تنظیمات — فایل متنی ساده روی FAT32
        //  فرمت: key=value (یک خط برای هر تنظیم)
        // ═══════════════════════════════════════════════════════════════════
        private const string SettingsPath = @"0:\Settings\settings.cfg";

        public static void SaveSetting(string key, string value)
        {
            try
            {
                // بارگذاری تنظیمات موجود
                var lines = new System.Collections.Generic.List<string>();
                if (System.IO.File.Exists(SettingsPath))
                {
                    var existing = System.IO.File.ReadAllText(SettingsPath);
                    foreach (var line in existing.Split('\n'))
                        if (line.Trim().Length > 0 && !line.StartsWith(key + "="))
                            lines.Add(line.Trim());
                }
                // اضافه کردن تنظیم جدید
                lines.Add(key + "=" + value);
                System.IO.File.WriteAllText(SettingsPath, string.Join("\n", lines));
            }
            catch (Exception e)
            {
                System.Console.WriteLine("SaveSetting error: " + e.Message);
            }
        }

        public static string LoadSetting(string key, string defaultValue = "")
        {
            try
            {
                if (!System.IO.File.Exists(SettingsPath)) return defaultValue;
                var content = System.IO.File.ReadAllText(SettingsPath);
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith(key + "="))
                        return trimmed.Substring(key.Length + 1);
                }
            }
            catch { }
            return defaultValue;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  BSOD
        // ═══════════════════════════════════════════════════════════════════
        private void BlueScreen(string msg)
        {
            try
            {
                var c = GraphicsManager.Canvas;
                int W = GraphicsManager.Width;
                int H = GraphicsManager.Height;
                Color bsodBg = Color.FromArgb(0, 0, 180);
                c.Clear(bsodBg);

                var wp = new Pen(Color.White);
                var bp = new Pen(Color.FromArgb(160, 200, 255));
                var hp = new Pen(Color.FromArgb(180, 220, 255));

                if (VazirFont != null)
                {
                    VazirFont.DrawString(c, ":(", wp, W / 2 - 16, H / 3, int.MaxValue, int.MaxValue, bsodBg);
                    VazirFont.DrawString(c, "ParsOS ran into a problem and needs to restart.",
                        wp, W / 2 - 220, H / 3 + 55, int.MaxValue, int.MaxValue, bsodBg);
                    VazirFont.DrawString(c, msg, bp, W / 2 - 220, H / 3 + 80, int.MaxValue, int.MaxValue, bsodBg);
                    VazirFont.DrawString(c, "Please restart your computer.",
                        wp, W / 2 - 120, H / 3 + 120, int.MaxValue, int.MaxValue, bsodBg);
                    VazirFont.DrawString(c, "Press  Shift + Alt  to restart now.",
                        hp, W / 2 - 140, H / 3 + 150, int.MaxValue, int.MaxValue, bsodBg);
                }
                else
                {
                    c.DrawString(":(", DefaultFont, wp, W / 2 - 16, H / 3);
                    c.DrawString("ParsOS ran into a problem and needs to restart.",
                        DefaultFont, wp, W / 2 - 200, H / 3 + 60);
                    c.DrawString(msg, DefaultFont, bp, W / 2 - 200, H / 3 + 88);
                    c.DrawString("Please restart your computer.",
                        DefaultFont, wp, W / 2 - 80, H / 3 + 130);
                    c.DrawString("Press  Shift + Alt  to restart now.",
                        DefaultFont, hp, W / 2 - 110, H / 3 + 155);
                }
                c.Display();
            }
            catch { }

            // ─── انتظار برای Shift+Alt — بدون allocation ─────────────────────
            bool shiftHeld = false;
            while (true)
            {
                try
                {
                    if (Sys.KeyboardManager.KeyAvailable)
                    {
                        var key = Sys.KeyboardManager.ReadKey();
                        // Shift چپ یا راست
                        if (key.Key == ConsoleKeyEx.LShift || key.Key == ConsoleKeyEx.RShift)
                            shiftHeld = true;
                        else if (shiftHeld && (key.Key == ConsoleKeyEx.LAlt || key.Key == ConsoleKeyEx.RAlt))
                            Sys.Power.Reboot();
                        else if (key.Key != ConsoleKeyEx.LShift && key.Key != ConsoleKeyEx.RShift)
                            shiftHeld = false; // کلید دیگری فشار داده شد → ریست
                    }
                }
                catch { }
            }
        }
    }
}