using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;
using ParsOS.Apps;
using ParsOS.Apps;
using ParsOS.Network;
using ParsOS.Audio;
using Sys = Cosmos.System;

namespace ParsOS.GUI
{
    // ═══════════════════════════════════════════════════════════
    //  تم رنگی - با پشتیبانی از حالت روشن/تاریک
    // ═══════════════════════════════════════════════════════════
    public static class Theme
    {
        public static bool DarkMode = true;

        // ─── تم تاریک ───────────────────────────────────────────
        private static Color _dark_Desktop = Color.FromArgb(20, 20, 35);
        private static Color _dark_TitleBar = Color.FromArgb(30, 30, 46);
        private static Color _dark_TitleBarInact = Color.FromArgb(25, 25, 38);
        private static Color _dark_TitleBarText = Color.FromArgb(220, 220, 235);
        private static Color _dark_WindowBg = Color.FromArgb(42, 42, 62);
        private static Color _dark_WindowBorder = Color.FromArgb(80, 80, 120);
        private static Color _dark_Taskbar = Color.FromArgb(18, 18, 30);
        private static Color _dark_TaskbarItem = Color.FromArgb(50, 50, 75);
        private static Color _dark_TaskbarActive = Color.FromArgb(80, 80, 130);
        private static Color _dark_TaskbarText = Color.FromArgb(210, 210, 230);
        private static Color _dark_TaskbarClock = Color.FromArgb(180, 180, 210);
        private static Color _dark_TextPrimary = Color.FromArgb(220, 220, 235);
        private static Color _dark_GridDot = Color.FromArgb(35, 35, 55);
        private static Color _dark_StartMenuBg = Color.FromArgb(28, 28, 44);
        private static Color _dark_StartMenuBdr = Color.FromArgb(90, 90, 140);

        // ─── تم روشن ────────────────────────────────────────────
        private static Color _light_Desktop = Color.FromArgb(235, 235, 245);
        private static Color _light_TitleBar = Color.FromArgb(210, 212, 228);
        private static Color _light_TitleBarInact = Color.FromArgb(220, 220, 232);
        private static Color _light_TitleBarText = Color.FromArgb(30, 30, 50);
        private static Color _light_WindowBg = Color.FromArgb(245, 245, 255);
        private static Color _light_WindowBorder = Color.FromArgb(160, 160, 200);
        private static Color _light_Taskbar = Color.FromArgb(200, 202, 220);
        private static Color _light_TaskbarItem = Color.FromArgb(220, 222, 240);
        private static Color _light_TaskbarActive = Color.FromArgb(180, 185, 230);
        private static Color _light_TaskbarText = Color.FromArgb(30, 30, 60);
        private static Color _light_TaskbarClock = Color.FromArgb(60, 60, 100);
        private static Color _light_TextPrimary = Color.FromArgb(30, 30, 55);
        private static Color _light_GridDot = Color.FromArgb(210, 210, 225);
        private static Color _light_StartMenuBg = Color.FromArgb(225, 226, 240);
        private static Color _light_StartMenuBdr = Color.FromArgb(150, 150, 200);

        // ─── رنگ‌های مشترک ──────────────────────────────────────
        public static Color BtnClose = Color.FromArgb(255, 96, 92);
        public static Color BtnMin = Color.FromArgb(255, 189, 68);
        public static Color BtnMax = Color.FromArgb(40, 200, 64);
        public static Color Accent = Color.FromArgb(100, 120, 230);
        public static Color AccentHover = Color.FromArgb(130, 150, 255);

        // ─── Getters پویا ────────────────────────────────────────
        public static Color Desktop => DarkMode ? _dark_Desktop : _light_Desktop;
        public static Color TitleBar => DarkMode ? _dark_TitleBar : _light_TitleBar;
        public static Color TitleBarInact => DarkMode ? _dark_TitleBarInact : _light_TitleBarInact;
        public static Color TitleBarText => DarkMode ? _dark_TitleBarText : _light_TitleBarText;
        public static Color WindowBg => DarkMode ? _dark_WindowBg : _light_WindowBg;
        public static Color WindowBorder => DarkMode ? _dark_WindowBorder : _light_WindowBorder;
        public static Color Taskbar => DarkMode ? _dark_Taskbar : _light_Taskbar;
        public static Color TaskbarItem => DarkMode ? _dark_TaskbarItem : _light_TaskbarItem;
        public static Color TaskbarActive => DarkMode ? _dark_TaskbarActive : _light_TaskbarActive;
        public static Color TaskbarText => DarkMode ? _dark_TaskbarText : _light_TaskbarText;
        public static Color TaskbarClock => DarkMode ? _dark_TaskbarClock : _light_TaskbarClock;
        public static Color TextPrimary => DarkMode ? _dark_TextPrimary : _light_TextPrimary;
        public static Color GridDot => DarkMode ? _dark_GridDot : _light_GridDot;
        public static Color StartMenuBg => DarkMode ? _dark_StartMenuBg : _light_StartMenuBg;
        public static Color StartMenuBorder => DarkMode ? _dark_StartMenuBdr : _light_StartMenuBdr;
    }

    // ═══════════════════════════════════════════════════════════
    //  Pen Pool - هر فریم rebuild می‌شود وقتی تم عوض شود
    //  (Pen‌ها برای DrawString و DrawLine با TTF هنوز لازمند)
    // ═══════════════════════════════════════════════════════════
    public static class Pens
    {
        // پن‌های ثابت (رنگ‌های مشترک)
        public static readonly Pen BtnClose = new Pen(Theme.BtnClose);
        public static readonly Pen BtnMin = new Pen(Theme.BtnMin);
        public static readonly Pen BtnMax = new Pen(Theme.BtnMax);
        public static readonly Pen Accent = new Pen(Theme.Accent);
        public static readonly Pen AccentHover = new Pen(Theme.AccentHover);
        public static readonly Pen White = new Pen(Color.White);
        public static readonly Pen Black = new Pen(Color.Black);
        public static readonly Pen Separator = new Pen(Color.FromArgb(60, 60, 90));
        public static readonly Pen ShutdownRed = new Pen(Color.FromArgb(200, 60, 60));
        public static readonly Pen RebootYellow = new Pen(Color.FromArgb(200, 160, 40));
        public static readonly Pen ShutdownHover = new Pen(Color.FromArgb(230, 80, 80));
        public static readonly Pen RebootHover = new Pen(Color.FromArgb(230, 190, 60));
        public static readonly Pen DimBorder = new Pen(Color.FromArgb(50, 50, 80));
        public static readonly Pen MemGreen = new Pen(Color.FromArgb(40, 200, 100));
        public static readonly Pen MemYellow = new Pen(Color.FromArgb(220, 170, 40));
        public static readonly Pen MemRed = new Pen(Color.FromArgb(220, 70, 70));

        // پن‌های پویا (بازسازی با تغییر تم)
        private static bool _lastDarkMode = true;
        private static Pen _desktop, _titleBar, _titleBarInact, _titleBarText,
                           _windowBg, _windowBorder, _taskbar, _taskbarItem,
                           _taskbarActive, _taskbarText, _taskbarClock,
                           _textPrimary, _startMenuBg, _startMenuBorder;

        private static void Rebuild()
        {
            // dispose کردن Pen‌های قدیمی قبل از ساخت جدید
            _desktop = new Pen(Theme.Desktop);
            _titleBar = new Pen(Theme.TitleBar);
            _titleBarInact = new Pen(Theme.TitleBarInact);
            _titleBarText = new Pen(Theme.TitleBarText);
            _windowBg = new Pen(Theme.WindowBg);
            _windowBorder = new Pen(Theme.WindowBorder);
            _taskbar = new Pen(Theme.Taskbar);
            _taskbarItem = new Pen(Theme.TaskbarItem);
            _taskbarActive = new Pen(Theme.TaskbarActive);
            _taskbarText = new Pen(Theme.TaskbarText);
            _taskbarClock = new Pen(Theme.TaskbarClock);
            _textPrimary = new Pen(Theme.TextPrimary);
            _startMenuBg = new Pen(Theme.StartMenuBg);
            _startMenuBorder = new Pen(Theme.StartMenuBorder);
            _lastDarkMode = Theme.DarkMode;
            // نکته بهینه‌سازی: قبلاً اینجا یک Heap.Collect() اجباری صدا زده
            // می‌شد. این باعث می‌شد دقیقاً وسط انیمیشن تغییر تم یک GC کامل و
            // synchronous اجرا شود (یک مکث محسوس/لَگ درست وسط نرمی انیمیشن).
            // چون GraphicsManager.Tick() از قبل یک زمان‌بند GC تطبیقی دارد
            // (_gcCounter + آستانه اضطراری RAM)، همان کافی است — Pen‌های
            // قدیمی به‌موقع و بدون مکث اضافه جمع‌آوری می‌شوند.
        }

        private static void EnsureFresh()
        {
            if (_desktop == null || _lastDarkMode != Theme.DarkMode) Rebuild();
        }

        public static Pen Desktop { get { EnsureFresh(); return _desktop; } }
        public static Pen TitleBar { get { EnsureFresh(); return _titleBar; } }
        public static Pen TitleBarInact { get { EnsureFresh(); return _titleBarInact; } }
        public static Pen TitleBarText { get { EnsureFresh(); return _titleBarText; } }
        public static Pen WindowBg { get { EnsureFresh(); return _windowBg; } }
        public static Pen WindowBorder { get { EnsureFresh(); return _windowBorder; } }
        public static Pen Taskbar { get { EnsureFresh(); return _taskbar; } }
        public static Pen TaskbarItem { get { EnsureFresh(); return _taskbarItem; } }
        public static Pen TaskbarActive { get { EnsureFresh(); return _taskbarActive; } }
        public static Pen TaskbarText { get { EnsureFresh(); return _taskbarText; } }
        public static Pen TaskbarClock { get { EnsureFresh(); return _taskbarClock; } }
        public static Pen TextPrimary { get { EnsureFresh(); return _textPrimary; } }
        public static Pen StartMenuBg { get { EnsureFresh(); return _startMenuBg; } }
        public static Pen StartMenuBorder { get { EnsureFresh(); return _startMenuBorder; } }
    }

    // ═══════════════════════════════════════════════════════════
    //  مدیر زبان ورودی — Shift+Alt برای تغییر EN↔FA
    // ═══════════════════════════════════════════════════════════
    public static class InputLanguage
    {
        public static bool IsFarsi = false;   // false=EN  true=FA

        public static void Toggle()
        {
            IsFarsi = !IsFarsi;
        }

        public static string Label => IsFarsi ? "FA" : "EN";
    }

