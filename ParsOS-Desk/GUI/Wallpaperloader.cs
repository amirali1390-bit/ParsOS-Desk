// ═══════════════════════════════════════════════════════════════════════════
//  WallpaperLoader.cs — لودر غیرهمزمان (deferred) والپیپر برای ParsOS
//
//  مشکل اصلی که حل می‌کند:
//    • ApplyWallpaperFromList() → PngDecoder.Decode → RebuildWallpaperCache
//      همه در یک تیک رخ می‌دادند → فریز ۲-۵ ثانیه
//
//  راه‌حل:
//    ۱. Decode و Scale به نوارهای افقی (Strips) تقسیم می‌شوند
//    ۲. هر Tick فقط چند نوار پردازش می‌شود (≤ RowsPerTick)
//    ۳. در حین پردازش نوار پیشرفت روی صفحه نشان داده می‌شود
//    ۴. بعد از اتمام، _wpCache نصب و رندر ادامه می‌یابد
//
//  نحوه استفاده (در GraphicsManager.Tick):
//    if (WallpaperLoader.IsBusy) WallpaperLoader.Tick(); // قبل از Render
//
//  نحوه شروع لود:
//    WallpaperLoader.StartLoad(pixels, srcW, srcH, dstW, dstH);
//    WallpaperLoader.StartLoadBmp(rawData, srcW, srcH, dstW, dstH);
// ═══════════════════════════════════════════════════════════════════════════

using System;

namespace ParsOS.GUI
{
    public static class WallpaperLoader
    {
        // ─── تعداد ردیف‌های پردازش‌شده در هر Tick ────────────────────────────
        // ۲۴ ردیف @ 1024px = ~24K عملیات — کمتر از ۵ms روی x86 ضعیف
        private const int RowsPerTick = 24;

        // ─── رنگ نوار پیشرفت (لاجوردی) ──────────────────────────────────────
        private static readonly int ColProgress = unchecked((int)(0xFF_1A_6E_C8u));
        private static readonly int ColProgressBg = unchecked((int)(0xFF_14_14_26u));
        private static readonly int ColProgressText = unchecked((int)(0xFF_CC_CC_DDu));

        // ─── وضعیت داخلی ─────────────────────────────────────────────────────
        private static bool _busy;
        private static int[] _srcPixels;   // ARGB int[] (PNG) یا null (BMP)
        private static int[] _srcRawBmp;   // rawData BMP — null اگر PNG است
        private static int _srcW, _srcH;
        private static int _dstW, _dstH;
        private static int[] _dstBuf;      // بافر در حال ساخت (W×H ints)
        private static int _xRatio, _yRatio; // fixed-point 16.16
        private static int _nextRow;          // ردیف بعدی برای پردازش
        private static bool _isBmp;            // true = منبع BMP است

        // رنگ پس‌زمینه پیش‌فرض برای ترکیب آلفا
        private const int BgR = 20, BgG = 20, BgB = 35;

        // ─── public API ──────────────────────────────────────────────────────
        public static bool IsBusy => _busy;
        public static int Progress => _dstH > 0 ? (_nextRow * 100 / _dstH) : 0;

