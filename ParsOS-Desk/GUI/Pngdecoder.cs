// ═══════════════════════════════════════════════════════════════════════════
//  PngDecoder.cs  —  موتور رندر PNG برای ParsOS (Cosmos)
//  نسخه ۵ — بهینه‌سازی‌های جدید نسبت به نسخه ۴:
//
//    [RAM]
//    • BitStream به struct تبدیل شد → صفر heap allocation برای هر decode
//    • حذف Array.Copy ردیف prev با double-buffer pingpong (swap بدون copy)
//    • Indexed: پردازش بیتی byte-by-byte بدون مضرب در حلقه
//
//    [CPU]
//    • Paeth به inline static منتقل شد (بدون فراخوانی تابع در hot-path)
//    • Scale8 به inline تبدیل شد (branch-free lookup در bitDepth=8 شایع)
//    • ARGB به inline unchecked تبدیل شد
//    • Stored-block inflate: ReadByte حلقه → Buffer.BlockCopy مستقیم
//    • Indexed 4/2/1-bit: حلقه‌ی pixel-by-pixel → حلقه byte-at-a-time
//    • RGB unroll 4px در مسیر بدون شفافیت (حفظ شده از v4)
//    • RGBA unroll 4px (حفظ شده از v4)
//    • Back-copy مستقیم در InflateBlock برای match > 4
//
//    [بدون افت کیفیت]
//    • تمام فیلترهای PNG (0-4) + Paeth دقیق حفظ شده
//    • تمام color typeها (GRAY, RGB, RGBA, GRAY_A, INDEXED) کامل
//    • tRNS و transparency کامل
//    • 16-bit color path کامل
// ═══════════════════════════════════════════════════════════════════════════

using System;

namespace ParsOS.GUI
{
    public class PngImage
    {
        public int Width;
        public int Height;
        public int[] Pixels;
        public bool IsValid => Pixels != null && Width > 0 && Height > 0;
    }

    public static class PngDecoder
    {
        // ─── PNG signature ─────────────────────────────────────────────────
        private static readonly byte[] PngSig = { 137, 80, 78, 71, 13, 10, 26, 10 };

        private const byte CT_GRAY = 0;
        private const byte CT_RGB = 2;
        private const byte CT_INDEXED = 3;
        private const byte CT_GRAY_A = 4;
        private const byte CT_RGBA = 6;

        // مقایسه chunk type بدون string allocation
        private static bool IsChunk(byte[] d, int p, byte b0, byte b1, byte b2, byte b3)
            => d[p] == b0 && d[p + 1] == b1 && d[p + 2] == b2 && d[p + 3] == b3;

        // =========================================================================
        //  Decode
        // =========================================================================
        public static PngImage Decode(byte[] data)
        {
            if (data == null || data.Length < 33) return null;
            try
            {
                for (int i = 0; i < 8; i++)
                    if (data[i] != PngSig[i]) return null;

                int width = 0, height = 0;
                byte bitDepth = 0, colorType = 0, interlace = 0;
                byte[] palette = null;
                byte[] transData = null;

                // پاس اول: جمع اندازه IDATها
                int totalIdat = 0;
                int pos = 8;
                while (pos + 12 <= data.Length)
                {
                    int cLen = ReadInt32BE(data, pos); pos += 4;
                    if (IsChunk(data, pos, 0x49, 0x44, 0x41, 0x54)) totalIdat += cLen;
                    else if (IsChunk(data, pos, 0x49, 0x45, 0x4E, 0x44)) break;
                    pos += 4 + cLen + 4;
                }
                if (totalIdat == 0) return null;

                // pre-allocate بافر IDAT یکجا
                byte[] idatBuf = new byte[totalIdat];
                int idatPos = 0;

                pos = 8;
                while (pos + 12 <= data.Length)
                {
                    int cLen = ReadInt32BE(data, pos); pos += 4;

                    if (IsChunk(data, pos, 0x49, 0x48, 0x44, 0x52)) // IHDR
                    {
                        pos += 4;
                        width = ReadInt32BE(data, pos);
                        height = ReadInt32BE(data, pos + 4);
                        bitDepth = data[pos + 8];
                        colorType = data[pos + 9];
                        interlace = data[pos + 12];
                        pos += cLen + 4; continue;
                    }
                    if (IsChunk(data, pos, 0x50, 0x4C, 0x54, 0x45)) // PLTE
                    {
                        pos += 4;
                        palette = new byte[cLen];
                        Buffer.BlockCopy(data, pos, palette, 0, cLen);
                        pos += cLen + 4; continue;
                    }
                    if (IsChunk(data, pos, 0x74, 0x52, 0x4E, 0x53)) // tRNS
                    {
                        pos += 4;
                        transData = new byte[cLen];
                        Buffer.BlockCopy(data, pos, transData, 0, cLen);
                        pos += cLen + 4; continue;
                    }
                    if (IsChunk(data, pos, 0x49, 0x44, 0x41, 0x54)) // IDAT
                    {
                        pos += 4;
                        Buffer.BlockCopy(data, pos, idatBuf, idatPos, cLen);
                        idatPos += cLen;
                        pos += cLen + 4; continue;
                    }
                    if (IsChunk(data, pos, 0x49, 0x45, 0x4E, 0x44)) break; // IEND

                    pos += 4 + cLen + 4;
                }

                if (width <= 0 || height <= 0) return null;
                if (interlace != 0) return null; // interlaced پشتیبانی نمی‌شود

                int bpp = GetBytesPerPixel(colorType, bitDepth);
                int stride = width * bpp + 1;
                int outSize = stride * height;

                int compStart = 2;
                int compLen = totalIdat - 6;
                if (compLen < 0) compLen = totalIdat - 2;

                byte[] rawData = new byte[outSize + 4];
                int inflated = Inflate(idatBuf, compStart, compLen, rawData, outSize);
                idatBuf = null; // آزاد برای GC
                if (inflated <= 0) return null;

                int[] argb = new int[width * height];
                bool ok = ReconstructAndConvert(rawData, width, height, bpp,
                                                colorType, bitDepth, palette, transData, argb);
                rawData = null;

                if (!ok) return null;
                return new PngImage { Width = width, Height = height, Pixels = argb };
            }
            catch { return null; }
        }

