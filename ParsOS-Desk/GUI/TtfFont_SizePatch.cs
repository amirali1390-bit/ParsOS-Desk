// TtfFont_LargeGlyph.cs — رندر گلیف بزرگ بدون محدودیت GlyphAtlas
// ─────────────────────────────────────────────────────────────────────────────
//
//  مشکل:
//    GlyphAtlas.CELL_W/H = 24px → فونت‌های بالای ~20px کلیپ می‌شوند.
//    RasterizerPool._bmp هم فقط 24×24 = 576 bool دارد.
//
//  راه‌حل:
//    LargeGlyphRenderer — یک rasterizer مستقل در partial class TtfFont که:
//      • هیچ ارتباطی به GlyphAtlas ندارد
//      • گلیف را در یک int[] اختصاصی رستر می‌کند (ARGB packed)
//      • نتیجه را با RenderSystem.BlitAlpha مستقیم در back-buffer می‌کشد
//      • کش ساده برای ارقام ۰-۹ و ':' (کاراکترهای ساعت) — ۱۱ گلیف
//      • static buffers اختصاصی (بدون تداخل با RasterizerPool)
//
//  نحوه استفاده در LockScreen.cs:
//    // جایگزین DrawString برای ساعت:
//    TtfFont.DrawLarge(canvas, Kernel.VazirFont, _cachedTime, pen, x, y, sizePx: 96);
//    // یا با اندازه‌گیری برای مرکزسازی:
//    int w = TtfFont.MeasureLarge(Kernel.VazirFont, _cachedTime, sizePx: 96);
//
//  مصرف RAM:
//    بافرهای static rasterizer: ≈ 40 KB (مستقل از RasterizerPool)
//    کش گلیف‌های ساعت (11 گلیف × 96×96 × 4): ≈ 4 MB حداکثر
//    (در عمل هر رقم ~60×80 = 19KB → 11 گلیف ≈ 210 KB)
//
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Cosmos.System.Graphics;
using ParsOS.GUI;

namespace ParsOS.GUI
{
    // ════════════════════════════════════════════════════════════════════════════
    //  LargeGlyphAtlas — اطلس اختصاصی برای گلیف‌های بزرگ (بدون محدودیت 24px)
    //
    //  معماری:
    //    • سلول‌های 128×128 پیکسل (کافی برای فونت‌های تا 120px)
    //    • ذخیره‌سازی به صورت int[] (ARGB packed) — آماده برای BlitColorized
    //    • کلید: (sizeToken << 20) | glyphId  ← token برای جداسازی اندازه‌ها
    //    • LRU eviction با 64 slot (≈ 64 × 128×128 × 4 = 4 MB حداکثر)
    //    • در عمل هر رقم ~70×90 = 25KB → 64 slot ≈ 1.6 MB
    //
    //  نحوه استفاده (داخلی — از TtfFont.GetOrRasterizeLargeAtlas فراخوانی می‌شود):
    //    int slot = LargeGlyphAtlas.FindSlot(key);
    //    if (slot < 0) slot = LargeGlyphAtlas.AllocSlot(key, gW, gH);
    //    LargeGlyphAtlas.BlitSlot(slot, x, drawY, colorArgb);
    // ════════════════════════════════════════════════════════════════════════════
    internal static class LargeGlyphAtlas
    {
        internal const int CELL = 128;          // حداکثر ابعاد هر گلیف
        internal const int MAX_SLOTS = 64;      // تعداد slot‌های موجود
        private const int SLOT_SIZE = CELL * CELL; // اندازه هر slot

        // ─── ذخیره‌سازی v2: به‌جای پیکسل ۱-بیتی (روشن/خاموش)، هر پیکسل یک
        // سطح coverage خاکستری (۰..GlyphAtlas.COV_MAX) نگه می‌دارد — دقیقاً
        // همان ترفند AA که در GlyphAtlas اصلی (TtfFont.cs، مسیر DrawString)
        // استفاده می‌شود. تفاوت اینجا: چون پس‌زمینه‌ی ساعت لاک‌اسکرین یک
        // تصویر بلور‌شده است نه یک رنگ یکدست، به‌جای میان‌یابی با یک
        // bgColor فرضی (که طبق هشدار GlyphAtlas روی پس‌زمینه‌ی غیریکدست
        // نتیجه‌ی غلط می‌دهد)، از RenderSystem.BlendPixelAlpha استفاده
        // می‌کنیم که واقعاً پیکسل زیرین back-buffer را می‌خواند و با آلفای
        // واقعی ترکیب می‌کند — یعنی AA درست، مستقل از این‌که زیرش چه چیزی
        // رسم شده.
        // slot s → _coverage[s * SLOT_SIZE .. (s+1) * SLOT_SIZE - 1]
        private static readonly byte[] _coverage = new byte[MAX_SLOTS * SLOT_SIZE];

