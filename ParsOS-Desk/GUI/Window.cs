// Window.cs - حالا فقط یک wrapper برای باز کردن پنجره جدید
// رندر واقعی توسط GraphicsManager انجام می‌شود

namespace ParsOS.GUI
{
    /// <summary>
    /// برای باز کردن یک پنجره جدید از هر جایی در کد کافی است بنویسید:
    ///   new Window("عنوان", "محتوا");
    /// </summary>
    public class Window
    {
        public Window(string title, string content = "")
        {
            GraphicsManager.OpenNewWindow(title, content);
        }
    }
}