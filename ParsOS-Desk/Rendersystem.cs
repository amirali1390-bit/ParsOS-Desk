// ═══════════════════════════════════════════════════════════════════════════
//  RenderSystem.cs  —  سیستم رندر بهینه برای ParsOS
//
//  معماری کلی:
//    ┌─────────────────────────────────────────────────────────┐
//    │  Back-Buffer (int[])  ←  تمام رسم‌ها اینجا انجام می‌شود │
//    │  Dirty-Rect tracker   ←  فقط ناحیه تغییر‌یافته flush    │
//    │  Layer compositor     ←  دسکتاپ / پنجره / UI / کرسر    │
//    │  DrawArray flush      ←  یک Canvas.DrawArray برای flush  │
//    └─────────────────────────────────────────────────────────┘
//
//  مزایا نسبت به کد قبلی:
//    • صفر Pen allocation در حلقه رندر
//    • back-buffer یکپارچه → هیچ DrawPoint تکی در hot-path نیست
//    • dirty-rect: فقط ناحیه‌ای که واقعاً تغییر کرده flush می‌شود
//    • کرسر با cursor-save/restore: فقط ۱۶×۱۶ پیکسل redo می‌شود
//    • wallpaper یک‌بار در RawData ذخیره می‌شود (no DrawImage loop)
//    • BlurBmp در LockScreen با همین ابزار بهینه‌تر می‌شود
//
//  نحوه ادغام:
//    1. این فایل را کنار GraphicsManager.cs قرار دهید
//    2. در GraphicsManager.Initialize()، پس از ساخت Canvas:
//         RenderSystem.Init(Canvas, Width, Height);
//    3. در GraphicsManager.Render()، به جای Canvas.DrawImage و Canvas.DrawPoint،
//       از RenderSystem.Fill / Blit / BlitAlpha / DrawRect استفاده کنید
//    4. در انتهای Render(): RenderSystem.Flush(Canvas);
//    5. Canvas.Display() همچنان لازم است (توسط Flush صدا زده می‌شود)
//
// ═══════════════════════════════════════════════════════════════════════════

using Cosmos.System.Graphics;
using System;
using System.Drawing;

namespace ParsOS.GUI
{
    // =========================================================================
    //  DirtyRect — ردیاب ناحیه تغییریافته
    // =========================================================================
    public struct DirtyRect
    {
        public int X1, Y1, X2, Y2;
        public bool IsEmpty;

        public static DirtyRect Empty => new DirtyRect { IsEmpty = true };

        public void Reset() { IsEmpty = true; X1 = Y1 = int.MaxValue; X2 = Y2 = int.MinValue; }

        public void Add(int x, int y, int w, int h)
        {
            int x2 = x + w, y2 = y + h;
            if (IsEmpty) { X1 = x; Y1 = y; X2 = x2; Y2 = y2; IsEmpty = false; return; }
            if (x < X1) X1 = x;
            if (y < Y1) Y1 = y;
            if (x2 > X2) X2 = x2;
            if (y2 > Y2) Y2 = y2;
        }

        public void AddPoint(int x, int y) => Add(x, y, 1, 1);

        public void ClampTo(int W, int H)
        {
            if (X1 < 0) X1 = 0;
            if (Y1 < 0) Y1 = 0;
            if (X2 > W) X2 = W;
            if (Y2 > H) Y2 = H;
        }

        public bool Contains(int x, int y) => !IsEmpty && x >= X1 && x < X2 && y >= Y1 && y < Y2;
    }

    // =========================================================================
    //  RenderSystem — موتور اصلی رندر با back-buffer یکپارچه
    // =========================================================================
    public static class RenderSystem
    {
        // ─── Back-buffer: آرایه int[] به جای Bitmap ─────────────────────────
        // هر عنصر = ARGB packed integer → DrawArray مستقیم روی Canvas
        // یک‌بار الوکیت می‌شود و هرگز رشد نمی‌کند
        private static int[] _buf;
        private static int _W, _H;

        public static int ScreenW => _W;
        public static int ScreenH => _H;

        // ─── Wallpaper cache: از RawData استفاده می‌کنیم ────────────────────
        // اگر wallpaper موجود باشد آن را یک‌بار در _wpCache کپی می‌کنیم
        // و در ابتدای هر فریم با Array.Copy پشت‌زمینه را بازیابی می‌کنیم
        private static int[] _wpCache;      // W×H ints (اندازه صفحه)
        private static bool _wpCacheReady;

        // ─── Dirty tracking ─────────────────────────────────────────────────
        private static DirtyRect _dirty;
        private static bool _fullRedraw;    // اولین فریم یا تغییر بزرگ

        // ─── Cursor save/restore ────────────────────────────────────────────
        // ذخیره پیکسل‌های زیر کرسر (16×16 ناحیه) قبل از رسم کرسر
        private const int CurW = 20, CurH = 20;
        private static int[] _cursorSave = new int[CurW * CurH];
        private static int _lastCurX = -1, _lastCurY = -1;
        private static bool _cursorDrawn;

        // ─── Screen Bitmap: یک‌بار ساخته می‌شود، rawData = _buf ────────────────
        // روش AuraOS: کل back-buffer را در یک Bitmap نگه می‌داریم
        // و با DrawImage کل صفحه را یک‌جا flush می‌کنیم
        private static Bitmap _screenBitmap;

