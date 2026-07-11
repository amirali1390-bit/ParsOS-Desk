using System;
using System.Collections.Generic;
using Cosmos.System.Graphics.Fonts;
using ParsOS.Audio;

namespace ParsOS.GUI
{
    /// <summary>
    /// برنامه‌ی موزیک پلیر ParsOS.
    /// دقیقاً مثل NotepadApp / FileExplorerApp / WebBrowserApp به‌عنوان یک
    /// "پلاگین محتوا" برای GraphicsManager عمل می‌کند (نگاه کنید به
    /// راهنمای اتصال INTEGRATION_GUIDE.md برای خط‌هایی که باید در
    /// GraphicsManager.cs و Kernel.cs اضافه شوند).
    ///
    /// فعلاً منبع آهنگ‌ها Embedded Resource است (طبق درخواست). آهنگ‌ها با
    /// MusicPlayerApp.AddTrack(title, artist, bytes) در Kernel.BeforeRun
    /// ثبت می‌شوند. بعداً می‌توان LoadFromFolder(...) را برای خواندن از
    /// دیسک (VFS) کامل کرد — اسکلتش پایین همین فایل آماده است.
    /// </summary>
    public static class MusicPlayerApp
    {
        public const string ContentFlag = "MUSICPLAYER_APP";

        public class Track
        {
            public string Title;
            public string Artist;
            public byte[] Data;
        }

        public static readonly List<Track> Tracks = new List<Track>();

        private static int _currentIndex = -1;
        private static string _status = "";
        private static bool _repeat = false;
        private static bool _shuffle = false;
        private static readonly Random _rng = new Random();

        // ─── اسکرول لیست پخش ──────────────────────────────────────────
        private static int _scrollOffset = 0;
        private const int RowH = 30;

        // ─── ابعاد المان‌های تعاملی ────────────────────────────────────
        private const int ArtSize = 78;
        private const int CtrlBtnR = 20;
        private const int BarH = 10;

        public static bool IsPlayerWindowOpen(WindowInfo w) => w != null && w.Content == ContentFlag;

        // ═══════════════════════════════════════════════════════════
        //  مدیریت پلی‌لیست و پخش
        // ═══════════════════════════════════════════════════════════
        public static void AddTrack(string title, string artist, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            Tracks.Add(new Track { Title = title, Artist = artist, Data = data });
        }

        // TODO (فاز بعدی): خواندن آهنگ‌ها از یک فولدر روی دیسک به‌جای Embedded
        // Resource. اسکلت پیاده‌سازی (وقتی VFS/File آماده شد):
        //
        // public static void LoadFromFolder(string folderPath)
        // {
        //     if (!System.IO.Directory.Exists(folderPath)) return;
        //     foreach (var file in System.IO.Directory.GetFiles(folderPath, "*.mp3"))
        //     {
        //         var data = System.IO.File.ReadAllBytes(file);
        //         AddTrack(System.IO.Path.GetFileNameWithoutExtension(file), "", data);
        //     }
        // }

        public static void PlayTrack(int index)
        {
            if (index < 0 || index >= Tracks.Count) return;
            _currentIndex = index;
            var t = Tracks[index];
            bool ok = SoundDriver.PlayMp3(t.Data);
            _status = ok ? "" : ("خطا در پخش: " + SoundDriver.LastError);
            EnsureVisible(index);
        }

        public static void TogglePlayPause()
        {
            if (_currentIndex < 0)
            {
                if (Tracks.Count > 0) PlayTrack(0);
                return;
            }
            if (SoundDriver.IsPlaying) SoundDriver.Pause();
            else if (SoundDriver.IsPaused) SoundDriver.Resume();
            else PlayTrack(_currentIndex);
        }

        public static void Next()
        {
            if (Tracks.Count == 0) return;
            int idx = _shuffle ? _rng.Next(Tracks.Count) : (_currentIndex + 1) % Tracks.Count;
            PlayTrack(idx);
        }