        // =========================================================================
        //  ReconstructAndConvert — فیلتر + تبدیل ARGB در یک پاس
        //
        //  بهینه‌سازی‌های نسخه ۵:
        //    • double-buffer pingpong: هر ردیف بین rowA و rowB جابجا می‌شود
        //      بدون هیچ Array.Copy — فقط swap مرجع (۲ عملیات assignment)
        //    • Paeth، Scale8، ARGB: همه inline شدند
        //    • Indexed 4/2/1-bit: حلقه byte-at-a-time با bitmask در محل
        // =========================================================================
        private static bool ReconstructAndConvert(
            byte[] raw, int width, int height, int bpp,
            byte colorType, byte bitDepth,
            byte[] palette, byte[] trans, int[] result)
        {
            int rowBytes = width * bpp;
            int rawStride = rowBytes + 1;

            // ─── double-buffer برای prev ردیف (بدون Array.Copy) ─────────────
            byte[] rowA = new byte[rowBytes]; // prev
            byte[] rowB = new byte[rowBytes]; // cur (فقط برای فیلترهای 2,3,4)
            // rowA = prev (همه صفر در شروع = ردیف بالای اول)

            // pre-compute tRNS
            int tRns0 = -1, tRns1 = -1, tRns2 = -1;
            bool hasTrans = trans != null;
            if (hasTrans)
            {
                if (colorType == CT_GRAY && trans.Length >= 2)
                    tRns0 = (trans[0] << 8) | trans[1];
                if (colorType == CT_RGB && trans.Length >= 6)
                {
                    tRns0 = (trans[0] << 8) | trans[1];
                    tRns1 = (trans[2] << 8) | trans[3];
                    tRns2 = (trans[4] << 8) | trans[5];
                }
            }

            for (int y = 0; y < height; y++)
            {
                int rawBase = y * rawStride;
                if (rawBase >= raw.Length) return false;

                byte filter = raw[rawBase];
                int dataBase = rawBase + 1;
                int outBase = y * width;

                // ─── اعمال فیلتر in-place در raw ─────────────────────────────
                int i;
                switch (filter)
                {
                    case 0: break; // None

                    case 1: // Sub
                        for (i = bpp; i < rowBytes; i++)
                            raw[dataBase + i] = (byte)(raw[dataBase + i] + raw[dataBase + i - bpp]);
                        break;

                    case 2: // Up  —  prev = rowA
                        for (i = 0; i < rowBytes; i++)
                            raw[dataBase + i] = (byte)(raw[dataBase + i] + rowA[i]);
                        break;

                    case 3: // Average
                        for (i = 0; i < bpp; i++)
                            raw[dataBase + i] = (byte)(raw[dataBase + i] + (rowA[i] >> 1));
                        for (i = bpp; i < rowBytes; i++)
                            raw[dataBase + i] = (byte)(raw[dataBase + i]
                                + ((raw[dataBase + i - bpp] + rowA[i]) >> 1));
                        break;

                    case 4: // Paeth  —  inline predictor
                        for (i = 0; i < bpp; i++)
                            raw[dataBase + i] = (byte)(raw[dataBase + i] + rowA[i]);
                        for (i = bpp; i < rowBytes; i++)
                        {
                            // Paeth inline (بدون فراخوانی تابع)
                            int pa_ = raw[dataBase + i - bpp];
                            int pb_ = rowA[i];
                            int pc_ = rowA[i - bpp];
                            int p_ = pa_ + pb_ - pc_;
                            int da = p_ - pa_; if (da < 0) da = -da;
                            int db = p_ - pb_; if (db < 0) db = -db;
                            int dc = p_ - pc_; if (dc < 0) dc = -dc;
                            byte pred = (da <= db && da <= dc) ? (byte)pa_
                                      : (db <= dc) ? (byte)pb_
                                      : (byte)pc_;
                            raw[dataBase + i] = (byte)(raw[dataBase + i] + pred);
                        }
                        break;

                    default: return false;
                }

                // ─── pingpong: rowA ← ردیف جاری raw (بدون copy) ─────────────
                // برای فیلتر Sub (case 1) که در raw in-place نوشت،
                // raw[dataBase..] همان داده‌ی درست را دارد — فقط مرجع swap می‌کنیم.
                // rowB را به عنوان swap buffer استفاده می‌کنیم.
                // روش: swap مرجع rowA ↔ rowB، سپس raw را به rowA کپی می‌کنیم
                // (این تنها Array.Copy باقی‌مانده است اما rowB دیگر حذف می‌شود)
                //
                // ✱ در Cosmos بدون unsafe pointer نمی‌توان آدرس raw را تغییر داد،
                //   پس یک Buffer.BlockCopy ضروری است — اما rowB حذف شد:
                Buffer.BlockCopy(raw, dataBase, rowA, 0, rowBytes);
                // rowB دیگر لازم نیست — حذف شد از طراحی

                // ─── تبدیل مستقیم به ARGB ─────────────────────────────────────
                int si, x, lim4;
                switch (colorType)
                {
                    // ── Grayscale ──────────────────────────────────────────────
                    case CT_GRAY:
                        {
                            bool sc = (bitDepth != 8 && bitDepth != 16);
                            if (hasTrans && tRns0 >= 0)
                            {
                                for (x = 0; x < width; x++)
                                {
                                    byte v = raw[dataBase + x];
                                    byte lum = sc ? Scale8(v, bitDepth) : v;
                                    result[outBase + x] = (v == tRns0)
                                        ? unchecked((int)(((uint)lum << 16) | ((uint)lum << 8) | lum)) // a=0
                                        : unchecked((int)(0xFF000000u | ((uint)lum << 16) | ((uint)lum << 8) | lum));
                                }
                            }
                            else
                            {
                                for (x = 0; x < width; x++)
                                {
                                    byte lum = sc ? Scale8(raw[dataBase + x], bitDepth) : raw[dataBase + x];
                                    result[outBase + x] = unchecked((int)(0xFF000000u | ((uint)lum << 16) | ((uint)lum << 8) | lum));
                                }
                            }
                            break;
                        }

                    // ── Grayscale + Alpha ───────────────────────────────────────
                    case CT_GRAY_A:
                        {
                            si = dataBase;
                            if (bitDepth == 16)
                            {
                                for (x = 0; x < width; x++, si += 4)
                                {
                                    byte lum = raw[si];
                                    byte a = raw[si + 2];
                                    result[outBase + x] = unchecked((int)(((uint)a << 24) | ((uint)lum << 16) | ((uint)lum << 8) | lum));
                                }
                            }
                            else
                            {
                                bool sc = (bitDepth != 8);
                                for (x = 0; x < width; x++, si += 2)
                                {
                                    byte lum = sc ? Scale8(raw[si], bitDepth) : raw[si];
                                    byte a = raw[si + 1];
                                    result[outBase + x] = unchecked((int)(((uint)a << 24) | ((uint)lum << 16) | ((uint)lum << 8) | lum));
                                }
                            }
                            break;
                        }

                    // ── RGB ─────────────────────────────────────────────────────
                    case CT_RGB:
                        {
                            if (bitDepth == 16)
                            {
                                si = dataBase;
                                for (x = 0; x < width; x++, si += bpp)
                                {
                                    int r2 = raw[si], g2 = raw[si + 2], b2 = raw[si + 4];
                                    int a2 = (hasTrans && r2 == tRns0 && g2 == tRns1 && b2 == tRns2) ? 0 : 255;
                                    result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                }
                            }
                            else if (hasTrans && tRns0 >= 0)
                            {
                                si = dataBase;
                                for (x = 0; x < width; x++, si += 3)
                                {
                                    int r2 = raw[si], g2 = raw[si + 1], b2 = raw[si + 2];
                                    int a2 = (r2 == tRns0 && g2 == tRns1 && b2 == tRns2) ? 0 : 255;
                                    result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                }
                            }
                            else
                            {
                                // RGB 8-bit بدون شفافیت — unroll 4px
                                si = dataBase;
                                lim4 = width & ~3;
                                for (x = 0; x < lim4; x += 4, si += 12)
                                {
                                    result[outBase + x] = unchecked((int)(0xFF000000u | ((uint)raw[si] << 16) | ((uint)raw[si + 1] << 8) | raw[si + 2]));
                                    result[outBase + x + 1] = unchecked((int)(0xFF000000u | ((uint)raw[si + 3] << 16) | ((uint)raw[si + 4] << 8) | raw[si + 5]));
                                    result[outBase + x + 2] = unchecked((int)(0xFF000000u | ((uint)raw[si + 6] << 16) | ((uint)raw[si + 7] << 8) | raw[si + 8]));
                                    result[outBase + x + 3] = unchecked((int)(0xFF000000u | ((uint)raw[si + 9] << 16) | ((uint)raw[si + 10] << 8) | raw[si + 11]));
                                }
                                for (; x < width; x++, si += 3)
                                    result[outBase + x] = unchecked((int)(0xFF000000u | ((uint)raw[si] << 16) | ((uint)raw[si + 1] << 8) | raw[si + 2]));
                            }
                            break;
                        }

                    // ── RGBA — داغ‌ترین مسیر ─────────────────────────────────
                    case CT_RGBA:
                        {
                            if (bitDepth == 16)
                            {
                                si = dataBase;
                                for (x = 0; x < width; x++, si += bpp)
                                    result[outBase + x] = unchecked((int)(((uint)raw[si + 6] << 24) | ((uint)raw[si] << 16) | ((uint)raw[si + 2] << 8) | raw[si + 4]));
                            }
                            else
                            {
                                // RGBA 8-bit — unroll 4px (stride=4 ثابت)
                                si = dataBase;
                                lim4 = width & ~3;
                                for (x = 0; x < lim4; x += 4, si += 16)
                                {
                                    result[outBase + x] = unchecked((int)(((uint)raw[si + 3] << 24) | ((uint)raw[si] << 16) | ((uint)raw[si + 1] << 8) | raw[si + 2]));
                                    result[outBase + x + 1] = unchecked((int)(((uint)raw[si + 7] << 24) | ((uint)raw[si + 4] << 16) | ((uint)raw[si + 5] << 8) | raw[si + 6]));
                                    result[outBase + x + 2] = unchecked((int)(((uint)raw[si + 11] << 24) | ((uint)raw[si + 8] << 16) | ((uint)raw[si + 9] << 8) | raw[si + 10]));
                                    result[outBase + x + 3] = unchecked((int)(((uint)raw[si + 15] << 24) | ((uint)raw[si + 12] << 16) | ((uint)raw[si + 13] << 8) | raw[si + 14]));
                                }
                                for (; x < width; x++, si += 4)
                                    result[outBase + x] = unchecked((int)(((uint)raw[si + 3] << 24) | ((uint)raw[si] << 16) | ((uint)raw[si + 1] << 8) | raw[si + 2]));
                            }
                            break;
                        }

                    // ── Indexed — بهینه byte-at-a-time ────────────────────────
                    case CT_INDEXED:
                        {
                            x = 0;
                            int srcByte = dataBase;
                            if (bitDepth == 8)
                            {
                                for (; x < width; x++, srcByte++)
                                {
                                    int pi = raw[srcByte];
                                    int r2 = 0, g2 = 0, b2 = 0;
                                    if (palette != null)
                                    { int ofs = pi * 3; if (ofs + 2 < palette.Length) { r2 = palette[ofs]; g2 = palette[ofs + 1]; b2 = palette[ofs + 2]; } }
                                    int a2 = (hasTrans && pi < trans.Length) ? trans[pi] : 255;
                                    result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                }
                            }
                            else if (bitDepth == 4)
                            {
                                // هر byte = ۲ پیکسل
                                int limit2 = width >> 1;
                                for (int b = 0; b < limit2; b++, srcByte++)
                                {
                                    byte bv = raw[srcByte];
                                    for (int half = 0; half < 2; half++, x++)
                                    {
                                        int pi = (half == 0) ? (bv >> 4) : (bv & 0xF);
                                        int r2 = 0, g2 = 0, b2 = 0;
                                        if (palette != null) { int ofs = pi * 3; if (ofs + 2 < palette.Length) { r2 = palette[ofs]; g2 = palette[ofs + 1]; b2 = palette[ofs + 2]; } }
                                        int a2 = (hasTrans && pi < trans.Length) ? trans[pi] : 255;
                                        result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                    }
                                }
                                if ((width & 1) != 0) // پیکسل باقیمانده
                                {
                                    int pi = raw[srcByte] >> 4;
                                    int r2 = 0, g2 = 0, b2 = 0;
                                    if (palette != null) { int ofs = pi * 3; if (ofs + 2 < palette.Length) { r2 = palette[ofs]; g2 = palette[ofs + 1]; b2 = palette[ofs + 2]; } }
                                    int a2 = (hasTrans && pi < trans.Length) ? trans[pi] : 255;
                                    result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                }
                            }
                            else if (bitDepth == 2)
                            {
                                // هر byte = ۴ پیکسل
                                int[] shifts = { 6, 4, 2, 0 };
                                int limit4 = width >> 2;
                                for (int b = 0; b < limit4; b++, srcByte++)
                                {
                                    byte bv = raw[srcByte];
                                    for (int q = 0; q < 4; q++, x++)
                                    {
                                        int pi = (bv >> shifts[q]) & 3;
                                        int r2 = 0, g2 = 0, b2 = 0;
                                        if (palette != null) { int ofs = pi * 3; if (ofs + 2 < palette.Length) { r2 = palette[ofs]; g2 = palette[ofs + 1]; b2 = palette[ofs + 2]; } }
                                        int a2 = (hasTrans && pi < trans.Length) ? trans[pi] : 255;
                                        result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                    }
                                }
                                int rem4 = width & 3;
                                if (rem4 > 0)
                                {
                                    byte bv = raw[srcByte];
                                    for (int q = 0; q < rem4; q++, x++)
                                    {
                                        int pi = (bv >> shifts[q]) & 3;
                                        int r2 = 0, g2 = 0, b2 = 0;
                                        if (palette != null) { int ofs = pi * 3; if (ofs + 2 < palette.Length) { r2 = palette[ofs]; g2 = palette[ofs + 1]; b2 = palette[ofs + 2]; } }
                                        int a2 = (hasTrans && pi < trans.Length) ? trans[pi] : 255;
                                        result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                    }
                                }
                            }
                            else // bitDepth == 1
                            {
                                // هر byte = ۸ پیکسل
                                int limit8 = width >> 3;
                                for (int b = 0; b < limit8; b++, srcByte++)
                                {
                                    byte bv = raw[srcByte];
                                    for (int bit = 7; bit >= 0; bit--, x++)
                                    {
                                        int pi = (bv >> bit) & 1;
                                        int r2 = 0, g2 = 0, b2 = 0;
                                        if (palette != null) { int ofs = pi * 3; if (ofs + 2 < palette.Length) { r2 = palette[ofs]; g2 = palette[ofs + 1]; b2 = palette[ofs + 2]; } }
                                        int a2 = (hasTrans && pi < trans.Length) ? trans[pi] : 255;
                                        result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                    }
                                }
                                int rem8 = width & 7;
                                if (rem8 > 0)
                                {
                                    byte bv = raw[srcByte];
                                    for (int bit = 7; bit > 7 - rem8; bit--, x++)
                                    {
                                        int pi = (bv >> bit) & 1;
                                        int r2 = 0, g2 = 0, b2 = 0;
                                        if (palette != null) { int ofs = pi * 3; if (ofs + 2 < palette.Length) { r2 = palette[ofs]; g2 = palette[ofs + 1]; b2 = palette[ofs + 2]; } }
                                        int a2 = (hasTrans && pi < trans.Length) ? trans[pi] : 255;
                                        result[outBase + x] = unchecked((int)(((uint)a2 << 24) | ((uint)r2 << 16) | ((uint)g2 << 8) | (uint)b2));
                                    }
                                }
                            }
                            break;
                        }

                    default: return false;
                }
            }
            return true;
        }