        // metadata: [gW, gH, OY, AdvW]
        private const int F_W = 0, F_H = 1, F_OY = 2, F_AW = 3, META = 4;
        private static readonly short[] _meta = new short[MAX_SLOTS * META];

        // LRU tracking
        private static readonly int[] _slotKey = new int[MAX_SLOTS];
        private static readonly int[] _slotAge = new int[MAX_SLOTS];
        private static int _clock;

        // Hash O(1) lookup
        private const int HASH_SIZE = 256;
        private const int HASH_MASK = HASH_SIZE - 1;
        private const int HASH_EMPTY = -1;
        private static readonly int[] _hashBucket = new int[HASH_SIZE];
        private static readonly int[] _hashKey = new int[HASH_SIZE];

        static LargeGlyphAtlas()
        {
            for (int i = 0; i < MAX_SLOTS; i++) _slotKey[i] = -1;
            for (int i = 0; i < HASH_SIZE; i++) _hashBucket[i] = HASH_EMPTY;
            // _coverage یک flat array است، پیش‌مقداردهی با 0 (پیش‌فرض C#)
        }

        // ─── FindSlot: O(1) hash lookup ──────────────────────────────────────
        internal static int FindSlot(int key)
        {
            int h = (int)((uint)key * 0x9E3779B9u) & HASH_MASK;
            while (_hashBucket[h] != HASH_EMPTY)
            {
                if (_hashKey[h] == key) return _hashBucket[h];
                h = (h + 1) & HASH_MASK;
            }
            return -1;
        }

        private static void HashInsert(int key, int slot)
        {
            int h = (int)((uint)key * 0x9E3779B9u) & HASH_MASK;
            while (_hashBucket[h] != HASH_EMPTY && _hashKey[h] != key)
                h = (h + 1) & HASH_MASK;
            _hashBucket[h] = slot;
            _hashKey[h] = key;
        }

        private static void HashRemove(int key)
        {
            int h = (int)((uint)key * 0x9E3779B9u) & HASH_MASK;
            while (_hashBucket[h] != HASH_EMPTY)
            {
                if (_hashKey[h] == key) { _hashBucket[h] = HASH_EMPTY; return; }
                h = (h + 1) & HASH_MASK;
            }
        }

        // ─── AllocSlot: slot خالی یا LRU را برمی‌گرداند ─────────────────────
        internal static int AllocSlot(int key)
        {
            // جستجوی slot خالی
            for (int i = 0; i < MAX_SLOTS; i++)
            {
                if (_slotKey[i] < 0)
                {
                    _slotKey[i] = key;
                    _slotAge[i] = ++_clock;
                    HashInsert(key, i);
                    return i;
                }
            }
            // LRU eviction
            int oldest = 0;
            for (int i = 1; i < MAX_SLOTS; i++)
                if (_slotAge[i] < _slotAge[oldest]) oldest = i;

            HashRemove(_slotKey[oldest]);
            // پاک کردن coverage قدیمی در flat array
            int pBase = oldest * SLOT_SIZE;
            for (int i = 0; i < SLOT_SIZE; i++) _coverage[pBase + i] = 0;

            _slotKey[oldest] = key;
            _slotAge[oldest] = ++_clock;
            HashInsert(key, oldest);
            return oldest;
        }

        internal static void Touch(int slot) { _slotAge[slot] = ++_clock; }

