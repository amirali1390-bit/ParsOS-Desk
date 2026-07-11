// PapAppRuntime.cs
// پوشه پیشنهادی: Apps/PapAppRuntime.cs
//
// ⛔ غیرفعال شده — کل سیستم اجرای برنامه‌های .pap (WASM runtime) به همراه
// نصب آن‌ها بسته شده است. این زیرسیستم در عمل پایدار نبود: در حین اجرا
// (هر فریم که Update/Draw صدا زده می‌شد) مصرف RAM به‌طور پیوسته بالا
// می‌رفت و هیچ‌وقت واقعاً پایین نمی‌آمد.
//
// این فایل عمداً به یک stub خالی/بی‌اثر تبدیل شده، نه حذف کامل، چون
// GraphicsManager.cs و Kernel.cs هنوز به همین امضاها (ContentPrefix،
// MakeContent، IsPapAppContent، Launch، Draw، Update، HandleClick،
// CleanupState) رفرنس دارند. اگر تصمیم گرفتی این ویژگی را کاملاً از پروژه
// حذف کنی، آن رفرنس‌ها را هم باید در GraphicsManager.cs/Kernel.cs پاک کنی؛
// من فراخوانی‌های Kernel.cs (seed کردن calcapp2) و GraphicsManager.cs
// (آیکون Calculator در تسک‌بار/استارت‌منو) را هم در همین رفع مشکل حذف
// کرده‌ام تا هیچ مسیر کاربری برای نصب یا اجرای یک .pap باقی نماند.
//
// هیچ WasmAppInstance، HostImportTable، یا فایل .wasm دیگر لود یا اجرا
// نمی‌شود.

using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using ParsOS.GUI;

namespace ParsOS.Apps
{
    public static class PapAppRuntime
    {
        // نمونه‌ی Content هر پنجره‌ی برنامه: "PAPAPP:com.example.myapp"
        // (نگه داشته شده فقط برای سازگاری امضا — دیگر هیچ پنجره‌ای با این
        // Content واقعاً باز نمی‌شود چون Launch دیگر کاری نمی‌کند)
        public const string ContentPrefix = "PAPAPP:";

        public static string MakeContent(string appId) => ContentPrefix + appId;

        public static bool IsPapAppContent(string content) =>
            !string.IsNullOrEmpty(content) && content.StartsWith(ContentPrefix);

        public static string ExtractAppId(string content) =>
            IsPapAppContent(content) ? content.Substring(ContentPrefix.Length) : "";

        // ⛔ دیگر هیچ پنجره‌ای باز نمی‌کند و هیچ برنامه‌ای اجرا نمی‌شود.
        public static void Launch(string appId)
        {
            Console.WriteLine("PapAppRuntime.Launch: غیرفعال شده (پایدار نبود) — appId=" + appId);
        }

        // نگه داشته شده برای سازگاری امضا با OpenNewWindow — دیگر صدا زده نمی‌شود
        // چون هیچ محتوایی از نوع PAPAPP باز نمی‌شود، اما اگر جایی هنوز رفرنس
        // داشته باشد بی‌خطر یک مقدار پیش‌فرض برمی‌گرداند.
        public static (int w, int h) GetPreferredWindowSize(string appId) => (480, 320);

        // ⛔ هیچ WASM ای اجرا/رسم نمی‌شود.
        public static void Draw(WindowInfo w, WindowCanvas canvas, TtfFont font, int cx, int cy)
        {
            canvas.DrawTtf(font, "اجرای برنامه‌های نصب‌شده غیرفعال شده است.", Pens.WindowBorder, cx, cy, Theme.WindowBg);
        }

        // ⛔ هیچ Update ای اجرا نمی‌شود.
        public static void Update(WindowInfo w, int dtMs) { }

        // ⛔ هیچ کلیکی به WASM منتقل نمی‌شود.
        public static void HandleClick(WindowInfo w, int mx, int my) { }

        // چیزی برای پاکسازی نیست چون هیچ state ای ساخته نمی‌شود؛
        // امضا فقط برای سازگاری با GraphicsManager نگه داشته شده.
        public static void CleanupState(WindowInfo w) { }
    }
}