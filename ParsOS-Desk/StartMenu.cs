// StartMenu.cs
// این فایل فقط برای documentation است.
// منطق منوی استارت در GraphicsManager.cs پیاده‌سازی شده
// تا از دسترسی مستقیم به Canvas بدون overhead اضافه استفاده شود.
//
// ساختار منوی استارت:
//   ┌────────────────────┐
//   │  ParsOS            │  ← عنوان
//   ├────────────────────┤
//   │                    │  ← فضا برای آیتم‌های آینده
//   │                    │
//   ├────────────────────┤
//   │  [Shut Down]       │  ← قرمز
//   │  [Restart ]        │  ← زرد
//   └────────────────────┘
//
// برای اضافه کردن آیتم‌های منو در آینده:
//   در DrawStartMenu() یک DrawString + hit-test در HandleClick() اضافه کنید.

namespace ParsOS.GUI
{
    // placeholder - منطق واقعی در GraphicsManager است
    public static class StartMenuInfo
    {
        public const int Width = 330;
        public const int Height = 360;
    }
}