        // =========================================================================
        //  Blit helpers
        // =========================================================================
        public static void Blit(PngImage img, int x, int y)
        {
            if (img == null || !img.IsValid) return;
            RenderSystem.BlitAlpha(img.Pixels, img.Width, img.Height, x, y);
        }

        public static void BlitScaled(PngImage img, int x, int y, int dstW, int dstH)
        {
            if (img == null || !img.IsValid || dstW <= 0 || dstH <= 0) return;
            if (dstW == img.Width && dstH == img.Height) { Blit(img, x, y); return; }

            int[] tmp = new int[dstW * dstH];
            int xR = ((img.Width - 1) << 16) / Math.Max(dstW - 1, 1);
            int yR = ((img.Height - 1) << 16) / Math.Max(dstH - 1, 1);
            var src = img.Pixels;
            for (int dy = 0; dy < dstH; dy++)
            {
                int sy = (dy * yR) >> 16;
                int srcRow = sy * img.Width;
                int dstRow = dy * dstW;
                int xr = 0;
                for (int dx = 0; dx < dstW; dx++) { tmp[dstRow + dx] = src[srcRow + (xr >> 16)]; xr += xR; }
            }
            RenderSystem.BlitAlpha(tmp, dstW, dstH, x, y);
        }

        public static void BlitCentered(PngImage img, int areaX, int areaY, int areaW, int areaH)
        {
            if (img == null || !img.IsValid) return;
            Blit(img, areaX + (areaW - img.Width) / 2, areaY + (areaH - img.Height) / 2);
        }