        // ─── WriteGlyph: بافر coverage خاکستری (۰..COV_MAX) را ذخیره می‌کند ──
        // قبلاً bmp یک bool[] بود (فقط روشن/خاموش) و اینجا آن را به یک پیکسل
        // سفید کامل تبدیل می‌کرد — دقیقاً همان چیزی که در GlyphAtlas اصلی
        // قبل از پچ AA (نسخه‌های v1..v6) وجود داشت. حالا bmp یک byte[] با
        // سطوح coverage واقعی (خروجی LgScanlineFill) است و مستقیم کپی
        // می‌شود، بدون فشرده‌سازی به یک بیت.
        internal static void WriteGlyph(int slot, byte[] bmp, int gW, int gH, int oy, int aw)
        {
            int b = slot * META;
            _meta[b + F_W] = (short)Math.Min(gW, CELL);
            _meta[b + F_H] = (short)Math.Min(gH, CELL);
            _meta[b + F_OY] = (short)oy;
            _meta[b + F_AW] = (short)aw;

            int pBase = slot * SLOT_SIZE;
            int w = _meta[b + F_W], h = _meta[b + F_H];
            // پاک کردن کامل ابتدا
            for (int i = 0; i < SLOT_SIZE; i++) _coverage[pBase + i] = 0;
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    int bi = dy * gW + dx;
                    _coverage[pBase + dy * CELL + dx] = (bi < bmp.Length) ? bmp[bi] : (byte)0;
                }
        }

        // ─── BlitSlot: رسم گلیف با AA خاکستری واقعی در back-buffer ──────────
        // قبلاً هر پیکسل یا کامل رسم می‌شد یا اصلاً نه (SetPixel باینری) —
        // یعنی لبه‌های ساعت لاک‌اسکرین پله‌ای بودند برخلاف بقیه‌ی متن‌های
        // سیستم که از نسخه‌ی ۷ به بعد AA خاکستری دارند. حالا هر پیکسل با
        // سطح coverage خودش (۰..COV_MAX) به آلفای ۰..۲۵۵ تبدیل و با
        // RenderSystem.BlendPixelAlpha روی back-buffer ترکیب می‌شود — این
        // متد واقعاً رنگ فعلیِ زیرین (پس‌زمینه‌ی بلورشده‌ی wallpaper) را
        // می‌خواند و آلفا-بلند می‌کند، پس برخلاف ترفند bgColor-interpolation
        // اطلس اصلی، روی پس‌زمینه‌ی غیریکدست هم درست کار می‌کند.
        internal static void BlitSlot(int slot, int dx, int dy, int colorArgb)
        {
            int b = slot * META;
            int gW = _meta[b + F_W];
            int gH = _meta[b + F_H];
            int pBase = slot * SLOT_SIZE;

            int rgb = colorArgb & 0x00FFFFFF;
            int screenW = RenderSystem.ScreenW;
            int screenH = RenderSystem.ScreenH;

            int x0 = Math.Max(dx, 0);
            int y0 = Math.Max(dy, 0);
            int x1 = Math.Min(dx + gW, screenW);
            int y1 = Math.Min(dy + gH, screenH);
            if (x0 >= x1 || y0 >= y1) return;

            for (int row = y0; row < y1; row++)
            {
                int srcRowBase = pBase + (row - dy) * CELL + (x0 - dx);
                for (int col = x0; col < x1; col++)
                {
                    byte cov = _coverage[srcRowBase + (col - x0)];
                    if (cov == 0) continue;
                    int alpha = cov >= GlyphAtlas.COV_MAX ? 255 : cov * 255 / GlyphAtlas.COV_MAX;
                    RenderSystem.BlendPixelAlpha(col, row, rgb, alpha);
                }
            }
        }

