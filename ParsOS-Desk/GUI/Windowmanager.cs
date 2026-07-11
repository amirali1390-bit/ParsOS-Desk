using Cosmos.System.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using ParsOS.Apps;
using ParsOS.GUI;
using Sys = Cosmos.System;

namespace ParsOS.GUI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  WindowManager — مدیر مرکزی پنجره‌ها
    //
    //  مسئولیت‌ها:
    //    • نگه‌داری لیست پنجره‌ها و ترتیب Z-order
    //    • پردازش کلیک، drag، resize، minimize، maximize، close
    //    • کنترل انیمیشن باز/بسته شدن پنجره‌ها
    //    • مدیریت Focus پنجره فعال
    //    • ارائه رابط IAppHandler برای ادغام برنامه‌های جدید
    //
    //  چه چیزی اینجا نیست:
    //    • رندر (در GraphicsManager باقی می‌ماند)
    //    • تسک‌بار، منوی استارت، تم (در GraphicsManager باقی می‌مانند)
    //    • ورودی کیبورد هر برنامه (هر IAppHandler خودش handle می‌کند)
    // ═══════════════════════════════════════════════════════════════════════════
    public static class WindowManager
    {
        // ─── لیست پنجره‌ها (آخرین = روی همه) ───────────────────────────────
        public static readonly List<WindowInfo> Windows = new List<WindowInfo>();

        // ─── ایندکس پنجره focused (همیشه = Windows.Count-1 یا -1) ──────────
        public static int FocusedIndex { get; private set; } = -1;

        // ─── اعلام نیاز به redraw ────────────────────────────────────────────
        public static bool NeedsRedraw { get; private set; } = false;

        // ─── رابط برنامه‌ها — ثبت می‌کنند تا WindowManager صدا بزند ─────────
        private static readonly List<IAppHandler> _registeredApps = new List<IAppHandler>();

        // ─── ابعاد صفحه (از GraphicsManager پر می‌شود) ──────────────────────
        private static int _screenW, _screenH;

        // ═══════════════════════════════════════════════════════════════════════
        //  راه‌اندازی
        // ═══════════════════════════════════════════════════════════════════════
        public static void Initialize(int screenW, int screenH)
        {
            _screenW = screenW;
            _screenH = screenH;
            Windows.Clear();
            FocusedIndex = -1;

            // ثبت برنامه‌های پیش‌فرض
            RegisterApp(new SettingsAppHandler());
            RegisterApp(new NotepadAppHandler());
            RegisterApp(new FileExplorerAppHandler());
            RegisterApp(new TerminalAppHandler());
        }

        /// <summary>
        /// ثبت یک برنامه جدید — برای افزودن برنامه‌های جدید بدون تغییر این فایل
        /// </summary>
        public static void RegisterApp(IAppHandler handler)
        {
            _registeredApps.Add(handler);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Tick — هر فریم فراخوانی می‌شود
        // ═══════════════════════════════════════════════════════════════════════
        public static void Tick(int mouseX, int mouseY, bool curLeft, bool lastLeft)
        {
            NeedsRedraw = false;

            // ─── انیمیشن‌ها ─────────────────────────────────────────────────
            UpdateAnimations();

            // ─── ورودی ──────────────────────────────────────────────────────
            bool justClicked = curLeft && !lastLeft;
            bool justReleased = !curLeft && lastLeft;

            if (justClicked) HandleClick(mouseX, mouseY);
            if (curLeft) HandleDrag(mouseX, mouseY);
            if (justReleased) StopDrag();

            // ─── ورودی کیبورد پنجره focused ─────────────────────────────────
            if (FocusedIndex >= 0 && FocusedIndex < Windows.Count)
            {
                var fw = Windows[FocusedIndex];
                if (!fw.Minimized)
                {
                    var handler = FindHandler(fw.Content);
                    if (handler != null && handler.HandleKeyboard(fw))
                        NeedsRedraw = true;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  باز/فوکوس کردن پنجره
        // ═══════════════════════════════════════════════════════════════════════
        public static void OpenOrFocus(string appName)
        {
            // پیدا کردن handler ثبت‌شده برای این نام
            var handler = FindHandlerByName(appName);
            if (handler == null) return;

            // اگر قبلاً باز است → فوکوس کن
            for (int i = 0; i < Windows.Count; i++)
            {
                if (Windows[i].Title == appName)
                {
                    if (Windows[i].Minimized)
                    {
                        Windows[i].Minimized = false;
                        StartOpenAnim(Windows[i]);
                    }
                    SetFocus(i);
                    return;
                }
            }

            // اگر باز نیست → پنجره جدید باز کن
            OpenNewWindow(appName, handler.ContentFlag, handler.DefaultWidth, handler.DefaultHeight);
        }

        /// <summary>
        /// باز کردن پنجره جدید با عنوان و محتوای دلخواه
        /// </summary>
        public static WindowInfo OpenNewWindow(string title, string content,
                                               int winW = 460, int winH = 300)
        {
            var w = new WindowInfo
            {
                Title = title,
                Content = content,
                X = Math.Max(0, Math.Min(100 + Windows.Count * 28, _screenW - winW - 20)),
                Y = Math.Max(0, Math.Min(60 + Windows.Count * 22, _screenH - winH - 60)),
                W = winW,
                H = winH
            };
            Windows.Add(w);
            StartOpenAnim(w);
            SetFocus(Windows.Count - 1);
            NeedsRedraw = true;
            return w;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  مدیریت Focus
        // ═══════════════════════════════════════════════════════════════════════
        public static void SetFocus(int idx)
        {
            if (idx < 0 || idx >= Windows.Count) return;
            if (FocusedIndex == idx && Windows[idx].Focused) return;

            // پنجره focused را به آخر لیست ببر (روی بقیه رسم شود)
            var w = Windows[idx];
            Windows.RemoveAt(idx);
            Windows.Add(w);
            idx = Windows.Count - 1;

            for (int i = 0; i < Windows.Count; i++)
                Windows[i].Focused = (i == idx);

            FocusedIndex = idx;
            NeedsRedraw = true;
        }

        public static WindowInfo FocusedWindow
            => (FocusedIndex >= 0 && FocusedIndex < Windows.Count)
               ? Windows[FocusedIndex] : null;

        // ═══════════════════════════════════════════════════════════════════════
        //  Maximize / Minimize
        // ═══════════════════════════════════════════════════════════════════════
        public static void ToggleMaximize(WindowInfo w)
        {
            if (w.Maximized)
            {
                w.X = w.RestoreX; w.Y = w.RestoreY;
                w.W = w.RestoreW; w.H = w.RestoreH;
                w.Maximized = false;
            }
            else
            {
                w.RestoreX = w.X; w.RestoreY = w.Y;
                w.RestoreW = w.W; w.RestoreH = w.H;
                w.X = 0; w.Y = 0;
                w.W = _screenW; w.H = _screenH - 40; // 40 = ارتفاع taskbar
                w.Maximized = true;
            }
            NeedsRedraw = true;
        }

        public static void MinimizeWindow(WindowInfo w)
        {
            w.Minimized = true;
            NeedsRedraw = true;
        }

        public static void RestoreWindow(WindowInfo w)
        {
            w.Minimized = false;
            StartOpenAnim(w);
            NeedsRedraw = true;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  بستن پنجره — انیمیشن و cleanup
        // ═══════════════════════════════════════════════════════════════════════
        public static void CloseWindow(WindowInfo w)
        {
            w.CloseAnimating = true;
            w.CloseAnimFrame = 0;
            NeedsRedraw = true;
        }

        // ─── فراخوانی می‌شود وقتی انیمیشن بستن تمام شود ─────────────────────
        private static void FinishClose(int idx)
        {
            var w = Windows[idx];
            var handler = FindHandler(w.Content);
            handler?.OnClose(w);

            Windows.RemoveAt(idx);
            if (FocusedIndex >= Windows.Count)
                FocusedIndex = Windows.Count - 1;

            Cosmos.Core.Memory.Heap.Collect();
            NeedsRedraw = true;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  پردازش کلیک
        //  بازگشت: آیا یک پنجره کلیک را مصرف کرد؟
        // ═══════════════════════════════════════════════════════════════════════
        public static bool HandleClick(int mx, int my)
        {
            NeedsRedraw = true;

            // از بالا به پایین (آخر = روی همه)
            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                var w = Windows[i];
                if (w.Minimized || w.CloseAnimating || !w.InWindow(mx, my)) continue;

                SetFocus(i);

                // دکمه‌های titlebar
                if (DistSq(mx, my, w.CloseCX, w.BtnCY) <= WindowInfo.BtnR * WindowInfo.BtnR)
                { CloseWindow(w); return true; }

                if (DistSq(mx, my, w.MinCX, w.BtnCY) <= WindowInfo.BtnR * WindowInfo.BtnR)
                { MinimizeWindow(w); return true; }

                if (DistSq(mx, my, w.MaxCX, w.BtnCY) <= WindowInfo.BtnR * WindowInfo.BtnR)
                { ToggleMaximize(w); return true; }

                // ورودی برنامه
                if (!w.InTitleBar(mx, my))
                {
                    var handler = FindHandler(w.Content);
                    if (handler != null && handler.HandleClick(w, mx, my))
                    { NeedsRedraw = true; return true; }
                }

                // resize شروع
                var edge = w.GetResizeEdge(mx, my);
                if (edge != ResizeEdge.None && !w.InTitleBar(mx, my))
                {
                    w.Resizing = true; w.ResizeEdge = edge;
                    w.ResizeStartMouseX = mx; w.ResizeStartMouseY = my;
                    w.ResizeStartX = w.X; w.ResizeStartY = w.Y;
                    w.ResizeStartW = w.W; w.ResizeStartH = w.H;
                    return true;
                }

                // drag
                if (w.InTitleBar(mx, my))
                { w.Dragging = true; w.DragOffsetX = mx - w.X; w.DragOffsetY = my - w.Y; }

                return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  پردازش drag (هر فریم که دکمه نگه داشته شده)
        // ═══════════════════════════════════════════════════════════════════════
        public static void HandleDrag(int mx, int my)
        {
            if (FocusedIndex < 0 || FocusedIndex >= Windows.Count) return;
            var w = Windows[FocusedIndex];

            if (w.Resizing) { ApplyResize(w, mx, my); return; }
            if (!w.Dragging || w.Maximized) return;

            int newX = Math.Max(0, Math.Min(mx - w.DragOffsetX, _screenW - w.W));
            int newY = Math.Max(0, Math.Min(my - w.DragOffsetY, _screenH - 40 - w.H));
            if (newX != w.X || newY != w.Y)
            {
                w.X = newX; w.Y = newY;
                NeedsRedraw = true;
            }
        }

        public static void StopDrag()
        {
            if (FocusedIndex >= 0 && FocusedIndex < Windows.Count)
            {
                Windows[FocusedIndex].Dragging = false;
                Windows[FocusedIndex].Resizing = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Resize
        // ═══════════════════════════════════════════════════════════════════════
        private static void ApplyResize(WindowInfo w, int mx, int my)
        {
            int dx = mx - w.ResizeStartMouseX, dy = my - w.ResizeStartMouseY;
            int newX = w.ResizeStartX, newY = w.ResizeStartY;
            int newW = w.ResizeStartW, newH = w.ResizeStartH;

            switch (w.ResizeEdge)
            {
                case ResizeEdge.Right:
                    newW = Math.Max(WindowInfo.MinW, w.ResizeStartW + dx); break;
                case ResizeEdge.Bottom:
                    newH = Math.Max(WindowInfo.MinH, w.ResizeStartH + dy); break;
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

            // محدود به صفحه
            newX = Math.Max(0, Math.Min(newX, _screenW - WindowInfo.MinW));
            newY = Math.Max(0, Math.Min(newY, _screenH - 40 - WindowInfo.MinH));
            newW = Math.Min(newW, _screenW - newX);
            newH = Math.Min(newH, _screenH - 40 - newY);

            if (newX != w.X || newY != w.Y || newW != w.W || newH != w.H)
            {
                w.X = newX; w.Y = newY; w.W = newW; w.H = newH;
                NeedsRedraw = true;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  انیمیشن‌ها
        // ═══════════════════════════════════════════════════════════════════════
        public static bool AnyAnimationActive()
        {
            for (int i = 0; i < Windows.Count; i++)
                if (Windows[i].OpenAnimating || Windows[i].CloseAnimating) return true;
            return false;
        }

        private static void UpdateAnimations()
        {
            for (int i = Windows.Count - 1; i >= 0; i--)
            {
                var w = Windows[i];

                if (w.OpenAnimating)
                {
                    w.OpenAnimFrame++;
                    if (w.OpenAnimFrame >= WindowInfo.AnimFrames)
                    { w.OpenAnimating = false; w.OpenAnimFrame = WindowInfo.AnimFrames; }
                    NeedsRedraw = true;
                }

                if (w.CloseAnimating)
                {
                    w.CloseAnimFrame++;
                    if (w.CloseAnimFrame >= WindowInfo.AnimFrames)
                    {
                        FinishClose(i);
                        // i را skip کن چون حذف شد
                        continue;
                    }
                    NeedsRedraw = true;
                }
            }
        }

        private static void StartOpenAnim(WindowInfo w)
        {
            w.OpenAnimating = true;
            w.OpenAnimFrame = 0;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  جستجوی handler
        // ═══════════════════════════════════════════════════════════════════════
        private static IAppHandler FindHandler(string contentFlag)
        {
            for (int i = 0; i < _registeredApps.Count; i++)
                if (_registeredApps[i].ContentFlag == contentFlag)
                    return _registeredApps[i];
            return null;
        }

        private static IAppHandler FindHandlerByName(string appName)
        {
            for (int i = 0; i < _registeredApps.Count; i++)
                if (_registeredApps[i].AppName == appName)
                    return _registeredApps[i];
            return null;
        }

        // ─── ابزار ──────────────────────────────────────────────────────────
        private static int DistSq(int x1, int y1, int x2, int y2)
        {
            int dx = x1 - x2, dy = y1 - y2;
            return dx * dx + dy * dy;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  IAppHandler — رابط ثبت برنامه در WindowManager
    //
    //  هر برنامه این رابط را پیاده‌سازی می‌کند و یک‌بار ثبت می‌شود.
    //  WindowManager بدون if/else chain بین برنامه‌ها می‌تواند عمل کند.
    // ═══════════════════════════════════════════════════════════════════════════
    public interface IAppHandler
    {
        /// <summary>نام نمایشی برنامه (عنوان پنجره)</summary>
        string AppName { get; }

        /// <summary>رشته شناسه‌ای که در WindowInfo.Content ذخیره می‌شود</summary>
        string ContentFlag { get; }

        /// <summary>اندازه پیش‌فرض پنجره</summary>
        int DefaultWidth { get; }
        int DefaultHeight { get; }

        /// <summary>پردازش کلیک داخل ناحیه محتوای پنجره</summary>
        bool HandleClick(WindowInfo w, int mx, int my);

        /// <summary>پردازش ورودی کیبورد وقتی پنجره focused است</summary>
        bool HandleKeyboard(WindowInfo w);

        /// <summary>فراخوانی هنگام بسته شدن پنجره (cleanup منابع)</summary>
        void OnClose(WindowInfo w);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  پیاده‌سازی‌های IAppHandler برای برنامه‌های موجود
    //  این کلاس‌ها فقط delegate هستند — منطق اصلی در فایل‌های برنامه است
    // ═══════════════════════════════════════════════════════════════════════════

    public class SettingsAppHandler : IAppHandler
    {
        public string AppName => "Settings";
        public string ContentFlag => "SETTINGS_APP";
        public int DefaultWidth => 460;
        public int DefaultHeight => 300;

        public bool HandleClick(WindowInfo w, int mx, int my)
        {
            // toggle dark mode — منطق از GraphicsManager.HandleClick استخراج شده
            int th = WindowInfo.TitleH;
            int baseCX = w.X + 16, baseCY = w.Y + th + 14;
            int toggleX = baseCX + 130, toggleY = baseCY + 28;
            if (mx >= toggleX && mx <= toggleX + 44 && my >= toggleY && my <= toggleY + 22)
            {
                Theme.DarkMode = !Theme.DarkMode;
                // GraphicsManager باید از این رویداد مطلع شود
                OnThemeChanged?.Invoke();
                return true;
            }
            return false;
        }

        public bool HandleKeyboard(WindowInfo w) => false;
        public void OnClose(WindowInfo w) { }

        /// <summary>رویداد تغییر تم — GraphicsManager مشترک می‌شود</summary>
        public static Action OnThemeChanged;
    }

    public class NotepadAppHandler : IAppHandler
    {
        public string AppName => "Notepad";
        public string ContentFlag => NotepadApp.ContentFlag;
        public int DefaultWidth => 500;
        public int DefaultHeight => 340;

        public bool HandleClick(WindowInfo w, int mx, int my)
            => NotepadApp.HandleMenuClick(w, mx, my);

        public bool HandleKeyboard(WindowInfo w)
            => NotepadApp.HandleKeyboard();

        public void OnClose(WindowInfo w) { }
    }

    public class FileExplorerAppHandler : IAppHandler
    {
        public string AppName => "File Explorer";
        public string ContentFlag => FileExplorerApp.ContentFlag;
        public int DefaultWidth => 720;
        public int DefaultHeight => 480;

        public bool HandleClick(WindowInfo w, int mx, int my)
        {
            FileExplorerApp.HandleClick(w, mx, my);
            return true;
        }

        public bool HandleKeyboard(WindowInfo w)
        {
            var state = FileExplorerApp.GetOrCreateState(w);
            return FileExplorerApp.HandleKeyboard(state);
        }

        public void OnClose(WindowInfo w)
            => FileExplorerApp.CleanupState(w);
    }

    public class TerminalAppHandler : IAppHandler
    {
        public string AppName => "Terminal";
        public string ContentFlag => TerminalApp.ContentFlag;
        public int DefaultWidth => 460;
        public int DefaultHeight => 300;

        public bool HandleClick(WindowInfo w, int mx, int my) => false;

        public bool HandleKeyboard(WindowInfo w)
            => TerminalApp.HandleKeyboard();

        public void OnClose(WindowInfo w) { }
    }
}