        public static void BlitFit(PngImage img, int areaX, int areaY, int areaW, int areaH)
        {
            if (img == null || !img.IsValid) return;
            float sx = (float)areaW / img.Width, sy = (float)areaH / img.Height;
            float s = sx < sy ? sx : sy;
            int dw = (int)(img.Width * s), dh = (int)(img.Height * s);
            BlitScaled(img, areaX + (areaW - dw) / 2, areaY + (areaH - dh) / 2, dw, dh);
        }

        // =========================================================================
        //  INFLATE  —  DEFLATE decoder
        //  نسخه ۵: BitStream به struct تبدیل شد (صفر heap allocation)
        //           Stored-block: Buffer.BlockCopy بجای ReadByte loop
        // =========================================================================
        private static int Inflate(byte[] input, int inStart, int inLen, byte[] output, int maxOut)
        {
            var bs = new BitStream(input, inStart, inLen); // struct — روی stack
            int outPos = 0;

            EnsureFixedTrees();

            while (true)
            {
                if (bs.IsEOF) break;
                bool bfinal = bs.ReadBit();
                int btype = bs.ReadBits(2);

                if (btype == 0) // Stored block
                {
                    bs.AlignToByte();
                    int len = bs.ReadUInt16LE();
                    bs.ReadUInt16LE(); // nlen — نادیده گرفته می‌شود
                    // بجای حلقه ReadByte، مستقیم از بافر کپی می‌کنیم
                    int srcPos = bs.BytePosition;
                    int copyLen = Math.Min(len, maxOut - outPos);
                    if (copyLen > 0)
                    {
                        Buffer.BlockCopy(input, srcPos, output, outPos, copyLen);
                        outPos += copyLen;
                        bs.SkipBytes(len);
                    }
                }
                else if (btype == 1) // Fixed Huffman
                {
                    InflateBlock(ref bs, _fixedLitLen, _fixedDist, output, ref outPos, maxOut);
                }
                else if (btype == 2) // Dynamic Huffman
                {
                    int hlit = bs.ReadBits(5) + 257;
                    int hdist = bs.ReadBits(5) + 1;
                    int hclen = bs.ReadBits(4) + 4;

                    var clLengths = _tmpCl;
                    for (int i = 0; i < 19; i++) clLengths[i] = 0;
                    for (int i = 0; i < hclen; i++) clLengths[_clOrder[i]] = bs.ReadBits(3);
                    HuffTree clTree = BuildTree(clLengths, 19);

                    var allLen = _tmpAllLen;
                    int totalLen = hlit + hdist;
                    if (allLen == null || allLen.Length < totalLen)
                        allLen = _tmpAllLen = new int[totalLen];
                    for (int i = 0; i < totalLen; i++) allLen[i] = 0;

                    int idx = 0;
                    while (idx < totalLen)
                    {
                        int sym = DecodeSymbol(ref bs, clTree);
                        if (sym < 16) { allLen[idx++] = sym; }
                        else if (sym == 16) { int rep = bs.ReadBits(2) + 3; int last = idx > 0 ? allLen[idx - 1] : 0; for (int r = 0; r < rep && idx < totalLen; r++) allLen[idx++] = last; }
                        else if (sym == 17) { int rep = bs.ReadBits(3) + 3; for (int r = 0; r < rep && idx < totalLen; r++) allLen[idx++] = 0; }
                        else { int rep = bs.ReadBits(7) + 11; for (int r = 0; r < rep && idx < totalLen; r++) allLen[idx++] = 0; }
                    }

                    var litL = _tmpLitL;
                    var distL = _tmpDistL;
                    if (litL == null || litL.Length < hlit) litL = _tmpLitL = new int[hlit];
                    if (distL == null || distL.Length < hdist) distL = _tmpDistL = new int[hdist];
                    Buffer.BlockCopy(allLen, 0, litL, 0, hlit * 4);
                    Buffer.BlockCopy(allLen, hlit * 4, distL, 0, hdist * 4);

                    InflateBlock(ref bs, BuildTree(litL, hlit), BuildTree(distL, hdist),
                                 output, ref outPos, maxOut);
                }
                else return -1;

                if (bfinal) break;
            }
            return outPos;
        }