        // ─── Packed color helpers ────────────────────────────────────────────
        private static int Pack(byte r, byte g, byte b) =>
            unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b));

        private static int PackA(byte a, byte r, byte g, byte b) =>
            unchecked((int)(((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b));

        // ─── از Color به packed int ──────────────────────────────────────────
        public static int ToInt(Color c) => Pack(c.R, c.G, c.B);
        public static int ToIntA(Color c) => PackA(c.A, c.R, c.G, c.B);



        // =========================================================================
        //  Init — یک‌بار در GraphicsManager.Initialize() صدا زده می‌شود
        // =========================================================================
        public static void Init(Canvas canvas, int w, int h)
        {
            _W = w; _H = h;
            // یک Bitmap با اندازه کل صفحه — یک‌بار ساخته می‌شود (روش AuraOS)
            _screenBitmap = new Bitmap((uint)w, (uint)h, ColorDepth.ColorDepth32);

            // ─── بهینه‌سازی: حذف کپی کامل صفحه در هر Flush ──────────────────
            // قبلاً _buf یک آرایه‌ی جدا بود و Flush() هر فریم کل آن را با
            // Array.Copy داخل rawData بیت‌مپ می‌ریخت (یک memcpy کامل به
            // اندازه‌ی صفحه، مثلاً ~3 مگابایت در 1024×768، هر فریم). چون
            // rawData یک آرایه‌ی معمولی و در دسترس است، مستقیماً همان را به
            // عنوان back-buffer خودمان استفاده می‌کنیم — هر رسمی که
            // RenderSystem انجام می‌دهد بلافاصله «داخل» بیت‌مپ هم هست، بدون
            // نیاز به کپی جدا. نیمی از ترافیک حافظه‌ی هر فریم حذف می‌شود.
            _buf = _screenBitmap.rawData;

            _dirty.Reset();
            _fullRedraw = true;
            _wpCacheReady = false;
        }

        // =========================================================================
        //  RebuildWallpaperCache — overload برای PNG (پیکسل‌های خام int[])
        //  مستقیم از PngImage.Pixels صدا زده می‌شود — بدون dependency به PngDecoder
        // =========================================================================
        public static void RebuildWallpaperCache(int[] pixels, int srcWidth, int srcHeight, int W, int H)
        {
            if (_wpCache == null || _wpCache.Length != W * H)
                _wpCache = new int[W * H];

            if (pixels == null || srcWidth <= 0 || srcHeight <= 0)
            {
                _wpCacheReady = false;
                return;
            }

            // ─── پردازش تدریجی به‌صورت نوارهای افقی ─────────────────────────
            // به جای کل تصویر یک‌دفعه، هر بار یک ردیف از مقصد محاسبه می‌شود
            // تا فشار cache CPU کمتر شود و RAM peak پایین بماند
            int sw = srcWidth;
            int sh = srcHeight;

            int xRatio = ((sw - 1) << 16) / Math.Max(W - 1, 1);
            int yRatio = ((sh - 1) << 16) / Math.Max(H - 1, 1);

            for (int y = 0; y < H; y++)
            {
                int sy = (y * yRatio) >> 16;
                int rowBase = y * W;
                int srcRow = sy * sw;
                int xr = 0;
                for (int x = 0; x < W; x++)
                {
                    // PNG پیکسل‌ها ARGB هستند؛ آلفا را با رنگ پس‌زمینه تیره ترکیب می‌کنیم
                    int pixel = pixels[srcRow + (xr >> 16)];
                    int a = (pixel >> 24) & 0xFF;
                    if (a >= 250)   // تقریباً کاملاً کدر — کپی مستقیم
                    {
                        _wpCache[rowBase + x] = pixel | unchecked((int)0xFF000000u);
                    }
                    else if (a <= 5)  // تقریباً کاملاً شفاف — پس‌زمینه تیره
                    {
                        _wpCache[rowBase + x] = Pack(20, 20, 35);
                    }
                    else
                    {
                        // آلفا ترکیب با پس‌زمینه تیره (20,20,35)
                        int ia = 255 - a;
                        int r = (((pixel >> 16) & 0xFF) * a + 20 * ia) >> 8;
                        int g = (((pixel >> 8) & 0xFF) * a + 20 * ia) >> 8;
                        int b = ((pixel & 0xFF) * a + 35 * ia) >> 8;
                        _wpCache[rowBase + x] = Pack((byte)r, (byte)g, (byte)b);
                    }
                    xr += xRatio;
                }
            }

            _wpCacheReady = true;
            _fullRedraw = true;
        }

        // =========================================================================
        //  RebuildWallpaperCache — overload اصلی برای BMP (fallback)
        //  wallpaper.RawData مستقیماً scale می‌شود به W×H
        // =========================================================================
        public static void RebuildWallpaperCache(Bitmap wallpaper, int W, int H)
        {
            if (_wpCache == null || _wpCache.Length != W * H)
                _wpCache = new int[W * H];

            if (wallpaper == null || wallpaper.rawData == null)
            {
                _wpCacheReady = false;
                return;
            }

            // nearest-neighbor scale از اندازه wallpaper به W×H
            int sw = (int)wallpaper.Width;
            int sh = (int)wallpaper.Height;
            var raw = wallpaper.rawData;

            int xRatio = ((sw - 1) << 16) / Math.Max(W - 1, 1);
            int yRatio = ((sh - 1) << 16) / Math.Max(H - 1, 1);

            for (int y = 0; y < H; y++)
            {
                int sy = (y * yRatio) >> 16;
                int rowBase = y * W;
                int srcRow = sy * sw;
                int xr = 0;
                for (int x = 0; x < W; x++)
                {
                    _wpCache[rowBase + x] = raw[(xr >> 16) + srcRow];
                    xr += xRatio;
                }
            }

            _wpCacheReady = true;
            _fullRedraw = true;
        }

        // =========================================================================
        //  InstallWallpaperCache — نصب بافر آماده‌شده توسط WallpaperLoader
        //  بدون realloc؛ فقط مرجع جابجا می‌شود
        // =========================================================================
        public static void InstallWallpaperCache(int[] readyBuf, int W, int H)
        {
            if (readyBuf == null || readyBuf.Length != W * H) return;
            _wpCache = readyBuf;
            _wpCacheReady = true;
            _fullRedraw = true;
        }

        // =========================================================================
        //  FillDirect — Fill مستقیم به back-buffer بدون dirty tracking
        //  فقط برای WallpaperLoader progress bar استفاده می‌شود
        // =========================================================================
        public static void FillDirect(int x, int y, int w, int h, int color)
        {
            if (w <= 0 || h <= 0 || _buf == null) return;
            int x2 = Math.Min(x + w, _W);
            int y2 = Math.Min(y + h, _H);
            x = Math.Max(x, 0);
            y = Math.Max(y, 0);
            if (x >= x2 || y >= y2) return;
            int rw = x2 - x;
            for (int row = y; row < y2; row++)
            {
                int idx = row * _W + x;
                for (int c = 0; c < rw; c++)
                    _buf[idx + c] = color;
            }
        }

        // =========================================================================
        //  DrawRectDirect — حاشیه مستطیل مستقیم (برای progress bar)
        // =========================================================================
        public static void DrawRectDirect(int x, int y, int w, int h, int color)
        {
            // top
            int x2 = Math.Min(x + w, _W);
            int yy = y;
            if (yy >= 0 && yy < _H)
                for (int i = Math.Max(x, 0); i < x2; i++) _buf[yy * _W + i] = color;
            // bottom
            yy = y + h - 1;
            if (yy >= 0 && yy < _H)
                for (int i = Math.Max(x, 0); i < x2; i++) _buf[yy * _W + i] = color;
            // left
            int y2 = Math.Min(y + h, _H);
            int xx = x;
            if (xx >= 0 && xx < _W)
                for (int i = Math.Max(y, 0); i < y2; i++) _buf[i * _W + xx] = color;
            // right
            xx = x + w - 1;
            if (xx >= 0 && xx < _W)
                for (int i = Math.Max(y, 0); i < y2; i++) _buf[i * _W + xx] = color;
        }

        // =========================================================================
        //  BeginFrame — ابتدای هر فریم فراخوانی شود
        //  پس‌زمینه را بازیابی می‌کند (wallpaper یا رنگ جامد)
        //  فقط ناحیه dirty را بازیابی می‌کند، نه کل صفحه
        // =========================================================================
        public static void BeginFrame(Color desktopFallback)
        {
            // چون Flush همیشه کل صفحه را با DrawImage ارسال می‌کند،
            // باید هر فریم کل back-buffer پاک شود — نه فقط dirty rect
            // وگرنه فریم‌های قبلی (پنجره جابجا شده، استارت منو بسته) می‌مانند
            if (_wpCacheReady)
                Array.Copy(_wpCache, _buf, _buf.Length);
            else
                FillSolid(0, 0, _W, _H, ToInt(desktopFallback));

            _dirty.Reset();
            _fullRedraw = false;
        }

        // =========================================================================
        //  ForceFullRedraw — وقتی تم عوض می‌شود / resize / انیمیشن بزرگ
        // =========================================================================
        public static void ForceFullRedraw() => _fullRedraw = true;

        // =========================================================================
        //  MarkDirty — ناحیه‌ای را dirty اعلام کنید
        // =========================================================================
        public static void MarkDirty(int x, int y, int w, int h) => _dirty.Add(x, y, w, h);

        // =========================================================================
        //  Fill — پر کردن مستطیل با رنگ packed
        // =========================================================================
        public static void Fill(int x, int y, int w, int h, int color)
        {
            FillSolid(x, y, w, h, color);
            _dirty.Add(x, y, w, h);
        }

        public static void Fill(int x, int y, int w, int h, Color color)
            => Fill(x, y, w, h, ToInt(color));

        // ─── FillSolid بدون dirty (برای BeginFrame) ─────────────────────────
        private static void FillSolid(int x, int y, int w, int h, int color)
        {
            if (w <= 0 || h <= 0) return;
            int x2 = Math.Min(x + w, _W);
            int y2 = Math.Min(y + h, _H);
            x = Math.Max(x, 0); y = Math.Max(y, 0);
            if (x >= x2 || y >= y2) return;
            int rw = x2 - x;
            for (int row = y; row < y2; row++)
            {
                int idx = row * _W + x;
                for (int c = 0; c < rw; c++)
                    _buf[idx + c] = color;
            }
        }

        // ─── BlitRegionFrom: کپی از آرایه منبع به _buf ─────────────────────
        private static void BlitRegionFrom(int[] src, int srcW, int dx, int dy, int w, int h)
        {
            if (w <= 0 || h <= 0) return;
            int x2 = Math.Min(dx + w, _W);
            int y2 = Math.Min(dy + h, _H);
            dx = Math.Max(dx, 0); dy = Math.Max(dy, 0);
            if (dx >= x2 || dy >= y2) return;
            int rw = x2 - dx;
            for (int row = dy; row < y2; row++)
                Array.Copy(src, row * srcW + dx, _buf, row * _W + dx, rw);
        }

        // =========================================================================
        //  Blit — کپی یک Bitmap.RawData (int[]) به back-buffer  ← بدون آلفا
        // =========================================================================
        public static void Blit(int[] src, int srcW, int srcH, int dx, int dy)
        {
            if (src == null) return;
            int maxX = Math.Min(dx + srcW, _W);
            int maxY = Math.Min(dy + srcH, _H);
            int startX = Math.Max(dx, 0);
            int startY = Math.Max(dy, 0);
            if (startX >= maxX || startY >= maxY) return;

            int copyW = maxX - startX;
            int srcOffX = startX - dx;

            for (int y = startY; y < maxY; y++)
            {
                int srcRow = (y - dy) * srcW + srcOffX;
                int dstRow = y * _W + startX;
                Array.Copy(src, srcRow, _buf, dstRow, copyW);
            }
            _dirty.Add(startX, startY, copyW, maxY - startY);
        }

        // =========================================================================
        //  BlitAlpha — کپی با آلفا-بلند (برای cursor، آیکون)
        //  فرمت src: ARGB packed int
        // =========================================================================
        public static void BlitAlpha(int[] src, int srcW, int srcH, int dx, int dy)
        {
            if (src == null) return;
            int maxX = Math.Min(dx + srcW, _W);
            int maxY = Math.Min(dy + srcH, _H);
            int startX = Math.Max(dx, 0);
            int startY = Math.Max(dy, 0);
            if (startX >= maxX || startY >= maxY) return;

            int srcOffX = startX - dx;

            for (int y = startY; y < maxY; y++)
            {
                int srcRow = (y - dy) * srcW + srcOffX;
                int dstRow = y * _W + startX;
                for (int x = startX; x < maxX; x++)
                {
                    int sp = src[srcRow + (x - startX)];
                    int a = (sp >> 24) & 0xFF;
                    if (a == 0) continue;
                    if (a == 255) { _buf[dstRow + (x - startX)] = sp | unchecked((int)0xFF000000); continue; }
                    int dp = _buf[dstRow + (x - startX)];
                    int ia = 255 - a;
                    int sr = (sp >> 16) & 0xFF, sg = (sp >> 8) & 0xFF, sb = sp & 0xFF;
                    int dr = (dp >> 16) & 0xFF, dg = (dp >> 8) & 0xFF, db = dp & 0xFF;
                    _buf[dstRow + (x - startX)] = Pack(
                        (byte)((sr * a + dr * ia) >> 8),
                        (byte)((sg * a + dg * ia) >> 8),
                        (byte)((sb * a + db * ia) >> 8));
                }
            }
            _dirty.Add(startX, startY, maxX - startX, maxY - startY);
        }

        // =========================================================================
        //  SetPixel — یک پیکسل بدون آلفا
        // =========================================================================
        public static void SetPixel(int x, int y, int color)
        {
            if ((uint)x >= (uint)_W || (uint)y >= (uint)_H) return;
            _buf[y * _W + x] = color;
            _dirty.AddPoint(x, y);
        }

        public static void SetPixel(int x, int y, Color c) => SetPixel(x, y, ToInt(c));

        // =========================================================================
        //  BlendPixelAlpha — یک پیکسل با آلفا-بلند واقعی (۰..۲۵۵ سطح خاکستری)
        //  روی back-buffer. برای گلیف‌های بزرگ (LargeGlyphAtlas) لازم بود چون
        //  BlitSlot قبلاً فقط SetPixel باینری (روشن/خاموش) می‌زد — یعنی فونت‌های
        //  ری‌سایزشده (مثلاً ساعت روی لاک‌اسکرین) هیچ آنتی‌الیاسینگی نداشتند و
        //  کاملاً پله‌ای دیده می‌شدند، برخلاف مسیر اصلی TtfFont که از نسخه‌ی ۷
        //  به بعد coverage خاکستری واقعی دارد. همان الگوریتم ترکیب رنگ
        //  BlitAlpha اینجا هم برای تک‌پیکسل تکرار شده تا هزینه‌ی allocate یک
        //  آرایه‌ی موقت int[] برای هر گلیف نداشته باشیم.
        // =========================================================================
        public static void BlendPixelAlpha(int x, int y, int rgb, int alpha)
        {
            if ((uint)x >= (uint)_W || (uint)y >= (uint)_H) return;
            if (alpha <= 0) return;
            int idx = y * _W + x;
            if (alpha >= 255) { _buf[idx] = rgb | unchecked((int)0xFF000000); _dirty.AddPoint(x, y); return; }
            int dp = _buf[idx];
            int ia = 255 - alpha;
            int sr = (rgb >> 16) & 0xFF, sg = (rgb >> 8) & 0xFF, sb = rgb & 0xFF;
            int dr = (dp >> 16) & 0xFF, dg = (dp >> 8) & 0xFF, db = dp & 0xFF;
            _buf[idx] = Pack(
                (byte)((sr * alpha + dr * ia) >> 8),
                (byte)((sg * alpha + dg * ia) >> 8),
                (byte)((sb * alpha + db * ia) >> 8));
            _dirty.AddPoint(x, y);
        }

        // =========================================================================
        //  HLine / VLine — خط افقی/عمودی بهینه
        // =========================================================================
        public static void HLine(int x, int y, int len, int color)
        {
            if (y < 0 || y >= _H) return;
            int x2 = Math.Min(x + len, _W);
            x = Math.Max(x, 0);
            if (x >= x2) return;
            int idx = y * _W + x;
            for (int i = x; i < x2; i++) _buf[idx++] = color;
            _dirty.Add(x, y, x2 - x, 1);
        }

        public static void VLine(int x, int y, int len, int color)
        {
            if (x < 0 || x >= _W) return;
            int y2 = Math.Min(y + len, _H);
            y = Math.Max(y, 0);
            for (int i = y; i < y2; i++) _buf[i * _W + x] = color;
            _dirty.Add(x, y, 1, y2 - y);
        }

        // =========================================================================
        //  DrawRect — حاشیه مستطیل
        // =========================================================================
        public static void DrawRect(int x, int y, int w, int h, int color)
        {
            HLine(x, y, w, color);
            HLine(x, y + h - 1, w, color);
            VLine(x, y, h, color);
            VLine(x + w - 1, y, h, color);
        }

        public static void DrawRect(int x, int y, int w, int h, Color c) => DrawRect(x, y, w, h, ToInt(c));

        // =========================================================================
        //  FillRoundRect — مستطیل پر با گوشه‌های گرد (بهینه‌شده)
        //  بدون هیچ DrawPoint تکی در hot-path
        // =========================================================================
        public static void FillRoundRect(int x, int y, int w, int h, int r, int color)
        {
            if (r <= 0) { Fill(x, y, w, h, color); return; }
            r = Math.Min(r, Math.Min(w / 2, h / 2));

            // بدنه افقی مرکزی
            for (int row = y + r; row < y + h - r; row++)
                HLine(x, row, w, color);

            // بالا و پایین (بدون گوشه)
            for (int row = y; row < y + r; row++)
                HLine(x + r, row, w - 2 * r, color);
            for (int row = y + h - r; row < y + h; row++)
                HLine(x + r, row, w - 2 * r, color);

            // گوشه‌ها با Bresenham دایره
            FillCornerQ(x + r, y + r, r, -1, -1, color);
            FillCornerQ(x + w - r - 1, y + r, r, 1, -1, color);
            FillCornerQ(x + r, y + h - r - 1, r, -1, 1, color);
            FillCornerQ(x + w - r - 1, y + h - r - 1, r, 1, 1, color);

            _dirty.Add(x, y, w, h);
        }

        public static void FillRoundRect(int x, int y, int w, int h, int r, Color color)
            => FillRoundRect(x, y, w, h, r, ToInt(color));

        // ─── ربع دایره پر — گوشه ──────────────────────────────────────────
        private static void FillCornerQ(int cx, int cy, int r, int sx, int sy, int color)
        {
            int r2 = r * r;
            for (int dy = 0; dy < r; dy++)
            {
                int rowY = cy + sy * dy;
                if (rowY < 0 || rowY >= _H) continue;
                int len = 0;
                for (int dx = 0; dx < r; dx++)
                    if (dx * dx + dy * dy <= r2) len = dx + 1;
                if (len == 0) continue;
                int startX = sx < 0 ? cx - len + 1 : cx;
                HLine(startX, rowY, len, color);
            }
        }

        // =========================================================================
        //  FillRoundRectTop — فقط گوشه‌های بالایی گرد (برای titlebar)
        // =========================================================================
        public static void FillRoundRectTop(int x, int y, int w, int h, int r, int color)
        {
            if (r <= 0) { Fill(x, y, w, h, color); return; }
            r = Math.Min(r, Math.Min(w / 2, h / 2));

            // بدنه اصلی
            for (int row = y + r; row < y + h; row++)
                HLine(x, row, w, color);

            // نوار بالای بدون گوشه
            for (int row = y; row < y + r; row++)
                HLine(x + r, row, w - 2 * r, color);

            // گوشه‌های بالا
            FillCornerQ(x + r, y + r, r, -1, -1, color);
            FillCornerQ(x + w - r - 1, y + r, r, 1, -1, color);

            _dirty.Add(x, y, w, h);
        }

        public static void FillRoundRectTop(int x, int y, int w, int h, int r, Color color)
            => FillRoundRectTop(x, y, w, h, r, ToInt(color));

        // =========================================================================
        //  DrawRoundRect — حاشیه مستطیل با گوشه‌های گرد
        // =========================================================================
        public static void DrawRoundRect(int x, int y, int w, int h, int r, int color)
        {
            if (r <= 0) { DrawRect(x, y, w, h, color); return; }
            HLine(x + r, y, w - 2 * r, color);
            HLine(x + r, y + h - 1, w - 2 * r, color);
            VLine(x, y + r, h - 2 * r, color);
            VLine(x + w - 1, y + r, h - 2 * r, color);
            DrawArcQ(x + r, y + r, r, -1, -1, color);
            DrawArcQ(x + w - r - 1, y + r, r, 1, -1, color);
            DrawArcQ(x + r, y + h - r - 1, r, -1, 1, color);
            DrawArcQ(x + w - r - 1, y + h - r - 1, r, 1, 1, color);
            _dirty.Add(x, y, w, h);
        }

        public static void DrawRoundRect(int x, int y, int w, int h, int r, Color color)
            => DrawRoundRect(x, y, w, h, r, ToInt(color));

        public static int[] GetWpCacheSnapshot(int W, int H)
        {
            if (!_wpCacheReady || _wpCache == null || _wpCache.Length != W * H)
                return null;

            var snap = new int[_wpCache.Length];
            Array.Copy(_wpCache, snap, snap.Length);
            return snap;
        }


        private static void DrawArcQ(int cx, int cy, int r, int sx, int sy, int color)
        {
            int xi = r, yi = 0, err = 0;
            while (xi >= yi)
            {
                PlotArcPt(cx, cy, xi, yi, sx, sy, color);
                PlotArcPt(cx, cy, yi, xi, sx, sy, color);
                yi++;
                if (err <= 0) err += 2 * yi + 1;
                else { xi--; err += 2 * (yi - xi) + 1; }
            }
        }

        private static void PlotArcPt(int cx, int cy, int dx, int dy, int sx, int sy, int color)
        {
            int px = cx + sx * dx, py = cy + sy * dy;
            if ((uint)px < (uint)_W && (uint)py < (uint)_H)
                _buf[py * _W + px] = color;
        }

        // =========================================================================
        //  FilledCircle — دایره پر بهینه
        // =========================================================================
        public static void FilledCircle(int cx, int cy, int r, int color)
        {
            int x = r, y = 0;
            int xch = 1 - r * 2, ych = 0, re = 0;
            while (x >= y)
            {
                HLine(cx - x, cy + y, 2 * x + 1, color);
                HLine(cx - x, cy - y, 2 * x + 1, color);
                HLine(cx - y, cy + x, 2 * y + 1, color);
                HLine(cx - y, cy - x, 2 * y + 1, color);
                y++;
                re += ych; ych += 2;
                if ((re * 2 + xch) > 0) { x--; re += xch; xch += 2; }
            }
            _dirty.Add(cx - r, cy - r, 2 * r + 1, 2 * r + 1);
        }

        public static void FilledCircle(int cx, int cy, int r, Color color)
            => FilledCircle(cx, cy, r, ToInt(color));

        // =========================================================================
        //  Line — خط با Bresenham (بهینه)
        // =========================================================================
        public static void Line(int x1, int y1, int x2, int y2, int color)
        {
            // clamp ساده
            x1 = Math.Max(0, Math.Min(_W - 1, x1));
            x2 = Math.Max(0, Math.Min(_W - 1, x2));
            y1 = Math.Max(0, Math.Min(_H - 1, y1));
            y2 = Math.Max(0, Math.Min(_H - 1, y2));

            int dx = Math.Abs(x2 - x1), dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1, sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                if ((uint)x1 < (uint)_W && (uint)y1 < (uint)_H)
                    _buf[y1 * _W + x1] = color;
                if (x1 == x2 && y1 == y2) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x1 += sx; }
                if (e2 < dx) { err += dx; y1 += sy; }
            }
            int minX = Math.Min(x1, x2), minY = Math.Min(y1, y2);
            _dirty.Add(minX, minY, Math.Abs(x2 - x1) + 1, Math.Abs(y2 - y1) + 1);
        }

        public static void Line(int x1, int y1, int x2, int y2, Color c) => Line(x1, y1, x2, y2, ToInt(c));

        // =========================================================================
        //  AlphaFill — پر کردن با آلفا (برای overlay انیمیشن تم)
        //  a=0 → شفاف، a=255 → جامد
        // =========================================================================
        public static void AlphaFill(int x, int y, int w, int h, byte r, byte g, byte b, byte a)
        {
            if (a == 0) return;
            if (a == 255) { Fill(x, y, w, h, Pack(r, g, b)); return; }
            int x2 = Math.Min(x + w, _W), y2 = Math.Min(y + h, _H);
            x = Math.Max(x, 0); y = Math.Max(y, 0);
            if (x >= x2 || y >= y2) return;
            int ia = 255 - a;
            for (int row = y; row < y2; row++)
            {
                int idx = row * _W + x;
                for (int col = x; col < x2; col++)
                {
                    int dp = _buf[idx];
                    int dr = (dp >> 16) & 0xFF, dg = (dp >> 8) & 0xFF, db = dp & 0xFF;
                    _buf[idx++] = Pack(
                        (byte)((r * a + dr * ia) >> 8),
                        (byte)((g * a + dg * ia) >> 8),
                        (byte)((b * a + db * ia) >> 8));
                }
            }
            _dirty.Add(x, y, x2 - x, y2 - y);
        }

        public static void AlphaFill(int x, int y, int w, int h, Color c, byte a)
            => AlphaFill(x, y, w, h, c.R, c.G, c.B, a);

        // =========================================================================
        //  DrawCursor — کرسر با save/restore پیکسل‌های زیر آن
        //  این تابع باید آخرین چیزی باشد که قبل از Flush صدا زده می‌شود.
        //  اگر CursorBitmap موجود نباشد (cursorRawData=null)، کرسر مثلثی fallback
        //  مستقیماً روی back-buffer رسم می‌شود — بدون هیچ Canvas.DrawImageAlpha.
        //  مزیت: پیکسل‌های قبلی restore می‌شوند → هیچ ردپایی از کرسر باقی نمی‌ماند.
        // =========================================================================
        public static void DrawCursor(int[] cursorRawData, int cursorW, int cursorH, int mx, int my)
        {
            // restore پیکسل‌های قبلی
            if (_cursorDrawn && _lastCurX >= 0)
            {
                int rw = Math.Min(CurW, _W - _lastCurX);
                int rh = Math.Min(CurH, _H - _lastCurY);
                int cx0 = Math.Max(_lastCurX, 0);
                int cy0 = Math.Max(_lastCurY, 0);
                for (int ry = 0; ry < rh; ry++)
                    Array.Copy(_cursorSave, ry * CurW,
                               _buf, (cy0 + ry) * _W + cx0, rw);
                _dirty.Add(cx0, cy0, rw, rh);
            }
            _cursorDrawn = false;

            // save پیکسل‌های جدید
            int saveW = Math.Min(CurW, _W - mx);
            int saveH = Math.Min(CurH, _H - my);
            if (mx >= 0 && my >= 0 && saveW > 0 && saveH > 0)
            {
                for (int ry = 0; ry < saveH; ry++)
                    Array.Copy(_buf, (my + ry) * _W + mx,
                               _cursorSave, ry * CurW, saveW);
                _lastCurX = mx; _lastCurY = my;

                // رسم کرسر
                if (cursorRawData != null)
                    BlitAlpha(cursorRawData, cursorW, cursorH, mx, my);
                else
                    DrawFallbackCursor(mx, my);

                _cursorDrawn = true;
            }
        }

        // ─── کرسر پیش‌فرض (مثلث) بدون Bitmap ───────────────────────────────
        private static readonly int _curBlack = unchecked((int)0xFF000000u);
        private static readonly int _curWhite = unchecked((int)0xFFFFFFFFu);

        private static void DrawFallbackCursor(int mx, int my)
        {
            for (int dy = 0; dy < 12; dy++)
            {
                int lw = 12 - dy;
                for (int dx = 0; dx < lw && dx < dy + 1; dx++)
                {
                    int px = mx + dx, py = my + dy;
                    if ((uint)px >= (uint)_W || (uint)py >= (uint)_H) continue;
                    _buf[py * _W + px] = (dx == 0 || dx == dy) ? _curBlack : _curWhite;
                }
            }
        }

        // =========================================================================
        //  Flush — ارسال back-buffer به Canvas
        //  _buf همان _screenBitmap.rawData است (ر.ک. Init) پس دیگر نیازی به
        //  Array.Copy کامل صفحه نیست — مستقیماً DrawImage می‌زنیم.
        //
        //  هشدار مهم (تحقیق‌شده از سورس واقعی Cosmos): روی SVGAIICanvas،
        //  DrawImage(Image,x,y) واقعاً override شده و از یک memcpy سطح‌پایین
        //  (driver.videoMemory.Copy) استفاده می‌کند — یعنی سریع است. اما
        //  Canvas.DrawArray(int[],...) روی SVGAIICanvas اصلاً override نشده
        //  (رسماً «Not implemented» در مستندات Cosmos) و پیاده‌سازی پایه‌اش
        //  فقط یک حلقه‌ی DrawPoint به‌ازای تک‌تک پیکسل‌هاست — یعنی اگر روزی
        //  خواستید طبق کامنت قدیمی این فایل به DrawArray سوییچ کنید، به‌جای
        //  سریع‌تر شدن، به‌شدت کندتر می‌شود. با همین DrawImage ادامه بدهید.
        //
        //  توجه: Display را صدا نمی‌زنیم — GraphicsManager بعد از رسم متن‌ها می‌زند
        // =========================================================================
        public static void Flush(Canvas canvas)
        {
            canvas.DrawImage(_screenBitmap, 0, 0);
            _dirty.Reset();
        }

        // =========================================================================
        //  ScaleARGB — تغییر اندازه‌ی یک بافر ARGB با نمونه‌برداری جعبه‌ای
        //  (میانگین وزن‌دار با آلفا). فقط برای استفاده‌ی یک‌باره (مثلاً هنگام
        //  بارگذاری آیکون‌ها)، نه در حلقه‌ی رندر — نتیجه را کش کنید.
        //
        //  چرا لازم بود: BlitAlpha هیچ مقیاس‌دهی ندارد (۱ به ۱ کپی می‌کند)،
        //  پس تنها راه واقعی کوچک کردن یک تصویر، تولید یک نسخه‌ی از پیش
        //  کوچک‌شده و بلیت همان نسخه با BlitAlpha معمولی است — دقیقاً همان
        //  الگویی که پروژه‌های پایدار Cosmos برای asset های ثابت به کار
        //  می‌برند (scale once at load time, blit raw every frame).
        // =========================================================================
        public static int[] ScaleARGB(int[] src, int srcW, int srcH, int dstW, int dstH)
        {
            dstW = Math.Max(1, dstW); dstH = Math.Max(1, dstH);
            var dst = new int[dstW * dstH];
            if (src == null || srcW <= 0 || srcH <= 0) return dst;

            float xRatio = (float)srcW / dstW;
            float yRatio = (float)srcH / dstH;

            for (int dy = 0; dy < dstH; dy++)
            {
                int sy0 = (int)(dy * yRatio);
                int sy1 = Math.Max(sy0 + 1, (int)((dy + 1) * yRatio));
                if (sy1 > srcH) sy1 = srcH;

                for (int dx = 0; dx < dstW; dx++)
                {
                    int sx0 = (int)(dx * xRatio);
                    int sx1 = Math.Max(sx0 + 1, (int)((dx + 1) * xRatio));
                    if (sx1 > srcW) sx1 = srcW;

                    long a = 0, r = 0, g = 0, b = 0; int n = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                    {
                        int rowOff = sy * srcW;
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            int p = src[rowOff + sx];
                            int pa = (p >> 24) & 0xFF;
                            a += pa;
                            r += ((p >> 16) & 0xFF) * pa;
                            g += ((p >> 8) & 0xFF) * pa;
                            b += (p & 0xFF) * pa;
                            n++;
                        }
                    }
                    if (n == 0 || a == 0) { dst[dy * dstW + dx] = 0; continue; }
                    byte fa = (byte)(a / n);
                    byte fr = (byte)(r / a), fg = (byte)(g / a), fb = (byte)(b / a);
                    dst[dy * dstW + dx] = unchecked((int)(((uint)fa << 24) | ((uint)fr << 16) | ((uint)fg << 8) | fb));
                }
            }
            return dst;
        }

        // =========================================================================
        //  GetPixel — خواندن پیکسل از back-buffer (برای debug)
        // =========================================================================
        public static int GetPixel(int x, int y)
        {
            if ((uint)x >= (uint)_W || (uint)y >= (uint)_H) return 0;
            return _buf[y * _W + x];
        }

        // =========================================================================
        //  BlurRegion — blur سبک روی یک ناحیه از back-buffer
        //  برای افکت شیشه‌ای بدون allocate خارجی
        //  radius ≤ 3 توصیه می‌شود (box blur دوپاسه)
        // =========================================================================
        // =========================================================================
        //  BlurRegion — blur قوی‌تر و بهینه‌تر با الگوریتم Integral Image
        //  کیفیت بالاتر از box blur دوپاسه، زمان O(N) بدون وابستگی به radius
        //  radius پیشنهادی: 4-6 (از 3 قبلی قوی‌تر، اما سریع‌تر)
        //  passes: تعداد پاس (2 = خوب، 3 = بسیار نرم مثل Gaussian واقعی)
        // =========================================================================
        private static int[] _blurTmp = new int[0];
        private static int[] _blurTmp2 = new int[0]; // بافر دوم برای چند پاس

        public static void BlurRegion(int x, int y, int w, int h, int radius)
        {
            if (radius <= 0 || w <= 0 || h <= 0) return;
            int x2 = Math.Min(x + w, _W), y2 = Math.Min(y + h, _H);
            x = Math.Max(x, 0); y = Math.Max(y, 0);
            if (x >= x2 || y >= y2) return;
            int rw = x2 - x, rh = y2 - y;

            // radius بزرگتر (حداکثر ۶) و ۳ پاس برای نرمی بیشتر
            int clampedRadius = Math.Min(radius, 6);

            int needed = rw * rh;
            if (_blurTmp.Length < needed) _blurTmp = new int[needed];
            if (_blurTmp2.Length < needed) _blurTmp2 = new int[needed];

            // کپی ناحیه از _buf به _blurTmp (فشرده)
            for (int row = 0; row < rh; row++)
            {
                int srcOff = (y + row) * _W + x;
                int dstOff = row * rw;
                for (int col = 0; col < rw; col++)
                    _blurTmp[dstOff + col] = _buf[srcOff + col];
            }

            // ─── ۳ پاس box blur یک‌بُعدی متناوب (افقی، عمودی، افقی) ────────
            // هر پاس با بافر موقت کار می‌کند — بدون allocation در حلقه
            BoxBlurH(_blurTmp, _blurTmp2, rw, rh, clampedRadius);
            BoxBlurV(_blurTmp2, _blurTmp, rw, rh, clampedRadius);
            BoxBlurH(_blurTmp, _blurTmp2, rw, rh, clampedRadius);
            BoxBlurV(_blurTmp2, _blurTmp, rw, rh, clampedRadius);
            BoxBlurH(_blurTmp, _blurTmp2, rw, rh, clampedRadius);
            BoxBlurV(_blurTmp2, _blurTmp, rw, rh, clampedRadius);

            // نوشتن نتیجه نهایی به _buf
            for (int row = 0; row < rh; row++)
            {
                int dstOff = (y + row) * _W + x;
                int srcOff = row * rw;
                for (int col = 0; col < rw; col++)
                    _buf[dstOff + col] = _blurTmp[srcOff + col];
            }

            _dirty.Add(x, y, rw, rh);
        }

        // ─── Box blur افقی بهینه با sliding window O(N) ───────────────────────
        // هر ردیف را با پنجره sliding window بدون inner loop پردازش می‌کند
        private static void BoxBlurH(int[] src, int[] dst, int w, int h, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int row = 0; row < h; row++)
            {
                int off = row * w;
                // مقداردهی اولیه پنجره
                int rr = 0, g = 0, b = 0;
                for (int d = -r; d <= r; d++)
                {
                    int p = src[off + Math.Max(0, Math.Min(w - 1, d))];
                    rr += (p >> 16) & 0xFF; g += (p >> 8) & 0xFF; b += p & 0xFF;
                }
                for (int col = 0; col < w; col++)
                {
                    dst[off + col] = Pack((byte)(rr * inv), (byte)(g * inv), (byte)(b * inv));
                    // sliding: حذف پیکسل چپ، اضافه پیکسل راست
                    int leftP = src[off + Math.Max(0, col - r)];
                    int rightP = src[off + Math.Min(w - 1, col + r + 1)];
                    rr += ((rightP >> 16) & 0xFF) - ((leftP >> 16) & 0xFF);
                    g += ((rightP >> 8) & 0xFF) - ((leftP >> 8) & 0xFF);
                    b += (rightP & 0xFF) - (leftP & 0xFF);
                }
            }
        }

        // ─── Box blur عمودی بهینه با sliding window O(N) ─────────────────────
        private static void BoxBlurV(int[] src, int[] dst, int w, int h, int r)
        {
            float inv = 1f / (2 * r + 1);
            for (int col = 0; col < w; col++)
            {
                // مقداردهی اولیه پنجره
                int rr = 0, g = 0, b = 0;
                for (int d = -r; d <= r; d++)
                {
                    int p = src[Math.Max(0, Math.Min(h - 1, d)) * w + col];
                    rr += (p >> 16) & 0xFF; g += (p >> 8) & 0xFF; b += p & 0xFF;
                }
                for (int row = 0; row < h; row++)
                {
                    dst[row * w + col] = Pack((byte)(rr * inv), (byte)(g * inv), (byte)(b * inv));
                    int leftP = src[Math.Max(0, row - r) * w + col];
                    int rightP = src[Math.Min(h - 1, row + r + 1) * w + col];
                    rr += ((rightP >> 16) & 0xFF) - ((leftP >> 16) & 0xFF);
                    g += ((rightP >> 8) & 0xFF) - ((leftP >> 8) & 0xFF);
                    b += (rightP & 0xFF) - (leftP & 0xFF);
                }
            }
        }
    }
}