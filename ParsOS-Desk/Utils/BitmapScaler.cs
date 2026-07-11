// BitmapScaler.cs
// ابزار تغییر سایز مطمئن، پایدار و بهینه تصاویر Bitmap برای Cosmos OS
// سازگار با Cosmos UserKit 20221121 (بدون RawData عمومی)
//
// روش کار:
//   چون در این نسخه Cosmos، فیلد rawData در کلاس Image به صورت
//   protected تعریف شده و از خارج قابل دسترسی نیست، از متد
//   Save(MemoryStream) برای خواندن pixel data و از constructor
//   Bitmap(uint, uint, byte[], ColorDepth) برای ساخت bitmap خروجی
//   استفاده می‌کنیم.
//
// ویژگی‌ها:
//   • ScaleTo      — تغییر سایز به ابعاد دقیق
//   • ScaleToFit   — جا دادن در کادر با حفظ نسبت + مرکزچینی
//   • ScaleUniform — تغییر سایز با ضریب float
//   • ScaleSquare  — تغییر سایز به مربع (مناسب آیکون)
//   • همه متدها null-safe و exception-safe هستند

using Cosmos.System.Graphics;
using System;
using System.Drawing;
using System.IO;

namespace ParsOS.Utils
{
    public static class BitmapScaler
    {
        // ═══════════════════════════════════════════════════════════════════
        //  ScaleTo  —  تغییر سایز به ابعاد دقیق (بدون حفظ نسبت)
        // ═══════════════════════════════════════════════════════════════════
        public static Bitmap ScaleTo(Bitmap src, int targetW, int targetH)
        {
            if (src == null || targetW <= 0 || targetH <= 0)
                return null;

            int srcW = (int)src.Width;
            int srcH = (int)src.Height;
            if (srcW <= 0 || srcH <= 0)
                return null;

            if (srcW == targetW && srcH == targetH)
                return src;

            try
            {
                // خواندن pixel data از طریق Save به MemoryStream
                int[] srcPixels = ReadPixels(src);
                if (srcPixels == null)
                    return null;

                // آرایه خروجی (ARGB 32bit)
                byte[] dstBytes = new byte[targetW * targetH * 4];

                int scaleX = (srcW << 16) / targetW;
                int scaleY = (srcH << 16) / targetH;

                for (int dy = 0; dy < targetH; dy++)
                {
                    int sy = (dy * scaleY) >> 16;
                    if (sy >= srcH) sy = srcH - 1;

                    for (int dx = 0; dx < targetW; dx++)
                    {
                        int sx = (dx * scaleX) >> 16;
                        if (sx >= srcW) sx = srcW - 1;

                        int pixel = srcPixels[sy * srcW + sx];
                        int dstIdx = (dy * targetW + dx) * 4;

                        // ترتیب bytes برای constructor Cosmos: B, G, R, A
                        dstBytes[dstIdx] = (byte)(pixel & 0xFF);           // B
                        dstBytes[dstIdx + 1] = (byte)((pixel >> 8) & 0xFF);   // G
                        dstBytes[dstIdx + 2] = (byte)((pixel >> 16) & 0xFF);  // R
                        dstBytes[dstIdx + 3] = (byte)((pixel >> 24) & 0xFF);  // A
                    }
                }

                return new Bitmap((uint)targetW, (uint)targetH,
                                   dstBytes, ColorDepth.ColorDepth32);
            }
            catch
            {
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ScaleToFit  —  جا دادن در کادر با حفظ نسبت ابعاد
        // ═══════════════════════════════════════════════════════════════════
        public static Bitmap ScaleToFit(Bitmap src, int boxW, int boxH,
                                        Color? bgColor = null,
                                        bool center = true)
        {
            if (src == null || boxW <= 0 || boxH <= 0)
                return null;

            int srcW = (int)src.Width;
            int srcH = (int)src.Height;
            if (srcW <= 0 || srcH <= 0)
                return null;

            int fitW, fitH;
            ComputeFitSize(srcW, srcH, boxW, boxH, out fitW, out fitH);

            try
            {
                int[] srcPixels = ReadPixels(src);
                if (srcPixels == null)
                    return null;

                // پر کردن با رنگ پس‌زمینه
                int bgRaw = (bgColor.HasValue && bgColor.Value.A > 0)
                    ? ColorToRaw(bgColor.Value) : 0;

                byte[] dstBytes = new byte[boxW * boxH * 4];

                if (bgRaw != 0)
                {
                    byte bB = (byte)(bgRaw & 0xFF);
                    byte bG = (byte)((bgRaw >> 8) & 0xFF);
                    byte bR = (byte)((bgRaw >> 16) & 0xFF);
                    byte bA = (byte)((bgRaw >> 24) & 0xFF);

                    for (int i = 0; i < boxW * boxH; i++)
                    {
                        int idx = i * 4;
                        dstBytes[idx] = bB;
                        dstBytes[idx + 1] = bG;
                        dstBytes[idx + 2] = bR;
                        dstBytes[idx + 3] = bA;
                    }
                }

                int offX = center ? (boxW - fitW) / 2 : 0;
                int offY = center ? (boxH - fitH) / 2 : 0;

                int scaleX = (srcW << 16) / fitW;
                int scaleY = (srcH << 16) / fitH;

                for (int dy = 0; dy < fitH; dy++)
                {
                    int sy = (dy * scaleY) >> 16;
                    if (sy >= srcH) sy = srcH - 1;

                    for (int dx = 0; dx < fitW; dx++)
                    {
                        int sx = (dx * scaleX) >> 16;
                        if (sx >= srcW) sx = srcW - 1;

                        int pixel = srcPixels[sy * srcW + sx];
                        int dstIdx = ((dy + offY) * boxW + (dx + offX)) * 4;

                        dstBytes[dstIdx] = (byte)(pixel & 0xFF);
                        dstBytes[dstIdx + 1] = (byte)((pixel >> 8) & 0xFF);
                        dstBytes[dstIdx + 2] = (byte)((pixel >> 16) & 0xFF);
                        dstBytes[dstIdx + 3] = (byte)((pixel >> 24) & 0xFF);
                    }
                }

                return new Bitmap((uint)boxW, (uint)boxH,
                                   dstBytes, ColorDepth.ColorDepth32);
            }
            catch
            {
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ScaleUniform  —  تغییر سایز با ضریب یکنواخت
        // ═══════════════════════════════════════════════════════════════════
        public static Bitmap ScaleUniform(Bitmap src, float scale)
        {
            if (src == null || scale <= 0f)
                return null;

            int newW = Math.Max(1, (int)((int)src.Width * scale));
            int newH = Math.Max(1, (int)((int)src.Height * scale));

            return ScaleTo(src, newW, newH);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  ScaleSquare  —  تغییر سایز به مربع (مناسب آیکون)
        // ═══════════════════════════════════════════════════════════════════
        public static Bitmap ScaleSquare(Bitmap src, int size, Color? bgColor = null)
        {
            return ScaleToFit(src, size, size, bgColor, center: true);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  متدهای کمکی خصوصی
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// pixel data را از Bitmap از طریق Save به MemoryStream می‌خواند.
        /// خروجی آرایه int[] با فرمت ARGB (سازگار با rawData داخلی Cosmos) است.
        /// </summary>
        private static int[] ReadPixels(Bitmap bmp)
        {
            try
            {
                int w = (int)bmp.Width;
                int h = (int)bmp.Height;

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.bmp);
                    byte[] bmpData = ms.ToArray();

                    // هدر BMP: آفست pixel data در بایت ۱۰
                    int pixelOffset = BitConverter.ToInt32(bmpData, 10);
                    // bit depth در بایت ۲۸
                    int bitDepth = BitConverter.ToUInt16(bmpData, 28);

                    int[] pixels = new int[w * h];
                    int bytesPerPixel = bitDepth / 8;

                    // BMP از پایین به بالا ذخیره می‌شود
                    // padding هر ردیف به مضرب ۴ بایت
                    int rowSize = ((w * bitDepth + 31) / 32) * 4;

                    for (int row = 0; row < h; row++)
                    {
                        // BMP flipped: ردیف ۰ در فایل = آخرین ردیف تصویر
                        int srcRow = h - 1 - row;
                        int rowBase = pixelOffset + srcRow * rowSize;

                        for (int col = 0; col < w; col++)
                        {
                            int byteIdx = rowBase + col * bytesPerPixel;
                            int argb;

                            if (bitDepth == 32)
                            {
                                byte b = bmpData[byteIdx];
                                byte g = bmpData[byteIdx + 1];
                                byte r = bmpData[byteIdx + 2];
                                byte a = bmpData[byteIdx + 3];
                                argb = (a << 24) | (r << 16) | (g << 8) | b;
                            }
                            else // 24bit
                            {
                                byte b = bmpData[byteIdx];
                                byte g = bmpData[byteIdx + 1];
                                byte r = bmpData[byteIdx + 2];
                                argb = (255 << 24) | (r << 16) | (g << 8) | b;
                            }

                            pixels[row * w + col] = argb;
                        }
                    }

                    return pixels;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void ComputeFitSize(int srcW, int srcH,
                                           int boxW, int boxH,
                                           out int fitW, out int fitH)
        {
            long scaleX16 = ((long)boxW << 16) / srcW;
            long scaleY16 = ((long)boxH << 16) / srcH;
            long scale16 = scaleX16 < scaleY16 ? scaleX16 : scaleY16;

            fitW = (int)((srcW * scale16) >> 16);
            fitH = (int)((srcH * scale16) >> 16);

            if (fitW < 1) fitW = 1;
            if (fitH < 1) fitH = 1;
            if (fitW > boxW) fitW = boxW;
            if (fitH > boxH) fitH = boxH;
        }

        private static int ColorToRaw(Color c)
        {
            return (c.A << 24) | (c.R << 16) | (c.G << 8) | c.B;
        }
    }
}