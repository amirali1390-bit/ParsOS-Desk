// PapPackage.cs
// پوشه پیشنهادی: Apps/Packaging/PapPackage.cs
//
// فرمت بسته‌ی نصب اختصاصی Xagros OS: پسوند «.pap»
//
// ⚠️ مرز مسئولیت این فایل (مهم، لطفاً قبل از استفاده بخوانید):
// این فایل فقط «فرمت بسته + نصب/حذف + رجیستری برنامه‌های نصب‌شده» را پیاده
// می‌کند و کاملاً مستقل و قابل‌کامپایل است. اجرای واقعیِ کدِ داخل بسته
// (یعنی چیزی که در بخش app.wasm ذخیره می‌شود) به AppInterpreterWasm.cs
// وابسته است که در همین پروژه هنوز فقط یک اسکلت ناقص است — WasmModule.Parse
// یک NotImplementedException پرتاب می‌کند و WasmInterpreter.CallFunction
// کنترل‌فلوی واقعی (block/loop/if/br) ندارد. یعنی:
//   • نصب، رجیستری، آیکون، باز شدن پنجره و اتصال به File Explorer → همین حالا کار می‌کند.
//   • محتوای واقعیِ رسم‌شده‌ی داخل برنامه → وقتی نمایش داده می‌شود که آن مفسر
//     تمام شود (کار جدا و بزرگی است؛ عمداً اینجا برایش تظاهر به کارکردن نکردم).
// PapAppRuntime.cs (فایل کناری) دقیقاً همین مرز را رعایت می‌کند: اگر مفسر
// fault کند، پنجره‌ی برنامه یک پیام «هنوز آماده نیست» تمیز نشان می‌دهد،
// نه کرش یا صفحه‌ی خالی.
//
// ───────────────────────────────────────────────────────────────────────
// ساختار باینری فایل .pap (little-endian، بدون هیچ کتابخانه‌ی فشرده‌سازی/
// سریالایز خارجی — چون زیر IL2CPU نمی‌توان به پشتیبانی کامل آن‌ها مطمئن بود؛
// دقیقاً همان فلسفه‌ای که در AppInterpreterWasm برای WASM هم رعایت شده):
//
//   offset  0  : magic        4 بایت   ASCII "XPAP"
//   offset  4  : version      1 بایت   = 1
//   سپس به‌ترتیب سه بلوک (هرکدام: طول int32 + داده):
//     manifest  → متن UTF8، سبک key=value (دقیقاً مثل 0:\Settings\settings.cfg)
//     icon      → بایت‌های خام یک فایل BMP (همان فرمتی که FileExplorerApp با
//                 «new Bitmap(data)» می‌خواند). طول صفر = بدون آیکون.
//     wasm      → ماژول .wasm برنامه (ورودی AppInterpreterWasm.WasmModule.Parse)
//
// فیلدهای شناخته‌شده‌ی manifest:
//   id=com.example.myapp      (اجباری؛ یکتا؛ اسم پوشه‌ی نصب هم همین می‌شود)
//   name=My App                (اجباری؛ چیزی که در Start Menu دیده می‌شود)
//   version=1.0.0
//   author=Someone
//   description=یک برنامه‌ی نمونه
//   window_w=480
//   window_h=320
// ───────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ParsOS.Apps.Packaging
{
    // ═══════════════════════════════════════════════════════════
    //  مانیفست یک برنامه‌ی .pap
    // ═══════════════════════════════════════════════════════════
    public sealed class PapManifest
    {
        public string Id = "";
        public string Name = "";
        public string Version = "1.0.0";
        public string Author = "";
        public string Description = "";
        public int WindowW = 480;
        public int WindowH = 320;

        public bool IsValid => Id.Length > 0 && Name.Length > 0;

        // ─── همان سبک key=value که Kernel.SaveSetting/LoadSetting استفاده می‌کند ──
        public static PapManifest Parse(string text)
        {
            var m = new PapManifest();
            if (string.IsNullOrEmpty(text)) return m;

            foreach (var rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "id": m.Id = SanitizeId(val); break;
                    case "name": m.Name = val; break;
                    case "version": m.Version = val; break;
                    case "author": m.Author = val; break;
                    case "description": m.Description = val; break;
                    case "window_w":
                        if (int.TryParse(val, out int ww) && ww >= 240) m.WindowW = ww;
                        break;
                    case "window_h":
                        if (int.TryParse(val, out int wh) && wh >= 160) m.WindowH = wh;
                        break;
                }
            }
            return m;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("id=").Append(Id).Append('\n');
            sb.Append("name=").Append(Name).Append('\n');
            sb.Append("version=").Append(Version).Append('\n');
            sb.Append("author=").Append(Author).Append('\n');
            sb.Append("description=").Append(Description).Append('\n');
            sb.Append("window_w=").Append(WindowW.ToString()).Append('\n');
            sb.Append("window_h=").Append(WindowH.ToString()).Append('\n');
            return sb.ToString();
        }

        // ─── id فقط باید مسیرِ پوشه‌ی معتبر روی FAT32 باشد ─────────────
        public static string SanitizeId(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-')
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  نتیجه‌ی یک عملیات نصب — به‌جای exception، چون در محیط کرنل بهتر
    //  است فراخوان (UI نصب) خودش تصمیم بگیرد چطور خطا را نشان دهد.
    // ═══════════════════════════════════════════════════════════
    public struct PapInstallResult
    {
        public bool Success;
        public string Error;
        public PapManifest Manifest;

        public static PapInstallResult Ok(PapManifest m) =>
            new PapInstallResult { Success = true, Manifest = m, Error = "" };

        public static PapInstallResult Fail(string err) =>
            new PapInstallResult { Success = false, Error = err };
    }

    // ═══════════════════════════════════════════════════════════
    //  PapPackage — parse/build/install/uninstall/registry
    // ═══════════════════════════════════════════════════════════
    public static class PapPackage
    {
        public const string Extension = "PAP";

        private static readonly byte[] Magic = { (byte)'X', (byte)'P', (byte)'A', (byte)'P' };
        private const byte FormatVersion = 1;

        // همه‌ی برنامه‌های نصب‌شده اینجا استخراج می‌شوند:
        // 0:\Apps\Installed\<id>\manifest.cfg
        // 0:\Apps\Installed\<id>\icon.bmp     (اختیاری)
        // 0:\Apps\Installed\<id>\app.wasm
        public const string InstallRoot = @"0:\Apps\Installed";

        public static void EnsureDirectories()
        {
            try
            {
                if (!Directory.Exists(@"0:\Apps")) Directory.CreateDirectory(@"0:\Apps");
                if (!Directory.Exists(InstallRoot)) Directory.CreateDirectory(InstallRoot);
            }
            catch (Exception e)
            {
                Console.WriteLine("PapPackage.EnsureDirectories error: " + e.Message);
            }
        }

        public static string AppDir(string appId) => InstallRoot + "\\" + appId;
        public static string ManifestPath(string appId) => AppDir(appId) + "\\manifest.cfg";
        public static string IconPath(string appId) => AppDir(appId) + "\\icon.bmp";
        public static string WasmPath(string appId) => AppDir(appId) + "\\app.wsm";

        // ═══════════════════════════════════════════════════════
        //  خواندن بسته از بایت‌های خام (بدون لمس دیسک — قابل تست جدا)
        // ═══════════════════════════════════════════════════════
        private sealed class ParsedPackage
        {
            public PapManifest Manifest;
            public byte[] Icon;   // ممکن است خالی (طول ۰) باشد
            public byte[] Wasm;
        }

        private static bool TryParseBytes(byte[] data, out ParsedPackage result, out string error)
        {
            result = null;
            error = "";

            if (data == null || data.Length < 9)
            { error = "فایل خیلی کوچک/خالی است"; return false; }

            for (int i = 0; i < 4; i++)
                if (data[i] != Magic[i])
                { error = "فرمت فایل .pap معتبر نیست (magic اشتباه)"; return false; }

            if (data[4] != FormatVersion)
            { error = "نسخه‌ی فرمت .pap پشتیبانی نمی‌شود: " + data[4]; return false; }

            int pos = 5;
            if (!ReadBlock(data, ref pos, out byte[] manifestBytes, out error)) return false;
            if (!ReadBlock(data, ref pos, out byte[] iconBytes, out error)) return false;
            if (!ReadBlock(data, ref pos, out byte[] wasmBytes, out error)) return false;

            string manifestText = Encoding.UTF8.GetString(manifestBytes, 0, manifestBytes.Length);
            var manifest = PapManifest.Parse(manifestText);

            if (!manifest.IsValid)
            { error = "مانیفست ناقص است (id یا name خالی است)"; return false; }

            result = new ParsedPackage { Manifest = manifest, Icon = iconBytes, Wasm = wasmBytes };
            return true;
        }

        private static bool ReadBlock(byte[] data, ref int pos, out byte[] block, out string error)
        {
            block = null; error = "";
            if (pos + 4 > data.Length) { error = "فایل بریده/خراب است"; return false; }

            int len = BitConverter.ToInt32(data, pos);
            pos += 4;

            if (len < 0 || pos + len > data.Length)
            { error = "طول بلوک داخل فایل نامعتبر است"; return false; }

            block = new byte[len];
            Array.Copy(data, pos, block, 0, len);
            pos += len;
            return true;
        }

        // ═══════════════════════════════════════════════════════
        //  ساخت بسته (ابزار توسعه‌دهنده — برای تولید فایل‌های .pap تست)
        // ═══════════════════════════════════════════════════════
        public static byte[] Build(PapManifest manifest, byte[] iconBmpBytes, byte[] wasmBytes)
        {
            if (manifest == null || !manifest.IsValid)
                throw new ArgumentException("مانیفست نامعتبر: id/name اجباری هستند");

            byte[] manifestBytes = Encoding.UTF8.GetBytes(manifest.Serialize());
            byte[] icon = iconBmpBytes ?? new byte[0];
            byte[] wasm = wasmBytes ?? new byte[0];

            using (var ms = new MemoryStream())
            {
                ms.Write(Magic, 0, 4);
                ms.WriteByte(FormatVersion);
                WriteBlock(ms, manifestBytes);
                WriteBlock(ms, icon);
                WriteBlock(ms, wasm);
                return ms.ToArray();
            }
        }

        private static void WriteBlock(MemoryStream ms, byte[] block)
        {
            byte[] lenBytes = BitConverter.GetBytes(block.Length);
            ms.Write(lenBytes, 0, 4);
            ms.Write(block, 0, block.Length);
        }

        // ═══════════════════════════════════════════════════════
        //  نصب — دقیقاً همان چیزی که با دوبار کلیک روی .pap در File
        //  Explorer باید صدا زده شود. بدون wizard؛ یک مرحله، بدون سؤال.
        // ═══════════════════════════════════════════════════════
        public static PapInstallResult Install(string papFilePath)
        {
            try
            {
                EnsureDirectories();

                if (!File.Exists(papFilePath))
                    return PapInstallResult.Fail("فایل پیدا نشد: " + papFilePath);

                byte[] data = File.ReadAllBytes(papFilePath);
                if (!TryParseBytes(data, out ParsedPackage pkg, out string parseErr))
                    return PapInstallResult.Fail(parseErr);

                string dir = AppDir(pkg.Manifest.Id);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(ManifestPath(pkg.Manifest.Id), pkg.Manifest.Serialize());
                if (pkg.Icon.Length > 0)
                    File.WriteAllBytes(IconPath(pkg.Manifest.Id), pkg.Icon);
                File.WriteAllBytes(WasmPath(pkg.Manifest.Id), pkg.Wasm);

                return PapInstallResult.Ok(pkg.Manifest);
            }
            catch (Exception e)
            {
                return PapInstallResult.Fail("خطا در نصب: " + e.Message);
            }
        }

        public static bool Uninstall(string appId)
        {
            try
            {
                string dir = AppDir(appId);
                if (!Directory.Exists(dir)) return false;

                foreach (var f in Directory.GetFiles(dir))
                    File.Delete(f);
                Directory.Delete(dir);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("PapPackage.Uninstall error: " + e.Message);
                return false;
            }
        }

        public static bool IsInstalled(string appId) => Directory.Exists(AppDir(appId));

        // ─── رجیستری = اسکن ساده‌ی پوشه‌ها، بدون فایل ایندکس جدا (کمتر جای
        // خطا برای desync بین رجیستری و فایل‌های واقعی روی دیسک) ──────────
        public static List<PapManifest> ListInstalled()
        {
            var list = new List<PapManifest>();
            try
            {
                EnsureDirectories();
                foreach (var dir in Directory.GetDirectories(InstallRoot))
                {
                    string mf = dir.TrimEnd('\\') + "\\manifest.cfg";
                    if (!File.Exists(mf)) continue;
                    var m = PapManifest.Parse(File.ReadAllText(mf));
                    if (m.IsValid) list.Add(m);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("PapPackage.ListInstalled error: " + e.Message);
            }
            return list;
        }

        public static PapManifest LoadManifest(string appId)
        {
            try
            {
                string mf = ManifestPath(appId);
                if (!File.Exists(mf)) return null;
                var m = PapManifest.Parse(File.ReadAllText(mf));
                return m.IsValid ? m : null;
            }
            catch { return null; }
        }
    }
}