        /// <summary>شروع لود تدریجی از پیکسل‌های PNG (ARGB int[])</summary>
        public static void StartLoad(int[] pixels, int srcW, int srcH, int dstW, int dstH)
        {
            if (pixels == null || srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
                return;

            _srcPixels = pixels;
            _srcRawBmp = null;
            _isBmp = false;
            BeginInternal(srcW, srcH, dstW, dstH);
        }

        /// <summary>شروع لود تدریجی از rawData BMP (int[])</summary>
        public static void StartLoadBmp(int[] rawData, int srcW, int srcH, int dstW, int dstH)
        {
            if (rawData == null || srcW <= 0 || srcH <= 0 || dstW <= 0 || dstH <= 0)
                return;

            _srcRawBmp = rawData;
            _srcPixels = null;
            _isBmp = true;
            BeginInternal(srcW, srcH, dstW, dstH);
        }

        private static void BeginInternal(int srcW, int srcH, int dstW, int dstH)
        {
            _srcW = srcW;
            _srcH = srcH;
            _dstW = dstW;
            _dstH = dstH;
            _nextRow = 0;
            _busy = true;

            // بافر مقصد — اگر اندازه عوض نشده reuse کن
            if (_dstBuf == null || _dstBuf.Length != dstW * dstH)
                _dstBuf = new int[dstW * dstH];

            _xRatio = ((srcW - 1) << 16) / Math.Max(dstW - 1, 1);
            _yRatio = ((srcH - 1) << 16) / Math.Max(dstH - 1, 1);
        }

        /// <summary>
        /// در هر Tick فراخوانی شود — RowsPerTick ردیف پردازش می‌کند.
        /// وقتی کامل شد، WallpaperCache را نصب و false برمی‌گرداند.
        /// </summary>
        public static bool Tick()
        {
            if (!_busy) return false;

            int endRow = Math.Min(_nextRow + RowsPerTick, _dstH);

            if (_isBmp)
                ProcessBmpRows(_nextRow, endRow);
            else
                ProcessPngRows(_nextRow, endRow);

            _nextRow = endRow;

            if (_nextRow >= _dstH)
            {
                // ─── لود کامل شد → نصب کش ─────────────────────────────────
                RenderSystem.InstallWallpaperCache(_dstBuf, _dstW, _dstH);

                // آزاد کردن منابع موقت
                _srcPixels = null;
                _srcRawBmp = null;
                _busy = false;
                return false;
            }

            // ─── رندر نوار پیشرفت در back-buffer ──────────────────────────
            DrawProgressBar();
            return true;
        }

        // ─── پردازش ردیف‌های PNG ─────────────────────────────────────────────
        private static void ProcessPngRows(int fromRow, int toRow)
        {
            var src = _srcPixels;
            var dst = _dstBuf;
            int sw = _srcW;
            int dw = _dstW;
            int xRatio = _xRatio;
            int yRatio = _yRatio;

            for (int y = fromRow; y < toRow; y++)
            {
                int sy = (y * yRatio) >> 16;
                int rowBase = y * dw;
                int srcRow = sy * sw;
                int xr = 0;

                for (int x = 0; x < dw; x++)
                {
                    int pixel = src[srcRow + (xr >> 16)];
                    int a = (pixel >> 24) & 0xFF;

                    if (a >= 250)
                    {
                        dst[rowBase + x] = pixel | unchecked((int)0xFF000000u);
                    }
                    else if (a <= 5)
                    {
                        dst[rowBase + x] = unchecked((int)(0xFF000000u | (BgR << 16) | (BgG << 8) | (uint)BgB));
                    }
                    else
                    {
                        int ia = 255 - a;
                        int r = (((pixel >> 16) & 0xFF) * a + BgR * ia) >> 8;
                        int g = (((pixel >> 8) & 0xFF) * a + BgG * ia) >> 8;
                        int b = ((pixel & 0xFF) * a + BgB * ia) >> 8;
                        dst[rowBase + x] = unchecked((int)(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
                    }

                    xr += xRatio;
                }
            }
        }

        // ─── پردازش ردیف‌های BMP ─────────────────────────────────────────────
        private static void ProcessBmpRows(int fromRow, int toRow)
        {
            var src = _srcRawBmp;
            var dst = _dstBuf;
            int sw = _srcW;
            int dw = _dstW;
            int xRatio = _xRatio;
            int yRatio = _yRatio;

            for (int y = fromRow; y < toRow; y++)
            {
                int sy = (y * yRatio) >> 16;
                int rowBase = y * dw;
                int srcRow = sy * sw;
                int xr = 0;

                for (int x = 0; x < dw; x++)
                {
                    dst[rowBase + x] = src[srcRow + (xr >> 16)];
                    xr += xRatio;
                }
            }
        }

        // ─── رسم نوار پیشرفت در back-buffer ─────────────────────────────────
        // نوار ۱۶px ارتفاع در پایین صفحه — بدون allocation
        private static void DrawProgressBar()
        {
            if (_dstH <= 0) return;

            int W = _dstW;
            int H = _dstH;
            int barH = 18;
            int barY = H - barH - 4;
            int barX = W / 4;
            int barW = W / 2;
            int fillW = (barW * _nextRow) / _dstH;

            // پس‌زمینه نوار
            RenderSystem.FillDirect(barX - 2, barY - 2, barW + 4, barH + 4, ColProgressBg);
            // پر کردن
            if (fillW > 0)
                RenderSystem.FillDirect(barX, barY, fillW, barH, ColProgress);
            // مرز
            RenderSystem.DrawRectDirect(barX - 2, barY - 2, barW + 4, barH + 4, ColProgressText);
        }

        /// <summary>لغو لود در جریان (مثلاً هنگام شاتدان)</summary>
        public static void Cancel()
        {
            _busy = false;
            _srcPixels = null;
            _srcRawBmp = null;
        }
    }
}