        internal static int GetAdvW(int slot) => _meta[slot * META + F_AW];
        internal static int GetOY(int slot) => _meta[slot * META + F_OY];
    }

    public partial class TtfFont
    {
        // ════════════════════════════════════════════════════════════════════
        //  Static buffers اختصاصی — مستقل از RasterizerPool
        //  اندازه‌ها برای فونت تا 256px کافی‌اند
        // ════════════════════════════════════════════════════════════════════
        private const int LG_MAX_PTS = 512;
        private const int LG_MAX_CNTRS = 32;
        private const int LG_MAX_SUBDIV = LG_MAX_PTS * 12;
        private const int LG_MAX_BMP = 256 * 256;   // حداکثر گلیف 256×256

        private static readonly byte[] _lgFlags = new byte[LG_MAX_PTS];
        private static readonly int[] _lgPx = new int[LG_MAX_PTS];
        private static readonly int[] _lgPy = new int[LG_MAX_PTS];
        private static readonly float[] _lgSpx = new float[LG_MAX_SUBDIV];
        private static readonly float[] _lgSpy = new float[LG_MAX_SUBDIV];
        private static readonly int[] _lgEnds = new int[LG_MAX_CNTRS];
        // ─── _lgBmp: قبلاً bool[] (فقط روشن/خاموش) بود که باعث پله‌ای دیده
        // شدن ساعت لاک‌اسکرین می‌شد. حالا مثل ScanlineFill اصلی در TtfFont.cs
        // یک سطح coverage خاکستری (۰..GlyphAtlas.COV_MAX) به‌ازای هر پیکسل
        // نگه می‌دارد.
        private static readonly byte[] _lgBmp = new byte[LG_MAX_BMP];
        private static readonly float[] _lgXs = new float[256];
        // بافر پوشش کسری افقی هر ردیف (همان ترفند رفع پله‌ی افقی که در
        // RasterizerPool._rowCov برای اطلس اصلی استفاده شده)
        private static readonly float[] _lgRowCov = new float[256];

        // ════════════════════════════════════════════════════════════════════
        //  GlyphPixels — نتیجه rasterize (بدون int[] heap allocation)
        //  Pixels حذف شد: RasterizeLarge مستقیم در _lgBmp می‌نویسد
        // ════════════════════════════════════════════════════════════════════
        private struct GlyphPixels
        {
            public int W, H, OY, AdvW;
            public bool Valid;
        }

        // ─── کلید اطلس بزرگ: (sizeToken << 20) | glyphId ───────────────────
        private int LargeAtlasKey(int gid) => gid | (_sizeToken << 20);

        // ─── GetOrRasterizeLargeAtlas: از LargeGlyphAtlas استفاده می‌کند ────
        // برمی‌گرداند: slot معتبر یا -1
        private int GetOrRasterizeLargeAtlas(int codepoint, int sizePx)
        {
            EnsureCmap();
            int gid = GetGlyphId((char)codepoint);
            if (gid < 0) gid = 0;

            int key = LargeAtlasKey(gid);
            int slot = LargeGlyphAtlas.FindSlot(key);
            if (slot >= 0) { LargeGlyphAtlas.Touch(slot); return slot; }

            // rasterize مستقیم در _lgBmp (static buffer)
            float scale = sizePx / (float)_unitsPerEm;
            var result = RasterizeLarge(gid, scale, sizePx);
            if (!result.Valid) return -1;

            // advance width
            int hmtxIdx = gid < _numHMetrics ? gid : _numHMetrics - 1;
            int advW = Math.Max(1, (int)(ReadU16((int)_hmtxOffset + hmtxIdx * 4) * scale));

            slot = LargeGlyphAtlas.AllocSlot(key);
            LargeGlyphAtlas.WriteGlyph(slot, _lgBmp, result.W, result.H, result.OY, advW);
            return slot;
        }

        // ─── نگهداری سازگاری با کد قدیم — اکنون از Atlas استفاده می‌کند ─────
        private GlyphPixels GetOrRasterizeLarge(int codepoint, int sizePx)
        {
            float scale = sizePx / (float)_unitsPerEm;
            int gid = GetGlyphId((char)codepoint);
            if (gid < 0) gid = 0;
            var gp = RasterizeLarge(gid, scale, sizePx);
            int idx = gid < _numHMetrics ? gid : _numHMetrics - 1;
            gp.AdvW = Math.Max(1, (int)(ReadU16((int)_hmtxOffset + idx * 4) * scale));
            return gp;
        }

        // ─── Rasterize بدون هیچ کلیپ CELL_W/H ──────────────────────────────
        private GlyphPixels RasterizeLarge(int gid, float scale, int sizePx)
        {
            var result = new GlyphPixels();

            int off = GetGlyfOffset(gid);
            if (off < 0) return result;

            int nc = ReadS16(off);
            if (nc < 0)
            {
                // composite glyph — برای ساعت معمولاً پیش نمی‌آید
                // fallback: گلیف خالی
                return result;
            }
            if (nc == 0) return result;

            int xMin = ReadS16(off + 2), yMin = ReadS16(off + 4);
            int xMax = ReadS16(off + 6), yMax = ReadS16(off + 8);
            int dataOff = off + 10;

            if (nc > _lgEnds.Length) nc = _lgEnds.Length;

            for (int i = 0; i < nc; i++) _lgEnds[i] = ReadU16(dataOff + i * 2);
            dataOff += nc * 2;
            int instr = ReadU16(dataOff); dataOff += 2 + instr;
            int nPts = _lgEnds[nc - 1] + 1;
            if (nPts > _lgFlags.Length) nPts = _lgFlags.Length;

            // ─── flags ───────────────────────────────────────────────────────
            int pi = 0;
            while (pi < nPts && dataOff < _data.Length)
            {
                byte fl = _data[dataOff++]; _lgFlags[pi++] = fl;
                if ((fl & 8) != 0 && dataOff < _data.Length)
                {
                    int rep = _data[dataOff++];
                    for (int r = 0; r < rep && pi < nPts; r++) _lgFlags[pi++] = fl;
                }
            }

            // ─── coordinates X ───────────────────────────────────────────────
            int cur = 0;
            for (int i = 0; i < nPts; i++)
            {
                byte fl = _lgFlags[i];
                if ((fl & 2) != 0) { int d = _data[dataOff++]; cur += (fl & 0x10) != 0 ? d : -d; }
                else if ((fl & 0x10) == 0) { cur += ReadS16(dataOff); dataOff += 2; }
                _lgPx[i] = cur;
            }

            // ─── coordinates Y ───────────────────────────────────────────────
            cur = 0;
            for (int i = 0; i < nPts; i++)
            {
                byte fl = _lgFlags[i];
                if ((fl & 4) != 0) { int d = _data[dataOff++]; cur += (fl & 0x20) != 0 ? d : -d; }
                else if ((fl & 0x20) == 0) { cur += ReadS16(dataOff); dataOff += 2; }
                _lgPy[i] = cur;
            }

            // ─── ابعاد bitmap بدون کلیپ ─────────────────────────────────────
            int bW = Math.Max(1, (int)Math.Ceiling((xMax - xMin) * scale) + 2);
            int bH = Math.Max(1, (int)Math.Ceiling((yMax - yMin) * scale) + 2);
            // محافظ: نباید از LG_MAX_BMP بزرگ‌تر شود
            bW = Math.Min(bW, 256);
            bH = Math.Min(bH, 256);

            float ox = -xMin * scale;
            float oy2 = yMax * scale;

            // ─── Bezier subdivision ──────────────────────────────────────────
            int maxPP = _lgSpx.Length, totalPP = 0, startPt = 0;

            for (int c = 0; c < nc; c++)
            {
                int end = _lgEnds[c], cnt = end - startPt + 1;
                for (int i = 0; i < cnt; i++)
                {
                    int curr = startPt + i, next = startPt + (i + 1) % cnt;
                    bool currOn = (_lgFlags[curr] & 1) != 0;
                    float ax2 = _lgPx[curr] * scale + ox;
                    float ay2 = oy2 - _lgPy[curr] * scale;

                    if (currOn)
                    {
                        if (totalPP < maxPP) { _lgSpx[totalPP] = ax2; _lgSpy[totalPP++] = ay2; }
                    }
                    else
                    {
                        int prev = startPt + (i - 1 + cnt) % cnt;
                        float p0x, p0y;
                        if ((_lgFlags[prev] & 1) != 0)
                        { p0x = _lgPx[prev] * scale + ox; p0y = oy2 - _lgPy[prev] * scale; }
                        else
                        { p0x = (_lgPx[prev] + _lgPx[curr]) * 0.5f * scale + ox; p0y = oy2 - (_lgPy[prev] + _lgPy[curr]) * 0.5f * scale; }

                        float p2x, p2y;
                        if ((_lgFlags[next] & 1) != 0)
                        { p2x = _lgPx[next] * scale + ox; p2y = oy2 - _lgPy[next] * scale; }
                        else
                        { p2x = (_lgPx[curr] + _lgPx[next]) * 0.5f * scale + ox; p2y = oy2 - (_lgPy[curr] + _lgPy[next]) * 0.5f * scale; }

                        // برای فونت‌های بزرگ ST=12 نقاط بیشتری برای انحنای نرم‌تر
                        const int ST = 12;
                        for (int s = 0; s <= ST && totalPP < maxPP; s++)
                        {
                            float t = s / (float)ST, mt = 1f - t;
                            _lgSpx[totalPP] = mt * mt * p0x + 2f * mt * t * ax2 + t * t * p2x;
                            _lgSpy[totalPP] = mt * mt * p0y + 2f * mt * t * ay2 + t * t * p2y;
                            totalPP++;
                        }
                    }
                }
                _lgEnds[c] = totalPP - 1;
                startPt = end + 1;
            }

            // ─── scanline fill — نتیجه مستقیم در _lgBmp (static buffer) ────────
            int bmpSize = bW * bH;
            for (int i = 0; i < bmpSize; i++) _lgBmp[i] = 0;
            LgScanlineFill(bW, bH, nc, totalPP);

            // _lgBmp حاوی نتیجه است — بدون heap allocation
            result.Valid = true;
            result.W = bW;
            result.H = bH;
            result.OY = (int)(yMax * scale);
            return result;
        }

        // ─── LG_SS: تعداد زیر-اسکن‌لاین عمودی — هم‌تراز با GlyphAtlas.COV_MAX
        // تا سطوح coverage تولیدشده اینجا دقیقاً با همان مقیاس ۰..COV_MAX
        // که LargeGlyphAtlas.BlitSlot برای تبدیل به آلفا انتظار دارد جور شود.
        private const int LG_SS = GlyphAtlas.COV_MAX;

        // ─── Scanline fill با AA خاکستری واقعی — همان ترفند ScanlineFill
        // اصلی در TtfFont.cs (supersampling عمودی LG_SS زیر-اسکن‌لاین +
        // پوشش کسری افقی واقعی هر span)، فقط با بافرهای اختصاصی _lg* تا
        // با مسیر اصلی (RasterizerPool) تداخلی نداشته باشد. قبلاً اینجا هر
        // ردیف با یک نمونه‌ی تکی در مرکز (y+0.5) پر می‌شد و مرزها با (int)
        // بریده می‌شدند — دقیقاً همان چیزی که در نسخه‌های اولیه‌ی اطلس اصلی
        // باعث پله‌ای دیده شدن لبه‌های مورب/منحنی می‌شد.
        private static void LgScanlineFill(int w, int h, int nc, int tot)
        {
            int rowW = Math.Min(w, _lgRowCov.Length);

            for (int y = 0; y < h; y++)
            {
                for (int i2 = 0; i2 < rowW; i2++) _lgRowCov[i2] = 0f;

                for (int sub = 0; sub < LG_SS; sub++)
                {
                    float fy = y + (sub + 0.5f) / LG_SS;
                    int xc = 0, s = 0;
                    for (int c = 0; c < nc; c++)
                    {
                        int e = _lgEnds[c];
                        for (int i = s; i <= e; i++)
                        {
                            int nx = (i == e) ? s : i + 1;
                            float ay = _lgSpy[i], by = _lgSpy[nx];
                            if ((ay <= fy && by > fy) || (by <= fy && ay > fy))
                                if (xc < _lgXs.Length)
                                    _lgXs[xc++] = _lgSpx[i] + (fy - ay) / (by - ay) * (_lgSpx[nx] - _lgSpx[i]);
                        }
                        s = e + 1;
                    }

                    // insertion sort
                    for (int a = 1; a < xc; a++)
                    {
                        float v = _lgXs[a]; int b2 = a - 1;
                        while (b2 >= 0 && _lgXs[b2] > v) { _lgXs[b2 + 1] = _lgXs[b2]; b2--; }
                        _lgXs[b2 + 1] = v;
                    }

                    // انباشت پوشش کسری افقی این زیر-اسکن‌لاین
                    for (int p = 0; p + 1 < xc; p += 2)
                    {
                        float xf0 = _lgXs[p], xf1 = _lgXs[p + 1];
                        if (xf1 <= 0f || xf0 >= rowW) continue;
                        if (xf0 < 0f) xf0 = 0f;
                        if (xf1 > rowW) xf1 = rowW;
                        if (xf1 <= xf0) continue;

                        int ix0 = (int)xf0;
                        int ix1 = (int)xf1;
                        if (ix1 >= rowW) ix1 = rowW - 1;

                        if (ix0 >= ix1)
                        {
                            _lgRowCov[ix0] += (xf1 - xf0);
                        }
                        else
                        {
                            _lgRowCov[ix0] += (ix0 + 1 - xf0);
                            for (int xx = ix0 + 1; xx < ix1; xx++)
                                _lgRowCov[xx] += 1f;
                            _lgRowCov[ix1] += (xf1 - ix1);
                        }
                    }
                }

                // جمع LG_SS زیر-اسکن‌لاین → گرد به سطح صحیح (۰..LG_SS)
                int rowBase = y * w;
                for (int xx = 0; xx < rowW; xx++)
                {
                    int cov = (int)(_lgRowCov[xx] + 0.5f);
                    if (cov > LG_SS) cov = LG_SS;
                    else if (cov < 0) cov = 0;
                    _lgBmp[rowBase + xx] = (byte)cov;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DrawLarge — رندر متن با فونت بزرگ از طریق RenderSystem
        //
        //  • گلیف‌ها را در LargeGlyphAtlas cache می‌کند
        //  • هر گلیف با BlitColorized در back-buffer کشیده می‌شود
        //  • رنگ از penColor گرفته می‌شود (R,G,B)
        //  • canvas.Display() را صدا نمی‌زند — GraphicsManager باید بزند
        //
        //  فراخوانی بعد از RenderSystem.Flush() و قبل از canvas.Display()
        // ════════════════════════════════════════════════════════════════════
        public void DrawLarge(int sizePx, string text, int colorArgb, int x, int y)
        {
            if (string.IsNullOrEmpty(text)) return;
            EnsureCmap();

            float scale = sizePx / (float)_unitsPerEm;
            int baseline = y + (int)(_ascender * scale);
            int curX = x;

            for (int i = 0; i < text.Length; i++)
            {
                int cp = (int)text[i];
                int slot = GetOrRasterizeLargeAtlas(cp, sizePx);
                if (slot < 0) { curX += sizePx / 2; continue; }

                int drawY = baseline - LargeGlyphAtlas.GetOY(slot);
                LargeGlyphAtlas.BlitSlot(slot, curX, drawY, colorArgb);
                curX += LargeGlyphAtlas.GetAdvW(slot);
            }
        }

        // ─── MeasureLarge — اندازه‌گیری عرض متن بزرگ ───────────────────────
        public int MeasureLarge(int sizePx, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            EnsureCmap();
            int w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                int slot = GetOrRasterizeLargeAtlas((int)text[i], sizePx);
                if (slot >= 0)
                    w += LargeGlyphAtlas.GetAdvW(slot);
                else
                    w += sizePx / 2;
            }
            return w;
        }

        // ─── BlitColorized — گلیف سفید را با رنگ دلخواه در back-buffer رسم کن
        // pixels[i] == 0xFFFFFFFF → پیکسل on → رنگ colorArgb رسم شود
        // pixels[i] == 0          → پیکسل off → رد شود
        private static void BlitColorized(int[] pixels, int gW, int gH, int dx, int dy, int colorArgb)
        {
            // رنگ نهایی: alpha=255، RGB از colorArgb
            int solidColor = colorArgb | unchecked((int)0xFF000000u);

            int screenW = RenderSystem.ScreenW;
            int screenH = RenderSystem.ScreenH;

            int x0 = Math.Max(dx, 0);
            int y0 = Math.Max(dy, 0);
            int x1 = Math.Min(dx + gW, screenW);
            int y1 = Math.Min(dy + gH, screenH);
            if (x0 >= x1 || y0 >= y1) return;

            for (int row = y0; row < y1; row++)
            {
                int srcRow = (row - dy) * gW + (x0 - dx);
                for (int col = x0; col < x1; col++)
                {
                    if (pixels[srcRow + (col - x0)] != 0)
                        RenderSystem.SetPixel(col, row, solidColor);
                }
            }
        }

        // ─── InvalidateLargeCache — اکنون LargeGlyphAtlas کش را مدیریت می‌کند ─
        public void InvalidateLargeCache() { /* LargeGlyphAtlas LRU خودکار evict می‌کند */ }
    }
}