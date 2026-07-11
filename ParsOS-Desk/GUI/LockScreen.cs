using Cosmos.System.Graphics;
using System;
using System.Drawing;
using ParsOS.GUI;

namespace ParsOS
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  LockScreen — ادغام‌شده با RenderSystem + LargeGlyphRenderer
    //
    //  ساعت: با TtfFont.DrawLarge (96px، بدون محدودیت GlyphAtlas CELL=24)
    //  پس‌زمینه: blur از _wpCache با ۴ پاس box-blur
    //  انیمیشن: slide-up ease-in-cubic
    // ═══════════════════════════════════════════════════════════════════════════
    public static class LockScreen
    {
        public static bool IsActive = true;

        private static bool _unlocking = false;
        private static int _unlockFrame = 0;
        private const int UnlockFrames = 35;

        private static bool _lastLeft = false;

        private static string _cachedTime = "00:00";
        private static int _lastMinute = -2;

        private static int[] _blurCache = null;
        private static bool _blurBuilt = false;

        private const int ClockFontSize = 110;

        private static readonly int _colDark = RenderSystem.ToInt(Color.FromArgb(15, 15, 28));
        private static readonly int _colWhite = RenderSystem.ToInt(Color.FromArgb(240, 242, 255));

        // ─── BuildBlurCache ───────────────────────────────────────────────────
        private static void BuildBlurCache(int W, int H)
        {
            if (_blurBuilt) return;
            _blurBuilt = true;

            int[] wp = RenderSystem.GetWpCacheSnapshot(W, H);
            if (wp == null)
            {
                _blurCache = new int[W * H];
                for (int i = 0; i < _blurCache.Length; i++) _blurCache[i] = _colDark;
                return;
            }

            var buf = new int[W * H];
            for (int i = 0; i < buf.Length; i++)
            {
                int p = wp[i];
                int r = (p >> 16) & 0xFF, g = (p >> 8) & 0xFF, b = p & 0xFF;
                // تیره‌تر و آبی‌تر از قبل برای جلوه بهتر
                r = r * 38 / 100;
                g = g * 40 / 100;
                b = Math.Min(b * 44 / 100 + 28, 255);
                buf[i] = Pack(r, g, b);
            }

            // ─── ۶ پاس sliding-window O(N) با radius=6 ───────────────────────
            // نتیجه بسیار نزدیک به Gaussian واقعی — نرم‌تر از box blur معمولی
            var tmp = new int[W * H];
            for (int pass = 0; pass < 6; pass++) BoxBlurPass(buf, tmp, W, H, 6);
            _blurCache = buf;
        }

        // ─── BoxBlurPass — sliding window O(N)، بدون inner loop ─────────────
        private static void BoxBlurPass(int[] pix, int[] tmp, int W, int H, int r)
        {
            float inv = 1f / (2 * r + 1);

            // پاس افقی: pix → tmp
            for (int y = 0; y < H; y++)
            {
                int rb = y * W;
                int rr = 0, g = 0, b = 0;
                // مقداردهی اولیه پنجره
                for (int d = -r; d <= r; d++)
                {
                    int s = pix[rb + (d < 0 ? 0 : d >= W ? W - 1 : d)];
                    rr += (s >> 16) & 0xFF; g += (s >> 8) & 0xFF; b += s & 0xFF;
                }
                for (int x = 0; x < W; x++)
                {
                    tmp[rb + x] = Pack((int)(rr * inv), (int)(g * inv), (int)(b * inv));
                    int o = pix[rb + (x - r < 0 ? 0 : x - r)];
                    int n = pix[rb + (x + r + 1 >= W ? W - 1 : x + r + 1)];
                    rr += ((n >> 16) & 0xFF) - ((o >> 16) & 0xFF);
                    g += ((n >> 8) & 0xFF) - ((o >> 8) & 0xFF);
                    b += (n & 0xFF) - (o & 0xFF);
                }
            }

            // پاس عمودی: tmp → pix
            for (int x = 0; x < W; x++)
            {
                int rr = 0, g = 0, b = 0;
                for (int d = -r; d <= r; d++)
                {
                    int s = tmp[(d < 0 ? 0 : d >= H ? H - 1 : d) * W + x];
                    rr += (s >> 16) & 0xFF; g += (s >> 8) & 0xFF; b += s & 0xFF;
                }
                for (int y = 0; y < H; y++)
                {
                    pix[y * W + x] = Pack((int)(rr * inv), (int)(g * inv), (int)(b * inv));
                    int o = tmp[(y - r < 0 ? 0 : y - r) * W + x];
                    int n = tmp[(y + r + 1 >= H ? H - 1 : y + r + 1) * W + x];
                    rr += ((n >> 16) & 0xFF) - ((o >> 16) & 0xFF);
                    g += ((n >> 8) & 0xFF) - ((o >> 8) & 0xFF);
                    b += (n & 0xFF) - (o & 0xFF);
                }
            }
        }

        private static int Pack(int r, int g, int b)
            => unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
        private static int Clamp(int v, int lo, int hi)
            => v < lo ? lo : v > hi ? hi : v;

        public static void InvalidateBlur() { _blurBuilt = false; _blurCache = null; }

        // ─── Tick ─────────────────────────────────────────────────────────────
        public static void Tick(Canvas canvas, int W, int H)
        {
            if (!_blurBuilt) BuildBlurCache(W, H);

            bool curLeft = Cosmos.System.MouseManager.MouseState == Cosmos.System.MouseState.Left;
            bool clicked = curLeft && !_lastLeft;
            _lastLeft = curLeft;

            if (clicked && !_unlocking) { _unlocking = true; _unlockFrame = 0; }
            if (_unlocking)
            {
                _unlockFrame++;
                if (_unlockFrame >= UnlockFrames)
                { IsActive = false; _unlocking = false; RenderSystem.ForceFullRedraw(); return; }
            }

            Render(canvas, W, H);
        }

        // ─── Render ───────────────────────────────────────────────────────────
        private static void Render(Canvas canvas, int W, int H)
        {
            // ۱. پس‌زمینه blur
            if (_blurCache != null)
                RenderSystem.Blit(_blurCache, W, H, 0, 0);
            else
                RenderSystem.Fill(0, 0, W, H, _colDark);

            // ۲. انیمیشن slide-up
            int slideY = 0;
            if (_unlocking)
            {
                float t = (float)_unlockFrame / UnlockFrames;
                float ease = t * t * t;
                slideY = (int)(ease * H);
                if (slideY > 0)
                    RenderSystem.AlphaFill(0, H - slideY, W, slideY, 15, 15, 28, 220);
            }

            // ۳. ساعت بزرگ با LargeGlyphRenderer — مستقیم در back-buffer
            UpdateTime();
            var font = Kernel.VazirFont;
            if (font != null)
            {
                int tw = font.MeasureLarge(ClockFontSize, _cachedTime);
                int th = ClockFontSize;
                int tx = W / 2 - tw / 2;
                int ty = H / 2 - th / 2 - slideY / 3;

                // متن بدون سایه — فقط رنگ سفید
                font.DrawLarge(ClockFontSize, _cachedTime, _colWhite, tx, ty);
            }

            // ۴. کرسر — باید قبل از Flush رسم شود تا در back-buffer ظاهر شود
            int mx = (int)Cosmos.System.MouseManager.X;
            int my = (int)Cosmos.System.MouseManager.Y;
            if (Kernel.CursorBitmap?.rawData != null)
                RenderSystem.DrawCursor(Kernel.CursorBitmap.rawData,
                    (int)Kernel.CursorBitmap.Width, (int)Kernel.CursorBitmap.Height, mx, my);
            else
                RenderSystem.DrawCursor(null, 0, 0, mx, my);

            // ۵. Flush → یک DrawImage (کرسر از قبل در back-buffer است)
            RenderSystem.Flush(canvas);

            canvas.Display();
        }

        private static void UpdateTime()
        {
            var now = DateTime.Now;
            if (now.Minute == _lastMinute) return;
            _lastMinute = now.Minute;
            int h = now.Hour, m = now.Minute;
            _cachedTime = (h < 10 ? "0" : "") + h + ":" + (m < 10 ? "0" : "") + m;
        }
    }
}