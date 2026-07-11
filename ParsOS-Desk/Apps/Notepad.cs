// apps/Notepad.cs
// برنامه Notepad برای Xagros OS

using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;
using ParsOS.GUI;   // WindowInfo, GraphicsManager, Pens, Theme
using Sys = Cosmos.System;

namespace ParsOS.Apps
{
    public static class NotepadApp
    {
        // ─── شناسه پنجره ────────────────────────────────────────
        public const string WindowTitle = "Notepad";
        public const string ContentFlag = "NOTEPAD_APP";

        // ─── وضعیت متن ──────────────────────────────────────────
        private static List<string> _lines = new List<string> { "" };
        private static int _cursorLine = 0;
        private static int _cursorCol = 0;
        private static int _scrollLine = 0;
        private static bool _dirty = false;

        // cursor blink
        private static int _blinkTick = 0;
        private const int BlinkInterval = 30;
        private static bool _cursorVisible = true;

        // ─── تنظیمات ────────────────────────────────────────────
        private const int LineHeight = 18;   // legacy — در Draw() از font.LineHeight استفاده می‌شود
        private const int PaddingX = 8;
        private const int CharW = 8;
        private const int GutterW = 36;
        private const int StatusH = 20;
        private const int MenuBarH = 22;   // ارتفاع نوار منو (Save/Open)

        // ─── وضعیت فایل ─────────────────────────────────────────
        private static string _currentFilePath = "";   // مسیر فایل جاری (خالی = بدون نام)

        // ─── وضعیت دیالوگ مسیر ───────────────────────────────────
        // دیالوگ ساده: یک input box برای وارد کردن مسیر فایل
        private static bool _dialogOpen = false;
        private static bool _dialogIsSave = false;   // true=Save، false=Open
        private static string _dialogInput = "";     // متن وارد‌شده توسط کاربر
        private static int _dialogCursorPos = 0;
        private static bool _dialogCursorVisible = true;
        private static int _dialogBlinkTick = 0;

        // ─── پیام وضعیت کوتاه‌مدت ────────────────────────────────
        private static string _statusMsg = "";
        private static int _statusMsgTick = 0;
        private const int StatusMsgDuration = 90;   // ~3 ثانیه در 30fps

        // ─── Pen‌های از پیش تخصیص‌یافته (هرگز new Pen در حلقه نباید باشد) ──
        private static readonly Pen _penCursor = new Pen(Color.FromArgb(100, 120, 230));
        private static readonly Pen _penLineNum = new Pen(Color.FromArgb(120, 120, 160));
        private static readonly Pen _penGutter = new Pen(Color.FromArgb(55, 55, 80));
        private static readonly Pen _penStatusBar = new Pen(Color.FromArgb(30, 30, 48));
        private static readonly Pen _penStatusText = new Pen(Color.FromArgb(160, 160, 200));
        private static readonly Pen _penDirty = new Pen(Color.FromArgb(255, 160, 60));
        // هایلایت خط فعال - دو نسخه برای تم تاریک/روشن
        private static readonly Pen _penHlDark = new Pen(Color.FromArgb(50, 52, 78));
        private static readonly Pen _penHlLight = new Pen(Color.FromArgb(225, 228, 248));
        // gutter background - دو نسخه برای تم
        private static readonly Pen _penGutterBgDark = new Pen(Color.FromArgb(38, 38, 58));
        private static readonly Pen _penGutterBgLight = new Pen(Color.FromArgb(215, 215, 230));
        // Menu bar pens
        private static readonly Pen _penMenuBar = new Pen(Color.FromArgb(24, 24, 40));
        private static readonly Pen _penMenuBtn = new Pen(Color.FromArgb(55, 55, 85));
        private static readonly Pen _penMenuBtnHover = new Pen(Color.FromArgb(80, 80, 140));
        private static readonly Pen _penMenuText = new Pen(Color.FromArgb(200, 200, 220));
        private static readonly Pen _penMenuSave = new Pen(Color.FromArgb(40, 160, 80));
        private static readonly Pen _penMenuOpen = new Pen(Color.FromArgb(60, 120, 200));
        private static readonly Pen _penMenuNew = new Pen(Color.FromArgb(140, 100, 200));
        // Dialog pens
        private static readonly Pen _penDialogBg = new Pen(Color.FromArgb(28, 28, 46));
        private static readonly Pen _penDialogBorder = new Pen(Color.FromArgb(100, 120, 230));
        private static readonly Pen _penDialogInput = new Pen(Color.FromArgb(40, 42, 62));
        private static readonly Pen _penDialogText = new Pen(Color.FromArgb(220, 220, 235));
        private static readonly Pen _penDialogHint = new Pen(Color.FromArgb(120, 120, 160));
        private static readonly Pen _penDialogConfirm = new Pen(Color.FromArgb(40, 160, 80));
        private static readonly Pen _penDialogCancel = new Pen(Color.FromArgb(180, 60, 60));
        private static readonly Pen _penStatusOk = new Pen(Color.FromArgb(60, 200, 100));
        private static readonly Pen _penStatusErr = new Pen(Color.FromArgb(220, 80, 80));