        public static void Prev()
        {
            if (Tracks.Count == 0) return;
            int idx = (_currentIndex - 1 + Tracks.Count) % Tracks.Count;
            PlayTrack(idx);
        }

        private static void EnsureVisible(int index)
        {
            int visibleRows = VisibleRowsCache > 0 ? VisibleRowsCache : 4;
            if (index < _scrollOffset) _scrollOffset = index;
            else if (index >= _scrollOffset + visibleRows) _scrollOffset = index - visibleRows + 1;
            if (_scrollOffset < 0) _scrollOffset = 0;
        }
        private static int VisibleRowsCache = 4;

        // فراخوانی می‌شود از GraphicsManager.Tick() یک‌بار در هر فریم —
        // برای رفتن خودکار به آهنگ بعدی وقتی آهنگ فعلی تمام شد.
        public static bool Tick()
        {
            if (_currentIndex >= 0 && SoundDriver.IsPlaying && SoundDriver.HasFinished())
            {
                if (_repeat) PlayTrack(_currentIndex);
                else if (Tracks.Count > 0) Next();
                return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════
        //  Layout — یک‌جا محاسبه می‌شود تا رسم و کلیک هرگز از هم جدا نشوند
        // ═══════════════════════════════════════════════════════════
        private struct Box
        {
            public int X, Y, W, H;
            public Box(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
            public bool Contains(int mx, int my) => mx >= X && mx <= X + W && my >= Y && my <= Y + H;
            public int CenterX => X + W / 2;
            public int CenterY => Y + H / 2;
        }

        private struct Layout
        {
            public Box Art;
            public Box ProgressBar;
            public Box BtnPrev, BtnPlay, BtnNext;
            public Box BtnShuffle, BtnRepeat;
            public Box VolumeBar;
            public Box ListArea;
            public Box ListUp, ListDown;
            public int VisibleRows;
            public int RowsTop;
        }

        private static Layout ComputeLayout(WindowInfo w)
        {
            int th = WindowInfo.TitleH;
            int left = w.X + 18;
            int top = w.Y + th + 16;
            int right = w.X + w.W - 18;

            var L = new Layout();

            L.Art = new Box(left, top, ArtSize, ArtSize);

            int infoTop = top; // عنوان/آرتیست کنار آرت رسم می‌شود (فقط متن، جایی برای کلیک نیست)

            int progY = top + ArtSize + 14;
            L.ProgressBar = new Box(left, progY, right - left, BarH);

            int ctrlY = progY + BarH + 26;
            int centerX = left + (right - left) / 2;
            L.BtnPlay = new Box(centerX - CtrlBtnR, ctrlY, CtrlBtnR * 2, CtrlBtnR * 2);
            L.BtnPrev = new Box(L.BtnPlay.X - CtrlBtnR * 2 - 18, ctrlY + (CtrlBtnR - 16), 32, 32);
            L.BtnNext = new Box(L.BtnPlay.X + L.BtnPlay.W + 18, ctrlY + (CtrlBtnR - 16), 32, 32);

            L.BtnShuffle = new Box(left, ctrlY + 4, 28, 24);
            L.BtnRepeat = new Box(right - 28, ctrlY + 4, 28, 24);

            int volY = ctrlY + CtrlBtnR * 2 + 20;
            L.VolumeBar = new Box(left + 50, volY, (right - left) - 50, BarH);

            int listY = volY + BarH + 22;
            int listBottom = w.Y + w.H - 12;
            int listH = Math.Max(RowH, listBottom - listY - 26);
            L.ListArea = new Box(left, listY, right - left, listH);
            L.VisibleRows = Math.Max(1, listH / RowH);
            L.RowsTop = listY;

            L.ListUp = new Box(right - 60, listY - 22, 24, 18);
            L.ListDown = new Box(right - 30, listY - 22, 24, 18);

            VisibleRowsCache = L.VisibleRows;
            return L;
        }

        // ═══════════════════════════════════════════════════════════
        //  رسم — شکل‌ها (روی back-buffer، قبل از Flush؛ مثل Settings/WebBrowser)
        // ═══════════════════════════════════════════════════════════
        public static void DrawShapes(WindowInfo w, int mouseX, int mouseY)
        {
            var L = ComputeLayout(w);

            int colBorder = RenderSystem.ToInt(Theme.WindowBorder);
            int colAccent = RenderSystem.ToInt(Theme.Accent);
            int colAccentHover = RenderSystem.ToInt(Theme.AccentHover);
            int colPanel = RenderSystem.ToInt(Theme.TaskbarItem);
            int colPanelActive = RenderSystem.ToInt(Theme.TaskbarActive);
            int colWinBg = RenderSystem.ToInt(Theme.WindowBg);

            bool isPlaying = SoundDriver.IsPlaying;

            // ── آرت آلبوم (ظاهر "وینیل") ───────────────────────────
            RenderSystem.FillRoundRect(L.Art.X, L.Art.Y, L.Art.W, L.Art.H, 14, colPanel);
            int discR = ArtSize / 2 - 6;
            RenderSystem.FilledCircle(L.Art.CenterX, L.Art.CenterY, discR, isPlaying ? colAccent : colBorder);
            RenderSystem.FilledCircle(L.Art.CenterX, L.Art.CenterY, discR - 10, colWinBg);
            RenderSystem.FilledCircle(L.Art.CenterX, L.Art.CenterY, 4, isPlaying ? colAccent : colBorder);

            // ── نوار پیشرفت ──────────────────────────────────────────
            RenderSystem.FillRoundRect(L.ProgressBar.X, L.ProgressBar.Y, L.ProgressBar.W, L.ProgressBar.H, BarH / 2, colPanel);
            float progress = _currentIndex >= 0 ? SoundDriver.GetProgress() : 0f;
            int progW = (int)(L.ProgressBar.W * Clamp01(progress));
            if (progW > 2)
                RenderSystem.FillRoundRect(L.ProgressBar.X, L.ProgressBar.Y, progW, L.ProgressBar.H, BarH / 2, colAccent);
            int handleX = L.ProgressBar.X + progW;
            RenderSystem.FilledCircle(handleX, L.ProgressBar.CenterY, 6, colAccentHover);

            // ── دکمه‌های کنترل ───────────────────────────────────────
            bool hoverPrev = L.BtnPrev.Contains(mouseX, mouseY);
            bool hoverPlay = L.BtnPlay.Contains(mouseX, mouseY);
            bool hoverNext = L.BtnNext.Contains(mouseX, mouseY);

            RenderSystem.FilledCircle(L.BtnPrev.CenterX, L.BtnPrev.CenterY, 16, hoverPrev ? colPanelActive : colPanel);
            RenderSystem.FilledCircle(L.BtnPlay.CenterX, L.BtnPlay.CenterY, CtrlBtnR, hoverPlay ? colAccentHover : colAccent);
            RenderSystem.FilledCircle(L.BtnNext.CenterX, L.BtnNext.CenterY, 16, hoverNext ? colPanelActive : colPanel);

            // شافل / تکرار
            RenderSystem.FillRoundRect(L.BtnShuffle.X, L.BtnShuffle.Y, L.BtnShuffle.W, L.BtnShuffle.H, 6,
                _shuffle ? colAccent : colPanel);
            RenderSystem.FillRoundRect(L.BtnRepeat.X, L.BtnRepeat.Y, L.BtnRepeat.W, L.BtnRepeat.H, 6,
                _repeat ? colAccent : colPanel);

            // ── نوار صدا ──────────────────────────────────────────────
            RenderSystem.FillRoundRect(L.VolumeBar.X, L.VolumeBar.Y, L.VolumeBar.W, L.VolumeBar.H, BarH / 2, colPanel);
            float vol = SoundDriver.CurrentVolume;
            int volW = (int)(L.VolumeBar.W * Clamp01(vol));
            if (volW > 2)
                RenderSystem.FillRoundRect(L.VolumeBar.X, L.VolumeBar.Y, volW, L.VolumeBar.H, BarH / 2, colAccent);
            RenderSystem.FilledCircle(L.VolumeBar.X + volW, L.VolumeBar.CenterY, 6, colAccentHover);

            // ── پنل پلی‌لیست ──────────────────────────────────────────
            RenderSystem.FillRoundRect(L.ListArea.X, L.ListArea.Y, L.ListArea.W, L.ListArea.H, 10, colPanel);
            RenderSystem.FillRoundRect(L.ListUp.X, L.ListUp.Y, L.ListUp.W, L.ListUp.H, 4, colPanel);
            RenderSystem.FillRoundRect(L.ListDown.X, L.ListDown.Y, L.ListDown.W, L.ListDown.H, 4, colPanel);

            for (int row = 0; row < L.VisibleRows; row++)
            {
                int idx = _scrollOffset + row;
                if (idx >= Tracks.Count) break;
                int ry = L.ListArea.Y + row * RowH;
                if (idx == _currentIndex)
                    RenderSystem.FillRoundRect(L.ListArea.X + 4, ry + 2, L.ListArea.W - 8, RowH - 4, 6, colPanelActive);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  رسم — متن‌ها (روی Canvas، بعد از Flush، از طریق WCanvas)
        // ═══════════════════════════════════════════════════════════
        public static void DrawTexts(WindowInfo w, WindowCanvas wc, TtfFont font)
        {
            var L = ComputeLayout(w);

            Track cur = (_currentIndex >= 0 && _currentIndex < Tracks.Count) ? Tracks[_currentIndex] : null;
            int infoX = L.Art.X + L.Art.W + 16;
            int infoY = L.Art.Y + 8;

            wc.DrawTtf(font, cur != null ? cur.Title : "هیچ آهنگی انتخاب نشده", Pens.TextPrimary, infoX, infoY, Theme.WindowBg);
            if (cur != null && !string.IsNullOrEmpty(cur.Artist))
                wc.DrawTtf(font, cur.Artist, Pens.WindowBorder, infoX, infoY + 22, Theme.WindowBg);

            if (!string.IsNullOrEmpty(_status))
                wc.DrawTtf(font, _status, Pens.ShutdownRed, infoX, infoY + 44, Theme.WindowBg);

            // زمان‌ها
            string elapsed = FormatTime((float)SoundDriver.GetElapsedSeconds());
            string total = FormatTime((float)SoundDriver.GetDurationSeconds());
            wc.DrawTtf(font, elapsed, Pens.WindowBorder, L.ProgressBar.X, L.ProgressBar.Y + 14, Theme.WindowBg);
            wc.DrawTtf(font, total, Pens.WindowBorder, L.ProgressBar.X + L.ProgressBar.W - 40, L.ProgressBar.Y + 14, Theme.WindowBg);

            // دکمه‌های کنترل (گلیف‌های ساده‌ی ASCII — تضمین‌شده در هر فونتی نمایش داده می‌شوند)
            bool isPlaying = SoundDriver.IsPlaying;
            wc.DrawTtf(font, "<<", Pens.TaskbarText, L.BtnPrev.X + 6, L.BtnPrev.Y + 8, Theme.TaskbarItem);
            wc.DrawTtf(font, isPlaying ? "||" : ">", Pens.White, L.BtnPlay.CenterX - (isPlaying ? 8 : 5), L.BtnPlay.CenterY - 9, Theme.Accent);
            wc.DrawTtf(font, ">>", Pens.TaskbarText, L.BtnNext.X + 6, L.BtnNext.Y + 8, Theme.TaskbarItem);

            wc.DrawTtf(font, "S", _shuffle ? Pens.White : Pens.TaskbarText, L.BtnShuffle.X + 9, L.BtnShuffle.Y + 4, _shuffle ? Theme.Accent : Theme.TaskbarItem);
            wc.DrawTtf(font, "R", _repeat ? Pens.White : Pens.TaskbarText, L.BtnRepeat.X + 9, L.BtnRepeat.Y + 4, _repeat ? Theme.Accent : Theme.TaskbarItem);

            wc.DrawTtf(font, "حجم", Pens.WindowBorder, L.VolumeBar.X - 46, L.VolumeBar.Y - 2, Theme.WindowBg);
            wc.DrawTtf(font, ((int)(SoundDriver.CurrentVolume * 100)) + "%", Pens.WindowBorder, L.VolumeBar.X + L.VolumeBar.W + 8, L.VolumeBar.Y - 2, Theme.WindowBg);

            wc.DrawTtf(font, "^", Pens.TaskbarText, L.ListUp.X + 9, L.ListUp.Y + 1, Theme.TaskbarItem);
            wc.DrawTtf(font, "v", Pens.TaskbarText, L.ListDown.X + 9, L.ListDown.Y + 1, Theme.TaskbarItem);

            if (Tracks.Count == 0)
            {
                wc.DrawTtf(font, "هیچ آهنگی بارگذاری نشده است.", Pens.WindowBorder, L.ListArea.X + 12, L.ListArea.Y + 10, Theme.TaskbarItem);
            }
            else
            {
                for (int row = 0; row < L.VisibleRows; row++)
                {
                    int idx = _scrollOffset + row;
                    if (idx >= Tracks.Count) break;
                    var t = Tracks[idx];
                    int ry = L.ListArea.Y + row * RowH;
                    bool active = idx == _currentIndex;
                    var pen = active ? Pens.White : Pens.TaskbarText;
                    var bg = active ? Theme.TaskbarActive : Theme.TaskbarItem;
                    string label = (active && SoundDriver.IsPlaying ? "> " : "  ") + t.Title;
                    wc.DrawTtf(font, label, pen, L.ListArea.X + 12, ry + 7, bg);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  کلیک — از همان Layout استفاده می‌کند تا با رسم هماهنگ بماند
        // ═══════════════════════════════════════════════════════════
        public static bool HandleClick(WindowInfo w, int mx, int my)
        {
            var L = ComputeLayout(w);

            if (L.BtnPlay.Contains(mx, my)) { TogglePlayPause(); return true; }
            if (L.BtnPrev.Contains(mx, my)) { Prev(); return true; }
            if (L.BtnNext.Contains(mx, my)) { Next(); return true; }
            if (L.BtnShuffle.Contains(mx, my)) { _shuffle = !_shuffle; return true; }
            if (L.BtnRepeat.Contains(mx, my)) { _repeat = !_repeat; return true; }

            if (L.ProgressBar.Contains(mx, my))
            {
                float frac = (float)(mx - L.ProgressBar.X) / L.ProgressBar.W;
                SoundDriver.SeekToFraction(Clamp01(frac));
                return true;
            }

            if (L.VolumeBar.Contains(mx, my))
            {
                float frac = (float)(mx - L.VolumeBar.X) / L.VolumeBar.W;
                SoundDriver.SetVolume(Clamp01(frac));
                return true;
            }

            if (L.ListUp.Contains(mx, my)) { _scrollOffset = Math.Max(0, _scrollOffset - 1); return true; }
            if (L.ListDown.Contains(mx, my))
            {
                int maxOffset = Math.Max(0, Tracks.Count - L.VisibleRows);
                _scrollOffset = Math.Min(maxOffset, _scrollOffset + 1);
                return true;
            }

            if (L.ListArea.Contains(mx, my))
            {
                int row = (my - L.ListArea.Y) / RowH;
                int idx = _scrollOffset + row;
                if (idx >= 0 && idx < Tracks.Count) { PlayTrack(idx); return true; }
            }

            return false;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static string FormatTime(float seconds)
        {
            if (seconds < 0 || float.IsNaN(seconds)) seconds = 0;
            int s = (int)seconds;
            int m = s / 60;
            int sec = s % 60;
            return (m < 10 ? "0" : "") + m + ":" + (sec < 10 ? "0" : "") + sec;
        }
    }
}