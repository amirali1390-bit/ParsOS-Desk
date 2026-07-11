// PapInstallerUI.cs
// پوشه پیشنهادی: GUI/PapInstallerUI.cs
//
// پنجره‌ی نصب یک برنامه‌ی .pap — عمداً یک مرحله‌ای و بدون wizard:
// دوبار کلیک روی .pap → بلافاصله نصب می‌شود → یک کارت کوچک و مدرن نتیجه را
// نشان می‌دهد با دو دکمه: «باز کردن برنامه» و «تمام». هیچ صفحه‌ی
// «Next / License Agreement / Choose Install Location»ای در کار نیست.

using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;
using ParsOS.Apps;
using ParsOS.Apps.Packaging;

namespace ParsOS.GUI
{
    public static class PapInstallerUI
    {
        public const string ContentFlag = "PAP_INSTALLER";

        private const int DialogW = 340;
        private const int DialogH = 200;

        private sealed class UiState
        {
            public bool Success;
            public string ErrorMessage = "";
            public PapManifest Manifest;
        }

        private static readonly Dictionary<WindowInfo, UiState> _states =
            new Dictionary<WindowInfo, UiState>();

        // ─── پن‌های اختصاصی این دیالوگ (یک‌بار ساخته می‌شوند) ──────────
        private static readonly Pen _penTitle = new Pen(Color.FromArgb(220, 220, 235));
        private static readonly Pen _penOk = new Pen(Color.FromArgb(60, 200, 120));
        private static readonly Pen _penErr = new Pen(Color.FromArgb(230, 90, 90));
        private static readonly Pen _penSub = new Pen(Color.FromArgb(150, 155, 180));
        private static readonly Pen _penBtnPrimary = new Pen(Color.FromArgb(100, 120, 230));
        private static readonly Pen _penBtnSecondary = new Pen(Color.FromArgb(60, 60, 90));
        private static readonly Pen _penBtnText = new Pen(Color.White);

        // ═══════════════════════════════════════════════════════
        //  از FileExplorerApp.OpenEntry صدا زده می‌شود وقتی روی یک فایل
        //  .pap دوبار کلیک شود. نصب بلافاصله (همین‌جا) انجام می‌شود — بدون
        //  پرسیدن مسیر، بدون تأیید مجوز، بدون هیچ سؤالی.
        // ═══════════════════════════════════════════════════════
        public static void BeginInstall(string papFilePath)
        {
            var result = PapPackage.Install(papFilePath);

            GraphicsManager.OpenNewWindow("نصب برنامه", ContentFlag, DialogW, DialogH);
            var wins = GraphicsManager.Windows;
            if (wins.Count == 0) return;
            var w = wins[wins.Count - 1];

            _states[w] = new UiState
            {
                Success = result.Success,
                ErrorMessage = result.Success ? "" : result.Error,
                Manifest = result.Manifest
            };
        }

        public static void CleanupState(WindowInfo w) => _states.Remove(w);

        // ═══════════════════════════════════════════════════════
        //  Draw — از DrawWindowContentTexts صدا زده می‌شود
        // ═══════════════════════════════════════════════════════
        public static void Draw(WindowInfo w, WindowCanvas canvas, TtfFont font, int cx, int cy)
        {
            if (!_states.TryGetValue(w, out UiState st)) return;

            Color bg = Theme.WindowBg;
            int contentTop = w.Y + WindowInfo.TitleH;

            if (st.Success)
            {
                canvas.DrawTtf(font, "✓ نصب شد", _penOk, cx, contentTop + 16, bg);
                canvas.DrawTtf(font, st.Manifest.Name, _penTitle, cx, contentTop + 42, bg);
                if (!string.IsNullOrEmpty(st.Manifest.Version))
                    canvas.DrawTtf(font, "نسخه " + st.Manifest.Version, _penSub, cx, contentTop + 62, bg);
                if (!string.IsNullOrEmpty(st.Manifest.Author))
                    canvas.DrawTtf(font, st.Manifest.Author, _penSub, cx, contentTop + 80, bg);

                DrawButton(canvas, font, w.X + 20, w.Y + w.H - 46, 150, 30, "باز کردن", _penBtnPrimary);
                DrawButton(canvas, font, w.X + 180, w.Y + w.H - 46, 140, 30, "تمام", _penBtnSecondary);
            }
            else
            {
                canvas.DrawTtf(font, "نصب انجام نشد", _penErr, cx, contentTop + 16, bg);
                string msg = st.ErrorMessage ?? "";
                if (msg.Length > 42) msg = msg.Substring(0, 39) + "...";
                canvas.DrawTtf(font, msg, _penSub, cx, contentTop + 42, bg);

                DrawButton(canvas, font, w.X + 20, w.Y + w.H - 46, 300, 30, "بستن", _penBtnSecondary);
            }
        }

        private static void DrawButton(WindowCanvas canvas, TtfFont font, int x, int y, int w, int h,
                                        string label, Pen fill)
        {
            canvas.DrawFilledRectangle(fill, x, y, w, h);
            int textW = font.MeasureWidth(label);
            canvas.DrawTtf(font, label, _penBtnText, x + (w - textW) / 2, y + 7, fill.Color);
        }

        // ═══════════════════════════════════════════════════════
        //  کلیک — از همان هوک کلیکِ FileExplorerApp/NotepadApp صدا زده می‌شود
        // ═══════════════════════════════════════════════════════
        public static void HandleClick(WindowInfo w, int mx, int my)
        {
            if (!_states.TryGetValue(w, out UiState st)) return;

            int btnY = w.Y + w.H - 46;
            if (my < btnY || my > btnY + 30) return;

            if (st.Success)
            {
                if (mx >= w.X + 20 && mx <= w.X + 170)
                {
                    // «باز کردن» — پنجره‌ی نصب را ببند و برنامه را اجرا کن
                    w.CloseAnimating = true; w.CloseAnimFrame = 0;
                    PapAppRuntime.Launch(st.Manifest.Id);
                    return;
                }
                if (mx >= w.X + 180 && mx <= w.X + 320)
                { w.CloseAnimating = true; w.CloseAnimFrame = 0; return; }
            }
            else
            {
                if (mx >= w.X + 20 && mx <= w.X + 320)
                { w.CloseAnimating = true; w.CloseAnimFrame = 0; return; }
            }
        }
    }
}