        // ═══════════════════════════════════════════════════════
        //  ورودی کیبورد
        // ═══════════════════════════════════════════════════════

        // ─── HandleKeyboard: پردازش ورودی کیبورد و تغییر زبان ────────────────
        // از ShiftPressed/AltPressed استفاده می‌کنیم چون Cosmos برای این کلیدها
        // KeyEvent نمی‌فرستد — این دو property وضعیت real-time دارند
        private static bool _langTogglePending = false;

        public static bool HandleKeyboard()
        {
            bool changed = false;

            // ─── تشخیص Shift+Alt برای تغییر زبان ────────────────────────────
            bool shiftNow = Sys.KeyboardManager.ShiftPressed;
            bool altNow = Sys.KeyboardManager.AltPressed;
            if (shiftNow && altNow)
            {
                if (!_langTogglePending)
                {
                    InputLanguage.Toggle();
                    _langTogglePending = true;
                    changed = true;
                }
            }
            else
            {
                _langTogglePending = false;
            }

            while (Sys.KeyboardManager.TryReadKey(out var key))
            {
                // کلیدهای modifier را skip می‌کنیم
                if (key.Key == Sys.ConsoleKeyEx.LShift || key.Key == Sys.ConsoleKeyEx.RShift ||
                    key.Key == Sys.ConsoleKeyEx.LAlt || key.Key == Sys.ConsoleKeyEx.RAlt)
                    continue;

                if (_dialogOpen)
                    ProcessDialogKey(key);
                else
                    ProcessKey(key);
                changed = true;
            }

            _blinkTick++;
            if (_blinkTick >= BlinkInterval)
            {
                _blinkTick = 0;
                _cursorVisible = !_cursorVisible;
                _dialogCursorVisible = _cursorVisible;
                changed = true; // فقط در لحظه toggle بلینک، redraw لازم است
            }

            // کاهش تایمر پیام وضعیت
            if (_statusMsgTick > 0) { _statusMsgTick--; changed = true; }

            return changed;
        }

        // ─── کلیدهای دیالوگ مسیر فایل ────────────────────────────
        private static void ProcessDialogKey(Sys.KeyEvent key)
        {
            switch (key.Key)
            {
                case Sys.ConsoleKeyEx.Escape:
                    _dialogOpen = false;
                    _dialogInput = "";
                    break;

                case Sys.ConsoleKeyEx.Enter:
                    ConfirmDialog();
                    break;

                case Sys.ConsoleKeyEx.Backspace:
                    if (_dialogCursorPos > 0 && _dialogInput.Length > 0)
                    {
                        _dialogInput = _dialogInput.Substring(0, _dialogCursorPos - 1)
                                     + _dialogInput.Substring(_dialogCursorPos);
                        _dialogCursorPos--;
                    }
                    break;

                case Sys.ConsoleKeyEx.Delete:
                    if (_dialogCursorPos < _dialogInput.Length)
                        _dialogInput = _dialogInput.Substring(0, _dialogCursorPos)
                                     + _dialogInput.Substring(_dialogCursorPos + 1);
                    break;

                case Sys.ConsoleKeyEx.LeftArrow:
                    if (_dialogCursorPos > 0) _dialogCursorPos--;
                    break;

                case Sys.ConsoleKeyEx.RightArrow:
                    if (_dialogCursorPos < _dialogInput.Length) _dialogCursorPos++;
                    break;

                case Sys.ConsoleKeyEx.Home:
                    _dialogCursorPos = 0;
                    break;

                case Sys.ConsoleKeyEx.End:
                    _dialogCursorPos = _dialogInput.Length;
                    break;

                default:
                    char c = key.KeyChar;
                    // کاراکترهای مجاز در مسیر فایل
                    if (c >= 32 && c < 127)
                    {
                        _dialogInput = _dialogInput.Substring(0, _dialogCursorPos)
                                     + c.ToString()
                                     + _dialogInput.Substring(_dialogCursorPos);
                        _dialogCursorPos++;
                    }
                    break;
            }
        }