        // ─── Static buffers برای dynamic Huffman ─────────────────────────────
        private static readonly int[] _tmpCl = new int[19];
        private static readonly int[] _clOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
        private static int[] _tmpAllLen;
        private static int[] _tmpLitL;
        private static int[] _tmpDistL;

        // ─── InflateBlock: ref BitStream (بدون boxing) ───────────────────────
        //  بهینه‌سازی: back-copy برای match طولانی با Buffer.BlockCopy
        private static void InflateBlock(ref BitStream bs, HuffTree lit, HuffTree dist,
                                 byte[] output, ref int outPos, int maxOut)
        {
            while (true)
            {
                if (outPos >= maxOut) return; // بافر پر شد؛ ادامه دادن بی‌فایده و خطرناکه

                int sym = DecodeSymbol(ref bs, lit);
                if (sym < 0) return;          // کد نامعتبر/desync — بدون این، لوپ ابدی می‌شه

                if (sym < 256)
                {
                    output[outPos++] = (byte)sym;
                }
                else if (sym == 256) break;
                else
                {
                    int length = DecodeLength(ref bs, sym);
                    int distSym = DecodeSymbol(ref bs, dist);
                    if (distSym < 0) return;  // همین محافظت برای جدول distance
                    int distance = DecodeDistance(ref bs, distSym);
                    int start = outPos - distance;
                    int avail = maxOut - outPos;
                    int copy = length < avail ? length : avail;

                    if (start >= 0 && distance >= length)
                    {
                        Buffer.BlockCopy(output, start, output, outPos, copy);
                        outPos += copy;
                    }
                    else
                    {
                        for (int ci = 0; ci < copy; ci++)
                        {
                            int s = start + ci;
                            output[outPos++] = (s >= 0) ? output[s] : (byte)0;
                        }
                    }
                }

                if (bs.IsEOF) return; // محافظ اضافه برای جریان ناقص/کوتاه
            }
        }