    // ═══════════════════════════════════════════════════════════
    //  اطلاعات یک پنجره + وضعیت انیمیشن
    // ═══════════════════════════════════════════════════════════
    public class WindowInfo
    {
        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                // کش عنوان بریده‌شده برای تسک‌بار — از Substring در حلقه رندر جلوگیری می‌کند
                ShortTitle = (value != null && value.Length > 10) ? value.Substring(0, 10) : value;
            }
        }
        public string ShortTitle { get; private set; }

        public int X, Y, W, H;
        public bool Minimized;
        public bool Maximized;

        // ─── کش محتوای تقسیم‌شده ────────────────────────────────
        private string _cachedContent;
        private string[] _cachedLines;
        private int _cachedLineCount;

        public void GetContentLines(out string[] lines, out int count)
        {
            if (_cachedContent != Content)
            {
                _cachedContent = Content;
                if (string.IsNullOrEmpty(Content))
                {
                    _cachedLines = new string[0];
                    _cachedLineCount = 0;
                }
                else
                {
                    _cachedLines = Content.Split('\n');
                    _cachedLineCount = _cachedLines.Length;
                }
            }
            lines = _cachedLines;
            count = _cachedLineCount;
        }

        public bool Focused;
        public string Content;
        public bool Dragging;
        public int DragOffsetX, DragOffsetY;
        public int RestoreX, RestoreY, RestoreW, RestoreH;

        // ─── Resize ─────────────────────────────────────────────
        public bool Resizing;
        public ResizeEdge ResizeEdge;
        public int ResizeStartMouseX, ResizeStartMouseY;
        public int ResizeStartX, ResizeStartY, ResizeStartW, ResizeStartH;
        public const int ResizeHandleSize = 8;
        public const int MinW = 240;
        public const int MinH = 120;

        public int OpenAnimFrame = 0;
        public bool OpenAnimating = false;
        public int CloseAnimFrame = 0;
        public bool CloseAnimating = false;

        // ─── انیمیشن Maximize/Restore ──────────────────────────────────────
        // قبلاً دکمه‌ی Maximize باعث یک "پرش" آنی در اندازه/موقعیت پنجره
        // می‌شد — تنها عملیات UI بدون هیچ انیمیشنی در کل سیستم، در حالی که
        // باز/بستن پنجره هر دو eased هستند. حالا همان الگو (rect مبدأ →
        // rect مقصد، فقط shape رسم می‌شود نه محتوا) برای Maximize/Restore
        // هم اعمال می‌شود تا حس یکدست و "دوستانه" در کل UI حفظ شود.
        public bool MaximizeAnimating = false;
        public int MaximizeAnimFrame = 0;
        public int AnimFromX, AnimFromY, AnimFromW, AnimFromH;
        public int AnimToX, AnimToY, AnimToW, AnimToH;
        // ─── MaximizeAnimFrames: کوتاه‌تر از AnimFrames (باز/بستن) عمداً —
        // این یک تغییر شکل/موقعیت است نه ظاهر شدن از عدم، پس باید سریع‌تر
        // و "فوری‌تر" حس شود؛ ~۱۸۰ms روی فریم‌لیمیتر ۶۰fps.
        public const int MaximizeAnimFrames = 11;

        public int CloseCX => X + 18;
        public int MinCX => X + 38;
        public int MaxCX => X + 58;
        public int BtnCY => Y + TitleH / 2;
        public const int BtnR = 7;
        public const int TitleH = 32;
        // ─── AnimFrames: قبلاً ۱۸ بود. چون حلقه‌ی اصلی الان با فریم‌لیمیتر
        // در Kernel.cs روی ~۶۰fps قفل شده، هر فریم اینجا ~۱۶ms واقعی است.
        // ۲۴ فریم ≈ ۴۰۰ms — به اندازه‌ی کافی طولانی که چشم آن را به‌عنوان
        // یک حرکت نرم ببیند، نه یک پرش ناگهانی.
        public const int AnimFrames = 24;

        public bool InTitleBar(int mx, int my) =>
            mx >= X && mx <= X + W && my >= Y && my <= Y + TitleH;

        public bool InWindow(int mx, int my) =>
            mx >= X && mx <= X + W && my >= Y && my <= Y + H;

        public ResizeEdge GetResizeEdge(int mx, int my)
        {
            if (Maximized) return ResizeEdge.None;
            int r = ResizeHandleSize;
            bool onLeft = mx >= X && mx <= X + r;
            bool onRight = mx >= X + W - r && mx <= X + W;
            bool onTop = my >= Y && my <= Y + r;
            bool onBottom = my >= Y + H - r && my <= Y + H;

            if (onTop && onLeft) return ResizeEdge.TopLeft;
            if (onTop && onRight) return ResizeEdge.TopRight;
            if (onBottom && onLeft) return ResizeEdge.BottomLeft;
            if (onBottom && onRight) return ResizeEdge.BottomRight;
            if (onLeft) return ResizeEdge.Left;
            if (onRight) return ResizeEdge.Right;
            if (onTop) return ResizeEdge.Top;
            if (onBottom) return ResizeEdge.Bottom;
            return ResizeEdge.None;
        }

        public static float EaseOut(float t) { float inv = 1f - t; return 1f - inv * inv * inv; }
        public static float EaseIn(float t) { return t * t * t; }
        public static float EaseOutBack(float t)
        {
            float c1 = 1.70158f, c3 = c1 + 1f, inv = t - 1f;
            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }
        public static float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - (-2f * t + 2f) * (-2f * t + 2f) / 2f;
        }

        // ─── EaseInOutCubic: شروع و پایان هر دو نرم، بدون هیچ شتاب ناگهانی —
        // برای حرکاتی که باید کاملاً «دوستانه» و بدون لبه‌ی تیز حس شوند
        // (مثل اسلاید لاک‌اسکرین) به‌جای EaseIn خالص (که فقط انتها را نرم
        // می‌کند و شروعش هنوز ناگهانی حس می‌شود).
        public static float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  جهت‌های resize
    // ═══════════════════════════════════════════════════════════
    public enum ResizeEdge
    {
        None,
        Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight
    }

    // ═══════════════════════════════════════════════════════════
    //  WindowCanvas — wrapper با clipping برای محدوده پنجره
    //  همه برنامه‌ها از این به جای Canvas مستقیم استفاده می‌کنند
    //  تمام عملیات draw خارج از clipRect نادیده گرفته می‌شود
    // ═══════════════════════════════════════════════════════════
    public class WindowCanvas
    {
        private Canvas _canvas;
        private int _cx1, _cy1, _cx2, _cy2; // clip bounds (inclusive/exclusive)

        public WindowCanvas(Canvas canvas) { _canvas = canvas; }

        public void SetClip(int x, int y, int w, int h)
        {
            _cx1 = x; _cy1 = y; _cx2 = x + w; _cy2 = y + h;
        }

        // ─── کمکی: برش مستطیل به clip rect ────────────────────
        private bool ClipRect(ref int x, ref int y, ref int w, ref int h)
        {
            int x2 = x + w, y2 = y + h;
            if (x < _cx1) x = _cx1;
            if (y < _cy1) y = _cy1;
            if (x2 > _cx2) x2 = _cx2;
            if (y2 > _cy2) y2 = _cy2;
            w = x2 - x; h = y2 - y;
            return w > 0 && h > 0;
        }

        private bool InClip(int x, int y)
            => x >= _cx1 && x < _cx2 && y >= _cy1 && y < _cy2;

        // ─── API معادل Canvas ────────────────────────────────────
        // توجه: تمام رسم‌ها مستقیماً روی Canvas انجام می‌شوند (نه RenderSystem._buf)
        // چون Draw برنامه‌ها بعد از RenderSystem.Flush() فراخوانی می‌شوند
        public void DrawFilledRectangle(Pen pen, int x, int y, int w, int h)
        {
            if (!ClipRect(ref x, ref y, ref w, ref h)) return;
            _canvas.DrawFilledRectangle(pen, x, y, w, h);
        }

        public void DrawRectangle(Pen pen, int x, int y, int w, int h)
        {
            // top
            int tx = x, ty = y, tw = w, th = 1;
            if (ClipRect(ref tx, ref ty, ref tw, ref th)) _canvas.DrawFilledRectangle(pen, tx, ty, tw, th);
            // bottom
            tx = x; ty = y + h - 1; tw = w; th = 1;
            if (ClipRect(ref tx, ref ty, ref tw, ref th)) _canvas.DrawFilledRectangle(pen, tx, ty, tw, th);
            // left
            tx = x; ty = y; tw = 1; th = h;
            if (ClipRect(ref tx, ref ty, ref tw, ref th)) _canvas.DrawFilledRectangle(pen, tx, ty, tw, th);
            // right
            tx = x + w - 1; ty = y; tw = 1; th = h;
            if (ClipRect(ref tx, ref ty, ref tw, ref th)) _canvas.DrawFilledRectangle(pen, tx, ty, tw, th);
        }

        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2)
        {
            if (y1 == y2) // افقی
            {
                int lx = Math.Min(x1, x2), rx = Math.Max(x1, x2) + 1;
                int cx = lx, cw = rx - lx, cy = y1, ch = 1;
                if (ClipRect(ref cx, ref cy, ref cw, ref ch)) _canvas.DrawFilledRectangle(pen, cx, cy, cw, ch);
            }
            else if (x1 == x2) // عمودی
            {
                int ty2 = Math.Min(y1, y2), by = Math.Max(y1, y2) + 1;
                int cx = x1, cw = 1, cy = ty2, ch = by - ty2;
                if (ClipRect(ref cx, ref cy, ref cw, ref ch)) _canvas.DrawFilledRectangle(pen, cx, cy, cw, ch);
            }
            else
            {
                _canvas.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        public void DrawString(string text, PCScreenFont font, Pen pen, int x, int y)
        {
            // متن فقط اگر y داخل clip باشد (clipping کامل متن)
            if (y + 16 < _cy1 || y > _cy2) return;
            if (x > _cx2) return;
            _canvas.DrawString(text, font, pen, x, y);
        }

        public void DrawImageAlpha(Bitmap bmp, int x, int y)
        {
            if (bmp == null) return;
            // DrawImageAlpha مستقیم روی Canvas — چون DrawWindowContentTexts بعد از
            // RenderSystem.Flush() صدا زده می‌شود، آیکون‌ها باید روی Canvas رسم شوند
            _canvas.DrawImageAlpha(bmp, x, y);
        }

        // ─── DrawTtf: رندر TTF با clip محدوده پنجره ────────────────
        // clipW/clipH به صورت خودکار از clip rect پنجره گرفته می‌شود
        //
        // نکته مهم (رفع باگ کیفیت فونت): TtfFont.DrawAuto/DrawString از نسخه‌ی
        // ۷ به بعد یک پارامتر اختیاری bgColor دارند که وقتی مقدار داشته باشد
        // آنتی‌الیاسینگ خاکستری واقعی (میان‌یابی رنگ بین پس‌زمینه و قلم) را
        // فعال می‌کند. قبلاً این wrapper اصلاً bgColor را قبول نمی‌کرد و آن را
        // به font.DrawAuto پاس نمی‌داد، پس در کل پروژه bgColor همیشه null
        // بود و همه‌جا (حتی بعد از آپدیت TtfFont) رندر باینری/پله‌ای قدیمی
        // اجرا می‌شد — دقیقاً همان چیزی که در سایزهای کوچک فونت به‌وضوح
        // دیده می‌شود. حالا bgColor یک پارامتر اختیاری اضافه دارد؛ فراخوان‌ها
        // فقط وقتی پس‌زمینه‌ی مقصد یکدست و شناخته‌شده است آن را پاس بدهند.
        public void DrawTtf(TtfFont font, string text, Pen pen, int x, int y, Color? bgColor = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (y + font.LineHeight < _cy1 || y > _cy2) return;
            if (x > _cx2) return;
            font.DrawAuto(_canvas, text, pen, x, y, _cx2, _cy2, bgColor);
        }

        // ─── DrawTtfRTL: رندر RTL با x به عنوان لبه راست ───────────
        public void DrawTtfRTL(TtfFont font, string text, Pen pen, int x, int y, Color? bgColor = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (y + font.LineHeight < _cy1 || y > _cy2) return;
            font.DrawStringRTL(_canvas, text, pen, x, y, _cx2, _cy2, bgColor);
        }

        // fallback برای متدهایی که نیاز به Canvas دارند
        public Canvas RawCanvas => _canvas;
    }

    // ═══════════════════════════════════════════════════════════
    //  مدیر اصلی گرافیک — با RenderSystem یکپارچه
    // ═══════════════════════════════════════════════════════════
    public static class GraphicsManager
    {
        public static Canvas Canvas;
        public static WindowCanvas WCanvas = new WindowCanvas(null); // بعد از init مقداردهی می‌شود
        public static int Width, Height;

        public static List<WindowInfo> Windows = new List<WindowInfo>();
        private static int _focusedIndex = -1;

        // موس
        private static int _mouseX, _mouseY;
        private static bool _lastLeft;
        private static bool _curLeft;

        // منوی استارت
        private static bool _startMenuOpen;
        private static float _startMenuAnimF;
        private const int StartMenuW = 330;
        private const int StartMenuH = 360;
        // ─── قبلاً این انیمیشن با یک «تعقیب نمایی» (diff * 0.22 هر تیک) پیش
        // می‌رفت. مشکل: این روش کاملاً به نرخ تیک وابسته است — روی حلقه‌ی
        // بدون محدودیت قبلی در چند میلی‌ثانیه به مقصد می‌رسید (پرش ناگهانی)،
        // و چون یک «تعقیب» است نه یک حرکت با مقصد ثابت، مدت زمانش هیچ‌وقت
        // دقیقاً یکسان نبود. حالا با شمارنده‌ی فریم مشخص (مثل پنجره‌ها) کار
        // می‌کند: دقیقاً StartMenuAnimFrames فریم طول می‌کشد، و چون شمارنده
        // به‌جای بازنشانی، فقط جهتش عوض می‌شود، اگر وسط انیمیشن کاربر دوباره
        // کلیک کند، حرکت به‌آرامی از همان‌جا برمی‌گردد (بدون پرش).
        private static int _startMenuAnimFrame = 0;
        private const int StartMenuAnimFrames = 20; // ≈ 330ms روی ۶۰fps — نرم و محسوس
        private static int _startMenuHover = -1;

        // dirty flag
        private static bool _needsRedraw = true;
        private static int _mouseXLast = -1, _mouseYLast = -1;

        // تنظیمات
        private static bool _settingsToggleHover = false;

        // ─── Settings tabs ─────────────────────────────────────────────────
        private static int _settingsTab = 0;   // 0=Display  1=Memory  2=Network  3=Personalize
        private static bool _netToggleHover = false;

        // ─── Personalize tab state ─────────────────────────────────────────
        // لیست فایل‌های تصویری در Assets\Images
        private static string[] _picFiles = new string[0];
        private static string[] _picNames = new string[0];
        private static int _picScrollOffset = 0;
        private static int _picSelected = -1;
        private static bool _picListLoaded = false;
        private const int PicRowH = 22;
        private const int PicListMaxRows = 8;

        // کش آمار شبکه
        private static string _cachedNetStatus = "";
        private static string _cachedNetIp = "";
        private static string _cachedNetMac = "";
        private static string _cachedNetGw = "";
        private static string _cachedNetDns = "";
        private static string _cachedPktSent = "";
        private static string _cachedPktRecv = "";
        private static string _cachedNetDiag1 = "";
        private static string _cachedNetDiag2 = "";
        private static int _netCacheCounter = 0;
        private const int NetCacheInterval = 60;

        // کش ساعت
        private static string _cachedTimeStr = "";
        private static int _lastCachedMinute = -1;
        private static int _cachedTimeStrW = 0;

        // کش نشانگر زبان
        private static bool _lastLangIsFarsi = false;
        private static int _cachedLangLabelW = 0;

        // dirty-region منوی استارت
        private static bool _startMenuDirty = false;
        private static int _prevStartMenuHover = -2;

        // کش حافظه
        private static ulong _cachedUsedBytes = 0;
        private static ulong _cachedTotalBytes = 0;
        private static int _memCacheCounter = 0;
        private const int MemCacheInterval = 60;
        private static string _cachedUsedStr = "";
        private static string _cachedFreeStr = "";
        private static string _cachedTotalStr = "";
        private static string _cachedPctStr = "";
        private static int _cachedPct = -1;

        // GC
        private static int _gcCounter = 0;
        private const int GCIntervalSingle = 90;
        private const int GCIntervalMulti = 45;
        private const ulong GCEmergencyThresholdMB = 110;

        // ردیابی برنامه‌ها
        // ⛔ «Calculator» (calcapp2, یک برنامه‌ی .pap) از اینجا حذف شد —
        // کل سیستم نصب/اجرای .pap بسته شده چون پایدار نبود.
        private static int[] _appUsageCount = new int[6] { 0, 0, 0, 0, 0, 0 };
        private static readonly string[] _appNames4 = { "Settings", "Notepad", "File Explorer", "Terminal", "Browser", "Music Player" };
        private static readonly string[] _appInitials4 = { "S", "N", "F", "T", "B", "M" }; // cache — بدون Substring هر فریم
        private static readonly string[] _appContent4 = {
            "SETTINGS_APP", NotepadApp.ContentFlag,
            FileExplorerApp.ContentFlag, "TERMINAL_APP",
            WebBrowserApp.ContentFlag, MusicPlayerApp.ContentFlag
        };

        // انیمیشن تم
        private static bool _themeAnimating = false;
        private static int _themeAnimFrame = 0;
        // قبلاً ۱۶ فریم با فید خطی بود — با فریم‌لیمیتر جدید در ~۲۷۰ms
        // تمام می‌شد که هنوز کمی ناگهانی حس می‌شود. حالا ۲۸ فریم (~۴۷۰ms)
        // با یک منحنی ease-out (رجوع کنید به Render) یک محو شدن نرم و
        // دوستانه‌تر برای تعویض حالت روشن/تاریک می‌سازد.
        private const int ThemeAnimFrames = 28;
        private static Color _themeFlashColor = Color.White;
        private static readonly Color _themeFlashLight = Color.FromArgb(235, 235, 245);
        private static readonly Color _themeFlashDark = Color.FromArgb(20, 20, 35);

        private static PCScreenFont _font => Kernel.DefaultFont;
        private static TtfFont TtfFontNormal => Kernel.VazirFont;
        private static TtfFont TtfFontSmall => Kernel.VazirFontSm ?? Kernel.VazirFont;

        // کش پیشنهادهای منوی استارت
        private static int[] _top2Cache = new int[2] { 1, 3 };
        private static bool _top2Dirty = true;

        // ─── رنگ‌های packed int برای RenderSystem (بدون Pen) ─────────────────
        // این مقادیر هر بار تم عوض می‌شود invalidate می‌شوند
        private static bool _colorsStale = true;
        private static bool _lastColorsDark = true;

        // رنگ‌های ثابت (یک‌بار محاسبه می‌شوند)
        private static readonly int ColBtnClose = RenderSystem.ToInt(Color.FromArgb(255, 96, 92));
        private static readonly int ColBtnMin = RenderSystem.ToInt(Color.FromArgb(255, 189, 68));
        private static readonly int ColBtnMax = RenderSystem.ToInt(Color.FromArgb(40, 200, 64));
        private static readonly int ColAccent = RenderSystem.ToInt(Color.FromArgb(100, 120, 230));
        private static readonly int ColAccentHover = RenderSystem.ToInt(Color.FromArgb(130, 150, 255));
        private static readonly int ColWhite = RenderSystem.ToInt(Color.White);
        private static readonly int ColBlack = RenderSystem.ToInt(Color.Black);
        private static readonly int ColSeparator = RenderSystem.ToInt(Color.FromArgb(60, 60, 90));
        private static readonly int ColShutdownRed = RenderSystem.ToInt(Color.FromArgb(200, 60, 60));
        private static readonly int ColRebootYellow = RenderSystem.ToInt(Color.FromArgb(200, 160, 40));
        private static readonly int ColShutdownHover = RenderSystem.ToInt(Color.FromArgb(230, 80, 80));
        private static readonly int ColRebootHover = RenderSystem.ToInt(Color.FromArgb(230, 190, 60));
        private static readonly int ColDimBorder = RenderSystem.ToInt(Color.FromArgb(50, 50, 80));
        private static readonly int ColMemGreen = RenderSystem.ToInt(Color.FromArgb(40, 200, 100));
        private static readonly int ColMemYellow = RenderSystem.ToInt(Color.FromArgb(220, 170, 40));
        private static readonly int ColMemRed = RenderSystem.ToInt(Color.FromArgb(220, 70, 70));

        // رنگ‌های پویا (بازسازی با تم)
        private static int ColDesktop, ColTitleBar, ColTitleBarInact, ColTitleBarText;
        private static int ColWindowBg, ColWindowBorder, ColTaskbar, ColTaskbarItem;
        private static int ColTaskbarActive, ColTaskbarText, ColTaskbarClock;
        private static int ColTextPrimary, ColStartMenuBg, ColStartMenuBorder;

        private static void EnsureColors()
        {
            if (!_colorsStale && _lastColorsDark == Theme.DarkMode) return;
            _colorsStale = false;
            _lastColorsDark = Theme.DarkMode;

            ColDesktop = RenderSystem.ToInt(Theme.Desktop);
            ColTitleBar = RenderSystem.ToInt(Theme.TitleBar);
            ColTitleBarInact = RenderSystem.ToInt(Theme.TitleBarInact);
            ColTitleBarText = RenderSystem.ToInt(Theme.TitleBarText);
            ColWindowBg = RenderSystem.ToInt(Theme.WindowBg);
            ColWindowBorder = RenderSystem.ToInt(Theme.WindowBorder);
            ColTaskbar = RenderSystem.ToInt(Theme.Taskbar);
            ColTaskbarItem = RenderSystem.ToInt(Theme.TaskbarItem);
            ColTaskbarActive = RenderSystem.ToInt(Theme.TaskbarActive);
            ColTaskbarText = RenderSystem.ToInt(Theme.TaskbarText);
            ColTaskbarClock = RenderSystem.ToInt(Theme.TaskbarClock);
            ColTextPrimary = RenderSystem.ToInt(Theme.TextPrimary);
            ColStartMenuBg = RenderSystem.ToInt(Theme.StartMenuBg);
            ColStartMenuBorder = RenderSystem.ToInt(Theme.StartMenuBorder);

            // وقتی تم عوض می‌شود wallpaper cache هم باید rebuild شود
            if (Kernel.WallpaperPng != null && Kernel.WallpaperPng.IsValid)
                RenderSystem.RebuildWallpaperCache(Kernel.WallpaperPng.Pixels, Kernel.WallpaperPng.Width, Kernel.WallpaperPng.Height, Width, Height);
            else
                RenderSystem.RebuildWallpaperCache(Kernel.Wallpaper, Width, Height);
            RenderSystem.ForceFullRedraw();
        }

        // ═══════════════════════════════════════════════════════
        //  راه‌اندازی
        // ═══════════════════════════════════════════════════════
        public static void Initialize()
        {
            Canvas = new SVGAIICanvas(new Mode(1024, 768, ColorDepth.ColorDepth32));
            Width = Canvas.Mode.Columns;
            Height = Canvas.Mode.Rows;

            Sys.MouseManager.ScreenWidth = (uint)Width;
            Sys.MouseManager.ScreenHeight = (uint)Height;
            Sys.MouseManager.X = (uint)(Width / 2);
            Sys.MouseManager.Y = (uint)(Height / 2);

            _mouseX = Width / 2;
            _mouseY = Height / 2;

            // ─── راه‌اندازی RenderSystem ──────────────────────────
            RenderSystem.Init(Canvas, Width, Height);
            // PNG اولویت دارد، در صورت عدم موجودیت BMP استفاده می‌شود
            if (Kernel.WallpaperPng != null && Kernel.WallpaperPng.IsValid)
                RenderSystem.RebuildWallpaperCache(Kernel.WallpaperPng.Pixels, Kernel.WallpaperPng.Width, Kernel.WallpaperPng.Height, Width, Height);
            else
                RenderSystem.RebuildWallpaperCache(Kernel.Wallpaper, Width, Height);

            // ─── WCanvas با Canvas واقعی مقداردهی ─────────────────
            WCanvas = new WindowCanvas(Canvas);
        }

        // ─── هنگام تغییر wallpaper از Kernel این را فراخوانی کنید ────────────
        public static void OnWallpaperChanged()
        {
            if (WallpaperLoader.IsBusy) WallpaperLoader.Cancel();

            if (Kernel.WallpaperPng != null && Kernel.WallpaperPng.IsValid
                && Kernel.WallpaperPng.Pixels != null)
            {
                // اگر در حال بوت هستیم (رندر هنوز شروع نشده) → synchronous
                // در غیر اینصورت → deferred
                WallpaperLoader.StartLoad(
                    Kernel.WallpaperPng.Pixels,
                    Kernel.WallpaperPng.Width,
                    Kernel.WallpaperPng.Height,
                    Width, Height);
                // Pixels را زود null نکن — WallpaperLoader هنوز به آن‌ها نیاز دارد
            }
            else if (Kernel.Wallpaper != null && Kernel.Wallpaper.rawData != null)
            {
                WallpaperLoader.StartLoadBmp(
                    Kernel.Wallpaper.rawData,
                    (int)Kernel.Wallpaper.Width,
                    (int)Kernel.Wallpaper.Height,
                    Width, Height);
            }
            else
            {
                RenderSystem.RebuildWallpaperCache((Cosmos.System.Graphics.Bitmap)null, Width, Height);
            }
            _needsRedraw = true;
        }

        // ═══════════════════════════════════════════════════════
        //  Tick - حلقه اصلی
        // ═══════════════════════════════════════════════════════
        public static void Tick()
        {
            // ─── اگر WallpaperLoader مشغول است، آن را ادوانس کن ─────────────────
            // این باید قبل از هر چیز دیگری باشد تا هر فریم پیشرفت داشته باشیم
            if (WallpaperLoader.IsBusy)
            {
                WallpaperLoader.Tick();
                _needsRedraw = true;
                // GC و ورودی را رد کن تا لود سریع‌تر تمام شود
                _lastLeft = Sys.MouseManager.MouseState == Sys.MouseState.Left;
                // فقط flush مستقیم — Render کامل لازم نیست
                RenderSystem.Flush(Canvas);
                Canvas.Display();
                return;
            }

            _gcCounter++;
            int gcInterval = Windows.Count <= 1 ? GCIntervalSingle : GCIntervalMulti;
            bool doGC = (_gcCounter >= gcInterval);
            if (!doGC && _gcCounter % 15 == 0)
            {
                try
                {
                    ulong usedMB = Cosmos.Core.GCImplementation.GetUsedRAM() / (1024 * 1024);
                    if (usedMB > GCEmergencyThresholdMB) doGC = true;
                }
                catch { }
            }
            if (doGC) { _gcCounter = 0; Cosmos.Core.Memory.Heap.Collect(); }

            UpdateInput();
            NetworkDriver.Tick();

            // ─── Tick موزیک پلیر — یک‌بار در هر فریم، مستقل از فوکوس پنجره
            // (باید حتی وقتی پنجره مینیمایز/بدون فوکوس است هم آهنگ بعدی
            // به‌صورت خودکار پخش شود) ──────────────────────────────────
            if (MusicPlayerApp.Tick()) _needsRedraw = true;

            // وقتی آهنگی در حال پخش است و پنجره‌ی موزیک پلیر باز/غیرمینیمایز
            // است، هر فریم را ری‌درا کن تا نوار پیشرفت و زمان‌سنج نرم آپدیت شوند
            if (SoundDriver.IsPlaying)
            {
                for (int mi = 0; mi < Windows.Count; mi++)
                {
                    if (Windows[mi].Content == MusicPlayerApp.ContentFlag && !Windows[mi].Minimized)
                    { _needsRedraw = true; break; }
                }
            }

            // ─── update() هر برنامه‌ی .pap در حال اجرا — یک‌بار در هر فریم،
            // دقیقاً مثل draw() که در DrawWindowContentTexts صدا زده می‌شود ──
            for (int pi = 0; pi < Windows.Count; pi++)
            {
                var pw = Windows[pi];
                if (!pw.Minimized && PapAppRuntime.IsPapAppContent(pw.Content))
                    PapAppRuntime.Update(pw, 16);
            }

            if (_focusedIndex >= 0 && _focusedIndex < Windows.Count
                && Windows[_focusedIndex].Content == NotepadApp.ContentFlag
                && !Windows[_focusedIndex].Minimized)
            { if (NotepadApp.HandleKeyboard()) _needsRedraw = true; }

            if (_focusedIndex >= 0 && _focusedIndex < Windows.Count
                && Windows[_focusedIndex].Content == TerminalApp.ContentFlag
                && !Windows[_focusedIndex].Minimized)
            { if (TerminalApp.HandleKeyboard()) _needsRedraw = true; }

            if (_focusedIndex >= 0 && _focusedIndex < Windows.Count
                && Windows[_focusedIndex].Content == FileExplorerApp.ContentFlag
                && !Windows[_focusedIndex].Minimized)
            {
                var feState = FileExplorerApp.GetOrCreateState(Windows[_focusedIndex]);
                if (FileExplorerApp.HandleKeyboard(feState)) _needsRedraw = true;
            }

            if (_focusedIndex >= 0 && _focusedIndex < Windows.Count
                && Windows[_focusedIndex].Content == WebBrowserApp.ContentFlag
                && !Windows[_focusedIndex].Minimized)
            {
                if (WebBrowserApp.HandleKeyboard(Windows[_focusedIndex])) _needsRedraw = true;
            }

            if (_mouseX != _mouseXLast || _mouseY != _mouseYLast)
            {
                _needsRedraw = true;
                _mouseXLast = _mouseX;
                _mouseYLast = _mouseY;
            }

            if (AnyAnimationActive())
            {
                _needsRedraw = true;
                _startMenuDirty = false;
            }

            if (_needsRedraw)
            {
                Render();
                _needsRedraw = false;
                _startMenuDirty = false;
            }
        }

        private static bool AnyAnimationActive()
        {
            if (_startMenuAnimF > 0f && _startMenuAnimF < 1f) return true;
            if (_startMenuOpen && _startMenuAnimF < 1f) return true;
            if (!_startMenuOpen && _startMenuAnimF > 0f) return true;
            if (_themeAnimating) return true;

            // نکته رفع باگ: قبلاً این حلقه فقط ایندکس‌های ۰ تا ۳ را چک می‌کرد
            // در حالی که _iconBounceFrame شش‌تایی است (۶ آیکون تسک‌بار).
            // نتیجه: بانس آیکون‌های ۴ و ۵ توسط این چک "زنده" نگه داشته
            // نمی‌شد و اگر هیچ محرک دیگری redraw باعث نمی‌شد، پرش آن‌ها
            // نیمه‌کاره/تکه‌تکه دیده می‌شد به‌جای یک انیمیشن پیوسته و نرم.
            for (int i = 0; i < _iconBounceFrame.Length; i++)
                if (_iconBounceFrame[i] > 0) return true;

            for (int i = 0; i < Windows.Count; i++)
            {
                if (Windows[i].OpenAnimating) return true;
                if (Windows[i].CloseAnimating) return true;
                if (Windows[i].MaximizeAnimating) return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════
        //  ورودی
        // ═══════════════════════════════════════════════════════
        private static void UpdateInput()
        {
            _mouseX = (int)Sys.MouseManager.X;
            _mouseY = (int)Sys.MouseManager.Y;
            _curLeft = Sys.MouseManager.MouseState == Sys.MouseState.Left;

            bool justClicked = _curLeft && !_lastLeft;
            bool justReleased = !_curLeft && _lastLeft;

            if (justClicked) HandleClick(_mouseX, _mouseY);
            if (_curLeft) HandleDrag(_mouseX, _mouseY);
            if (justReleased) StopDrag();

            _lastLeft = _curLeft;

            // نشانگر زبان: redraw اگر تغییر کرده (toggle از Notepad.HandleKeyboard انجام می‌شود)
            if (_lastLangIsFarsi != InputLanguage.IsFarsi)
                _needsRedraw = true;

            if (_startMenuOpen)
                UpdateStartMenuHover(_mouseX, _mouseY);

            if (_themeAnimating)
            {
                _themeAnimFrame++;
                if (_themeAnimFrame >= ThemeAnimFrames)
                {
                    _themeAnimating = false;
                    _colorsStale = true; // رنگ‌های packed را refresh کن
                }
                _needsRedraw = true;
            }

            // ─── حرکت منوی استارت: seek با شمارنده‌ی فریم به‌جای تعقیب نمایی ──
            // به‌جای resetکردن فریم موقع تغییر جهت، فقط جهت شمارش عوض می‌شود
            // تا برگشت وسط راه نرم باشد نه یک پرش.
            bool smMoved = false;
            if (_startMenuOpen && _startMenuAnimFrame < StartMenuAnimFrames)
            {
                _startMenuAnimFrame++;
                smMoved = true;
            }
            else if (!_startMenuOpen && _startMenuAnimFrame > 0)
            {
                _startMenuAnimFrame--;
                smMoved = true;
            }
            if (smMoved)
            {
                float lin = (float)_startMenuAnimFrame / StartMenuAnimFrames;
                _startMenuAnimF = WindowInfo.EaseOut(lin);
                _needsRedraw = true;
            }

            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                var w = Windows[i];

                if (w.MaximizeAnimating)
                {
                    w.MaximizeAnimFrame++;
                    if (w.MaximizeAnimFrame >= WindowInfo.MaximizeAnimFrames)
                    {
                        w.MaximizeAnimating = false;
                        w.X = w.AnimToX; w.Y = w.AnimToY;
                        w.W = w.AnimToW; w.H = w.AnimToH;
                    }
                    else
                    {
                        // EaseOut ساده (بدون overshoot) عمداً انتخاب شد — بر
                        // خلاف EaseOutBack که برای باز شدن پنجره استفاده می‌شود،
                        // اینجا overshoot باعث می‌شود پنجره لحظه‌ای از مرز صفحه
                        // بیرون بزند یا از اندازه‌ی هدف بزرگ‌تر/کوچک‌تر شود —
                        // که برای یک تغییر اندازه‌ی دقیق نامناسب و "لرزان" است.
                        float e = WindowInfo.EaseOut((float)w.MaximizeAnimFrame / WindowInfo.MaximizeAnimFrames);
                        w.X = w.AnimFromX + (int)((w.AnimToX - w.AnimFromX) * e);
                        w.Y = w.AnimFromY + (int)((w.AnimToY - w.AnimFromY) * e);
                        w.W = w.AnimFromW + (int)((w.AnimToW - w.AnimFromW) * e);
                        w.H = w.AnimFromH + (int)((w.AnimToH - w.AnimFromH) * e);
                    }
                    _needsRedraw = true;
                }

                if (w.OpenAnimating)
                {
                    w.OpenAnimFrame++;
                    if (w.OpenAnimFrame >= WindowInfo.AnimFrames)
                    { w.OpenAnimating = false; w.OpenAnimFrame = WindowInfo.AnimFrames; }
                }

                if (w.CloseAnimating)
                {
                    w.CloseAnimFrame++;
                    if (w.CloseAnimFrame >= WindowInfo.AnimFrames)
                    {
                        if (w.Content == FileExplorerApp.ContentFlag)
                        {
                            // اگر این پنجره picker بود، حالت را reset کن
                            if (w.Title == "Select Wallpaper")
                            {
                                FileExplorerApp.WallpaperPickerMode = false;
                                FileExplorerApp.OnWallpaperPicked = null;
                            }
                            FileExplorerApp.CleanupState(w);
                        }
                        if (w.Content == WebBrowserApp.ContentFlag)
                            WebBrowserApp.CleanupState(w);
                        if (w.Content == PapInstallerUI.ContentFlag)
                            PapInstallerUI.CleanupState(w);
                        if (PapAppRuntime.IsPapAppContent(w.Content))
                            PapAppRuntime.CleanupState(w);
                        w.Content = null;
                        Windows.RemoveAt(i);
                        if (_focusedIndex >= Windows.Count)
                            _focusedIndex = Windows.Count - 1;
                        // نکته بهینه‌سازی: قبلاً اینجا هم یک Heap.Collect() اجباری
                        // بود — دقیقاً یک فریم بعد از پایان انیمیشن بسته‌شدن، یعنی
                        // درست جایی که کاربر بیشترین حساسیت به نرمی را دارد، یک
                        // GC کامل و synchronous اجرا می‌شد (مکث/لَگ محسوس، همان
                        // چیزی که به‌عنوان «مصرف عجیب RAM» حس می‌شود). زمان‌بند GC
                        // تطبیقی در Tick() (پایین‌تر تعریف شده) خودش طی چند فریم
                        // بعدی، بدون هیچ افت نرمی، این حافظه را آزاد می‌کند.
                        _needsRedraw = true;
                        continue;
                    }
                }
            }
        }

        // ─── DrawText: LTR ──────────────────────────────────────────────────────
        // نکته کیفیت فونت: bgColor باید رنگ یکدست پس‌زمینه‌ی زیر متن باشد تا
        // TtfFont بتواند AA خاکستری واقعی (میان‌یابی رنگ) انجام دهد؛ همان
        // تکنیکی که در WebBrowserApp.DrawTexts استفاده شده. اگر پاس داده
        // نشود، رندر به حالت باینری/پله‌ای قدیمی برمی‌گردد.
        private static void DrawText(string text, Pen pen, int x, int y, bool small = false, Color? bgColor = null)
        {
            var ttf = small ? TtfFontSmall : TtfFontNormal;
            if (ttf != null) ttf.DrawString(Canvas, text, pen, x, y, int.MaxValue, int.MaxValue, bgColor);
            else Canvas.DrawString(text, Kernel.DefaultFont, pen, x, y);
        }

        private static void DrawTextRTL(string text, Pen pen, int x, int y, bool small = false, Color? bgColor = null)
        {
            var ttf = small ? TtfFontSmall : TtfFontNormal;
            if (ttf != null) ttf.DrawStringRTL(Canvas, text, pen, x, y, int.MaxValue, int.MaxValue, bgColor);
            else Canvas.DrawString(text, Kernel.DefaultFont, pen, x, y);
        }

        private static int MeasureText(string text, bool small = false)
        {
            var ttf = small ? TtfFontSmall : TtfFontNormal;
            return ttf?.MeasureWidth(text) ?? text.Length * 8;
        }

        private static int MeasureTextRTL(string text, bool small = false)
        {
            var ttf = small ? TtfFontSmall : TtfFontNormal;
            return ttf?.MeasureRTLWidth(text) ?? text.Length * 8;
        }

        private static void UpdateStartMenuHover(int mx, int my)
        {
            float easedT = WindowInfo.EaseInOutQuad(_startMenuAnimF);
            int visH = (int)(StartMenuH * easedT);
            int menuX = 4;
            int fullMenuBottom = Height - 40;
            int itemH = 44;
            int shutY = fullMenuBottom - itemH * 2 - 6;
            int rebY = fullMenuBottom - itemH - 3;

            int prevHover = _startMenuHover;
            _startMenuHover = -1;
            if (mx >= menuX && mx <= menuX + StartMenuW)
            {
                if (my >= shutY && my < shutY + itemH) _startMenuHover = 0;
                if (my >= rebY && my < rebY + itemH) _startMenuHover = 1;
            }

            if (_startMenuHover != prevHover)
            {
                _needsRedraw = true;
                _startMenuDirty = true;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  کلیک
        // ═══════════════════════════════════════════════════════
        private static void HandleClick(int mx, int my)
        {
            _needsRedraw = true;
            int tbY = Height - 40;

            if (_startMenuOpen)
            {
                int menuX = 4;
                int menuY = Height - 40 - StartMenuH;
                bool inMenu = mx >= menuX && mx <= menuX + StartMenuW && my >= menuY && my <= tbY;
                bool inStartBtn = mx < 70 && my >= tbY;

                if (!inMenu && !inStartBtn) _startMenuOpen = false;

                if (inMenu && _startMenuAnimF >= 0.9f)
                {
                    if (_startMenuHover == 0) Sys.Power.Shutdown();
                    if (_startMenuHover == 1) Sys.Power.Reboot();

                    int[] top2 = GetTop2AppIndices();
                    int suggestBaseY = menuY + 46;
                    int suggestItemH = 52;
                    for (int si = 0; si < 2; si++)
                    {
                        int idx = top2[si];
                        if (idx < 0) continue;
                        int siy = suggestBaseY + si * (suggestItemH + 4);
                        if (mx >= menuX + 6 && mx <= menuX + StartMenuW - 6
                            && my >= siy && my <= siy + suggestItemH)
                        {
                            _appUsageCount[idx]++;
                            _top2Dirty = true;
                            _startMenuOpen = false;
                            OpenOrFocusApp(_appNames4[idx], _appContent4[idx]);
                            return;
                        }
                    }
                    return;
                }
            }

            if (my >= tbY) { HandleTaskbarClick(mx, my); return; }

            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                var w = Windows[i];
                if (w.Minimized || w.CloseAnimating || !w.InWindow(mx, my)) continue;

                SetFocus(i);

                if (DistSq(mx, my, w.CloseCX, w.BtnCY) <= WindowInfo.BtnR * WindowInfo.BtnR)
                {
                    // اگر وسط انیمیشن Maximize/Restore بود، متوقفش کن تا با
                    // انیمیشن بسته‌شدن تداخل نکند (دو انیمیشن هم‌زمان روی
                    // یک rect باعث لرزش دیداری می‌شد).
                    if (w.MaximizeAnimating) { w.MaximizeAnimating = false; w.X = w.AnimToX; w.Y = w.AnimToY; w.W = w.AnimToW; w.H = w.AnimToH; }
                    w.CloseAnimating = true; w.CloseAnimFrame = 0; return;
                }

                if (DistSq(mx, my, w.MinCX, w.BtnCY) <= WindowInfo.BtnR * WindowInfo.BtnR)
                {
                    if (w.MaximizeAnimating) { w.MaximizeAnimating = false; w.X = w.AnimToX; w.Y = w.AnimToY; w.W = w.AnimToW; w.H = w.AnimToH; }
                    w.Minimized = true; return;
                }

                if (DistSq(mx, my, w.MaxCX, w.BtnCY) <= WindowInfo.BtnR * WindowInfo.BtnR)
                { ToggleMaximize(w); return; }

                if (w.Title == "Settings" && !w.InTitleBar(mx, my))
                {
                    if (HandleSettingsClick(w, mx, my)) return;
                }

                if (w.Content == NotepadApp.ContentFlag && !w.InTitleBar(mx, my))
                {
                    if (NotepadApp.HandleMenuClick(w, mx, my)) { _needsRedraw = true; return; }
                }

                if (w.Content == FileExplorerApp.ContentFlag && !w.InTitleBar(mx, my))
                { FileExplorerApp.HandleClick(w, mx, my); _needsRedraw = true; return; }

                if (w.Content == WebBrowserApp.ContentFlag && !w.InTitleBar(mx, my))
                { WebBrowserApp.HandleClick(w, mx, my); _needsRedraw = true; return; }

                if (w.Content == MusicPlayerApp.ContentFlag && !w.InTitleBar(mx, my))
                { MusicPlayerApp.HandleClick(w, mx, my); _needsRedraw = true; return; }

                // ⛔ PapInstallerUI (نصب برنامه‌های .pap) و PapAppRuntime.HandleClick
                // عمداً حذف شدند — کل سیستم نصب/اجرای .pap بسته شده چون پایدار نبود.

                var edge = w.GetResizeEdge(mx, my);
                if (edge != ResizeEdge.None && !w.InTitleBar(mx, my))
                {
                    w.Resizing = true; w.ResizeEdge = edge;
                    w.ResizeStartMouseX = mx; w.ResizeStartMouseY = my;
                    w.ResizeStartX = w.X; w.ResizeStartY = w.Y;
                    w.ResizeStartW = w.W; w.ResizeStartH = w.H;
                    return;
                }

                if (w.InTitleBar(mx, my))
                { w.Dragging = true; w.DragOffsetX = mx - w.X; w.DragOffsetY = my - w.Y; }

                return;
            }
        }

        private static void HandleTaskbarClick(int mx, int my)
        {
            if (mx < 70) { _startMenuOpen = !_startMenuOpen; _needsRedraw = true; return; }

            // ⛔ «Calculator» حذف شد — سیستم .pap بسته شده
            int[] appStartX = { 74, 122, 170, 218, 266, 314 };
            string[] appNames = { "Settings", "Notepad", "File Explorer", "Terminal", "Browser", "Music Player" };
            string[] appContent = { "SETTINGS_APP", NotepadApp.ContentFlag, FileExplorerApp.ContentFlag, "TERMINAL_APP", WebBrowserApp.ContentFlag, MusicPlayerApp.ContentFlag };

            for (int i = 0; i < appStartX.Length; i++)
            {
                if (mx >= appStartX[i] && mx < appStartX[i] + IconSlotW)
                {
                    TriggerIconBounce(i);
                    _appUsageCount[i]++;
                    _top2Dirty = true;
                    OpenOrFocusApp(appNames[i], appContent[i]);
                    _needsRedraw = true;
                    return;
                }
            }

            int dynStartX = 324, btnW = 114;
            for (int i = 0; i < Windows.Count; i++)
            {
                int bx = dynStartX + i * (btnW + 4);
                if (mx >= bx && mx <= bx + btnW)
                {
                    var w = Windows[i];
                    if (w.Minimized) { w.Minimized = false; StartOpenAnim(w); SetFocus(i); }
                    else if (_focusedIndex == i) w.Minimized = true;
                    else SetFocus(i);
                    _needsRedraw = true;
                    return;
                }
            }
        }

        private static void OpenOrFocusApp(string name, string content)
        {
            for (int i = 0; i < Windows.Count; i++)
            {
                if (Windows[i].Title == name)
                {
                    if (Windows[i].Minimized) { Windows[i].Minimized = false; StartOpenAnim(Windows[i]); }
                    SetFocus(i);
                    return;
                }
            }
            OpenNewWindow(name, content);
        }

        private static void HandleDrag(int mx, int my)
        {
            if (_focusedIndex < 0 || _focusedIndex >= Windows.Count) return;
            var w = Windows[_focusedIndex];
            if (w.Resizing) { HandleResize(w, mx, my); return; }
            if (!w.Dragging || w.Maximized) return;
            int newX = Math.Max(0, Math.Min(mx - w.DragOffsetX, Width - w.W));
            int newY = Math.Max(0, Math.Min(my - w.DragOffsetY, Height - 40 - w.H));
            if (newX != w.X || newY != w.Y) { w.X = newX; w.Y = newY; _needsRedraw = true; }
        }

        private static void HandleResize(WindowInfo w, int mx, int my)
        {
            int dx = mx - w.ResizeStartMouseX, dy = my - w.ResizeStartMouseY;
            int newX = w.ResizeStartX, newY = w.ResizeStartY;
            int newW = w.ResizeStartW, newH = w.ResizeStartH;

            switch (w.ResizeEdge)
            {
                case ResizeEdge.Right: newW = Math.Max(WindowInfo.MinW, w.ResizeStartW + dx); break;
                case ResizeEdge.Bottom: newH = Math.Max(WindowInfo.MinH, w.ResizeStartH + dy); break;
                case ResizeEdge.Left:
                    newW = Math.Max(WindowInfo.MinW, w.ResizeStartW - dx);
                    newX = w.ResizeStartX + (w.ResizeStartW - newW); break;
                case ResizeEdge.Top:
                    newH = Math.Max(WindowInfo.MinH, w.ResizeStartH - dy);
                    newY = w.ResizeStartY + (w.ResizeStartH - newH); break;
                case ResizeEdge.BottomRight:
                    newW = Math.Max(WindowInfo.MinW, w.ResizeStartW + dx);
                    newH = Math.Max(WindowInfo.MinH, w.ResizeStartH + dy); break;
                case ResizeEdge.BottomLeft:
                    newW = Math.Max(WindowInfo.MinW, w.ResizeStartW - dx);
                    newX = w.ResizeStartX + (w.ResizeStartW - newW);
                    newH = Math.Max(WindowInfo.MinH, w.ResizeStartH + dy); break;
                case ResizeEdge.TopRight:
                    newW = Math.Max(WindowInfo.MinW, w.ResizeStartW + dx);
                    newH = Math.Max(WindowInfo.MinH, w.ResizeStartH - dy);
                    newY = w.ResizeStartY + (w.ResizeStartH - newH); break;
                case ResizeEdge.TopLeft:
                    newW = Math.Max(WindowInfo.MinW, w.ResizeStartW - dx);
                    newX = w.ResizeStartX + (w.ResizeStartW - newW);
                    newH = Math.Max(WindowInfo.MinH, w.ResizeStartH - dy);
                    newY = w.ResizeStartY + (w.ResizeStartH - newH); break;
            }

            if (newX != w.X || newY != w.Y || newW != w.W || newH != w.H)
            {
                w.X = newX; w.Y = newY; w.W = newW; w.H = newH;
                _needsRedraw = true;
            }
        }

        private static void StopDrag()
        {
            if (_focusedIndex >= 0 && _focusedIndex < Windows.Count)
            {
                Windows[_focusedIndex].Dragging = false;
                Windows[_focusedIndex].Resizing = false;
            }
        }

        // ─── نسخه‌ی عمومی SetFocus برای کدهای بیرون از GraphicsManager
        // (مثل PapAppRuntime.Launch) که فقط WindowInfo دارند، نه ایندکس ──
        public static void FocusWindow(WindowInfo w)
        {
            int idx = Windows.IndexOf(w);
            if (idx < 0) return;
            if (Windows[idx].Minimized) { Windows[idx].Minimized = false; StartOpenAnim(Windows[idx]); }
            SetFocus(idx);
        }

        private static void SetFocus(int idx)
        {
            if (idx < 0 || idx >= Windows.Count) return;
            if (_focusedIndex == idx && Windows[idx].Focused) return;

            // پنجره focused را به آخر لیست ببر (روی بقیه رسم شود)
            var w = Windows[idx];
            Windows.RemoveAt(idx);
            Windows.Add(w);
            idx = Windows.Count - 1;

            for (int i = 0; i < Windows.Count; i++)
                Windows[i].Focused = (i == idx);
            _focusedIndex = idx;
            _needsRedraw = true;
        }

        private static void ToggleMaximize(WindowInfo w)
        {
            // ─── مبدأ انیمیشن: rect واقعی فعلی پنجره ────────────────────────
            // اگر کاربر وسط یک انیمیشن Maximize/Restore قبلی دوباره کلیک کند،
            // w.X/Y/W/H هنوز مقدار "لحظه‌ی فعلی" هستند (چون Tick هر فریم آن‌ها
            // را lerp می‌کند)، پس شروع از اینجا خودش‌به‌خود نرم و بدون پرش است.
            int curX = w.X, curY = w.Y, curW = w.W, curH = w.H;

            if (w.Maximized)
            {
                w.AnimToX = w.RestoreX; w.AnimToY = w.RestoreY;
                w.AnimToW = w.RestoreW; w.AnimToH = w.RestoreH;
                w.Maximized = false;
            }
            else
            {
                w.RestoreX = curX; w.RestoreY = curY;
                w.RestoreW = curW; w.RestoreH = curH;
                w.AnimToX = 0; w.AnimToY = 0; w.AnimToW = Width; w.AnimToH = Height - 40;
                w.Maximized = true;
            }

            w.AnimFromX = curX; w.AnimFromY = curY;
            w.AnimFromW = curW; w.AnimFromH = curH;
            w.MaximizeAnimating = true;
            w.MaximizeAnimFrame = 0;
            _needsRedraw = true;
        }

        private static void StartOpenAnim(WindowInfo w) { w.OpenAnimating = true; w.OpenAnimFrame = 0; }

        public static void OpenNewWindow(string title, string content = "")
            => OpenNewWindow(title, content, -1, -1);

        // ─── overload با اندازه‌ی اجباری — برای دیالوگ‌های کوچک ثابت مثل
        // PapInstallerUI که نباید اندازه‌ی پیش‌فرض 460×340 را بگیرند ─────────
        public static void OpenNewWindow(string title, string content, int forcedW, int forcedH)
        {
            int winW = 460, winH = 340;
            if (content == FileExplorerApp.ContentFlag) { winW = 720; winH = 480; }
            else if (content == NotepadApp.ContentFlag) { winW = 500; winH = 340; }
            else if (title == "Settings") { winW = 500; winH = 380; }
            else if (content == WebBrowserApp.ContentFlag) { winW = 700; winH = 500; }
            else if (content == MusicPlayerApp.ContentFlag) { winW = 480; winH = 560; }
            else if (PapAppRuntime.IsPapAppContent(content))
            {
                var sz = PapAppRuntime.GetPreferredWindowSize(PapAppRuntime.ExtractAppId(content));
                winW = sz.w; winH = sz.h;
            }

            if (forcedW > 0) winW = forcedW;
            if (forcedH > 0) winH = forcedH;

            var w = new WindowInfo
            {
                Title = title,
                Content = content,
                X = Math.Max(0, Math.Min(100 + Windows.Count * 28, Width - winW - 20)),
                Y = Math.Max(0, Math.Min(60 + Windows.Count * 22, Height - winH - 60)),
                W = winW,
                H = winH
            };
            Windows.Add(w);
            StartOpenAnim(w);
            SetFocus(Windows.Count - 1);
            _needsRedraw = true;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  رندر اصلی — از RenderSystem استفاده می‌کند
        //
        //  جریان کار:
        //    EnsureColors()              ← رنگ‌های packed را بروز نگه می‌دارد
        //    RenderSystem.BeginFrame()   ← wallpaper/desktop را به back-buffer می‌کشد
        //    DrawWindows()               ← پنجره‌ها روی back-buffer
        //    DrawStartMenu()             ← منوی استارت
        //    DrawTaskbar()               ← تسک‌بار
        //    DrawThemeOverlay()          ← انیمیشن تم (AlphaFill)
        //    RenderSystem.DrawCursor()   ← کرسر با save/restore
        //    RenderSystem.Flush()        ← فقط dirty region به Canvas ارسال می‌شود
        // ═══════════════════════════════════════════════════════════════════════
        private static void Render()
        {
            EnsureColors();

            // ─── BeginFrame: پس‌زمینه را از کش wallpaper بازیابی کن ──────────
            RenderSystem.BeginFrame(Theme.Desktop);

            // ─── پنجره‌ها (فقط شکل‌ها — بدون متن) ─────────────────────────────
            for (int i = 0; i < Windows.Count; i++)
            {
                var w = Windows[i];
                if (w.Minimized) continue;

                if (w.CloseAnimating)
                {
                    float t = (float)w.CloseAnimFrame / WindowInfo.AnimFrames;
                    DrawWindowClosing(w, WindowInfo.EaseIn(t));
                }
                else if (w.OpenAnimating)
                {
                    float t = (float)w.OpenAnimFrame / WindowInfo.AnimFrames;
                    DrawWindowOpening(w, WindowInfo.EaseOutBack(t));
                }
                else
                {
                    DrawWindowShapes(w, i == _focusedIndex);
                }
            }

            // ─── منوی استارت (فقط شکل‌ها) ───────────────────────────────────
            if (_startMenuAnimF > 0f) DrawStartMenuShapes();

            // ─── تسک‌بار (فقط شکل‌ها) ────────────────────────────────────────
            DrawTaskbarShapes();

            // ─── انیمیشن تم ─────────────────────────────────────────────────
            // قبلاً افت خطی بود (fade با سرعت ثابت) که کمی مکانیکی/ناگهانی
            // حس می‌شد. حالا با EaseOut محو می‌شود: اول سریع‌تر تیره/روشن
            // می‌شود، بعد به‌آرامی به رنگ نهایی می‌رسد — یک fade نرم و آشنا.
            if (_themeAnimating)
            {
                float tf = (float)_themeAnimFrame / ThemeAnimFrames;
                int alpha = (int)(220 * (1f - WindowInfo.EaseOut(tf)));
                if (alpha > 0)
                    RenderSystem.AlphaFill(0, 0, Width, Height,
                        _themeFlashColor, (byte)alpha);
            }

            // ─── Flush: back-buffer → Canvas.DrawImage ────────────────────────
            RenderSystem.Flush(Canvas);

            // ─── متن‌ها روی Canvas (بعد از DrawImage، قبل از Display) ─────────
            // نکته مهم: متن هر پنجره مستقیم روی Canvas واقعی نوشته می‌شود (نه back-buffer)
            // و فقط به محدوده‌ی خودِ همان پنجره کلیپ می‌شود — نه به این‌که چه پنجره‌ای
            // جلوی آن قرار دارد. چون این حلقه هم از عقب به جلو اجرا می‌شود، اگر پنجره‌ی
            // زیرین (i کوچک‌تر) زیر پنجره‌ی رویین (i بزرگ‌تر) قرار گرفته باشد، متنِ پنجره‌ی
            // زیرین مستقیماً روی پیکسل‌هایی نوشته می‌شود که پنجره‌ی رویین از قبل آنجا
            // رسم شده — و چون پنجره‌ی رویین فقط جاهایی را که خودش متن دارد دوباره رسم
            // می‌کند، باقی نواحی همان متنِ نشتی‌کرده‌ی پنجره‌ی زیرین را نشان می‌دهند.
            // راه‌حل: اگر پنجره‌ای توسط پنجره‌ی دیگری که جلوتر است (و باز/غیرمینیمایز است)
            // پوشانده شده، اصلاً متنش را در این فریم رسم نکن — پنجره‌ی جلویی به‌محض
            // فوکوس گرفتن یا جابه‌جا شدن، خودش به‌درستی رسم می‌شود.
            for (int i = 0; i < Windows.Count; i++)
            {
                var w = Windows[i];
                if (w.Minimized || w.CloseAnimating || w.OpenAnimating || w.MaximizeAnimating) continue;
                DrawWindowTexts(w, i == _focusedIndex, i);
            }
            if (_startMenuAnimF > 0f) DrawStartMenuTexts();
            DrawTaskbarTexts();

            // ─── کرسر روی Canvas (آخرین لایه — روی همه عناصر) ─────────────────
            // DrawImageAlpha آلفا کانال را رعایت می‌کند → پس‌زمینه شفاف می‌شود
            if (Kernel.CursorBitmap != null)
                Canvas.DrawImageAlpha(Kernel.CursorBitmap, _mouseX, _mouseY);
            else
                DrawFallbackCursorOnCanvas(_mouseX, _mouseY);

            Canvas.Display();
        }

        // ═══════════════════════════════════════════════════════
        //  DrawWindowShapes — فقط شکل‌ها روی back-buffer (بدون DrawString)
        // ═══════════════════════════════════════════════════════
        private static void DrawWindowShapes(WindowInfo w, bool focused)
        {
            int th = WindowInfo.TitleH, r = 8;

            RenderSystem.FillRoundRect(w.X, w.Y, w.W, w.H, r, ColWindowBg);
            int tbCol = focused ? ColTitleBar : ColTitleBarInact;
            RenderSystem.FillRoundRectTop(w.X, w.Y, w.W, th, r, tbCol);
            RenderSystem.HLine(w.X, w.Y + th, w.W, ColSeparator);
            int borderCol = focused ? ColAccent : ColWindowBorder;
            RenderSystem.DrawRoundRect(w.X, w.Y, w.W, w.H, r, borderCol);

            RenderSystem.FilledCircle(w.CloseCX, w.BtnCY, WindowInfo.BtnR, ColBtnClose);
            RenderSystem.FilledCircle(w.MinCX, w.BtnCY, WindowInfo.BtnR, ColBtnMin);
            RenderSystem.FilledCircle(w.MaxCX, w.BtnCY, WindowInfo.BtnR, ColBtnMax);

            // محتوای بیت‌مپی (آیکون‌ها و غیره که روی back-buffer هستند)
            DrawWindowContentShapes(w);

            if (focused && !w.Maximized)
                DrawResizeHandles(w);
        }

        // ═══════════════════════════════════════════════════════
        //  محاسبه نواحی دیداری (غیرپوشیده) یک پنجره — بدون allocation
        //
        //  چرا لازم است: قبلاً اگر پنجره‌ای حتی ۱ پیکسل با پنجره‌ی جلوتر
        //  هم‌پوشانی داشت، کل متنش حذف می‌شد (IsOccludedByFrontWindow قدیمی).
        //  این یعنی همین که دو پنجره کمی روی هم بیفتند، متن پنجره‌ی پشتی
        //  (غیرفوکوس) کاملاً ناپدید می‌شد، حتی در بخش‌های کاملاً قابل‌مشاهده‌اش.
        //
        //  راه‌حل: مستطیل پنجره را از مستطیل هر پنجره‌ی جلوترِ همپوشان
        //  «تفریق» می‌کنیم (الگوریتم استاندارد rectangle subtraction —
        //  حداکثر ۴ نوار باقی‌مانده در هر تفریق) و فقط در نواحی واقعاً
        //  دیداری متن می‌کشیم.
        // ═══════════════════════════════════════════════════════
        private const int MaxVisRects = 8;
        private static readonly int[] _visX = new int[MaxVisRects];
        private static readonly int[] _visY = new int[MaxVisRects];
        private static readonly int[] _visW = new int[MaxVisRects];
        private static readonly int[] _visH = new int[MaxVisRects];
        private static int _visCount;

        private static readonly int[] _tmpX = new int[MaxVisRects];
        private static readonly int[] _tmpY = new int[MaxVisRects];
        private static readonly int[] _tmpW = new int[MaxVisRects];
        private static readonly int[] _tmpH = new int[MaxVisRects];

        private static void ComputeVisibleRects(WindowInfo w, int index)
        {
            _visCount = 1;
            _visX[0] = w.X; _visY[0] = w.Y; _visW[0] = w.W; _visH[0] = w.H;

            for (int j = index + 1; j < Windows.Count && _visCount > 0; j++)
            {
                var f = Windows[j];
                if (f.Minimized || f.CloseAnimating || f.OpenAnimating || f.MaximizeAnimating) continue;

                int tmpCount = 0;
                for (int k = 0; k < _visCount; k++)
                    SubtractRect(_visX[k], _visY[k], _visW[k], _visH[k], f.X, f.Y, f.W, f.H, ref tmpCount);

                for (int k = 0; k < tmpCount; k++)
                {
                    _visX[k] = _tmpX[k]; _visY[k] = _tmpY[k]; _visW[k] = _tmpW[k]; _visH[k] = _tmpH[k];
                }
                _visCount = tmpCount;
            }
        }

        // مستطیل (ax,ay,aw,ah) منهای (bx,by,bw,bh) → حداکثر ۴ نوار در _tmp* اضافه می‌شود
        private static void SubtractRect(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh, ref int tmpCount)
        {
            int ax2 = ax + aw, ay2 = ay + ah;
            int bx2 = bx + bw, by2 = by + bh;

            if (bx2 <= ax || bx >= ax2 || by2 <= ay || by >= ay2)
            {
                AddTmp(ax, ay, aw, ah, ref tmpCount); // بدون تداخل → کل مستطیل باقی می‌ماند
                return;
            }

            if (by > ay) AddTmp(ax, ay, aw, by - ay, ref tmpCount);                 // نوار بالا
            if (by2 < ay2) AddTmp(ax, by2, aw, ay2 - by2, ref tmpCount);            // نوار پایین

            int midY = Math.Max(ay, by), midY2 = Math.Min(ay2, by2);
            if (bx > ax) AddTmp(ax, midY, bx - ax, midY2 - midY, ref tmpCount);      // نوار چپ
            if (bx2 < ax2) AddTmp(bx2, midY, ax2 - bx2, midY2 - midY, ref tmpCount); // نوار راست
        }

        private static void AddTmp(int x, int y, int w, int h, ref int tmpCount)
        {
            if (w <= 0 || h <= 0 || tmpCount >= MaxVisRects) return;
            _tmpX[tmpCount] = x; _tmpY[tmpCount] = y; _tmpW[tmpCount] = w; _tmpH[tmpCount] = h;
            tmpCount++;
        }

        // ─── آیا یک مستطیل کوچک (مثلاً متن عنوان) توسط پنجره‌ی جلوتری پوشیده شده؟ ───
        // برای DrawText که خودش clip ندارد، فقط با gate ساده (رسم/عدم‌رسم) استفاده می‌شود
        private static bool IsRectCoveredByFront(int index, int x, int y, int w, int h)
        {
            for (int j = index + 1; j < Windows.Count; j++)
            {
                var f = Windows[j];
                if (f.Minimized || f.CloseAnimating || f.OpenAnimating || f.MaximizeAnimating) continue;
                if (x < f.X + f.W && x + w > f.X && y < f.Y + f.H && y + h > f.Y)
                    return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════
        //  DrawWindowTexts — فقط متن‌ها مستقیم روی Canvas (بعد از Flush)
        //  متن‌ها محدود به محدوده پنجره هستند
        // ═══════════════════════════════════════════════════════
        private static void DrawWindowTexts(WindowInfo w, bool focused, int index)
        {
            int th = WindowInfo.TitleH;

            // عنوان — فقط داخل titlebar، و فقط اگر توسط پنجره‌ی جلوتری پوشیده نشده
            // (DrawText مستقیم روی Canvas است و clip ندارد، پس اینجا gate می‌کنیم نه clip)
            int titleW = w.Title.Length * 8;
            int titleX = w.X + (w.W - titleW) / 2;
            if (titleX >= w.X && titleX + titleW <= w.X + w.W
                && !IsRectCoveredByFront(index, titleX, w.Y + 9, titleW, 16))
                DrawText(w.Title, Pens.TitleBarText, titleX, w.Y + 9, false,
                         focused ? Theme.TitleBar : Theme.TitleBarInact);

            // محتوای متنی — کلیپ به بزرگ‌ترین ناحیه‌ی دیداری (غیرپوشیده) پنجره
            // (به‌جای صدا زدن چندباره‌ی Draw برنامه برای هر قطعه‌ی دیداری —
            //  که می‌توانست تایمرهای داخلی مثل چشمک کرسر را دوبار جلو ببرد —
            //  فقط بزرگ‌ترین ناحیه‌ی پیوسته را انتخاب و یک‌بار رسم می‌کنیم)
            ComputeVisibleRects(w, index);
            if (_visCount == 0) return; // کاملاً پوشیده شده

            int bestIdx = 0, bestArea = _visW[0] * _visH[0];
            for (int r = 1; r < _visCount; r++)
            {
                int area = _visW[r] * _visH[r];
                if (area > bestArea) { bestArea = area; bestIdx = r; }
            }

            DrawWindowContentTexts(w, _visX[bestIdx], _visY[bestIdx], _visW[bestIdx], _visH[bestIdx]);
        }

        // ─── DrawWindow قدیمی: هنوز برای انیمیشن‌ها لازم است ────────────────
        private static void DrawWindow(WindowInfo w, bool focused)
        {
            DrawWindowShapes(w, focused);
        }


        private static void DrawResizeHandles(WindowInfo w)
        {
            int s = 6;
            for (int i = 0; i < s; i++)
            {
                RenderSystem.SetPixel(w.X + w.W - i, w.Y + w.H - (s - i), ColWindowBorder);
                RenderSystem.SetPixel(w.X + i, w.Y + w.H - (s - i), ColWindowBorder);
                RenderSystem.SetPixel(w.X + w.W - i, w.Y + (s - i), ColWindowBorder);
                RenderSystem.SetPixel(w.X + i, w.Y + (s - i), ColWindowBorder);
            }
        }

        // ─── DrawWindowContentShapes: فقط بخش‌های back-buffer ──────────────────
        private static void DrawWindowContentShapes(WindowInfo w)
        {
            // برنامه‌های خاص محتوای بیت‌مپی ندارند — Settings تب‌دار شده
            if (w.Content == "SETTINGS_APP")
            {
                DrawSettingsShapes(w);
            }
            else if (w.Content == WebBrowserApp.ContentFlag)
            {
                WebBrowserApp.DrawShapes(w, _mouseX, _mouseY);
            }
            else if (w.Content == MusicPlayerApp.ContentFlag)
            {
                MusicPlayerApp.DrawShapes(w, _mouseX, _mouseY);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DrawSettingsShapes — شکل‌های Settings با سه تب روی back-buffer
        // ═══════════════════════════════════════════════════════════════════
        private static void DrawSettingsShapes(WindowInfo w)
        {
            int th = WindowInfo.TitleH;
            int cx = w.X + 16;
            int tabsY = w.Y + th + 8;
            const int tabW = 90, tabH = 26;
            int rightEdge = w.X + w.W - 16;
            int contentY = tabsY + tabH + 12;

            // ─── رسم تب‌ها (۴ تب) ───────────────────────────────────────────
            for (int ti = 0; ti < 4; ti++)
            {
                int tx = cx + ti * (tabW + 4);
                int col = (ti == _settingsTab) ? ColAccent : ColWindowBorder;
                RenderSystem.FillRoundRect(tx, tabsY, tabW, tabH, 5, col);
            }

            // خط جداکننده زیر عنوان بخش
            RenderSystem.HLine(cx, contentY + 18, rightEdge - cx, ColSeparator);

            if (_settingsTab == 0)      // Display
            {
                DrawToggleSwitch(cx + 130, contentY + 32, Theme.DarkMode);
            }
            else if (_settingsTab == 1) // Memory
            {
                DrawMemoryBar(w, cx, contentY + 28, rightEdge);
            }
            else if (_settingsTab == 2) // Network
            {
                DrawNetworkShapes(w, cx, contentY + 28, rightEdge);
            }
            else                        // Personalize
            {
                DrawPersonalizeShapes(w, cx, contentY + 28, rightEdge);
            }
        }

        // ─── DrawWindowContentTexts: فقط DrawString‌ها روی Canvas ───────────────
        private static void DrawWindowContentTexts(WindowInfo w, int clipX, int clipY, int clipW, int clipH)
        {
            if (_startMenuAnimF >= 0.99f)
            {
                int menuX = 4, menuY = Height - 40 - StartMenuH;
                if (w.X >= menuX && w.X + w.W <= menuX + StartMenuW
                    && w.Y + WindowInfo.TitleH >= menuY) return;
            }

            int th = WindowInfo.TitleH;
            int cx = w.X + 16, cy = w.Y + th + 14;
            int contentBottom = w.Y + w.H - 4;

            // ناحیه محتوای پنجره (بدون حاشیه/تایتل‌بار)
            int contentX = w.X + 1, contentY = w.Y + th, contentW = w.W - 2, contentH = w.H - th - 1;

            // محدوده نهایی = اشتراک محتوای پنجره با ناحیه‌ی دیداری (غیرپوشیده) ورودی
            int ix = Math.Max(contentX, clipX);
            int iy = Math.Max(contentY, clipY);
            int ix2 = Math.Min(contentX + contentW, clipX + clipW);
            int iy2 = Math.Min(contentY + contentH, clipY + clipH);
            if (ix2 <= ix || iy2 <= iy) return; // چیزی از محتوا در ناحیه دیداری نیست

            WCanvas.SetClip(ix, iy, ix2 - ix, iy2 - iy);

            if (w.Content == "SETTINGS_APP") { DrawSettingsContentTexts(w, cx, cy); return; }
            if (w.Content == NotepadApp.ContentFlag) { NotepadApp.Draw(w, cx, cy, TtfFontNormal); return; }
            if (w.Content == TerminalApp.ContentFlag) { TerminalApp.Draw(w, _font); return; }
            // قبلاً _font (PCScreenFont داخلی کاسموس بدون AA/گلیف فارسی) استفاده می‌شد؛
            // حالا مثل NotepadApp/WebBrowserApp از TtfFontNormal (Vazir) استفاده می‌کند
            // تا کیفیت متن File Explorer با بقیه‌ی سیستم یکسان باشد.
            if (w.Content == FileExplorerApp.ContentFlag) { FileExplorerApp.Draw(w, cx, cy, TtfFontNormal); return; }
            if (w.Content == WebBrowserApp.ContentFlag)
            {
                // قبلاً _font (PCScreenFont داخلی کاسموس، بدون گلیف فارسی/عربی
                // و بدون شکل‌دهی حروف) به مرورگر داده می‌شد که علت اصلی نمایش
                // کاراکترهای عجیب در صفحه خوش‌آمدگویی و هر متن فارسی دیگر در
                // مرورگر بود. حالا مثل NotepadApp از TtfFontNormal (Vazir) با
                // مسیر DrawAuto (شکل‌دهی + BiDi) استفاده می‌شود.
                WebBrowserApp.DrawTexts(w, WCanvas, TtfFontNormal);
                return;
            }
            if (w.Content == MusicPlayerApp.ContentFlag)
            {
                MusicPlayerApp.DrawTexts(w, WCanvas, TtfFontNormal);
                return;
            }

            // ⛔ نصب/اجرای برنامه‌های .pap بسته شده. اگر به هر دلیلی هنوز یک
            // پنجره با این نوع Content باز شده باشد (مثلاً یک نصب قدیمی)،
            // فقط یک پیام روشن نشان بده، نه رابط نصب/اجرای واقعی.
            if (w.Content == PapInstallerUI.ContentFlag || PapAppRuntime.IsPapAppContent(w.Content))
            {
                WCanvas.DrawTtf(TtfFontNormal, "این ویژگی غیرفعال شده است.", Pens.WindowBorder, cx, cy, Theme.WindowBg);
                return;
            }

            if (!string.IsNullOrEmpty(w.Content))
            {
                w.GetContentLines(out string[] lines, out int lineCount);
                int maxLines = (w.H - th - 16) / 18;
                int drawCount = lineCount < maxLines ? lineCount : maxLines;
                for (int i = 0; i < drawCount; i++)
                {
                    int lineY = cy + i * 18;
                    if (lineY + 16 > contentBottom) break;
                    WCanvas.DrawTtf(TtfFontNormal, lines[i], Pens.TextPrimary, cx, lineY, Theme.WindowBg);
                }
            }
        }

        // ─── DrawWindowContent قدیمی (برای سازگاری با کدهای دیگر) ───────────────
        private static void DrawWindowContent(WindowInfo w)
        {
            DrawWindowContentTexts(w, w.X, w.Y, w.W, w.H);
        }


        private static void DrawSettingsContentTexts(WindowInfo w, int cx, int cy)
        {
            int th = WindowInfo.TitleH;
            int tabsY = w.Y + th + 8;
            const int tabW = 90, tabH = 26;
            int contentY = tabsY + tabH + 12;

            // ─── برچسب تب‌ها (۴ تب) ──────────────────────────────────────────
            string[] tabLabels = { "Display", "Memory", "Network", "Personalize" };
            for (int ti = 0; ti < 4; ti++)
            {
                int tx = cx + ti * (tabW + 4) + (ti == 3 ? 4 : 8);
                Pen lp = (ti == _settingsTab) ? Pens.White : Pens.TextPrimary;
                WCanvas.DrawTtf(TtfFontNormal, tabLabels[ti], lp, tx, tabsY + 6,
                                 (ti == _settingsTab) ? Theme.Accent : Theme.WindowBorder);
            }

            if (_settingsTab == 0)      // Display
            {
                WCanvas.DrawTtf(TtfFontNormal, "Display", Pens.Accent, cx, contentY, Theme.WindowBg);
                WCanvas.DrawTtf(TtfFontNormal, "Dark Mode", Pens.TextPrimary, cx, contentY + 32 + 4, Theme.WindowBg);
                string desc = Theme.DarkMode ? "On  - Dark interface" : "Off - Light interface";
                WCanvas.DrawTtf(TtfFontNormal, desc, Pens.WindowBorder, cx, contentY + 32 + 22, Theme.WindowBg);
            }
            else if (_settingsTab == 1) // Memory
            {
                WCanvas.DrawTtf(TtfFontNormal, "Memory", Pens.Accent, cx, contentY, Theme.WindowBg);
                DrawMemoryBarTexts(w, cx, contentY + 28);
            }
            else if (_settingsTab == 2) // Network
            {
                WCanvas.DrawTtf(TtfFontNormal, "Network", Pens.Accent, cx, contentY, Theme.WindowBg);
                DrawNetworkTexts(w, cx, contentY + 28);
            }
            else                        // Personalize
            {
                WCanvas.DrawTtf(TtfFontNormal, "Personalize", Pens.Accent, cx, contentY, Theme.WindowBg);
                DrawPersonalizeTexts(w, cx, contentY + 28);
            }
        }

        private static void DrawSettingsContent(WindowInfo w, int cx, int cy)
        {
            int rightEdge = w.X + w.W - 16;

            WCanvas.DrawTtf(TtfFontNormal, "Display", Pens.Accent, cx, cy, Theme.WindowBg);
            RenderSystem.HLine(cx, cy + 18, rightEdge - cx, ColSeparator);

            int rowY = cy + 28;
            WCanvas.DrawTtf(TtfFontNormal, "Dark Mode", Pens.TextPrimary, cx, rowY + 4, Theme.WindowBg);
            DrawToggleSwitch(cx + 130, rowY, Theme.DarkMode);

            string desc = Theme.DarkMode ? "On  - Dark interface" : "Off - Light interface";
            WCanvas.DrawTtf(TtfFontNormal, desc, Pens.WindowBorder, cx, rowY + 26, Theme.WindowBg);

            int memSectionY = rowY + 60;
            WCanvas.DrawTtf(TtfFontNormal, "Memory", Pens.Accent, cx, memSectionY, Theme.WindowBg);
            RenderSystem.HLine(cx, memSectionY + 18, rightEdge - cx, ColSeparator);

            DrawMemoryBar(w, cx, memSectionY + 28, rightEdge);
        }

        private static void DrawMemoryBar(WindowInfo w, int cx, int y, int rightEdge)
        {
            _memCacheCounter++;
            if (_memCacheCounter >= MemCacheInterval || _cachedTotalBytes == 0)
            {
                _memCacheCounter = 0;
                try
                {
                    _cachedTotalBytes = Cosmos.Core.CPU.GetAmountOfRAM() * 1024UL * 1024UL;
                    _cachedUsedBytes = Cosmos.Core.GCImplementation.GetUsedRAM();
                }
                catch { _cachedTotalBytes = 256UL * 1024 * 1024; _cachedUsedBytes = 64UL * 1024 * 1024; }

                if (_cachedTotalBytes == 0) _cachedTotalBytes = 1;

                ulong usedMB = _cachedUsedBytes / (1024 * 1024);
                ulong totalMB = _cachedTotalBytes / (1024 * 1024);
                ulong freeMB = totalMB > usedMB ? totalMB - usedMB : 0;
                int newPct = (int)((float)_cachedUsedBytes / (float)_cachedTotalBytes * 100f);
                if (newPct > 100) newPct = 100;
                if (newPct != _cachedPct)
                {
                    _cachedPct = newPct;
                    _cachedPctStr = newPct.ToString() + "%";
                    _cachedUsedStr = "Used: " + usedMB.ToString() + " MB";
                    _cachedFreeStr = "Free: " + freeMB.ToString() + " MB";
                    _cachedTotalStr = "Total: " + totalMB.ToString() + " MB";
                }
            }

            float ratio = (float)_cachedUsedBytes / (float)_cachedTotalBytes;
            if (ratio > 1f) ratio = 1f;

            int barW = rightEdge - cx, barH = 22, barR = 6;

            RenderSystem.FillRoundRect(cx, y, barW, barH, barR, ColWindowBorder);

            int fillW = (int)(barW * ratio);
            if (fillW > barR * 2 + 2)
            {
                int fillCol = ratio < 0.6f ? ColMemGreen : (ratio < 0.85f ? ColMemYellow : ColMemRed);
                RenderSystem.FillRoundRect(cx, y, fillW, barH, barR, fillCol);
            }

            int pctX = cx + barW / 2 - _cachedPctStr.Length * 4;
            Color pctBg = ratio < 0.6f ? Pens.MemGreen.Color : (ratio < 0.85f ? Pens.MemYellow.Color : Pens.MemRed.Color);
            WCanvas.DrawTtf(TtfFontNormal, _cachedPctStr, Pens.White, pctX, y + 4, pctBg);
            WCanvas.DrawTtf(TtfFontNormal, _cachedUsedStr, Pens.TextPrimary, cx, y + barH + 6, Theme.WindowBg);
            WCanvas.DrawTtf(TtfFontNormal, _cachedFreeStr, Pens.TextPrimary, cx + barW / 2 - 30, y + barH + 6, Theme.WindowBg);
            WCanvas.DrawTtf(TtfFontNormal, _cachedTotalStr, Pens.WindowBorder, cx, y + barH + 22, Theme.WindowBg);
        }

        // ─── DrawMemoryBarTexts: فقط متن‌های نوار حافظه ─────────────────────────
        private static void DrawMemoryBarTexts(WindowInfo w, int cx, int y)
        {
            if (_cachedTotalBytes == 0) return;
            float ratio = (float)_cachedUsedBytes / (float)_cachedTotalBytes;
            if (ratio > 1f) ratio = 1f;
            int barW = w.X + w.W - 16 - cx, barH = 22;
            int pctX = cx + barW / 2 - _cachedPctStr.Length * 4;
            // درصد روی نوار پرشده رسم می‌شود، پس پس‌زمینه‌اش رنگ همان نوار
            // (سبز/زرد/قرمز بسته به میزان مصرف) است، نه پس‌زمینه‌ی پنجره
            Color pctBg = ratio < 0.6f ? Pens.MemGreen.Color : (ratio < 0.85f ? Pens.MemYellow.Color : Pens.MemRed.Color);
            WCanvas.DrawTtf(TtfFontNormal, _cachedPctStr, Pens.White, pctX, y + 4, pctBg);
            WCanvas.DrawTtf(TtfFontNormal, _cachedUsedStr, Pens.TextPrimary, cx, y + barH + 6, Theme.WindowBg);
            WCanvas.DrawTtf(TtfFontNormal, _cachedFreeStr, Pens.TextPrimary, cx + barW / 2 - 30, y + barH + 6, Theme.WindowBg);
            WCanvas.DrawTtf(TtfFontNormal, _cachedTotalStr, Pens.WindowBorder, cx, y + barH + 22, Theme.WindowBg);
        }


        // ═══════════════════════════════════════════════════════════════════
        //  HandleSettingsClick — کلیک‌های درون پنجره Settings
        // ═══════════════════════════════════════════════════════════════════
        private static bool HandleSettingsClick(WindowInfo w, int mx, int my)
        {
            int th = WindowInfo.TitleH;
            int cx = w.X + 16;
            int tabsY = w.Y + th + 8;
            const int tabW = 90, tabH = 26;
            int contentY = tabsY + tabH + 12;

            // ─── کلیک روی تب‌ها (۴ تب) ──────────────────────────────────
            for (int ti = 0; ti < 4; ti++)
            {
                int tx = cx + ti * (tabW + 4);
                if (mx >= tx && mx <= tx + tabW && my >= tabsY && my <= tabsY + tabH)
                {
                    if (ti != _settingsTab)
                    {
                        _settingsTab = ti;
                        _netCacheCounter = NetCacheInterval; // force refresh
                        if (ti == 3) { _picScrollOffset = 0; _picSelected = -1; }
                        _needsRedraw = true;
                    }
                    return true;
                }
            }

            // ─── تب Display: تاگل تم ─────────────────────────────────
            if (_settingsTab == 0)
            {
                int toggleX = cx + 130, toggleY = contentY + 32;
                if (mx >= toggleX && mx <= toggleX + 44 && my >= toggleY && my <= toggleY + 22)
                {
                    _themeFlashColor = Theme.DarkMode ? _themeFlashLight : _themeFlashDark;
                    Theme.DarkMode = !Theme.DarkMode;
                    _themeAnimFrame = 0;
                    _themeAnimating = true;
                    _colorsStale = true;
                    _needsRedraw = true;
                    return true;
                }
            }

            // ─── تب Network: تاگل شبکه ───────────────────────────────
            if (_settingsTab == 2)
            {
                int toggleX = cx + 130, toggleY = contentY + 28;
                if (mx >= toggleX && mx <= toggleX + 44 && my >= toggleY && my <= toggleY + 22)
                {
                    NetworkDriver.Toggle();
                    _netCacheCounter = NetCacheInterval; // force refresh
                    _needsRedraw = true;
                    return true;
                }
            }

            // ─── تب Personalize ────────────────────────────────────────
            if (_settingsTab == 3)
            {
                return HandlePersonalizeClick(w, mx, my, cx, contentY);
            }

            return false;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DrawNetworkShapes — عناصر back-buffer تب شبکه
        // ═══════════════════════════════════════════════════════════════════
        private static void DrawNetworkShapes(WindowInfo w, int cx, int y, int rightEdge)
        {
            // کلید Enable/Disable
            DrawToggleSwitch(cx + 130, y, NetworkDriver.IsEnabled);

            // نوار رنگی وضعیت
            int barY = y + 40;
            int barW = rightEdge - cx;
            int statusCol;
            switch (NetworkDriver.Status)
            {
                case NetworkStatus.Connected: statusCol = ColMemGreen; break;
                case NetworkStatus.Connecting: statusCol = ColMemYellow; break;
                case NetworkStatus.Error: statusCol = ColMemRed; break;
                default: statusCol = ColWindowBorder; break;
            }
            RenderSystem.FillRoundRect(cx, barY, barW, 10, 4, statusCol);

            // جداکننده جزییات
            RenderSystem.HLine(cx, barY + 20, rightEdge - cx, ColSeparator);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  DrawNetworkTexts — متن‌های تب شبکه روی Canvas
        // ═══════════════════════════════════════════════════════════════════
        private static void DrawNetworkTexts(WindowInfo w, int cx, int y)
        {
            // ─── بروزرسانی کش آمار ──────────────────────────────────
            _netCacheCounter++;
            if (_netCacheCounter >= NetCacheInterval || _cachedNetStatus.Length == 0)
            {
                _netCacheCounter = 0;
                _cachedNetStatus = NetworkDriver.StatusText();
                _cachedNetIp = "IP:     " + NetworkDriver.IpAddress;
                _cachedNetMac = "MAC:  " + NetworkDriver.MacAddress;
                _cachedNetGw = "GW:    " + NetworkDriver.Gateway;
                _cachedNetDns = "DNS:  " + NetworkDriver.DnsServer;
                _cachedPktSent = "Sent:  " + NetworkDriver.PacketsSent.ToString();
                _cachedPktRecv = "Recv: " + NetworkDriver.PacketsReceived.ToString();
                SplitDiagnosticLines(NetworkDriver.Diagnostic, out _cachedNetDiag1, out _cachedNetDiag2);
            }

            // ─── برچسب کلید و نام آداپتور ───────────────────────────
            WCanvas.DrawTtf(TtfFontNormal, "Enable Network", Pens.TextPrimary, cx, y + 4, Theme.WindowBg);
            WCanvas.DrawTtf(TtfFontNormal, NetworkDriver.AdapterName, Pens.WindowBorder, cx, y + 22, Theme.WindowBg);

            // ─── نوار وضعیت + برچسب ─────────────────────────────────
            // توجه: این برچسب زیر نوار رنگی (ارتفاع ۱۰px) قرار می‌گیرد، نه رویش،
            // پس پس‌زمینه‌اش رنگ پنجره است نه رنگ نوار
            int barY = y + 40;
            Pen statusPen;
            switch (NetworkDriver.Status)
            {
                case NetworkStatus.Connected: statusPen = Pens.MemGreen; break;
                case NetworkStatus.Connecting: statusPen = Pens.MemYellow; break;
                case NetworkStatus.Error: statusPen = Pens.MemRed; break;
                default: statusPen = Pens.WindowBorder; break;
            }
            WCanvas.DrawTtf(TtfFontNormal, _cachedNetStatus, statusPen, cx, barY + 14, Theme.WindowBg);

            // ─── جزییات شبکه ────────────────────────────────────────
            if (!NetworkDriver.HasAdapter)
            {
                WCanvas.DrawTtf(TtfFontNormal, "No network adapter detected.", Pens.MemRed, cx, barY + 34, Theme.WindowBg);
                return;
            }

            int dy = barY + 34;
            WCanvas.DrawTtf(TtfFontNormal, _cachedNetMac, Pens.TextPrimary, cx, dy, Theme.WindowBg); dy += 18;

            if (NetworkDriver.Status == NetworkStatus.Connected)
            {
                WCanvas.DrawTtf(TtfFontNormal, _cachedNetIp, Pens.MemGreen, cx, dy, Theme.WindowBg); dy += 18;
                WCanvas.DrawTtf(TtfFontNormal, _cachedNetGw, Pens.TextPrimary, cx, dy, Theme.WindowBg); dy += 18;
                WCanvas.DrawTtf(TtfFontNormal, _cachedNetDns, Pens.TextPrimary, cx, dy, Theme.WindowBg); dy += 18;
                WCanvas.DrawTtf(TtfFontNormal, _cachedPktSent, Pens.WindowBorder, cx, dy, Theme.WindowBg); dy += 16;
                WCanvas.DrawTtf(TtfFontNormal, _cachedPktRecv, Pens.WindowBorder, cx, dy, Theme.WindowBg); dy += 20;
            }
            else if (NetworkDriver.Status == NetworkStatus.Connecting)
            {
                WCanvas.DrawTtf(TtfFontNormal, "Waiting for DHCP response...", Pens.MemYellow, cx, dy, Theme.WindowBg);
                dy += 20;
            }
            else if (NetworkDriver.Status == NetworkStatus.Error)
            {
                WCanvas.DrawTtf(TtfFontNormal, NetworkDriver.LastError, Pens.MemRed, cx, dy, Theme.WindowBg);
                dy += 20;
            }

            // ─── خط تشخیصی — چون Console دیده نمی‌شود، وضعیت واقعی DHCP
            //     (تایم‌اوت / exception / static fallback) همیشه این‌جا نشان
            //     داده می‌شود، صرف‌نظر از وضعیت نهایی اتصال ──────────────────
            if (_cachedNetDiag1.Length > 0)
            {
                WCanvas.DrawTtf(TtfFontNormal, _cachedNetDiag1, Pens.MemYellow, cx, dy, Theme.WindowBg); dy += 16;
                if (_cachedNetDiag2.Length > 0)
                    WCanvas.DrawTtf(TtfFontNormal, _cachedNetDiag2, Pens.MemYellow, cx, dy, Theme.WindowBg);
            }
        }

        // ─── شکستن پیام تشخیصی شبکه به حداکثر دو خط (بدون کتابخانه‌ی wrap) ──
        private static void SplitDiagnosticLines(string msg, out string line1, out string line2)
        {
            line1 = ""; line2 = "";
            if (string.IsNullOrEmpty(msg)) return;

            const int maxChars = 46;
            if (msg.Length <= maxChars) { line1 = msg; return; }

            int breakAt = msg.LastIndexOf(' ', Math.Min(maxChars, msg.Length - 1));
            if (breakAt <= 0) breakAt = maxChars;

            line1 = msg.Substring(0, breakAt);
            string rest = msg.Substring(breakAt + 1);
            line2 = rest.Length > maxChars ? rest.Substring(0, maxChars - 1) + "…" : rest;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Personalize — تب شخصی‌سازی
        //  دو بخش:
        //    ۱) لیست تصاویر داخل Assets\Images (اسکرول‌پذیر)
        //    ۲) دکمه باز کردن File Explorer برای انتخاب تصویر دلخواه
        // ═══════════════════════════════════════════════════════════════════

        // رنگ‌های اختصاصی Personalize (packed int، یک‌بار محاسبه)
        private static readonly int ColPicRowBg = RenderSystem.ToInt(System.Drawing.Color.FromArgb(38, 38, 58));
        private static readonly int ColPicRowAlt = RenderSystem.ToInt(System.Drawing.Color.FromArgb(32, 32, 50));
        private static readonly int ColPicRowSel = RenderSystem.ToInt(System.Drawing.Color.FromArgb(60, 80, 160));
        private static readonly int ColPicBtnBg = RenderSystem.ToInt(System.Drawing.Color.FromArgb(55, 55, 100));
        private static readonly int ColPicBtnApply = RenderSystem.ToInt(System.Drawing.Color.FromArgb(40, 130, 60));
        private static readonly Pen _penPicRowBg = new Pen(System.Drawing.Color.FromArgb(38, 38, 58));
        private static readonly Pen _penPicRowAlt = new Pen(System.Drawing.Color.FromArgb(32, 32, 50));
        private static readonly Pen _penPicRowSel = new Pen(System.Drawing.Color.FromArgb(60, 80, 160));
        private static readonly Pen _penPicText = new Pen(System.Drawing.Color.FromArgb(210, 215, 240));
        private static readonly Pen _penPicImgText = new Pen(System.Drawing.Color.FromArgb(250, 185, 65));
        private static readonly Pen _penPicBtnBg = new Pen(System.Drawing.Color.FromArgb(55, 55, 100));
        private static readonly Pen _penPicBtnApply = new Pen(System.Drawing.Color.FromArgb(40, 130, 60));
        private static readonly Pen _penPicBorder = new Pen(System.Drawing.Color.FromArgb(70, 70, 120));
        private static readonly Pen _penPicHdr = new Pen(System.Drawing.Color.FromArgb(130, 140, 200));

        // ─── بارگذاری لیست تصاویر از Assets\Images ──────────────────────────────
        // پیشوند ویژه برای تصاویر embedded (نه مسیر دیسک)
        private const string EmbedPrefix = "EMBED:";

        // کش پسوندها — یک‌بار محاسبه می‌شوند، هر فریم reuse می‌شوند
        private static string[] _picExts = new string[0];

        private static void LoadPicturesList()
        {
            if (_picListLoaded) return;
            _picListLoaded = true;

            var assets = Kernel.GetWallpaperAssetsPublic(); // از کش برمی‌گردد

            // تعداد کل: embeds + فایل‌های دیسک (حداکثر ۳۲)
            // از List استفاده نمی‌کنیم — array ثابت با شمارنده
            int maxSlots = assets.Length + 32;
            var tempF = new string[maxSlots];
            var tempN = new string[maxSlots];
            var tempE = new string[maxSlots]; // پسوند pre-computed
            int count = 0;

            // ─── ۱. embeds ───────────────────────────────────────────────
            for (int i = 0; i < assets.Length && count < maxSlots; i++)
            {
                if (assets[i].Data == null || assets[i].Data.Length == 0) continue;
                tempF[count] = EmbedPrefix + i.ToString();
                tempN[count] = assets[i].FileName;
                // پسوند یک‌بار محاسبه می‌شود
                string e = System.IO.Path.GetExtension(assets[i].FileName);
                tempE[count] = e != null ? e.ToUpper() : "";
                count++;
            }

            // ─── ۲. فایل‌های دیسک — فقط اگر VFS آماده است ───────────────
            // Directory.GetFiles را در یک try خلاصه می‌کنیم تا فریز نشود
            try
            {
                string picPath = @"0:\Assets\Images";
                if (System.IO.Directory.Exists(picPath))
                {
                    string[] files = System.IO.Directory.GetFiles(picPath);
                    for (int i = 0; i < files.Length && count < maxSlots; i++)
                    {
                        string ext = System.IO.Path.GetExtension(files[i]);
                        if (ext == null) continue;
                        string extU = ext.ToUpper();
                        if (extU != ".BMP" && extU != ".PNG") continue; // JPG پشتیبانی نداریم

                        string fname = System.IO.Path.GetFileName(files[i]);
                        // بررسی تکراری بودن
                        bool isDup = false;
                        for (int j = 0; j < count; j++)
                            if (tempN[j] == fname) { isDup = true; break; }
                        if (isDup) continue;

                        tempF[count] = files[i];
                        tempN[count] = fname;
                        tempE[count] = extU;
                        count++;
                    }
                }
            }
            catch { }

            // کپی به آرایه‌های نهایی با اندازه دقیق
            _picFiles = new string[count];
            _picNames = new string[count];
            _picExts = new string[count];
            for (int i = 0; i < count; i++)
            {
                _picFiles[i] = tempF[i];
                _picNames[i] = tempN[i];
                _picExts[i] = tempE[i];
            }
        }

        // ─── شکل‌های Personalize روی back-buffer ────────────────────────────
        private static void DrawPersonalizeShapes(WindowInfo w, int cx, int y, int rightEdge)
        {
            LoadPicturesList();
            int listW = rightEdge - cx;
            int listY = y + 22;  // زیر عنوان بخش

            // ─── پس‌زمینه لیست ─────────────────────────────────────────────
            int listH = PicRowH * PicListMaxRows;
            RenderSystem.FillRoundRect(cx, listY, listW, listH, 4, ColPicRowBg);
            RenderSystem.DrawRect(cx, listY, listW, listH, ColWindowBorder);

            // ─── ردیف‌های تصویر ────────────────────────────────────────────
            for (int i = 0; i < PicListMaxRows; i++)
            {
                int idx = i + _picScrollOffset;
                if (idx >= _picFiles.Length) break;
                int ry = listY + i * PicRowH;
                int rowCol = (idx == _picSelected) ? ColPicRowSel
                           : (i % 2 == 0 ? ColPicRowBg : ColPicRowAlt);
                RenderSystem.Fill(cx + 1, ry + 1, listW - 2, PicRowH - 1, rowCol);
            }

            // ─── دکمه‌ها ────────────────────────────────────────────────────
            int btnY = listY + listH + 8;
            // دکمه Apply (اعمال انتخاب از لیست)
            RenderSystem.FillRoundRect(cx, btnY, 100, 26, 5, ColPicBtnApply);
            // دکمه Browse (باز کردن File Explorer)
            RenderSystem.FillRoundRect(cx + 108, btnY, 120, 26, 5, ColPicBtnBg);
        }

        // ─── متن‌های Personalize روی Canvas ─────────────────────────────────
        private static void DrawPersonalizeTexts(WindowInfo w, int cx, int y)
        {
            LoadPicturesList();
            int rightEdge = w.X + w.W - 16;
            int listW = rightEdge - cx;
            int listY = y + 22;

            // ─── عنوان بخش ─────────────────────────────────────────────────
            WCanvas.DrawTtf(TtfFontNormal, "Wallpaper", Pens.TextPrimary, cx, y, Theme.WindowBg);
            WCanvas.DrawTtf(TtfFontNormal, @"Assets\Images:", _penPicHdr, cx, y + 8, Theme.WindowBg);

            // ─── هدر ستون ──────────────────────────────────────────────────
            WCanvas.DrawTtf(TtfFontNormal, "File Name", _penPicHdr, cx + 8, listY + 2, _penPicRowBg.Color);

            if (_picFiles.Length == 0)
            {
                WCanvas.DrawTtf(TtfFontNormal, @"No images found in 0:\Assets\Images", Pens.WindowBorder, cx + 8, listY + PicRowH + 4, _penPicRowBg.Color);
                WCanvas.DrawTtf(TtfFontNormal, "(BMP / PNG supported)", Pens.WindowBorder, cx + 8, listY + PicRowH + 20, _penPicRowBg.Color);
            }
            else
            {
                for (int i = 0; i < PicListMaxRows; i++)
                {
                    int idx = i + _picScrollOffset;
                    if (idx >= _picFiles.Length) break;
                    int ry = listY + i * PicRowH + 4;
                    bool sel = (idx == _picSelected);
                    string name = _picNames[idx];
                    if (name.Length > 34) name = name.Substring(0, 31) + "...";
                    // پس‌زمینه‌ی ردیف دقیقاً همان منطق DrawPersonalizeShapes را
                    // تکرار می‌کند: انتخاب‌شده / زوج / فرد
                    Color rowBg = sel ? _penPicRowSel.Color : (i % 2 == 0 ? _penPicRowBg.Color : _penPicRowAlt.Color);
                    WCanvas.DrawTtf(TtfFontNormal, name, sel ? Pens.White : _penPicImgText, cx + 8, ry, rowBg);
                    // پسوند از کش — بدون GetExtension/ToUpper در هر فریم
                    if (_picExts != null && idx < _picExts.Length)
                        WCanvas.DrawTtf(TtfFontNormal, _picExts[idx], _penPicHdr, cx + listW - 42, ry, rowBg);
                }

                // اسکرول‌بار متنی
                if (_picFiles.Length > PicListMaxRows)
                {
                    int total = _picFiles.Length;
                    string scrollInfo = (_picScrollOffset + 1).ToString() + "-" +
                        System.Math.Min(_picScrollOffset + PicListMaxRows, total).ToString() +
                        " / " + total.ToString();
                    // آخرین ردیف دیداری لیست ممکن است انتخاب‌شده یا زوج/فرد باشد؛
                    // چون مکان این برچسب پایین لیست است، پس‌زمینه‌ی ردیف آخر را حساب می‌کنیم
                    int lastRowIdx = PicListMaxRows - 1 + _picScrollOffset;
                    Color scrollBg = (lastRowIdx == _picSelected) ? _penPicRowSel.Color
                                   : ((PicListMaxRows - 1) % 2 == 0 ? _penPicRowBg.Color : _penPicRowAlt.Color);
                    WCanvas.DrawTtf(TtfFontNormal, scrollInfo, _penPicHdr, cx + listW - scrollInfo.Length * 8 - 4, listY + PicRowH * PicListMaxRows - 14, scrollBg);
                }
            }

            // ─── دکمه‌ها ────────────────────────────────────────────────────
            int btnY = listY + PicRowH * PicListMaxRows + 8;
            WCanvas.DrawTtf(TtfFontNormal, "Apply", Pens.White, cx + 28, btnY + 6, _penPicBtnApply.Color);
            WCanvas.DrawTtf(TtfFontNormal, "Browse Files...", Pens.TextPrimary, cx + 122, btnY + 6, _penPicBtnBg.Color);

            // ─── نام wallpaper فعلی ─────────────────────────────────────────
            int infoY = btnY + 34;
            WCanvas.DrawTtf(TtfFontNormal, "Current wallpaper:", _penPicHdr, cx, infoY, Theme.WindowBg);
            string curName = Kernel.Wallpaper != null ? "Custom / Loaded" : "Default";
            WCanvas.DrawTtf(TtfFontNormal, curName, Pens.TextPrimary, cx, infoY + 16, Theme.WindowBg);
        }

        // ─── هندلر کلیک Personalize ─────────────────────────────────────────
        private static bool HandlePersonalizeClick(WindowInfo w, int mx, int my, int cx, int contentY)
        {
            int rightEdge = w.X + w.W - 16;
            int listW = rightEdge - cx;
            int y = contentY + 28;
            int listY = y + 22;
            int listH = PicRowH * PicListMaxRows;
            int btnY = listY + listH + 8;

            // ─── کلیک روی لیست ─────────────────────────────────────────────
            if (mx >= cx && mx <= cx + listW && my >= listY && my < listY + listH)
            {
                int clickedRow = (my - listY) / PicRowH;
                int idx = clickedRow + _picScrollOffset;
                if (idx >= 0 && idx < _picFiles.Length)
                {
                    if (_picSelected == idx)
                    {
                        // دوبار کلیک شبیه‌سازی → اعمال
                        ApplyWallpaperFromList();
                    }
                    else
                    {
                        _picSelected = idx;
                        _needsRedraw = true;
                    }
                }
                return true;
            }

            // ─── دکمه Apply ────────────────────────────────────────────────
            if (mx >= cx && mx <= cx + 100 && my >= btnY && my <= btnY + 26)
            {
                ApplyWallpaperFromList();
                return true;
            }

            // ─── دکمه Browse (باز کردن File Explorer در حالت picker) ────────
            if (mx >= cx + 108 && mx <= cx + 228 && my >= btnY && my <= btnY + 26)
            {
                OpenWallpaperPicker();
                return true;
            }

            // ─── اسکرول با کلیک روی لبه پایین لیست ─────────────────────────
            // (اسکرول ساده: کلیک روی نیمه پایین لیست = scroll down)
            if (mx >= cx + listW - 16 && my >= listY && my < listY + listH)
            {
                if (my > listY + listH / 2)
                {
                    if (_picScrollOffset + PicListMaxRows < _picFiles.Length)
                    { _picScrollOffset++; _needsRedraw = true; }
                }
                else
                {
                    if (_picScrollOffset > 0)
                    { _picScrollOffset--; _needsRedraw = true; }
                }
                return true;
            }

            return false;
        }

        // ─── نمایش فوری پیام «در حال بارگذاری» قبل از عملیات بلوکه‌کننده ────
        // نکته مهم: PngDecoder.Decode (و new Bitmap برای BMP) هنوز sync هستند —
        // فقط مرحله‌ی Scale/Composite توسط WallpaperLoader غیرهمزمان شده.
        // برای تصاویر بزرگ، Decode خودش می‌تواند ۱ تا چند ثانیه طول بکشد و
        // چون Cosmos رشته (Thread) ندارد، در همان تیک تمام UI را بلوکه می‌کند.
        // تا زمانی‌که PngDecoder خودش به‌صورت تدریجی (شبیه WallpaperLoader)
        // بازنویسی نشود، حداقل کاری که می‌توانیم بکنیم این است که قبل از شروع
        // Decode یک فریم را فوری Flush/Display کنیم تا کاربر پیام «در حال
        // بارگذاری» را ببیند، نه یک سیستم بی‌پاسخ که گمان کند هنگ کرده.
        private static void ShowBlockingProgressMessage(string text)
        {
            try
            {
                EnsureColors();
                RenderSystem.BeginFrame(Theme.Desktop);
                RenderSystem.Flush(Canvas);

                int textW = MeasureText(text);
                int boxW = textW + 48, boxH = 56;
                int boxX = (Width - boxW) / 2, boxY = (Height - boxH) / 2;

                Canvas.DrawFilledRectangle(Pens.Black, boxX, boxY, boxW, boxH);
                Canvas.DrawRectangle(Pens.White, boxX, boxY, boxW, boxH);
                DrawText(text, Pens.White, boxX + (boxW - textW) / 2, boxY + boxH / 2 - 8, false, Color.Black);

                Canvas.Display();
            }
            catch { /* اگر چیزی هنوز init نشده، صرفاً از این فیدبک بصری صرف‌نظر کن */ }
        }

        // ─── اعمال wallpaper از لیست انتخاب‌شده ────────────────────────────
        private static void ApplyWallpaperFromList()
        {
            if (_picSelected < 0 || _picSelected >= _picFiles.Length) return;
            if (WallpaperLoader.IsBusy) return; // از double-apply جلوگیری کن

            string filePath = _picFiles[_picSelected];
            try
            {
                if (filePath.StartsWith(EmbedPrefix))
                {
                    // ─── تصویر embedded ────────────────────────────────────────
                    int embedIdx = int.Parse(filePath.Substring(EmbedPrefix.Length));
                    var assets = Kernel.GetWallpaperAssetsPublic();
                    if (embedIdx < 0 || embedIdx >= assets.Length) return;
                    byte[] data = assets[embedIdx].Data;
                    if (data == null || data.Length == 0) return;

                    string name = assets[embedIdx].FileName;
                    string extU = System.IO.Path.GetExtension(name).ToUpper();

                    if (extU == ".PNG")
                    {
                        // ─── Decode در همین تیک (سریع‌تر از فایل دیسک) ────────
                        ShowBlockingProgressMessage("Loading wallpaper...");
                        var decoded = PngDecoder.Decode(data);
                        if (decoded == null || !decoded.IsValid || decoded.Pixels == null) return;

                        // Scale را به WallpaperLoader بده — غیرهمزمان
                        WallpaperLoader.StartLoad(decoded.Pixels, decoded.Width, decoded.Height, Width, Height);

                        // ذخیره metadata
                        Kernel.WallpaperPng = decoded;
                        Kernel.Wallpaper = null;
                        Kernel.SaveSetting("wallpaper", "embed:" + embedIdx.ToString());
                    }
                    else
                    {
                        // BMP embedded — BMP decode خودش سریع است
                        var bmp = new Cosmos.System.Graphics.Bitmap(data);
                        WallpaperLoader.StartLoadBmp(bmp.rawData, (int)bmp.Width, (int)bmp.Height, Width, Height);
                        Kernel.WallpaperPng = null;
                        Kernel.Wallpaper = bmp;
                        Kernel.SaveSetting("wallpaper", "embed:" + embedIdx.ToString());
                    }
                }
                else
                {
                    // ─── فایل روی دیسک ──────────────────────────────────────────
                    if (!System.IO.File.Exists(filePath)) return;
                    byte[] data = System.IO.File.ReadAllBytes(filePath);
                    if (data == null || data.Length == 0) return;
                    string extU = System.IO.Path.GetExtension(filePath).ToUpper();

                    if (extU == ".PNG")
                    {
                        ShowBlockingProgressMessage("Loading wallpaper...");
                        var decoded = PngDecoder.Decode(data);
                        if (decoded == null || !decoded.IsValid || decoded.Pixels == null) return;
                        WallpaperLoader.StartLoad(decoded.Pixels, decoded.Width, decoded.Height, Width, Height);
                        Kernel.WallpaperPng = decoded;
                        Kernel.Wallpaper = null;
                        data = null;
                        Kernel.SaveSetting("wallpaper", "file:" + filePath);
                    }
                    else
                    {
                        var bmp = new Cosmos.System.Graphics.Bitmap(data);
                        WallpaperLoader.StartLoadBmp(bmp.rawData, (int)bmp.Width, (int)bmp.Height, Width, Height);
                        Kernel.WallpaperPng = null;
                        Kernel.Wallpaper = bmp;
                        Kernel.SaveSetting("wallpaper", "file:" + filePath);
                    }
                }

                _needsRedraw = true;
                // GC را به بعد از اتمام WallpaperLoader موکول کن
                // (WallpaperLoader.Tick پس از اتمام Heap.Collect نمی‌زند — مدیریت GC با Kernel است)
            }
            catch { }
        }

        // ─── باز کردن File Explorer در حالت Wallpaper Picker ───────────────
        private static void OpenWallpaperPicker()
        {
            // اگر پنجره File Explorer picker قبلاً باز است، فقط focus کن
            for (int i = 0; i < Windows.Count; i++)
            {
                if (Windows[i].Title == "Select Wallpaper")
                { SetFocus(i); return; }
            }

            // حالت picker را فعال کن
            FileExplorerApp.WallpaperPickerMode = true;
            FileExplorerApp.OnWallpaperPicked = (path) =>
            {
                // callback: wallpaper را لود و اعمال کن
                try
                {
                    // ─── فیدبک فوری قبل از Decode بلوکه‌کننده ───────────────
                    // این همان مسیری است که هنگام «انتخاب والپیپر» از File
                    // Explorer اجرا می‌شود — قبلاً اصلاً فیدبکی قبل از Decode
                    // نشان داده نمی‌شد و سیستم کاملاً فریزشده به‌نظر می‌رسید.
                    ShowBlockingProgressMessage("Loading wallpaper...");

                    byte[] data = System.IO.File.ReadAllBytes(path);
                    string extU2 = System.IO.Path.GetExtension(path).ToUpper();
                    if (extU2 == ".PNG")
                    {
                        var decoded = PngDecoder.Decode(data);
                        Kernel.WallpaperPng = decoded;
                        Kernel.Wallpaper = null;
                        data = null;
                    }
                    else
                    {
                        Kernel.WallpaperPng = null;
                        Kernel.Wallpaper = new Bitmap(data);
                    }
                    OnWallpaperChanged();
                    // ─── ذخیره مسیر فایل در تنظیمات ──────────────────────
                    Kernel.SaveSetting("wallpaper", "file:" + path);
                }
                catch { }

                // پنجره picker را ببند و حالت را reset کن
                FileExplorerApp.WallpaperPickerMode = false;
                FileExplorerApp.OnWallpaperPicked = null;
                for (int i = 0; i < Windows.Count; i++)
                {
                    if (Windows[i].Title == "Select Wallpaper")
                    {
                        Windows[i].CloseAnimating = true;
                        Windows[i].CloseAnimFrame = 0;
                        break;
                    }
                }
                _needsRedraw = true;
            };

            // باز کردن File Explorer در پوشه Assets\Images
            OpenNewWindow("Select Wallpaper", FileExplorerApp.ContentFlag);
            // Navigate به Assets\Images
            var newWin = Windows[Windows.Count - 1];
            var st = FileExplorerApp.GetOrCreateState(newWin);
            FileExplorerApp.NavigateTo(st, @"0:\Assets\Images");
        }

        private static void DrawToggleSwitch(int x, int y, bool on)
        {
            int bgCol = on ? ColAccent : ColWindowBorder;
            RenderSystem.Fill(x + 11, y + 2, 22, 18, bgCol);
            RenderSystem.FilledCircle(x + 11, y + 11, 9, bgCol);
            RenderSystem.FilledCircle(x + 33, y + 11, 9, bgCol);
            int thumbX = on ? x + 33 : x + 11;
            RenderSystem.FilledCircle(thumbX, y + 11, 7, ColWhite);
        }

        // ═══════════════════════════════════════════════════════
        //  انیمیشن باز شدن — با RenderSystem
        // ═══════════════════════════════════════════════════════
        private static void DrawWindowOpening(WindowInfo w, float t)
        {
            // دامنه‌ی قبلی (مقیاس ۰.۸۸→۱ و اسلاید ۱۲px) خیلی کوچک بود — حتی
            // با مدت‌زمان درست، حرکت به‌سختی دیده می‌شد. حالا مقیاس از ۰.۷۸
            // شروع می‌شود و اسلاید تا ۲۲px می‌رود تا پنجره واقعاً «باز شدن»
            // را با یک حس نرم و دوستانه (EaseOutBack از قبل توسط فراخوان
            // اعمال شده) نشان دهد.
            float s = 0.78f + t * 0.22f;
            int sw = (int)(w.W * s), sh = (int)(w.H * s);
            int sx = w.X + (w.W - sw) / 2;
            int slideOffset = (int)((1f - t) * 22f);
            int sy = w.Y + (w.H - sh) / 2 + slideOffset;

            if (t < 0.35f)
            {
                RenderSystem.Fill(sx, sy, sw, Math.Min(sh, WindowInfo.TitleH), ColTitleBarInact);
                RenderSystem.DrawRect(sx, sy, sw, sh, ColDimBorder);
            }
            else if (t < 0.7f)
            {
                RenderSystem.FillRoundRect(sx, sy, sw, sh, 8, ColWindowBg);
                RenderSystem.FillRoundRectTop(sx, sy, sw, Math.Min(sh, WindowInfo.TitleH), 8, ColTitleBar);
                RenderSystem.HLine(sx, sy + WindowInfo.TitleH, sw, ColSeparator);
                RenderSystem.DrawRoundRect(sx, sy, sw, sh, 8, ColAccent);
            }
            else
            {
                RenderSystem.FillRoundRect(sx, sy, sw, sh, 8, ColWindowBg);
                RenderSystem.FillRoundRectTop(sx, sy, sw, Math.Min(sh, WindowInfo.TitleH), 8, ColTitleBar);
                RenderSystem.HLine(sx, sy + WindowInfo.TitleH, sw, ColSeparator);
                RenderSystem.DrawRoundRect(sx, sy, sw, sh, 8, ColAccent);
                int titleW2 = w.Title.Length * 8;
                DrawText(w.Title, Pens.TitleBarText, sx + (sw - titleW2) / 2, sy + 8, false, Theme.TitleBar);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  انیمیشن بستن — با RenderSystem
        // ═══════════════════════════════════════════════════════
        private static void DrawWindowClosing(WindowInfo w, float t)
        {
            // هم‌تراز با DrawWindowOpening: دامنه‌ی حرکت بزرگ‌تر شد تا واقعاً
            // «جمع‌شدن و رفتن به سمت پایین» حس شود، نه فقط یک کوچک‌شدن جزئی.
            float s = 1f - t * 0.62f;
            int sw = (int)(w.W * s), sh = (int)(w.H * s);
            int sx = w.X + (w.W - sw) / 2;
            int slideOffset = (int)(t * 18f);
            int sy = w.Y + (w.H - sh) / 2 + slideOffset;
            if (sw < 4 || sh < 4) return;

            if (t < 0.5f)
            {
                RenderSystem.FillRoundRect(sx, sy, sw, sh, 8, ColWindowBg);
                RenderSystem.FillRoundRectTop(sx, sy, sw, Math.Min(sh, WindowInfo.TitleH), 8, ColTitleBar);
                RenderSystem.DrawRoundRect(sx, sy, sw, sh, 8, ColBtnClose);
            }
            else
            {
                RenderSystem.DrawRect(sx, sy, sw, sh, ColDimBorder);
            }
        }

        private static int[] GetTop2AppIndices()
        {
            if (!_top2Dirty) return _top2Cache;
            _top2Dirty = false;

            bool anyUsage = false;
            for (int i = 0; i < 6; i++) if (_appUsageCount[i] > 0) { anyUsage = true; break; }
            if (!anyUsage) { _top2Cache[0] = 1; _top2Cache[1] = 4; return _top2Cache; }

            int best1 = -1, best2 = -1, score1 = -1, score2 = -1;
            for (int i = 0; i < 6; i++)
            {
                if (_appUsageCount[i] > score1) { score2 = score1; best2 = best1; score1 = _appUsageCount[i]; best1 = i; }
                else if (_appUsageCount[i] > score2) { score2 = _appUsageCount[i]; best2 = i; }
            }
            _top2Cache[0] = best1; _top2Cache[1] = best2;
            return _top2Cache;
        }

        // ═══════════════════════════════════════════════════════
        //  منوی استارت — با RenderSystem
        // ═══════════════════════════════════════════════════════
        private static readonly string[] _shutdownStr = { "  Shut Down", "  Restart" };

        private static void DrawStartMenuShapes()
        {
            float easedT = WindowInfo.EaseInOutQuad(_startMenuAnimF);
            int visH = (int)(StartMenuH * easedT);
            if (visH <= 2) return;

            int menuX = 4;
            int slideUp = (int)((1f - easedT) * 16f);
            int clipY = Height - 40 - visH;
            int drawY = clipY + slideUp;
            int drawH = visH - slideUp;
            if (drawH <= 0) return;

            RenderSystem.FillRoundRectTop(menuX, drawY, StartMenuW, drawH, 10, ColStartMenuBg);
            RenderSystem.DrawRoundRect(menuX, drawY, StartMenuW, drawH, 10, ColStartMenuBorder);

            if (_startMenuAnimF < 0.55f) return;

            RenderSystem.HLine(menuX + 6, drawY + 33, StartMenuW - 12, ColSeparator);

            int[] top2 = GetTop2AppIndices();
            int suggestBaseY = drawY + 38 + 16;
            int suggestItemH = 52;
            int iconOff = (48 - 32) / 2;

            for (int si = 0; si < 2; si++)
            {
                int idx = top2[si];
                if (idx < 0) continue;
                int siy = suggestBaseY + si * (suggestItemH + 4);
                RenderSystem.FillRoundRect(menuX + 6, siy, StartMenuW - 12, suggestItemH - 4, 6, ColTaskbarItem);

                Bitmap icon = null;
                if (idx == 0) icon = Kernel.IconSettings;
                else if (idx == 1) icon = Kernel.IconNotepad;
                else if (idx == 2) icon = Kernel.IconFiles;
                else if (idx == 3) icon = Kernel.IconTerminal;
                else if (idx == 4) icon = Kernel.IconBrowser;
                else if (idx == 5) icon = Kernel.IconMusicPlayer;

                int iconX = menuX + 14, iconY = siy + (suggestItemH - 4 - 32) / 2;
                if (icon != null)
                    RenderSystem.BlitAlpha(icon.rawData, (int)icon.Width, (int)icon.Height,
                                           iconX - iconOff, iconY - iconOff);
                else
                    RenderSystem.FillRoundRect(iconX, iconY, 32, 32, 5, ColAccent);
            }

            int dividerY = suggestBaseY + 2 * (suggestItemH + 4) + 2;
            RenderSystem.HLine(menuX + 6, dividerY, StartMenuW - 12, ColSeparator);

            int fullMenuBottom = Height - 40, itemH = 44;
            int shutY = fullMenuBottom - itemH * 2 - 6;
            int rebY = fullMenuBottom - itemH - 3;

            if (shutY >= drawY && shutY + itemH <= fullMenuBottom)
            {
                int shutCol = _startMenuHover == 0 ? ColShutdownHover : ColShutdownRed;
                RenderSystem.FillRoundRect(menuX + 6, shutY, StartMenuW - 12, itemH - 4, 6, shutCol);
            }

            if (rebY >= drawY && rebY + itemH <= fullMenuBottom)
            {
                int rebCol = _startMenuHover == 1 ? ColRebootHover : ColRebootYellow;
                RenderSystem.FillRoundRect(menuX + 6, rebY, StartMenuW - 12, itemH - 4, 6, rebCol);
            }
        }

        private static void DrawStartMenuTexts()
        {
            float easedT = WindowInfo.EaseInOutQuad(_startMenuAnimF);
            int visH = (int)(StartMenuH * easedT);
            if (visH <= 2 || _startMenuAnimF < 0.55f) return;

            int menuX = 4;
            int slideUp = (int)((1f - easedT) * 16f);
            int clipY = Height - 40 - visH;
            int drawY = clipY + slideUp;
            int drawH = visH - slideUp;
            if (drawH <= 0) return;

            DrawText("ParsOS", Pens.AccentHover, menuX + 14, drawY + 10, false, Theme.StartMenuBg);

            int suggestLabelY = drawY + 38;
            DrawText("Suggested", Pens.WindowBorder, menuX + 14, suggestLabelY, false, Theme.StartMenuBg);

            int[] top2 = GetTop2AppIndices();
            int suggestBaseY = suggestLabelY + 16;
            int suggestItemH = 52;

            for (int si = 0; si < 2; si++)
            {
                int idx = top2[si];
                if (idx < 0) continue;
                int siy = suggestBaseY + si * (suggestItemH + 4);
                int iconX = menuX + 14;

                Bitmap icon = null;
                if (idx == 0) icon = Kernel.IconSettings;
                else if (idx == 1) icon = Kernel.IconNotepad;
                else if (idx == 2) icon = Kernel.IconFiles;
                else if (idx == 3) icon = Kernel.IconTerminal;
                else if (idx == 4) icon = Kernel.IconBrowser;
                else if (idx == 5) icon = Kernel.IconMusicPlayer;

                // نکته: ردیف پیشنهادی خودش یک پس‌زمینه‌ی جدا دارد (ColTaskbarItem در
                // DrawStartMenuShapes)، نه رنگ کلی منوی استارت — پس همان را پاس می‌دهیم
                if (icon == null)
                    DrawText(_appInitials4[idx], Pens.White, iconX + 12, siy + 10, false, Theme.TaskbarItem);

                int textX = iconX + 40;
                DrawText(_appNames4[idx], Pens.TextPrimary, textX, siy + 8, false, Theme.TaskbarItem);
                DrawText("App", Pens.WindowBorder, textX, siy + 24, false, Theme.TaskbarItem);
            }

            int fullMenuBottom = Height - 40, itemH = 44;
            int shutY = fullMenuBottom - itemH * 2 - 6;
            int rebY = fullMenuBottom - itemH - 3;

            if (shutY >= drawY && shutY + itemH <= fullMenuBottom)
            {
                Color shutBg = _startMenuHover == 0 ? Pens.ShutdownHover.Color : Pens.ShutdownRed.Color;
                DrawText(_shutdownStr[0], Pens.White, menuX + 16, shutY + 12, false, shutBg);
            }

            if (rebY >= drawY && rebY + itemH <= fullMenuBottom)
            {
                Color rebBg = _startMenuHover == 1 ? Pens.RebootHover.Color : Pens.RebootYellow.Color;
                DrawText(_shutdownStr[1], Pens.Black, menuX + 16, rebY + 12, false, rebBg);
            }
        }

        private static void DrawStartMenu()
        {
            DrawStartMenuShapes();
        }


        private static void DrawTaskbarShapes()
        {
            int tbH = 40, tbY = Height - tbH;
            RenderSystem.Fill(0, tbY, Width, tbH, ColTaskbar);
            RenderSystem.HLine(0, tbY, Width, ColAccent);

            int startCol = _startMenuOpen ? ColAccentHover : ColAccent;
            RenderSystem.FillRoundRect(4, tbY + 4, 62, 32, 6, startCol);

            UpdateIconBounce();
            DrawAppIcon(74, tbY, Kernel.IconSettings, "Settings", 0);
            DrawAppIcon(122, tbY, Kernel.IconNotepad, "Notepad", 1);
            DrawAppIcon(170, tbY, Kernel.IconFiles, "File Explorer", 2);
            DrawAppIcon(218, tbY, Kernel.IconTerminal, "Terminal", 3);
            DrawAppIcon(266, tbY, Kernel.IconBrowser, "Browser", 4);
            DrawAppIcon(314, tbY, Kernel.IconMusicPlayer, "Music Player", 5);
            // ⛔ آیکون Calculator (.pap) حذف شد — سیستم .pap بسته شده

            RenderSystem.VLine(366, tbY + 7, tbH - 14, ColSeparator);

            int dynX = 372, btnW = 110;
            for (int i = 0; i < Windows.Count; i++)
            {
                int bx = dynX + i * (btnW + 4);
                if (bx + btnW > Width - 80) break;

                bool active = (i == _focusedIndex) && !Windows[i].Minimized;
                int bg = active ? ColTaskbarActive : ColTaskbarItem;
                RenderSystem.FillRoundRect(bx, tbY + 4, btnW, 32, 5, bg);
                RenderSystem.DrawRect(bx, tbY + 4, btnW, 32, active ? ColAccent : ColDimBorder);

                if (!Windows[i].Minimized)
                    RenderSystem.FilledCircle(bx + btnW / 2, tbY + tbH - 3, 2, ColAccent);
            }
        }

        // ─── کش رنگ‌های ثابت نشانگر زبان (یک‌بار محاسبه) ──────────────────
        private static readonly Color ColLangFaC = Color.FromArgb(70, 50, 130);
        private static readonly Color ColLangEnC = Color.FromArgb(30, 80, 50);
        private static readonly int ColLangFa = RenderSystem.ToInt(ColLangFaC);
        private static readonly int ColLangEn = RenderSystem.ToInt(ColLangEnC);

        private static void DrawTaskbarTexts()
        {
            int tbH = 40, tbY = Height - tbH;

            Color startBg = _startMenuOpen ? Theme.AccentHover : Theme.Accent;
            DrawText("START", Pens.White, 11, tbY + 12, false, startBg);

            int dynX = 372, btnW = 110;
            for (int i = 0; i < Windows.Count; i++)
            {
                int bx = dynX + i * (btnW + 4);
                if (bx + btnW > Width - 80) break;

                bool active = (i == _focusedIndex) && !Windows[i].Minimized;
                string label = Windows[i].ShortTitle ?? Windows[i].Title;
                DrawText(label, Pens.TaskbarText, bx + 8, tbY + 12, false,
                         active ? Theme.TaskbarActive : Theme.TaskbarItem);
            }

            // ساعت
            var now = DateTime.Now;
            if (now.Minute != _lastCachedMinute)
            {
                _lastCachedMinute = now.Minute;
                int h = now.Hour, m = now.Minute;
                _cachedTimeStr = (h < 10 ? "0" : "") + h.ToString()
                               + ":" + (m < 10 ? "0" : "") + m.ToString();
                _cachedTimeStrW = MeasureText(_cachedTimeStr);
            }
            int clockX = Width - _cachedTimeStrW - 8;
            DrawText(_cachedTimeStr, Pens.TaskbarClock, clockX, tbY + 10, false, Theme.Taskbar);

            // ─── نشانگر زبان (کنار چپ ساعت) ──────────────────────────────────
            if (_lastLangIsFarsi != InputLanguage.IsFarsi)
            {
                _lastLangIsFarsi = InputLanguage.IsFarsi;
                _cachedLangLabelW = MeasureText(InputLanguage.Label);
            }
            int langX = clockX - _cachedLangLabelW - 10;
            // پس‌زمینه کوچک برای خوانایی — از رنگ‌های کش‌شده استفاده می‌کنیم
            int langBgColor = InputLanguage.IsFarsi ? ColLangFa : ColLangEn;
            RenderSystem.FillRoundRect(langX - 4, tbY + 6, _cachedLangLabelW + 8, 28, 5, langBgColor);
            Pen langPen = InputLanguage.IsFarsi ? Pens.AccentHover : Pens.MemGreen;
            DrawText(InputLanguage.Label, langPen, langX, tbY + 10, false,
                     InputLanguage.IsFarsi ? ColLangFaC : ColLangEnC);
        }

        private static void DrawTaskbar()
        {
            DrawTaskbarShapes();
        }


        // ─── آیکون برنامه در تسک‌بار ──────────────────────────────
        // رفع باگ «آیکون‌ها کوچک نمی‌شوند»: قبلاً IconSrcOff فقط مبدأ رسم را
        // جابه‌جا می‌کرد، در حالی که RenderSystem.BlitAlpha اصلاً پارامتر
        // مقیاس ندارد و همیشه کل تصویر ۴۸×۴۸ را ۱ به ۱ می‌کشد — یعنی آیکون
        // هیچ‌وقت واقعاً کوچک نمی‌شد، فقط جابه‌جا و روی عناصر مجاور می‌افتاد.
        // حالا هر آیکون فقط یک‌بار (اولین بار که رسم می‌شود) با
        // RenderSystem.ScaleARGB به سایز واقعی نمایش کوچک و کش می‌شود؛ از آن
        // به بعد فقط همان نسخه‌ی کوچک با BlitAlpha معمولی (بدون مقیاس‌دهی در
        // حلقه‌ی رندر) کشیده می‌شود.
        private const int IconDrawSize = 30;   // سایز واقعی نمایش (بزرگ‌تر شد)
        private const int IconSlotW = 48;
        private static readonly int[][] _smallTaskbarIcons = new int[6][];

        private static int[] _iconBounceFrame = new int[6] { 0, 0, 0, 0, 0, 0 };
        // قبلاً ۲۰ فریم با دامنه‌ی فقط ۴px بود — خیلی ریز بود و روی حلقه‌ی
        // بدون محدودیت قبلی عملاً دیده نمی‌شد. حالا ۲۶ فریم با دامنه‌ی ۹px
        // و یک منحنی «پرش با ته‌نشین‌شدن نرم» (parabola + ease-out) دارد که
        // واقعاً به‌چشم می‌آید ولی همچنان ظریف و دوستانه است.
        private const int BounceFrames = 26;
        private const float BounceHeight = 9f;

        private static void TriggerIconBounce(int iconIndex)
        {
            if (iconIndex >= 0 && iconIndex < 6) _iconBounceFrame[iconIndex] = 1;
        }

        private static void UpdateIconBounce()
        {
            // نکته: ریست واقعی فریم بانس در DrawAppIcon انجام می‌شود (همان جا
            // که bf >= BounceFrames چک می‌شود) چون هر ۶ آیکون هر فریم رسم
            // می‌شوند. اینجا فقط شمارنده را جلو می‌بریم و redraw درخواست
            // می‌کنیم — یک شاخه‌ی else قبلی اینجا هیچ‌وقت اجرا نمی‌شد چون
            // شرطش (>= BounceFrames) وقتی به آن می‌رسیدیم همیشه false بود؛
            // حذف شد تا کد گمراه‌کننده نباشد.
            for (int i = 0; i < _iconBounceFrame.Length; i++)
                if (_iconBounceFrame[i] > 0) { _iconBounceFrame[i]++; _needsRedraw = true; }
        }

        private static void DrawAppIcon(int x, int tbY, Bitmap icon, string appName, int bounceIdx)
        {
            bool isOpen = false;
            for (int i = 0; i < Windows.Count; i++)
                if (Windows[i].Title == appName) { isOpen = true; break; }

            int bounceOffY = 0;
            int bf = _iconBounceFrame[bounceIdx];
            if (bf > 0 && bf < BounceFrames)
            {
                float t = (float)bf / BounceFrames;
                // پرش سریع به بالا، بعد نشستن نرم به پایین (نه یک پارابولای
                // کاملاً متقارن) — یک حس «دوستانه و زنده‌تر» به بانس می‌دهد.
                float curve = t < 0.35f
                    ? WindowInfo.EaseOut(t / 0.35f)
                    : 1f - WindowInfo.EaseOut((t - 0.35f) / 0.65f);
                bounceOffY = -(int)(curve * BounceHeight);
            }
            else if (bf >= BounceFrames)
                _iconBounceFrame[bounceIdx] = 0;

            int centerX = x + IconSlotW / 2;
            int iconX = centerX - IconDrawSize / 2;
            int iconY = tbY + (40 - IconDrawSize) / 2 + bounceOffY;

            if (icon != null)
            {
                // اولین بار برای این آیکون: یک‌بار کوچک کن و کش کن (نه هر فریم)
                if (_smallTaskbarIcons[bounceIdx] == null)
                {
                    _smallTaskbarIcons[bounceIdx] = RenderSystem.ScaleARGB(
                        icon.rawData, (int)icon.Width, (int)icon.Height,
                        IconDrawSize, IconDrawSize);
                }
                RenderSystem.BlitAlpha(_smallTaskbarIcons[bounceIdx], IconDrawSize, IconDrawSize, iconX, iconY);
            }
            else
                DrawText(appName.Length >= 1 ? appName.Substring(0, 1) : "?", Pens.TaskbarText, x + 6, tbY + 11, false, Theme.Taskbar);

            if (isOpen)
                RenderSystem.FilledCircle(centerX, tbY + 38, 2, ColAccent);
        }

        // ─── ابزارها ───────────────────────────────────────────────────────────
        private static int DistSq(int x1, int y1, int x2, int y2)
        {
            int dx = x1 - x2, dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        // ─── Pen ثابت برای کرسر پیش‌فرض (یک‌بار تخصیص، هرگز new Pen در حلقه نیست) ─
        private static readonly Pen _penFallbackCursorBlack = new Pen(Color.Black);
        private static readonly Pen _penFallbackCursorWhite = new Pen(Color.White);

        // ─── کرسر پیش‌فرض مستقیم روی Canvas (وقتی CursorBitmap نداریم) ─────────
        private static void DrawFallbackCursorOnCanvas(int mx, int my)
        {
            for (int dy = 0; dy < 12; dy++)
            {
                int lw = dy + 1;
                Canvas.DrawFilledRectangle(_penFallbackCursorBlack, mx, my + dy, lw, 1);
            }
        }
    }
}