        // ─── تأیید دیالوگ: ذخیره یا باز کردن فایل ────────────────
        private static void ConfirmDialog()
        {
            string path = _dialogInput.Trim();
            if (string.IsNullOrEmpty(path))
            {
                _dialogOpen = false;
                return;
            }

            // اگر مسیر drive letter ندارد، به 0:\ اضافه کن
            if (path.Length < 2 || path[1] != ':')
                path = @"0:\" + path;

            // اطمینان از پسوند .txt برای ذخیره
            if (_dialogIsSave && !path.Contains("."))
                path = path + ".txt";

            if (_dialogIsSave)
            {
                SaveToFile(path);
                _currentFilePath = path;
                _statusMsg = "Saved: " + path;
                _statusMsgTick = StatusMsgDuration;
            }
            else
            {
                // بررسی وجود فایل قبل از باز کردن
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        LoadFromFile(path);
                        _currentFilePath = path;
                        _statusMsg = "Opened: " + path;
                        _statusMsgTick = StatusMsgDuration;
                    }
                    else
                    {
                        _statusMsg = "Not found: " + path;
                        _statusMsgTick = StatusMsgDuration;
                    }
                }
                catch
                {
                    _statusMsg = "Error opening file";
                    _statusMsgTick = StatusMsgDuration;
                }
            }