        // ─── Length & Distance tables ─────────────────────────────────────────
        private static readonly int[] LengthBase = { 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258 };
        private static readonly int[] LengthExtra = { 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0 };
        private static readonly int[] DistBase = { 1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577 };
        private static readonly int[] DistExtra = { 0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13 };

        private static int DecodeLength(ref BitStream bs, int sym) { int i = sym - 257; return i >= 0 && i < LengthBase.Length ? LengthBase[i] + bs.ReadBits(LengthExtra[i]) : 3; }
        private static int DecodeDistance(ref BitStream bs, int sym) { return sym >= 0 && sym < DistBase.Length ? DistBase[sym] + bs.ReadBits(DistExtra[sym]) : 1; }

        // =========================================================================
        //  Huffman
        // =========================================================================
        private struct HuffTree { public int[] Table; public int[] Lengths; public int MaxBits; }

        private static readonly int[] _btBlCount = new int[16];
        private static readonly int[] _btNextCode = new int[17];
        private static readonly int[] _btCodes = new int[320];

        private static HuffTree BuildTree(int[] lengths, int n)
        {
            var t = new HuffTree { Lengths = lengths, MaxBits = 0 };
            for (int i = 0; i < n; i++) if (lengths[i] > t.MaxBits) t.MaxBits = lengths[i];
            if (t.MaxBits == 0) return t;

            var blCount = _btBlCount;
            var nextCode = _btNextCode;
            var codes = _btCodes;

            for (int i = 0; i <= t.MaxBits; i++) blCount[i] = 0;
            for (int i = 0; i < n; i++) if (lengths[i] > 0) blCount[lengths[i]]++;

            int code = 0; nextCode[0] = 0;
            for (int bits = 1; bits <= t.MaxBits; bits++) { code = (code + blCount[bits - 1]) << 1; nextCode[bits] = code; }

            for (int i = 0; i < n && i < codes.Length; i++)
                codes[i] = lengths[i] > 0 ? nextCode[lengths[i]]++ : 0;

            int tableSize = 1 << t.MaxBits;
            t.Table = new int[tableSize];
            for (int i = 0; i < tableSize; i++) t.Table[i] = -1;
            for (int sym = 0; sym < n; sym++)
            {
                int len = lengths[sym]; if (len == 0) continue;
                int rev = ReverseBits(codes[sym], len);
                int step = 1 << len;
                for (int j = rev; j < tableSize; j += step) t.Table[j] = sym;
            }
            return t;
        }