            _dialogOpen = false;
            _dialogInput = "";
        }

        // ─── باز کردن دیالوگ از بیرون (کلیک روی دکمه) ───────────
        public static void OpenSaveDialog()
        {
            _dialogOpen = true;
            _dialogIsSave = true;
            // پیش‌پر کردن با مسیر جاری
            _dialogInput = string.IsNullOrEmpty(_currentFilePath) ? @"0:\document.txt" : _currentFilePath;
            _dialogCursorPos = _dialogInput.Length;
        }

        public static void OpenFileDialog()
        {
            _dialogOpen = true;
            _dialogIsSave = false;
            _dialogInput = string.IsNullOrEmpty(_currentFilePath) ? @"0:\" : _currentFilePath;
            _dialogCursorPos = _dialogInput.Length;
        }

        public static void NewFile()
        {
            Reset();
            _currentFilePath = "";
            _statusMsg = "New file created";
            _statusMsgTick = StatusMsgDuration;
        }

        // ─── آیا دیالوگ باز است (برای مدیریت کلیک در GraphicsManager) ─
        public static bool IsDialogOpen => _dialogOpen;

        // ─── هندل کلیک موس روی دکمه‌های منو (توسط GraphicsManager فراخوانی می‌شود) ─
        public static bool HandleMenuClick(WindowInfo w, int mx, int my)
        {
            int th = WindowInfo.TitleH;
            int menuY = w.Y + th;

            // بررسی آیا کلیک در نوار منوست
            if (my < menuY || my > menuY + MenuBarH) return false;

            // دکمه New: x=w.X+4
            if (mx >= w.X + 4 && mx < w.X + 44) { NewFile(); return true; }
            // دکمه Open: x=w.X+48
            if (mx >= w.X + 48 && mx < w.X + 100) { OpenFileDialog(); return true; }
            // دکمه Save: x=w.X+104
            if (mx >= w.X + 104 && mx < w.X + 154) { OpenSaveDialog(); return true; }
            // دکمه Save As: x=w.X+158
            if (mx >= w.X + 158 && mx < w.X + 222)
            {
                _currentFilePath = ""; // پاک کردن مسیر → Save As
                OpenSaveDialog();
                return true;
            }

            return false;
        }

        // ─── جدول تبدیل کیبورد فارسی (layout استاندارد فارسی ISIRI) ──────────
        // کلید EN → کاراکتر فارسی معادل
        private static string MapToFarsi(char c)
        {
            switch (c)
            {
                case 'q': return "\u0636"; // ض
                case 'w': return "\u0635"; // ص
                case 'e': return "\u062B"; // ث
                case 'r': return "\u0642"; // ق
                case 't': return "\u0641"; // ف
                case 'y': return "\u063A"; // غ
                case 'u': return "\u0639"; // ع
                case 'i': return "\u0647"; // ه
                case 'o': return "\u062E"; // خ
                case 'p': return "\u062D"; // ح
                case 'a': return "\u0634"; // ش
                case 's': return "\u0633"; // س
                case 'd': return "\u06CC"; // ی
                case 'f': return "\u0628"; // ب
                case 'g': return "\u0644"; // ل
                case 'h': return "\u0627"; // ا
                case 'j': return "\u062A"; // ت
                case 'k': return "\u0646"; // ن
                case 'l': return "\u0645"; // م
                case 'z': return "\u0638"; // ظ
                case 'x': return "\u0637"; // ط
                case 'c': return "\u0632"; // ز
                case 'v': return "\u0631"; // ر
                case 'b': return "\u0630"; // ذ
                case 'n': return "\u062F"; // د
                case 'm': return "\u062C"; // ج
                case 'Q': return "\u0652"; // ّ (شدّه)
                case 'W': return "\u064C"; // ٌ
                case 'E': return "\u064B"; // ً
                case 'R': return "\u064D"; // ٍ
                case 'T': return "\u0644\u0627"; // لا — دو کاراکتر؛ جداگانه هندل می‌شود
                case 'Y': return "\u0625"; // إ
                case 'U': return "\u0621"; // ء
                case 'I': return "\u0622"; // آ
                case 'O': return "\u00AB"; // «
                case 'P': return "\u00BB"; // »
                case 'A': return "\u0648"; // و (shift+a)
                case 'S': return "\u0627"; // ا (با مد)
                case 'D': return "\u0649"; // ى
                case 'F': return "\u0626"; // ئ
                case 'G': return "\u0644\u0627"; // لا
                case 'H': return "\u0623"; // أ
                case 'J': return "\u0640"; // ـ (تطویل)
                case 'K': return "\u060C"; // ،
                case 'L': return "\u003A"; // :
                case 'Z': return "\u0644\u0627"; // لا
                case 'X': return "\u0629"; // ة
                case 'C': return "\u0698"; // ژ
                case 'V': return "\u0632"; // ز  (مثل c)
                case 'B': return "\u0646"; // ن (مثل k)
                case 'N': return "\u062F"; // د (مثل n)
                case 'M': return "\u067E"; // پ
                case ' ': return " ";
                case '0': return "\u06F0"; // ۰
                case '1': return "\u06F1"; // ۱
                case '2': return "\u06F2"; // ۲
                case '3': return "\u06F3"; // ۳
                case '4': return "\u06F4"; // ۴
                case '5': return "\u06F5"; // ۵
                case '6': return "\u06F6"; // ۶
                case '7': return "\u06F7"; // ۷
                case '8': return "\u06F8"; // ۸
                case '9': return "\u06F9"; // ۹

                // ─── کلیدهای باقی‌مانده (مطابق چیدمان استاندارد ویندوز: Persian Standard / kbdfar) ───
                case '[': return "\u062C"; // ج
                case ']': return "\u0686"; // چ
                case ';': return "\u06A9"; // ک
                case '\'': return "\u06AF"; // گ
                case ',': return "\u0648"; // و
                case '\\': return "\\";    // \ — در استاندارد بدون تغییر می‌ماند

                // حالت Shift همان کلیدها (کاراکتری که Cosmos طبق چیدمان US برای Shift می‌فرستد)
                case '{': return "}";      // Shift+[ → } (جابه‌جایی کروشه‌ها طبق استاندارد)
                case '}': return "{";      // Shift+] → {
                case '"': return "\u061B"; // Shift+' → ؛
                case ':': return ":";      // Shift+; → : (بدون تغییر)
                case '<': return "<";      // Shift+, → < (بدون تغییر)
                case '|': return "|";      // Shift+\ → | (بدون تغییر)

                default: return c.ToString();
            }
        }

        private static void ProcessKey(Sys.KeyEvent key)
        {
            _cursorVisible = true;
            _blinkTick = 0;

            switch (key.Key)
            {
                case Sys.ConsoleKeyEx.LeftArrow:
                    if (_cursorCol > 0) _cursorCol--;
                    else if (_cursorLine > 0)
                    { _cursorLine--; _cursorCol = _lines[_cursorLine].Length; }
                    EnsureCursorVisible();
                    break;

                case Sys.ConsoleKeyEx.RightArrow:
                    if (_cursorCol < _lines[_cursorLine].Length) _cursorCol++;
                    else if (_cursorLine < _lines.Count - 1)
                    { _cursorLine++; _cursorCol = 0; }
                    EnsureCursorVisible();
                    break;

                case Sys.ConsoleKeyEx.UpArrow:
                    if (_cursorLine > 0) { _cursorLine--; ClampCol(); }
                    EnsureCursorVisible();
                    break;

                case Sys.ConsoleKeyEx.DownArrow:
                    if (_cursorLine < _lines.Count - 1) { _cursorLine++; ClampCol(); }
                    EnsureCursorVisible();
                    break;

                case Sys.ConsoleKeyEx.Home:
                    _cursorCol = 0;
                    break;

                case Sys.ConsoleKeyEx.End:
                    _cursorCol = _lines[_cursorLine].Length;
                    break;

                case Sys.ConsoleKeyEx.Enter:
                    string rest = _lines[_cursorLine].Substring(_cursorCol);
                    _lines[_cursorLine] = _lines[_cursorLine].Substring(0, _cursorCol);
                    _lines.Insert(_cursorLine + 1, rest);
                    _cursorLine++;
                    _cursorCol = 0;
                    _dirty = true;
                    EnsureCursorVisible();
                    break;

                case Sys.ConsoleKeyEx.Backspace:
                    // نکته مهم (رفع باگ): Backspace همیشه باید کاراکتر «قبل از cursor در
                    // ترتیب منطقی رشته» را حذف کند — دقیقاً مثل حالت LTR — چه خط RTL باشد
                    // چه نه. جهت (RTL/LTR) فقط روی نحوه‌ی رسم بصری تأثیر دارد، نه روی
                    // مدل منطقی متن. شاخه‌ی قبلی برای RTL در واقع یک forward-delete
                    // (حذف کاراکتر بعد از cursor) با cursor ثابت انجام می‌داد که هم با
                    // رفتار واقعی Backspace در RTL در تناقض بود و هم باعث می‌شد بعد از
                    // حذف، ترتیب باقی‌ماندهٔ کاراکترها به‌هم بریزد.
                    if (_cursorCol > 0)
                    {
                        string ln = _lines[_cursorLine];
                        _lines[_cursorLine] = ln.Substring(0, _cursorCol - 1) + ln.Substring(_cursorCol);
                        _cursorCol--;
                        _dirty = true;
                    }
                    else if (_cursorLine > 0)
                    {
                        int prevLen = _lines[_cursorLine - 1].Length;
                        _lines[_cursorLine - 1] += _lines[_cursorLine];
                        _lines.RemoveAt(_cursorLine);
                        _cursorLine--;
                        _cursorCol = prevLen;
                        _dirty = true;
                        EnsureCursorVisible();
                    }
                    break;

                case Sys.ConsoleKeyEx.Delete:
                    if (_cursorCol < _lines[_cursorLine].Length)
                    {
                        string l = _lines[_cursorLine];
                        _lines[_cursorLine] = l.Substring(0, _cursorCol) + l.Substring(_cursorCol + 1);
                        _dirty = true;
                    }
                    else if (_cursorLine < _lines.Count - 1)
                    {
                        _lines[_cursorLine] += _lines[_cursorLine + 1];
                        _lines.RemoveAt(_cursorLine + 1);
                        _dirty = true;
                    }
                    break;

                case Sys.ConsoleKeyEx.Tab:
                    InsertText("    ");
                    break;

                default:
                    char c = key.KeyChar;
                    if (c >= 32 && c < 127)
                    {
                        if (InputLanguage.IsFarsi)
                        {
                            InsertText(MapToFarsi(c));
                        }
                        else
                        {
                            InsertText(c.ToString());
                        }
                    }
                    break;
            }
        }

        private static void InsertText(string text)
        {
            // نکته مهم (رفع باگ ترتیب حروف فارسی): متن همیشه در رشتهٔ منطقی به
            // ترتیب تایپ ذخیره می‌شود — دقیقاً مثل LTR — و cursor همیشه بعد از
            // متن تازه‌درج‌شده می‌آید. جهت RTL/LTR فقط تعیین می‌کند خط چطور
            // روی صفحه رسم شود (DrawTtfRTL در Draw()), نه اینکه کاراکترهای جدید
            // کجای رشته قرار بگیرند. شاخهٔ قبلی RTL چون cursor را ثابت نگه
            // می‌داشت، هر حرف تازه را قبل از حروف قبلی می‌چپاند و در نتیجه کل
            // کلمه معکوس می‌شد (مثلاً تایپ «سلام» نتیجه‌اش «مالس» در حافظه بود).
            string line = _lines[_cursorLine];
            _lines[_cursorLine] = line.Substring(0, _cursorCol) + text + line.Substring(_cursorCol);
            _cursorCol += text.Length;
            _dirty = true;
        }

        private static void ClampCol()
        {
            int max = _lines[_cursorLine].Length;
            if (_cursorCol > max) _cursorCol = max;
        }

        private static void EnsureCursorVisible()
        {
            if (_cursorLine < _scrollLine)
                _scrollLine = _cursorLine;
        }

        // ═══════════════════════════════════════════════════════
        //  رندر
        // ═══════════════════════════════════════════════════════
        // ─── تشخیص RTL بودن خط: اگر اولین کاراکتر غیر فاصله فارسی/عربی باشد ──
        private static bool IsLineRTL(string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                int cp = (int)line[i];
                if (cp == ' ' || cp == '	') continue;
                // بازه‌های Unicode عربی/فارسی
                if ((cp >= 0x0600 && cp <= 0x06FF) ||
                    (cp >= 0xFB50 && cp <= 0xFDFF) ||
                    (cp >= 0xFE70 && cp <= 0xFEFF))
                    return true;
                return false;
            }
            return false;
        }

        public static void Draw(WindowInfo w, int cx, int cy, TtfFont font)
        {
            int th = WindowInfo.TitleH;
            var canvas = GraphicsManager.WCanvas;

            // ارتفاع خط دینامیک از فونت TTF (با کمی padding)
            int lineH = font.LineHeight + 4;

            // ─── نوار منو ────────────────────────────────────────
            DrawMenuBar(w, font, canvas);

            // محتوای متنی پایین‌تر از نوار منو شروع می‌شود
            int menuOffY = MenuBarH + 2;
            int contentX = cx + GutterW;
            int contentY = cy - 4 + menuOffY;
            int contentW = w.W - GutterW - PaddingX * 2;
            int contentH = w.H - th - StatusH - 8 - menuOffY;
            int visLines = contentH / lineH;

            // ─── gutter ─────────────────────────────────────────
            Pen gutterBg = Theme.DarkMode ? _penGutterBgDark : _penGutterBgLight;
            canvas.DrawFilledRectangle(gutterBg, cx - PaddingX, contentY, GutterW + PaddingX, contentH + 4);
            canvas.DrawLine(_penGutter, cx + GutterW - 2, contentY, cx + GutterW - 2, contentY + contentH);

            if (_scrollLine > _lines.Count - 1)
                _scrollLine = Math.Max(0, _lines.Count - 1);

            // ─── خطوط متن ────────────────────────────────────────
            for (int i = 0; i < visLines; i++)
            {
                int lineIdx = _scrollLine + i;
                if (lineIdx >= _lines.Count) break;
                int drawY = contentY + i * lineH;

                // شماره خط
                string lineNum = (lineIdx + 1).ToString();
                int numX = cx + GutterW - font.MeasureWidth(lineNum) - 4;
                canvas.DrawTtf(font, lineNum,
                    lineIdx == _cursorLine ? Pens.Accent : _penLineNum, numX, drawY + 1, gutterBg.Color);

                // هایلایت خط فعال
                Color lineBg;
                if (lineIdx == _cursorLine)
                {
                    Pen hl = Theme.DarkMode ? _penHlDark : _penHlLight;
                    canvas.DrawFilledRectangle(hl, contentX, drawY, contentW, lineH - 1);
                    lineBg = hl.Color;
                }
                else
                {
                    lineBg = Theme.WindowBg;
                }

                // متن — راست‌چین برای فارسی، چپ‌چین برای لاتین
                string lineText = _lines[lineIdx];
                bool lineIsRTL = IsLineRTL(lineText);
                if (lineText.Length > 0)
                {
                    if (lineIsRTL)
                        // رسم RTL: x = لبه راست ناحیه محتوا
                        // DrawTtfRTL از WindowCanvas استفاده می‌کند (بدون نیاز به Canvas مستقیم)
                        canvas.DrawTtfRTL(font, lineText, Pens.TextPrimary,
                            contentX + contentW, drawY + 1, lineBg);
                    else
                        canvas.DrawTtf(font, lineText, Pens.TextPrimary, contentX, drawY + 1, lineBg);
                }

                // cursor
                if (lineIdx == _cursorLine && _cursorVisible)
                {
                    int curX;
                    if (lineIsRTL)
                    {
                        // در RTL، cursor بر اساس کاراکترهای بعد از cursorCol حساب می‌شود
                        string afterCursor = lineText.Substring(_cursorCol);
                        curX = contentX + contentW - font.MeasureRTLWidth(afterCursor);
                    }
                    else
                    {
                        string beforeCursor = lineText.Substring(0, _cursorCol);
                        curX = contentX + font.MeasureWidth(beforeCursor);
                    }
                    canvas.DrawLine(_penCursor, curX, drawY + 2, curX, drawY + lineH - 3);
                }
            }

            // ─── نوار وضعیت ─────────────────────────────────────
            int sbY = w.Y + w.H - StatusH - 2;
            canvas.DrawFilledRectangle(_penStatusBar, w.X + 1, sbY, w.W - 2, StatusH);
            canvas.DrawLine(_penGutter, w.X + 1, sbY, w.X + w.W - 1, sbY);

            string pos = "Ln " + (_cursorLine + 1) + ", Col " + (_cursorCol + 1);
            string total = _lines.Count + " lines";
            canvas.DrawTtf(font, pos, _penStatusText, w.X + 10, sbY + 3, _penStatusBar.Color);
            canvas.DrawTtf(font, total, _penStatusText, w.X + 130, sbY + 3, _penStatusBar.Color);

            if (_dirty)
                canvas.DrawTtf(font, "● Unsaved", _penDirty, w.X + 240, sbY + 3, _penStatusBar.Color);
            else
                canvas.DrawTtf(font, "Saved", _penStatusText, w.X + 240, sbY + 3, _penStatusBar.Color);

            // ─── پیام وضعیت کوتاه‌مدت ────────────────────────────
            if (_statusMsgTick > 0)
            {
                Pen msgPen = _statusMsg.StartsWith("Error") || _statusMsg.StartsWith("Not found")
                    ? _penStatusErr : _penStatusOk;
                // پس‌زمینه کوچک برای خوانایی
                canvas.DrawFilledRectangle(_penMenuBar, w.X + 1, sbY - 18, w.W - 2, 16);
                canvas.DrawTtf(font, _statusMsg, msgPen, w.X + 10, sbY - 16, _penMenuBar.Color);
            }

            // ─── دیالوگ ورود مسیر فایل ───────────────────────────
            if (_dialogOpen)
                DrawDialog(w, font, canvas);
        }

        // ─── رسم نوار منو ─────────────────────────────────────────
        private static void DrawMenuBar(WindowInfo w, TtfFont font, WindowCanvas canvas)
        {
            int th = WindowInfo.TitleH;
            int mbY = w.Y + th;

            // پس‌زمینه نوار منو
            canvas.DrawFilledRectangle(_penMenuBar, w.X + 1, mbY, w.W - 2, MenuBarH);
            canvas.DrawLine(_penGutter, w.X + 1, mbY + MenuBarH - 1, w.X + w.W - 1, mbY + MenuBarH - 1);

            // دکمه New
            canvas.DrawFilledRectangle(_penMenuNew, w.X + 4, mbY + 3, 38, MenuBarH - 6);
            canvas.DrawTtf(font, "New", _penMenuText, w.X + 9, mbY + 5, _penMenuNew.Color);

            // دکمه Open
            canvas.DrawFilledRectangle(_penMenuOpen, w.X + 48, mbY + 3, 50, MenuBarH - 6);
            canvas.DrawTtf(font, "Open", _penMenuText, w.X + 53, mbY + 5, _penMenuOpen.Color);

            // دکمه Save
            canvas.DrawFilledRectangle(_penMenuSave, w.X + 104, mbY + 3, 48, MenuBarH - 6);
            canvas.DrawTtf(font, "Save", _penMenuText, w.X + 110, mbY + 5, _penMenuSave.Color);

            // دکمه Save As
            canvas.DrawFilledRectangle(_penMenuBtn, w.X + 158, mbY + 3, 62, MenuBarH - 6);
            canvas.DrawTtf(font, "Save As", _penMenuText, w.X + 162, mbY + 5, _penMenuBtn.Color);

            // نمایش نام فایل جاری
            string nameDisplay = string.IsNullOrEmpty(_currentFilePath) ? "Untitled" : _currentFilePath;
            // کوتاه کردن نام طولانی
            if (nameDisplay.Length > 30) nameDisplay = "..." + nameDisplay.Substring(nameDisplay.Length - 27);
            canvas.DrawTtf(font, nameDisplay, _penMenuHint2, w.X + 230, mbY + 5, _penMenuBar.Color);
        }

        // ─── Pen اضافی برای نام فایل در منو ──────────────────────
        private static readonly Pen _penMenuHint2 = new Pen(Color.FromArgb(100, 100, 140));

        // ─── رسم دیالوگ ورود مسیر ─────────────────────────────────
        private static void DrawDialog(WindowInfo w, TtfFont font, WindowCanvas canvas)
        {
            // دیالوگ مرکزی پنجره
            int dlgW = Math.Min(w.W - 40, 360);
            int dlgH = 110;
            int dlgX = w.X + (w.W - dlgW) / 2;
            int dlgY = w.Y + (w.H - dlgH) / 2;

            // پس‌زمینه و border
            canvas.DrawFilledRectangle(_penDialogBg, dlgX, dlgY, dlgW, dlgH);
            canvas.DrawRectangle(_penDialogBorder, dlgX, dlgY, dlgW, dlgH);

            // عنوان دیالوگ
            string title = _dialogIsSave ? "Save File" : "Open File";
            canvas.DrawTtf(font, title, _penDialogBorder, dlgX + 12, dlgY + 10, _penDialogBg.Color);
            canvas.DrawLine(_penGutter, dlgX + 4, dlgY + 26, dlgX + dlgW - 4, dlgY + 26);

            // راهنما
            string hint = _dialogIsSave
                ? "Enter path (e.g. 0:\\notes.txt):"
                : "Enter file path to open:";
            canvas.DrawTtf(font, hint, _penDialogHint, dlgX + 10, dlgY + 32, _penDialogBg.Color);

            // کادر ورودی
            int inputY = dlgY + 48;
            int inputW = dlgW - 20;
            canvas.DrawFilledRectangle(_penDialogInput, dlgX + 10, inputY, inputW, 20);
            canvas.DrawRectangle(_penDialogBorder, dlgX + 10, inputY, inputW, 20);

            // متن ورودی (اگر طولانی است، از انتها نشان بده)
            string displayText = _dialogInput;
            // با TTF: ببین چقدر از انتها در inputW جا می‌شود
            while (displayText.Length > 0 && font.MeasureWidth(displayText) > inputW - 10)
                displayText = displayText.Substring(1);
            canvas.DrawTtf(font, displayText, _penDialogText, dlgX + 14, inputY + 4, _penDialogInput.Color);

            // cursor در input — موقعیت با MeasureWidth محاسبه می‌شود
            if (_dialogCursorVisible)
            {
                // اگر متن از ابتدا کوتاه‌شده، cursor را به انتهای displayText ببند
                string cursorText = _dialogInput.Substring(0, Math.Min(_dialogCursorPos, _dialogInput.Length));
                // offset نسبت به شروع displayText در input box
                int offsetChars = _dialogInput.Length - displayText.Length;
                string visibleBeforeCursor = _dialogCursorPos >= offsetChars
                    ? cursorText.Substring(offsetChars)
                    : "";
                int curX = dlgX + 14 + font.MeasureWidth(visibleBeforeCursor);
                canvas.DrawLine(_penCursor, curX, inputY + 3, curX, inputY + 16);
            }

            // دکمه‌های Confirm / Cancel
            int btnY = dlgY + dlgH - 26;
            int confirmX = dlgX + dlgW - 140;
            int cancelX = dlgX + dlgW - 70;

            canvas.DrawFilledRectangle(_penDialogConfirm, confirmX, btnY, 62, 18);
            canvas.DrawTtf(font, "Confirm", _penMenuText, confirmX + 5, btnY + 3, _penDialogConfirm.Color);

            canvas.DrawFilledRectangle(_penDialogCancel, cancelX, btnY, 58, 18);
            canvas.DrawTtf(font, "Cancel", _penMenuText, cancelX + 7, btnY + 3, _penDialogCancel.Color);

            // راهنمای کلید
            canvas.DrawTtf(font, "Enter=OK  Esc=Cancel", _penDialogHint, dlgX + 10, btnY + 3, _penDialogBg.Color);
        }

        // ═══════════════════════════════════════════════════════
        //  ذخیره / بارگذاری
        // ═══════════════════════════════════════════════════════
        public static void SaveToFile(string path)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < _lines.Count; i++)
                {
                    sb.Append(_lines[i]);
                    if (i < _lines.Count - 1) sb.Append('\n');
                }
                System.IO.File.WriteAllText(path, sb.ToString());
                _dirty = false;
            }
            catch { }
        }

        public static void LoadFromFile(string path)
        {
            try
            {
                string content = System.IO.File.ReadAllText(path);
                string[] parts = content.Split('\n');
                _lines = new List<string>(parts);
                _cursorLine = 0;
                _cursorCol = 0;
                _scrollLine = 0;
                _dirty = false;
            }
            catch { }
        }

        public static void Reset()
        {
            _lines = new List<string> { "" };
            _cursorLine = 0;
            _cursorCol = 0;
            _scrollLine = 0;
            _dirty = false;
        }

        // ═══════════════════════════════════════════════════════
        //  بارگذاری محتوا از خارج (مثلاً FileExplorer)
        // ═══════════════════════════════════════════════════════
        public static void SetContent(WindowInfo w, string content)
        {
            // w فقط برای سازگاری با امضای تابع است (آینده: چند نمونه notepad)
            if (string.IsNullOrEmpty(content))
            {
                Reset();
                return;
            }
            string[] parts = content.Split('\n');
            _lines = new List<string>(parts);
            _cursorLine = 0;
            _cursorCol = 0;
            _scrollLine = 0;
            _dirty = false;
        }
    }
}