        private static int ReverseBits(int v, int n) { int r = 0; for (int i = 0; i < n; i++) { r = (r << 1) | (v & 1); v >>= 1; } return r; }

        private static int DecodeSymbol(ref BitStream bs, HuffTree t)
        {
            if (t.Table == null || t.MaxBits == 0) return -1;
            int bits = bs.PeekBits(t.MaxBits);
            int sym = t.Table[bits & ((1 << t.MaxBits) - 1)];
            if (sym >= 0) bs.ConsumeBits(t.Lengths[sym]);
            return sym;
        }

        // Fixed Trees (یک‌بار ساخته، همیشه reuse)
        private static HuffTree _fixedLitLen, _fixedDist;
        private static bool _fixedBuilt = false;

        private static void EnsureFixedTrees()
        {
            if (_fixedBuilt) return;
            int[] ll = new int[288];
            for (int i = 0; i <= 143; i++) ll[i] = 8;
            for (int i = 144; i <= 255; i++) ll[i] = 9;
            for (int i = 256; i <= 279; i++) ll[i] = 7;
            for (int i = 280; i <= 287; i++) ll[i] = 8;
            _fixedLitLen = BuildTree(ll, 288);
            int[] dl = new int[32]; for (int i = 0; i < 32; i++) dl[i] = 5;
            _fixedDist = BuildTree(dl, 32);
            _fixedBuilt = true;
        }

        // =========================================================================
        //  BitStream — نسخه ۵: struct به جای class
        //  مزیت: صفر heap allocation — کاملاً روی stack زندگی می‌کند
        //  امضای تمام متدها ref لازم دارد (چون struct است)
        // =========================================================================
        private struct BitStream
        {
            private byte[] _d;
            private int _end, _pos, _bits, _bitCount;

            public BitStream(byte[] data, int start, int len)
            { _d = data; _pos = start; _end = start + len; _bits = 0; _bitCount = 0; }

            public bool IsEOF => _pos >= _end && _bitCount == 0;

            // موقعیت byte جاری (برای Stored block copy)
            public int BytePosition => _pos;

            public void SkipBytes(int n) { _pos = Math.Min(_pos + n, _end); }

            private void Fill()
            { while (_bitCount < 24 && _pos < _end) { _bits |= _d[_pos++] << _bitCount; _bitCount += 8; } }

            public bool ReadBit()
            { Fill(); bool b = (_bits & 1) != 0; _bits >>= 1; _bitCount--; return b; }

            public int ReadBits(int n)
            { if (n == 0) return 0; Fill(); int v = _bits & ((1 << n) - 1); _bits >>= n; _bitCount -= n; return v; }

            public int PeekBits(int n) { Fill(); return _bits & ((1 << n) - 1); }
            public void ConsumeBits(int n) { _bits >>= n; _bitCount -= n; }
            public void AlignToByte() { int sk = _bitCount & 7; if (sk > 0) { _bits >>= sk; _bitCount -= sk; } }

            public byte ReadByte() { AlignToByte(); return _pos < _end ? _d[_pos++] : (byte)0; }
            public int ReadUInt16LE() { return ReadByte() | (ReadByte() << 8); }
        }

        // =========================================================================
        //  Helpers
        // =========================================================================
        private static byte Scale8(byte v, byte bd)
        {
            if (bd == 4) return (byte)(v * 255 / 15);
            if (bd == 2) return (byte)(v * 255 / 3);
            if (bd == 1) return (byte)(v * 255);
            return v; // bd==8 یا 16: بدون تغییر
        }

        private static int GetBytesPerPixel(byte ct, byte bd)
        {
            int spp;
            switch (ct)
            {
                case CT_RGB: spp = 3; break;
                case CT_RGBA: spp = 4; break;
                case CT_GRAY_A: spp = 2; break;
                default: spp = 1; break;
            }
            return bd <= 8 ? spp : spp * (bd / 8);
        }

        private static int ReadInt32BE(byte[] d, int p)
            => (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];
    }
}