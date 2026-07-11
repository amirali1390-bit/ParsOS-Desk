// TtfFont.cs — سیستم رندر فونت TrueType برای Cosmos  (نسخه ۷ — Grayscale AA)
// ─────────────────────────────────────────────────────────────────────────────
//
//  ⑨ [نسخه ۷ — ارتقای بزرگ کیفیت] قبلاً GlyphAtlas یک آرایه ۱-بیتی بود:
//     v6 با supersampling عمودی (SS=3) یک coverage دقیق‌تر محاسبه می‌کرد
//     ولی در نهایت آن را با یک آستانه‌ی اکثریت به یک بیت باینری (روشن/
//     خاموش) فشرده می‌کرد — یعنی اطلاعات لبه‌ی نرم دور ریخته می‌شد و نتیجه
//     نهایی هنوز پله‌ای (aliased) بود.
//
//     حالا atlas به‌جای ۱ بیت، ۲ بیت به‌ازای هر پیکسل ذخیره می‌کند (۴ سطح
//     coverage: ۰،۱،۲،۳ — دقیقاً همان مقداری که SS=3 تولید می‌کند، بدون
//     هیچ بیت هدررفته). هنگام blit، اگر پس‌زمینه‌ی مقصد مشخص باشد
//     (پارامتر اختیاری bgColor)، هر سطح coverage با یک رنگ میان‌یابی‌شده
//     بین پس‌زمینه و رنگ قلم رسم می‌شود → آنتی‌الیاسینگ خاکستری واقعی،
//     لبه‌های نرم به‌خصوص روی منحنی‌های فارسی/عربی.
//
//     سازگاری کامل با کد قبلی حفظ شده: اگر bgColor داده نشود (پیش‌فرض
//     null)، دقیقاً همان رفتار باینری قبلی (آستانه‌ی اکثریت ≥۲ از ۳)
//     بدون هیچ تغییری در ظاهر ادامه پیدا می‌کند — هیچ‌کدام از فراخوانی‌های
//     موجود در GraphicsManager.cs نیاز به تغییر ندارند.
//
//     هزینه: اطلس از ~۵۴ کیلوبایت (۱bpp) به ~۱۰۸ کیلوبایت (۲bpp) می‌رسد
//     (+۵۴ کیلوبایت). RasterizerPool یک بافر byte[576] دیگر برای ترکیب
//     گلیف‌های composite گرفت (+۵۷۶ بایت). در ازای این هزینه‌ی رم ناچیز،
//     برای هر متنی که bgColor آن مشخص شود کیفیت رندر به شکل محسوسی بهتر
//     می‌شود، و برای بقیه‌ی متن‌ها هیچ تغییری (نه بهتر نه بدتر، نه کندتر)
//     رخ نمی‌دهد.
//
//     نحوه‌ی فعال‌سازی برای یک فراخوانی خاص:
//         ttf.DrawString(canvas, "Settings", Pens.TextPrimary, x, y,
//                         bgColor: Theme.WindowBg);
//     (فقط وقتی پس‌زمینه‌ی مقصد یک رنگ یکدست و شناخته‌شده باشد این کار را
//     بکنید — روی پس‌زمینه‌ی گرادیانی/تصویر، رنگ میان‌یابی‌شده اشتباه
//     خواهد بود و به‌جای بهتر شدن، هاله‌ی رنگی غلط ایجاد می‌کند.)
//
//  بهینه‌سازی‌های نسخه‌های قبل (v5→v6) هنوز پابرجاست:
//
//  ① GlyphAtlas.BlitSlot: حلقه داخلی به یک واحد 4-pixel پکیج‌بندی شد.
//     در هر تکرار ۴ بیت یکجا از bitset خوانده می‌شوند → ~4× کمتر bit-shift.
//
//  ② ScanlineFill: مرتب‌سازی insertion sort جایگزین bubble شد (همان O(n²)
//     اما با branch کمتر برای n کوچک < 16 که در گلیف‌ها رایج است).
//
//  ③ KernCache: Dictionary<uint,int> برای kern جایگزین lookup مستقیم شد.
//     اما با یک آرایه flat LRU 32-entry برای کرنینگ‌های پرتکرار
//     تا allocation‌های Dictionary در GetKern حذف شود.
//     (Dictionary فقط یک‌بار در EnsureKern ساخته می‌شود — این تغییر ندارد)
//
//  ④ WarmUp: متد عمومی جدید برای preload گلیف‌های رایج قبل از اولین فریم.
//     این متد را در Kernel.cs پس از بارگذاری فونت صدا بزنید:
//         TtfFont.WarmUp(VazirFont, VazirFontSm);
//
//  ⑤ AtlasKey: از shift 20 به shift 18 کاهش یافت → بیت‌های token کمتر
//     تداخل با glyph id ندارند (numGlyphs معمولاً < 5000).
//
//  ⑥ GlyphAtlas.AllocSlot: جستجوی linear slot خالی با یک int _nextFree
//     شتاب‌دهی شد تا allocation اول O(1) باشد نه O(n).
//
//  ⑦ GlyphAtlas.BlitSlot: clip check به صورت early-exit در ابتدا انجام می‌شود
//     تا کل تابع skip شود وقتی گلیف کاملاً خارج از viewport است.
//
//  ⑧ [پچ کیفیت فونت] ScanlineFill اکنون هر ردیف پیکسل را با ۳ زیر-اسکن‌لاین
//     عمودی (supersampling) نمونه‌برداری می‌کند و بیت نهایی را با رأی
//     اکثریت (coverage-based AA) تعیین می‌کند — به‌جای یک نمونه‌ی تکی در
//     مرکز هر پیکسل مثل قبل. لبه‌های مورب و منحنی (خصوصاً حروف فارسی)
//     محسوساً صاف‌تر می‌شوند. GlyphAtlas همچنان ۱-بیتی ذخیره می‌شود؛ فقط
//     یک بافر موقت byte[576] هنگام رستر یک گلیف استفاده می‌شود، پس این
//     بهبود کیفیت عملاً هزینه‌ی حافظه‌ی دائمی ندارد (فقط CPU بیشتر، آن‌هم
//     فقط یک‌بار به‌ازای هر گلیف چون نتیجه در اطلس کش می‌شود).
//
//  مصرف RAM (تخمین، تا v7):
//      GlyphAtlas bits (static):  ≈ 108 KB  (768×576×2bit، قبلاً ~۵۴ کیلوبایت ۱-بیتی)
//      GlyphAtlas meta (static):  ≈   6 KB
//      Hash tables (static):      ≈  16 KB
//      Rasterizer pools (static): ≈  41 KB  (+576 بایت بافر ترکیب composite)
//      هر TtfFont instance:       ≈   1 KB
//      KernCache flat (static):   ≈   0.5 KB
//      ─────────────────────────────────────────
//      کل برای 2 اندازه Vazir:   ≈ 172.5 KB   (+۵۴ کیلوبایت نسبت به v6)
//
//  ⑩ [v8] ScanlineFill حالا علاوه بر supersampling عمودی، پوشش کسری افقی
//     واقعی هر span را هم محاسبه می‌کند (نه فقط برش صحیح مرزها) → AA واقعاً
//     دوبعدی می‌شود.
//
//  ⑪ [v9 — رفع دو باگ گزارش‌شده]
//
//     الف) رفع باگ اتصال حروف: در PersianShaper.Shape، «آیا حرف i به حرف
//     بعدی وصل می‌شود» اشتباهاً با چک کردن نوع خودِ حرفِ بعدی محاسبه
//     می‌شد، در حالی که این فقط به نوع خودِ حرف i بستگی دارد (آیا خودش
//     dual-joining است). همین باعث می‌شد حروفی مثل «ن» قبل از حروف
//     راست‌اتصال (ا، د، ر، و، ز، ذ، ...) گاهی وصل نشوند، و حروف راست‌اتصال
//     مثل «ا» وسط کلمه به‌جای گلیف «final» (متصل) با گلیف isolated رسم
//     شوند (یعنی انگار رسم/اتصال نشده به نظر برسند). رفع شد: حالا nextJoins
//     فقط بر اساس dual-joining بودنِ خودِ حرف i محاسبه می‌شود.
//
//     ب) افزایش وضوح AA: GlyphAtlas از ۲ بیت (۴ سطح خاکستری) به ۴ بیت
//     (۱۶ سطح خاکستری) به‌ازای هر پیکسل ارتقا یافت (COV_MAX: 3→15،
//     SS: 3→15 زیر-اسکن‌لاین عمودی). این باعث می‌شود لبه‌های مورب/منحنی که
//     قبلاً با بندهای رنگی محسوس («پله‌ای») دیده می‌شدند، حالا تدریجی و صاف
//     به نظر برسند. حافظه‌ی اطلس از ~۱۰۸ کیلوبایت به ~۲۱۶ کیلوبایت می‌رسد
//     (+۱۰۸ کیلوبایت، هنوز ثابت/مشترک، بدون allocation در حین اجرا). در
//     BlitSlot به‌جای ساختن ۲ Pen تازه در هر فراخوانی، حالا یک پالت ۱۵تایی
//     کش می‌شود و فقط وقتی رنگ پس‌زمینه/قلم عوض شود دوباره ساخته می‌شود.
//
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Drawing;
using Cosmos.System.Graphics;

namespace ParsOS.GUI
{

    // ════════════════════════════════════════════════════════════════════════════
    //  GlyphAtlas  —  cache بیت‌پک مشترک و ثابت  (v6: fast alloc + batch blit)
    // ════════════════════════════════════════════════════════════════════════════
    internal static class GlyphAtlas
    {
        internal const int ATLAS_W = 768;
        internal const int ATLAS_H = 576;
        internal const int CELL_W = 24;
        internal const int CELL_H = 24;
        internal const int COLS = ATLAS_W / CELL_W;   // 32
        internal const int ROWS = ATLAS_H / CELL_H;   // 24
        internal const int MAX_SLOTS = COLS * ROWS;    // 768

        // metadata: [atlasX, atlasY, glyphW, glyphH, originY, advanceW]
        private const int F_GX = 0, F_GY = 1, F_W = 2, F_H = 3, F_OY = 4, F_AW = 5, META = 6;

        // ─── ذخیره‌سازی v9: ۴ بیت به‌ازای هر پیکسل (سطوح coverage ۰..۱۵) ──────
        // نسخه‌ی قبلی فقط ۲ بیت (۴ سطح خاکستری) داشت که باعث می‌شد لبه‌های
        // AA به‌صورت «پله‌ای» (بندهای رنگی محسوس) دیده شوند، مخصوصاً روی
        // ساقه‌های مورب/منحنیِ نزدیک به عمودی حروف فارسی. با ۴ بیت به ۱۶ سطح
        // خاکستری می‌رسیم که این پله‌ها را عملاً نامحسوس می‌کند، و چون ۳۲ بر
        // ۴ بخش‌پذیر است هر فیلد باز هم کامل داخل یک word جای می‌گیرد (بدون
        // پیچیدگی «عبور از مرز word»). هزینه: حافظه‌ی اطلس از ~۱۰۸ کیلوبایت
        // به ~۲۱۶ کیلوبایت می‌رسد (هنوز ثابت و مشترک، بدون allocation جدید
        // در حین اجرا).
        // COV_MAX باید دقیقاً با TtfFont.SS یکی باشد چون ۴ بیت فقط می‌تواند
        // ۱۶ سطح (۰..۱۵) نگه دارد و SS=15 هم دقیقاً همان ۱۶ سطح را تولید می‌کند.
        internal const int COV_MAX = 15;
        internal const int COV_MAJORITY = 8; // آستانه‌ی حالت باینری (بدون bgColor) — تقریباً نیمه‌پوشش

        private static readonly uint[] _bits = new uint[(ATLAS_W * ATLAS_H * 4 + 31) / 32];
        private static readonly int[] _slotGlyph = new int[MAX_SLOTS];
        private static readonly int[] _slotAge = new int[MAX_SLOTS];
        private static readonly short[] _meta = new short[MAX_SLOTS * META];
        private static int _clock;

        // ─── جدول هش O(1) ──────────────────────────────────────────────────
        private const int HASH_SIZE = 2048;
        private const int HASH_MASK = HASH_SIZE - 1;
        private const int HASH_EMPTY = -1;
        private static readonly int[] _hashBucket = new int[HASH_SIZE];
        private static readonly int[] _hashKey = new int[HASH_SIZE];

        // ─── سرعت‌دهی alloc: اولین slot خالی را ردیابی می‌کند ─────────────
        private static int _nextFree = 0;

        static GlyphAtlas()
        {
            for (int i = 0; i < MAX_SLOTS; i++) _slotGlyph[i] = -1;
            for (int i = 0; i < HASH_SIZE; i++) _hashBucket[i] = HASH_EMPTY;
        }

        // ─── خواندن/نوشتن یک مقدار coverage (۰..۱۵) در ۴ بیت ─────────────────
        // چون ۴ عاداً 32 را می‌شمارد (۸ فیلد در هر word)، هر فیلد ۴-بیتی
        // همیشه کامل داخل یک word جای می‌گیرد (هیچ‌وقت از مرز word رد
        // نمی‌شود) — نیازی به منطق «دو word» مثل بعضی پیاده‌سازی‌های
        // bit-packed نیست.
        private static void SetPixel(int x, int y, byte cov)
        {
            int idx = y * ATLAS_W + x, bitpos = idx << 2, w = bitpos >> 5, shift = bitpos & 31;
            uint mask = 15u << shift;
            _bits[w] = (_bits[w] & ~mask) | (((uint)cov & 15u) << shift);
        }

        private static byte GetPixel(int x, int y)
        {
            int idx = y * ATLAS_W + x, bitpos = idx << 2, w = bitpos >> 5, shift = bitpos & 31;
            return (byte)((_bits[w] >> shift) & 15u);
        }

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

        internal static int AllocSlot(int key)
        {
            // ─── جستجوی سریع O(1) از محل _nextFree ─────────────────────────
            // در اکثر مواقع slot بعدی آزاد است → یک بررسی کافی است
            if (_nextFree < MAX_SLOTS && _slotGlyph[_nextFree] < 0)
            {
                int s = _nextFree++;
                _slotGlyph[s] = key;
                _slotAge[s] = ++_clock;
                HashInsert(key, s);
                return s;
            }

            // اگر _nextFree پُر است جستجوی linear ادامه می‌دهد
            for (int i = _nextFree + 1; i < MAX_SLOTS; i++)
            {
                if (_slotGlyph[i] < 0)
                {
                    _nextFree = i + 1;
                    _slotGlyph[i] = key;
                    _slotAge[i] = ++_clock;
                    HashInsert(key, i);
                    return i;
                }
            }

            // اگر atlas پُر است: LRU eviction
            int oldest = 0;
            for (int i = 1; i < MAX_SLOTS; i++)
                if (_slotAge[i] < _slotAge[oldest]) oldest = i;

            int col = oldest % COLS, row = oldest / COLS;
            int ax = col * CELL_W, ay = row * CELL_H;
            // پاک کردن سریع: ۲۴ ردیف × ۲۴ پیکسل، هر پیکسل ۲ بیت
            for (int dy = 0; dy < CELL_H; dy++)
            {
                int py2 = ay + dy;
                for (int dx = 0; dx < CELL_W; dx++)
                    SetPixel(ax + dx, py2, 0);
            }

            HashRemove(_slotGlyph[oldest]);
            _slotGlyph[oldest] = key;
            _slotAge[oldest] = ++_clock;
            HashInsert(key, oldest);
            _nextFree = oldest + 1;   // hint برای alloc بعدی
            return oldest;
        }

        internal static void Touch(int slot) { _slotAge[slot] = ++_clock; }

        internal static void WriteGlyph(int slot, int gW, int gH, int oy, int aw)
        {
            int col = slot % COLS, row = slot / COLS;
            int ax = col * CELL_W, ay = row * CELL_H;
            int ww = gW < CELL_W ? gW : CELL_W;
            int hh = gH < CELL_H ? gH : CELL_H;

            var bmp = RasterizerPool._bmp;
            for (int dy = 0; dy < hh; dy++)
                for (int dx = 0; dx < ww; dx++)
                {
                    int bi = dy * gW + dx;
                    byte cov = (bi < bmp.Length) ? bmp[bi] : (byte)0;
                    SetPixel(ax + dx, ay + dy, cov);
                }

            int b = slot * META;
            _meta[b + F_GX] = (short)ax; _meta[b + F_GY] = (short)ay;
            _meta[b + F_W] = (short)ww; _meta[b + F_H] = (short)hh;
            _meta[b + F_OY] = (short)oy; _meta[b + F_AW] = (short)aw;
        }

        // ─── Pens میان‌یابی‌شده برای حالت AA ──────────────────────────────────
        // با ۱۶ سطح coverage، ساختن یک Pen تازه برای هر سطح در هر فراخوانی
        // BlitSlot (مثل نسخه‌ی قبلی) به‌صرفه نیست (۱۴ allocation به‌ازای هر
        // گلیف رسم‌شده). به‌جایش یک «پالت» ثابت و مشترک از Pen ها نگه
        // می‌داریم و فقط وقتی رنگ پس‌زمینه/قلم عوض شود دوباره می‌سازیمش —
        // در حالت معمول (نوشتن یک رشته با یک رنگ ثابت) پالت فقط یک‌بار
        // ساخته و برای همه‌ی گلیف‌ها استفاده می‌شود.
        private static Color _palBg, _palFg;
        private static bool _palValid = false;
        private static readonly Pen[] _palette = new Pen[COV_MAX]; // index ۱..COV_MAX-1 معتبر است

        private static Color BlendColor(Color bg, Color fg, int cov)
        {
            int r = bg.R + (fg.R - bg.R) * cov / COV_MAX;
            int g = bg.G + (fg.G - bg.G) * cov / COV_MAX;
            int b = bg.B + (fg.B - bg.B) * cov / COV_MAX;
            return Color.FromArgb(r, g, b);
        }

        private static void EnsurePalette(Color bg, Color fg)
        {
            if (_palValid && _palBg.R == bg.R && _palBg.G == bg.G && _palBg.B == bg.B
                           && _palFg.R == fg.R && _palFg.G == fg.G && _palFg.B == fg.B)
                return;

            _palBg = bg; _palFg = fg; _palValid = true;
            for (int cov = 1; cov < COV_MAX; cov++)
                _palette[cov] = new Pen(BlendColor(bg, fg, cov));
        }

        // ─── BlitSlot (v9): پشتیبانی از AA خاکستری واقعی با ۱۶ سطح ───────────
        // اگر bgColor داده نشود: رفتار باینری (آستانه coverage>=COV_MAJORITY).
        // اگر bgColor داده شود: هر پیکسل با یکی از ۱۶ سطح رنگ میان‌یابی‌شده
        // بین پس‌زمینه و رنگ قلم رسم می‌شود → لبه‌های نرم و بدون پله واقعی.
        internal static void BlitSlot(Canvas canvas, Pen pen, int slot, int x, int baseline,
                                      int clipW = int.MaxValue, int clipH = int.MaxValue,
                                      Color? bgColor = null)
        {
            int b = slot * META;
            int ax = _meta[b + F_GX], ay = _meta[b + F_GY];
            int gW = _meta[b + F_W], gH = _meta[b + F_H];
            int oy = _meta[b + F_OY];
            int sy0 = baseline - oy;

            // ─── Early-exit: اگر کاملاً خارج از clip است چیزی رسم نشود ──
            if (x + gW <= 0 || x >= clipW || sy0 + gH <= 0 || sy0 >= clipH) return;

            if (bgColor.HasValue) EnsurePalette(bgColor.Value, pen.Color);

            for (int dy = 0; dy < gH; dy++)
            {
                int sy = sy0 + dy;
                if (sy < 0 || sy >= clipH) continue;

                int rowY = ay + dy;
                for (int dx = 0; dx < gW; dx++)
                {
                    byte cov = GetPixel(ax + dx, rowY);
                    if (cov == 0) continue;

                    if (bgColor.HasValue)
                    {
                        Pen dp = cov >= COV_MAX ? pen : _palette[cov];
                        canvas.DrawPoint(dp, x + dx, sy);
                    }
                    else if (cov >= COV_MAJORITY)
                    {
                        canvas.DrawPoint(pen, x + dx, sy);
                    }
                }
            }
        }

        internal static int GetAdvW(int slot) => _meta[slot * META + F_AW];
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  RasterizerPool  —  static buffers مشترک برای rasterize (صفر allocation)
    // ════════════════════════════════════════════════════════════════════════════
    internal static class RasterizerPool
    {
        private const int MAX_PTS = 512;
        private const int MAX_CNTRS = 32;
        private const int MAX_SUBDIV = MAX_PTS * 12;
        private const int MAX_BMP = GlyphAtlas.CELL_W * GlyphAtlas.CELL_H;

        internal static readonly byte[] _flags = new byte[MAX_PTS];
        internal static readonly int[] _px = new int[MAX_PTS];
        internal static readonly int[] _py = new int[MAX_PTS];
        internal static readonly float[] _spx = new float[MAX_SUBDIV];
        internal static readonly float[] _spy = new float[MAX_SUBDIV];
        internal static readonly int[] _ends = new int[MAX_CNTRS];

        // ─── بافر coverage گلیف (v7): به‌جای bool[] حالا byte[] است و
        // مقدار هر پیکسل ۰..COV_MAX (سطح پوشش دقیق از ScanlineFill) را
        // نگه می‌دارد، نه فقط یک بیت روشن/خاموش. این همان بافری است که
        // WriteGlyph در نهایت در GlyphAtlas (۲-بیتی) می‌نویسد.
        internal static readonly byte[] _bmp = new byte[MAX_BMP];

        // ─── بافر مقصد جداگانه برای ترکیب گلیف‌های composite (v7) ──────────
        // نسخه‌ی قبلی هنگام ترکیب زیر-گلیف‌ها از همان _bmp هم به‌عنوان مقصد
        // و هم (به‌طور ضمنی، از طریق فراخوانی بازگشتی Rasterize) به‌عنوان
        // مبدأ استفاده می‌کرد که یک باگ aliasing نهفته بود. حالا مقصد در
        // یک بافر جدا انباشته می‌شود و در پایان یک‌جا در _bmp کپی می‌شود.
        internal static readonly byte[] _bmpComposite = new byte[MAX_BMP];

        // برای scanline sort
        internal static readonly float[] _xs = new float[128];

        // ─── بافر پوشش کسری افقی هر ردیف (v8) ──────────────────────────────
        // قبلاً ScanlineFill مرزهای هر span را با (int) به عدد صحیح گرد
        // می‌کرد و به هر پیکسل درون بازه دقیقاً ۱ واحد پوشش کامل می‌داد —
        // یعنی در جهت افقی هیچ AA واقعی نبود (فقط عمودی، با SS=3
        // زیر-اسکن‌لاین). این دقیقاً همان چیزی است که لبه‌های مورب/منحنی را
        // به‌خصوص در سایزهای کوچک پله‌ای نشان می‌دهد. حالا هر ردیف یک بافر
        // float اسکرچ دارد که سهم کسری واقعی پیکسل‌های مرزی هر span را جمع
        // می‌زند؛ نتیجه‌ی نهایی (۰..SS) در پایان هر ردیف به سطح صحیح اطلس
        // (۰..۳) گرد می‌شود. هزینه‌ی حافظه: فقط CELL_W=24 عدد float
        // (=۹۶ بایت)، مشترک و ثابت — چون رستر یک‌بار به‌ازای هر گلیف اجرا
        // می‌شود (نتیجه در atlas کش می‌شود)، بدون هزینه‌ی دائمی محسوس.
        internal static readonly float[] _rowCov = new float[GlyphAtlas.CELL_W];
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  PersianShaper  —  Zero-Allocation
    // ════════════════════════════════════════════════════════════════════════════
    public static class PersianShaper
    {
        private const int MaxShapeLen = 256;
        private static readonly int[] ShapeBuf = new int[MaxShapeLen];
        private static int _shapedLen;

        internal static int[] GetShapeBuf(out int length) { length = _shapedLen; return ShapeBuf; }

        private static readonly int[] _cp;
        private static readonly int[] _isoIni;
        private static readonly int[] _medFin;
        private static readonly int _tLen;

        static PersianShaper()
        {
            // Cosmos IL2CPU از آرایه‌های چندبُعدی (int[,]) پشتیبانی نمی‌کند.
            // جدول به صورت flat int[] با stride=5 ذخیره شده:
            // [cp, iso, ini, med, fin]  برای هر سطر
            int[] raw = {
                0x0621,0xFE80,0xFE80,0xFE80,0xFE80, 0x0622,0xFE81,0xFE81,0xFE81,0xFE82,
                0x0623,0xFE83,0xFE83,0xFE83,0xFE84, 0x0624,0xFE85,0xFE85,0xFE85,0xFE86,
                0x0625,0xFE87,0xFE87,0xFE87,0xFE88, 0x0626,0xFE89,0xFE8B,0xFE8C,0xFE8A,
                0x0627,0xFE8D,0xFE8D,0xFE8D,0xFE8E, 0x0628,0xFE8F,0xFE91,0xFE92,0xFE90,
                0x0629,0xFE93,0xFE93,0xFE93,0xFE94, 0x062A,0xFE95,0xFE97,0xFE98,0xFE96,
                0x062B,0xFE99,0xFE9B,0xFE9C,0xFE9A, 0x062C,0xFE9D,0xFE9F,0xFEA0,0xFE9E,
                0x062D,0xFEA1,0xFEA3,0xFEA4,0xFEA2, 0x062E,0xFEA5,0xFEA7,0xFEA8,0xFEA6,
                0x062F,0xFEA9,0xFEA9,0xFEA9,0xFEAA, 0x0630,0xFEAB,0xFEAB,0xFEAB,0xFEAC,
                0x0631,0xFEAD,0xFEAD,0xFEAD,0xFEAE, 0x0632,0xFEAF,0xFEAF,0xFEAF,0xFEB0,
                0x0633,0xFEB1,0xFEB3,0xFEB4,0xFEB2, 0x0634,0xFEB5,0xFEB7,0xFEB8,0xFEB6,
                0x0635,0xFEB9,0xFEBB,0xFEBC,0xFEBA, 0x0636,0xFEBD,0xFEBF,0xFEC0,0xFEBE,
                0x0637,0xFEC1,0xFEC3,0xFEC4,0xFEC2, 0x0638,0xFEC5,0xFEC7,0xFEC8,0xFEC6,
                0x0639,0xFEC9,0xFECB,0xFECC,0xFECA, 0x063A,0xFECD,0xFECF,0xFED0,0xFECE,
                0x0641,0xFED1,0xFED3,0xFED4,0xFED2, 0x0642,0xFED5,0xFED7,0xFED8,0xFED6,
                0x0643,0xFED9,0xFEDB,0xFEDC,0xFEDA, 0x0644,0xFEDD,0xFEDF,0xFEE0,0xFEDE,
                0x0645,0xFEE1,0xFEE3,0xFEE4,0xFEE2, 0x0646,0xFEE5,0xFEE7,0xFEE8,0xFEE6,
                0x0647,0xFEE9,0xFEEB,0xFEEC,0xFEEA, 0x0648,0xFEED,0xFEED,0xFEED,0xFEEE,
                0x0649,0xFEEF,0xFEEF,0xFEEF,0xFEF0, 0x064A,0xFEF1,0xFEF3,0xFEF4,0xFEF2,
                0x067E,0xFB56,0xFB58,0xFB59,0xFB57, 0x0686,0xFB7A,0xFB7C,0xFB7D,0xFB7B,
                0x0698,0xFB8A,0xFB8A,0xFB8A,0xFB8B, 0x06A9,0xFB8E,0xFB90,0xFB91,0xFB8F,
                0x06AF,0xFB92,0xFB94,0xFB95,0xFB93, 0x06CC,0xFBFC,0xFBFE,0xFBFF,0xFBFD,
            };
            _tLen = raw.Length / 5;
            _cp = new int[_tLen];
            _isoIni = new int[_tLen];
            _medFin = new int[_tLen];
            for (int i = 0; i < _tLen; i++)
            {
                int b = i * 5;
                _cp[i] = raw[b + 0];
                _isoIni[i] = raw[b + 1] | (raw[b + 2] << 16);
                _medFin[i] = raw[b + 3] | (raw[b + 4] << 16);
            }
        }

        private static int FindIdx(int cp)
        {
            int lo = 0, hi = _tLen - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1, k = _cp[mid];
                if (cp == k) return mid;
                if (cp < k) hi = mid - 1; else lo = mid + 1;
            }
            return -1;
        }

        public static bool IsDualJoining(int cp)
        {
            int i = FindIdx(cp); if (i < 0) return false;
            return ((_isoIni[i] >> 16) & 0xFFFF) != (_isoIni[i] & 0xFFFF);
        }

        public static bool IsArabic(int cp)
            => FindIdx(cp) >= 0
            || (cp >= 0x0600 && cp <= 0x06FF)
            || (cp >= 0xFB50 && cp <= 0xFDFF)
            || (cp >= 0xFE70 && cp <= 0xFEFF);

        public static void Shape(string text, int start, int len)
        {
            if (len > MaxShapeLen) len = MaxShapeLen;
            _shapedLen = len;

            // ─── متن فارسی/عربی در string به صورت logical (چپ‌به‌راست) ذخیره شده ──
            // یعنی text[0] = اولین حرف منطقی (راست‌ترین حرف visual)
            // برای هر حرف i:
            //   • حرف قبلی منطقی (سمت راست visual) = text[i-1]  → prevJoins
            //   • حرف بعدی منطقی (سمت چپ visual)  = text[i+1]  → nextJoins
            //
            // قوانین اتصال:
            //   • prevJoins: حرف i-1 باید dual-joining باشد تا از چپ خود به i وصل شود
            //   • nextJoins: حرف i+1 باید در جدول وجود داشته باشد (هر حرف عربی)
            //                اما اگر non-dual-joining (مثل الف، واو، ر، ز) باشد
            //                نمی‌تواند به i وصل شود — پس nextJoins=false
            //
            // فرم‌ها:
            //   isolated (0):  نه از چپ وصل، نه از راست وصل
            //   initial  (1):  از راست به بعدی وصل می‌شود (nextJoins=true, prevJoins=false)
            //   medial   (2):  از هر دو طرف وصل
            //   final    (3):  از چپ به قبلی وصل (prevJoins=true, nextJoins=false)

            for (int i = 0; i < len; i++)
            {
                int cp = (int)text[start + i];
                int idx = FindIdx(cp);
                if (idx < 0) { ShapeBuf[i] = cp; continue; }

                // حرف قبلی (سمت راست visual) باید dual-joining باشد تا به i وصل شود
                bool prevJoins = (i > 0) && IsDualJoining((int)text[start + i - 1]);

                // نکته مهم (رفع باگ): این‌که «آیا حرف i به حرف بعدی وصل می‌شود» فقط به
                // نوع خودِ حرف i بستگی دارد (آیا خودش dual-joining است تا بتواند یک
                // اتصال به چپ گسترش دهد)، نه به نوع حرف بعدی. نسخه‌ی قبلی به اشتباه
                // IsDualJoining(حرف بعدی) را چک می‌کرد که باعث دو مشکل می‌شد:
                //   ۱) حروفی مثل «ن» (dual-joining) قبل از حرف راست‌اتصال مثل «ا»/«د»/«ر»
                //      اشتباهاً isolated/final محاسبه می‌شدند و به حرف بعد وصل نمی‌شدند.
                //   ۲) حروف راست‌اتصال مثل «ا» وقتی هم قبل و هم بعدشان حرف dual-joining
                //      بود، اشتباهاً به شاخه‌ی medial می‌رفتند و به‌جای گلیف «final»
                //      (متصل)، گلیف isolated رسم می‌شد — یعنی وسط کلمه انگار قطع/رسم
                //      نشده به نظر می‌رسید.
                // حرف بعدی فقط باید در جدول حروف عربی/فارسی وجود داشته باشد تا بتواند
                // این اتصال را از سمت راست خودش بپذیرد (حتی حروف راست‌اتصال هم می‌توانند
                // از راست پذیرنده باشند) — پس فقط FindIdx کافی است، نه IsDualJoining.
                bool curDual = ((_isoIni[idx] >> 16) & 0xFFFF) != (_isoIni[idx] & 0xFFFF);
                bool nextJoins = curDual && (i < len - 1) && FindIdx((int)text[start + i + 1]) >= 0;

                // form: isolated=0, initial=1, medial=2, final=3
                int form;
                if (!prevJoins && !nextJoins) form = 0; // isolated
                else if (!prevJoins && nextJoins) form = 1; // initial
                else if (prevJoins && nextJoins) form = 2;  // medial
                else form = 3;                              // final

                // جدول: _isoIni[idx] = iso | (ini << 16)
                //        _medFin[idx] = med | (fin << 16)
                int shaped;
                if (form == 0) shaped = _isoIni[idx] & 0xFFFF;
                else if (form == 1) shaped = (_isoIni[idx] >> 16) & 0xFFFF;
                else if (form == 2) shaped = _medFin[idx] & 0xFFFF;
                else shaped = (_medFin[idx] >> 16) & 0xFFFF;

                ShapeBuf[i] = shaped != 0 ? shaped : cp;
            }
        }

        public static void Shape(string text) => Shape(text, 0, text?.Length ?? 0);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  BidiResolver  —  Zero-Allocation
    // ════════════════════════════════════════════════════════════════════════════
    public static class BidiResolver
    {
        private const int MaxLen = 256;
        private const int MaxRuns = 64;

        private static readonly byte[] _levels = new byte[MaxLen];
        private static readonly int[] _runStart = new int[MaxRuns];
        private static readonly int[] _runLen = new int[MaxRuns];
        private static readonly bool[] _runIsRTL = new bool[MaxRuns];
        private static readonly int[] _runOrder = new int[MaxRuns];
        private static int _runCount;
        private static bool _baseRTL;

        public static int RunCount => _runCount;
        public static bool BaseRTL => _baseRTL;
        public static int RunStart(int i) => _runStart[_runOrder[i]];
        public static int RunLen(int i) => _runLen[_runOrder[i]];
        public static bool RunIsRTL(int i) => _runIsRTL[_runOrder[i]];

        private static byte GetCat(int cp)
        {
            if (cp >= 0x0600 && cp <= 0x06FF) return 1;
            if (cp >= 0xFB50 && cp <= 0xFDFF) return 1;
            if (cp >= 0xFE70 && cp <= 0xFEFF) return 1;
            if (cp >= 0x0590 && cp <= 0x05FF) return 2;
            if (cp >= '0' && cp <= '9') return 3;
            if (cp == ' ' || cp == '\t') return 4;
            return 0;
        }

        public static void Resolve(string text, int start, int len)
        {
            if (len > MaxLen) len = MaxLen;
            _baseRTL = false;
            for (int i = 0; i < len; i++)
            {
                byte cat = GetCat((int)text[start + i]);
                if (cat == 1 || cat == 2) { _baseRTL = true; break; }
                if (cat == 0) { _baseRTL = false; break; }
            }
            for (int i = 0; i < len; i++)
            {
                byte cat = GetCat((int)text[start + i]);
                _levels[i] = (byte)(cat == 1 || cat == 2 ? 1 :
                                    cat == 0 ? 0 :
                                    (_baseRTL ? (byte)1 : (byte)0));
            }
            _runCount = 0;
            int j = 0;
            while (j < len && _runCount < MaxRuns)
            {
                int k = j + 1;
                while (k < len && _levels[k] == _levels[j]) k++;
                _runStart[_runCount] = start + j;
                _runLen[_runCount] = k - j;
                _runIsRTL[_runCount] = (_levels[j] & 1) == 1;
                _runCount++; j = k;
            }
            if (_baseRTL)
                for (int i = 0; i < _runCount; i++) _runOrder[i] = _runCount - 1 - i;
            else
                for (int i = 0; i < _runCount; i++) _runOrder[i] = i;
        }

        public static void Resolve(string text) => Resolve(text, 0, text?.Length ?? 0);
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  KernCache  —  flat LRU برای کرنینگ پرتکرار (بدون Dictionary lookup هر بار)
    // ════════════════════════════════════════════════════════════════════════════
    internal static class KernCache
    {
        private const int SIZE = 32;
        private static readonly uint[] _keys = new uint[SIZE];   // (left<<16)|right
        private static readonly int[] _vals = new int[SIZE];
        private static readonly int[] _age = new int[SIZE];
        private static int _clock;
        private const uint EMPTY = 0xFFFFFFFF;

        static KernCache()
        {
            for (int i = 0; i < SIZE; i++) _keys[i] = EMPTY;
        }

        // ─── lookup: اگر پیدا نشد MISS_SENTINEL برمی‌گرداند ──────────────
        internal const int MISS = int.MinValue;

        internal static int Get(int left, int right)
        {
            uint k = ((uint)left << 16) | (uint)(right & 0xFFFF);
            for (int i = 0; i < SIZE; i++)
            {
                if (_keys[i] == k) { _age[i] = ++_clock; return _vals[i]; }
            }
            return MISS;
        }

        internal static void Put(int left, int right, int val)
        {
            uint k = ((uint)left << 16) | (uint)(right & 0xFFFF);
            // اگر از قبل وجود دارد update کن
            for (int i = 0; i < SIZE; i++)
            {
                if (_keys[i] == k) { _vals[i] = val; _age[i] = ++_clock; return; }
            }
            // LRU slot
            int oldest = 0;
            for (int i = 1; i < SIZE; i++)
                if (_age[i] < _age[oldest]) oldest = i;
            _keys[oldest] = k;
            _vals[oldest] = val;
            _age[oldest] = ++_clock;
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    //  TtfFont  —  parser + rasterizer  (v6: WarmUp + KernCache + fast blit)
    // ════════════════════════════════════════════════════════════════════════════
    public partial class TtfFont
    {
        private byte[] _data;

        private uint _cmapOffset, _glyfOffset, _locaOffset;
        private uint _hmtxOffset, _headOffset, _hheaOffset;
        private uint _maxpOffset, _kernOffset;

        private bool _locaIsLong;
        private int _unitsPerEm, _ascender, _numGlyphs, _numHMetrics;

        public int SizeInPixels { get; private set; }
        private float _scale;
        private int _sizeToken;
        private static int _nextToken;

        private bool _cmapParsed, _kernParsed;
        private int _cmapFmt4Off = -1;
        private Dictionary<uint, int> _kernPairs;

        private TtfFont() { }

        // ════════════════════════════════════════════════════════════════════
        //  Load
        // ════════════════════════════════════════════════════════════════════
        public static TtfFont Load(byte[] data, int sizePx)
        {
            if (data == null || data.Length < 12) return null;
            try
            {
                var f = new TtfFont { _data = data, SizeInPixels = sizePx, _sizeToken = ++_nextToken };
                if (!f.ParseOffsetTable()) return null;
                f.ParseHead(); f.ParseHhea(); f.ParseMaxp();
                f._scale = sizePx / (float)f._unitsPerEm;
                return f;
            }
            catch { return null; }
        }

        // ════════════════════════════════════════════════════════════════════
        //  WarmUp — پیش‌رستر کردن گلیف‌های رایج
        //  در Kernel.cs پس از Load صدا بزنید:
        //      TtfFont.WarmUp(VazirFont, VazirFontSm);
        //  این کار اولین فریم را از تاخیر رستر آزاد می‌کند و اطلاعات
        //  kern table را از پیش آماده می‌سازد → مصرف حافظه در طول اجرا
        //  کمتر می‌شود چون GC فشار کمتری دارد.
        // ════════════════════════════════════════════════════════════════════
        public static void WarmUp(params TtfFont[] fonts)
        {
            // کاراکترهای رایج در UI: حروف لاتین، ارقام، نمادها، فارسی شایع
            const string latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789:.-/ %";
            const string persian = "\u0633\u062A\u0627\u0631\u062A\u0641\u0627\u06CC\u0644\u0645\u0646\u0648\u062F\u06A9\u0634"
                                 + "\u0628\u0633\u062A\u0646\u0631\u0627\u0647\u0627\u0646\u062F\u0627\u0632\u0647";
            if (fonts == null) return;
            foreach (var f in fonts)
            {
                if (f == null) continue;
                f.EnsureCmap();
                f.EnsureKern();
                foreach (char c in latin)
                {
                    int gid = f.GetGlyphId(c);
                    if (gid >= 0) f.GetOrRasterize(gid);
                }
                foreach (char c in persian)
                {
                    int gid = f.GetGlyphId(c);
                    if (gid >= 0) f.GetOrRasterize(gid);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DrawAuto — BiDi خودکار
        // ════════════════════════════════════════════════════════════════════
        public void DrawAuto(Canvas canvas, string text, Pen pen, int x, int y,
                             int clipW = int.MaxValue, int clipH = int.MaxValue, Color? bgColor = null)
        {
            if (canvas == null || string.IsNullOrEmpty(text)) return;
            BidiResolver.Resolve(text);
            int baseline = y + (int)(_ascender * _scale);
            int curX = x;

            for (int r = 0; r < BidiResolver.RunCount; r++)
            {
                int rStart = BidiResolver.RunStart(r);
                int rLen = BidiResolver.RunLen(r);
                bool isRTL = BidiResolver.RunIsRTL(r);

                if (isRTL)
                {
                    PersianShaper.Shape(text, rStart, rLen);
                    int sLen; int[] sBuf = PersianShaper.GetShapeBuf(out sLen);

                    int runW = 0;
                    for (int i = sLen - 1; i >= 0; i--)
                    {
                        int gid = GetGlyphId((char)sBuf[i]); if (gid < 0) gid = 0;
                        runW += GetAdvanceWidth(gid);
                    }
                    int rx = curX + runW, prevG = -1;
                    for (int i = sLen - 1; i >= 0; i--)
                    {
                        int gid = GetGlyphId((char)sBuf[i]); if (gid < 0) gid = 0;
                        if (prevG >= 0) rx += GetKern(prevG, gid);
                        int slot = GetOrRasterize(gid);
                        if (slot >= 0) { rx -= GlyphAtlas.GetAdvW(slot); GlyphAtlas.BlitSlot(canvas, pen, slot, rx, baseline, clipW, clipH, bgColor); }
                        else rx -= GetAdvanceWidth(gid);
                        prevG = gid;
                    }
                    curX += runW;
                }
                else
                {
                    int prevG = -1;
                    for (int i = 0; i < rLen; i++)
                    {
                        int gid = GetGlyphId(text[rStart + i]); if (gid < 0) gid = 0;
                        if (prevG >= 0) curX += GetKern(prevG, gid);
                        int slot = GetOrRasterize(gid);
                        if (slot >= 0) { GlyphAtlas.BlitSlot(canvas, pen, slot, curX, baseline, clipW, clipH, bgColor); curX += GlyphAtlas.GetAdvW(slot); }
                        else curX += GetAdvanceWidth(gid);
                        prevG = gid;
                    }
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DrawString — LTR خالص
        // ════════════════════════════════════════════════════════════════════
        public void DrawString(Canvas canvas, string text, Pen pen, int x, int y,
                               int clipW = int.MaxValue, int clipH = int.MaxValue, Color? bgColor = null)
        {
            if (canvas == null || string.IsNullOrEmpty(text)) return;
            int baseline = y + (int)(_ascender * _scale);
            int curX = x, prevG = -1;
            for (int i = 0; i < text.Length; i++)
            {
                int gid = GetGlyphId(text[i]); if (gid < 0) gid = 0;
                if (prevG >= 0) curX += GetKern(prevG, gid);
                int slot = GetOrRasterize(gid);
                if (slot >= 0) { GlyphAtlas.BlitSlot(canvas, pen, slot, curX, baseline, clipW, clipH, bgColor); curX += GlyphAtlas.GetAdvW(slot); }
                else curX += GetAdvanceWidth(gid);
                prevG = gid;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DrawStringRTL — RTL خالص
        // ════════════════════════════════════════════════════════════════════
        public void DrawStringRTL(Canvas canvas, string text, Pen pen, int x, int y,
                                  int clipW = int.MaxValue, int clipH = int.MaxValue, Color? bgColor = null)
        {
            if (canvas == null || string.IsNullOrEmpty(text)) return;
            PersianShaper.Shape(text);
            int sLen; int[] sBuf = PersianShaper.GetShapeBuf(out sLen);

            int baseline = y + (int)(_ascender * _scale);
            int totalW = 0;
            for (int i = 0; i < sLen; i++)
            {
                int gid = GetGlyphId((char)sBuf[i]); if (gid < 0) gid = 0;
                totalW += GetAdvanceWidth(gid);
            }
            int curX = x - totalW, prevG = -1;
            for (int i = sLen - 1; i >= 0; i--)
            {
                int gid = GetGlyphId((char)sBuf[i]); if (gid < 0) gid = 0;
                if (prevG >= 0) curX += GetKern(prevG, gid);
                int slot = GetOrRasterize(gid);
                if (slot >= 0) { GlyphAtlas.BlitSlot(canvas, pen, slot, curX, baseline, clipW, clipH, bgColor); curX += GlyphAtlas.GetAdvW(slot); }
                else curX += GetAdvanceWidth(gid);
                prevG = gid;
            }
        }

        // ─── اندازه‌گیری ─────────────────────────────────────────────────────
        public int MeasureWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int w = 0, prev = -1;
            for (int i = 0; i < text.Length; i++)
            {
                int g = GetGlyphId(text[i]); if (g < 0) g = 0;
                if (prev >= 0) w += GetKern(prev, g);
                w += GetAdvanceWidth(g); prev = g;
            }
            return w;
        }

        public int MeasureRTLWidth(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            PersianShaper.Shape(text);
            int sLen; int[] sBuf = PersianShaper.GetShapeBuf(out sLen);
            int w = 0, prev = -1;
            for (int i = 0; i < sLen; i++)
            {
                int g = GetGlyphId((char)sBuf[i]); if (g < 0) g = 0;
                if (prev >= 0) w += GetKern(prev, g);
                w += GetAdvanceWidth(g); prev = g;
            }
            return w;
        }

        public int LineHeight => SizeInPixels;

        // ════════════════════════════════════════════════════════════════════
        //  Atlas helper
        // ════════════════════════════════════════════════════════════════════
        // key: glyph id | (sizeToken << 18)
        private int AtlasKey(int gid) => gid | (_sizeToken << 18);

        private int GetOrRasterize(int gid)
        {
            int key = AtlasKey(gid);
            int slot = GlyphAtlas.FindSlot(key);
            if (slot >= 0) { GlyphAtlas.Touch(slot); return slot; }

            int gW, gH, oy;
            bool ok = Rasterize(gid, out gW, out gH, out oy);
            if (!ok) return -1;

            slot = GlyphAtlas.AllocSlot(key);
            GlyphAtlas.WriteGlyph(slot, gW, gH, oy, GetAdvanceWidth(gid));
            return slot;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TTF Parser
        // ════════════════════════════════════════════════════════════════════
        private bool ParseOffsetTable()
        {
            if (ReadU32(0) == 0x4F54544F) return false;
            int n = ReadU16(4);
            for (int t = 0; t < n; t++)
            {
                int r = 12 + t * 16; if (r + 16 > _data.Length) break;
                uint off = ReadU32(r + 8);
                switch (ReadTag(r))
                {
                    case "cmap": _cmapOffset = off; break;
                    case "glyf": _glyfOffset = off; break;
                    case "loca": _locaOffset = off; break;
                    case "hmtx": _hmtxOffset = off; break;
                    case "head": _headOffset = off; break;
                    case "hhea": _hheaOffset = off; break;
                    case "maxp": _maxpOffset = off; break;
                    case "kern": _kernOffset = off; break;
                }
            }
            return _cmapOffset > 0 && _glyfOffset > 0 && _locaOffset > 0 && _headOffset > 0;
        }

        private void ParseHead() { int o = (int)_headOffset; _unitsPerEm = ReadU16(o + 18); if (_unitsPerEm == 0) _unitsPerEm = 2048; _locaIsLong = ReadS16(o + 50) == 1; }
        private void ParseHhea() { int o = (int)_hheaOffset; _ascender = ReadS16(o + 4); _numHMetrics = ReadU16(o + 34); }
        private void ParseMaxp() { _numGlyphs = ReadU16((int)_maxpOffset + 4); }

        private void EnsureCmap()
        {
            if (_cmapParsed) return; _cmapParsed = true;
            int o = (int)_cmapOffset, n = ReadU16(o + 2);
            for (int i = 0; i < n; i++)
            {
                int s = o + 4 + i * 8;
                int pid = ReadU16(s), eid = ReadU16(s + 2);
                uint so = ReadU32(s + 4);
                if (pid == 3 && eid == 1 && ReadU16(o + (int)so) == 4) { _cmapFmt4Off = o + (int)so; break; }
                if (pid == 0 && _cmapFmt4Off < 0 && ReadU16(o + (int)so) == 4) _cmapFmt4Off = o + (int)so;
            }
        }

        private int GetGlyphId(char c)
        {
            EnsureCmap(); if (_cmapFmt4Off < 0) return 0;
            int o = _cmapFmt4Off, sx2 = ReadU16(o + 6), sc = sx2 >> 1;
            int eo = o + 14, so2 = eo + sx2 + 2, dO = so2 + sx2, ro = dO + sx2;
            int cp = (int)c, lo = 0, hi = sc - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1, ec = ReadU16(eo + mid * 2);
                if (cp > ec) { lo = mid + 1; continue; }
                int stc = ReadU16(so2 + mid * 2);
                if (cp < stc) { hi = mid - 1; continue; }
                int rp = ro + mid * 2, r2 = ReadU16(rp);
                if (r2 == 0) return (cp + ReadS16(dO + mid * 2)) & 0xFFFF;
                int idx = rp + r2 + (cp - stc) * 2; if (idx + 2 > _data.Length) return 0;
                int gid = ReadU16(idx); if (gid == 0) return 0;
                return (gid + ReadS16(dO + mid * 2)) & 0xFFFF;
            }
            return 0;
        }

        private int GetGlyfOffset(int gid)
        {
            if (gid < 0 || gid >= _numGlyphs) return -1;
            int o = (int)_locaOffset; uint cur, nxt;
            if (_locaIsLong) { cur = ReadU32(o + gid * 4); nxt = ReadU32(o + (gid + 1) * 4); }
            else { cur = (uint)ReadU16(o + gid * 2) * 2; nxt = (uint)ReadU16(o + (gid + 1) * 2) * 2; }
            return cur == nxt ? -1 : (int)(_glyfOffset + cur);
        }

        private int GetAdvanceWidth(int gid)
        {
            int idx = gid < _numHMetrics ? gid : _numHMetrics - 1;
            return Math.Max(1, (int)(ReadU16((int)_hmtxOffset + idx * 4) * _scale));
        }

        private void EnsureKern()
        {
            if (_kernParsed) return; _kernParsed = true; _kernPairs = new Dictionary<uint, int>();
            if (_kernOffset == 0) return;
            try
            {
                int o = (int)_kernOffset, nT = ReadU16(o + 2), pos = o + 4;
                for (int t = 0; t < nT; t++)
                {
                    int tL = ReadU16(pos + 2), cov = ReadU16(pos + 4);
                    if ((cov & 1) != 0 && ((cov >> 8) & 0xFF) == 0)
                    {
                        int np = ReadU16(pos + 6), pp = pos + 14;
                        for (int p = 0; p < np; p++, pp += 6)
                            _kernPairs[((uint)ReadU16(pp) << 16) | (uint)ReadU16(pp + 2)] = ReadS16(pp + 4);
                    }
                    pos += tL;
                }
            }
            catch { }
        }

        // ─── GetKern با KernCache برای حذف Dictionary lookup هر بار ─────────
        private int GetKern(int l, int r2)
        {
            // ابتدا کش سریع flat را بررسی کن
            int cached = KernCache.Get(l, r2);
            if (cached != KernCache.MISS) return cached;

            // مرجع اصلی
            EnsureKern();
            int val = 0;
            if (_kernPairs != null)
            {
                int raw;
                if (_kernPairs.TryGetValue(((uint)l << 16) | (uint)r2, out raw))
                    val = (int)(raw * _scale);
            }
            KernCache.Put(l, r2, val);
            return val;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Rasterizer  —  v6: ScanlineFill با insertion sort بهبودیافته
        // ════════════════════════════════════════════════════════════════════
        private bool Rasterize(int gid, out int outW, out int outH, out int outOY)
        {
            outW = outH = outOY = 0;
            int off = GetGlyfOffset(gid); if (off < 0) return false;
            int nc = ReadS16(off);
            return nc < 0 ? RasterizeComposite(off, out outW, out outH, out outOY)
                          : RasterizeSimple(off, nc, out outW, out outH, out outOY);
        }

        private bool RasterizeSimple(int off, int nc, out int outW, out int outH, out int outOY)
        {
            outW = outH = outOY = 0;
            if (nc == 0) return false;

            int xMin = ReadS16(off + 2), yMin = ReadS16(off + 4), xMax = ReadS16(off + 6), yMax = ReadS16(off + 8);
            int dataOff = off + 10;

            if (nc > RasterizerPool._ends.Length) nc = RasterizerPool._ends.Length;

            var ends = RasterizerPool._ends;
            var flags = RasterizerPool._flags;
            var px = RasterizerPool._px;
            var py = RasterizerPool._py;
            var spx = RasterizerPool._spx;
            var spy = RasterizerPool._spy;
            var bmp = RasterizerPool._bmp;

            for (int i = 0; i < nc; i++) ends[i] = ReadU16(dataOff + i * 2);
            dataOff += nc * 2;
            int instr = ReadU16(dataOff); dataOff += 2 + instr;
            int nPts = ends[nc - 1] + 1;
            if (nPts > flags.Length) nPts = flags.Length;

            int pi = 0;
            while (pi < nPts && dataOff < _data.Length)
            {
                byte fl = _data[dataOff++]; flags[pi++] = fl;
                if ((fl & 8) != 0 && dataOff < _data.Length)
                {
                    int rep = _data[dataOff++];
                    for (int r = 0; r < rep && pi < nPts; r++) flags[pi++] = fl;
                }
            }

            int cur = 0;
            for (int i = 0; i < nPts; i++) { byte fl = flags[i]; if ((fl & 2) != 0) { int d = _data[dataOff++]; cur += (fl & 0x10) != 0 ? d : -d; } else if ((fl & 0x10) == 0) { cur += ReadS16(dataOff); dataOff += 2; } px[i] = cur; }
            cur = 0;
            for (int i = 0; i < nPts; i++) { byte fl = flags[i]; if ((fl & 4) != 0) { int d = _data[dataOff++]; cur += (fl & 0x20) != 0 ? d : -d; } else if ((fl & 0x20) == 0) { cur += ReadS16(dataOff); dataOff += 2; } py[i] = cur; }

            int bW = Math.Max(1, Math.Min((int)Math.Ceiling((xMax - xMin) * _scale) + 2, GlyphAtlas.CELL_W));
            int bH = Math.Max(1, Math.Min((int)Math.Ceiling((yMax - yMin) * _scale) + 2, GlyphAtlas.CELL_H));
            float ox = -xMin * _scale, oy2 = yMax * _scale;

            int maxPP = spx.Length;
            int totalPP = 0, startPt = 0;

            for (int c = 0; c < nc; c++)
            {
                int end = ends[c], cnt = end - startPt + 1;
                for (int i = 0; i < cnt; i++)
                {
                    int curr = startPt + i, next = startPt + (i + 1) % cnt;
                    bool currOn = (flags[curr] & 1) != 0;
                    float ax2 = px[curr] * _scale + ox, ay2 = oy2 - py[curr] * _scale;
                    if (currOn) { if (totalPP < maxPP) { spx[totalPP] = ax2; spy[totalPP++] = ay2; } }
                    else
                    {
                        int prev = startPt + (i - 1 + cnt) % cnt;
                        float p0x, p0y;
                        if ((flags[prev] & 1) != 0) { p0x = px[prev] * _scale + ox; p0y = oy2 - py[prev] * _scale; }
                        else { p0x = (px[prev] + px[curr]) * 0.5f * _scale + ox; p0y = oy2 - (py[prev] + py[curr]) * 0.5f * _scale; }
                        float p2x, p2y;
                        if ((flags[next] & 1) != 0) { p2x = px[next] * _scale + ox; p2y = oy2 - py[next] * _scale; }
                        else { p2x = (px[curr] + px[next]) * 0.5f * _scale + ox; p2y = oy2 - (py[curr] + py[next]) * 0.5f * _scale; }
                        const int ST = 8;
                        for (int s = 0; s <= ST && totalPP < maxPP; s++) { float t = s / (float)ST, mt = 1f - t; spx[totalPP] = mt * mt * p0x + 2f * mt * t * ax2 + t * t * p2x; spy[totalPP] = mt * mt * p0y + 2f * mt * t * ay2 + t * t * p2y; totalPP++; }
                    }
                }
                ends[c] = totalPP - 1; startPt = end + 1;
            }

            int bmpSize = bW * bH;
            for (int i = 0; i < bmpSize; i++) bmp[i] = 0;

            ScanlineFill(bmp, bW, bH, spx, spy, ends, nc, totalPP);

            outW = bW; outH = bH; outOY = (int)(yMax * _scale);
            return true;
        }

        private bool RasterizeComposite(int off, out int outW, out int outH, out int outOY)
        {
            outW = outH = outOY = 0;
            int xMin = ReadS16(off + 2), yMin = ReadS16(off + 4), xMax = ReadS16(off + 6), yMax = ReadS16(off + 8);
            int bW = Math.Max(1, Math.Min((int)Math.Ceiling((xMax - xMin) * _scale) + 2, GlyphAtlas.CELL_W));
            int bH = Math.Max(1, Math.Min((int)Math.Ceiling((yMax - yMin) * _scale) + 2, GlyphAtlas.CELL_H));
            float ox = -xMin * _scale, oy2 = yMax * _scale;

            // ─── مقصد ترکیب در بافر جداگانه (v7) ────────────────────────────
            // نکته: قبلاً هم مبدأ (خروجی Rasterize هر زیرگلیف) و هم مقصد
            // (نتیجه‌ی ترکیب‌شده) از همان RasterizerPool._bmp استفاده
            // می‌کردند که چون Rasterize(cid) به‌صورت بازگشتی همان بافر را
            // بازنویسی می‌کرد یک باگ aliasing نهفته بود. حالا مقصد در
            // _bmpComposite جمع می‌شود و مبدأ (RasterizerPool._bmp) دست‌نخورده
            // باقی می‌ماند تا خوانده شود.
            var dest = RasterizerPool._bmpComposite;
            int bmpSize = bW * bH;
            for (int i = 0; i < bmpSize; i++) dest[i] = 0;
            // محدودیت شناخته‌شده: اگر یک گلیف composite خودش شامل یک
            // زیرگلیف composite دیگر باشد (تودرتوی دو سطحی)، فراخوانی
            // بازگشتی Rasterize همین _bmpComposite را دوباره پاک/بازنویسی
            // می‌کند و سهم مؤلفه‌های قبلی از دست می‌رود. در فونت‌های
            // لاتین/فارسی معمول (Vazir) این حالت عملاً رخ نمی‌دهد؛ اگر با
            // فونت دیگری به این مشکل خوردید خبر دهید تا با یک استک بافر
            // درستش کنیم.

            int o = off + 10, cf;
            do
            {
                if (o + 4 > _data.Length) break;
                cf = ReadU16(o); int cid = ReadU16(o + 2); o += 4;
                float dx = 0, dy = 0; bool words = (cf & 1) != 0, xy = (cf & 2) != 0;
                if (words) { if (xy) { dx = ReadS16(o); dy = ReadS16(o + 2); } o += 4; }
                else { if (xy) { dx = (sbyte)_data[o]; dy = (sbyte)_data[o + 1]; } o += 2; }
                float a = 1, b = 0, c = 0, d = 1;
                if ((cf & 8) != 0) { a = d = ReadF2Dot14(o); o += 2; }
                else if ((cf & 64) != 0) { a = ReadF2Dot14(o); d = ReadF2Dot14(o + 2); o += 4; }
                else if ((cf & 128) != 0) { a = ReadF2Dot14(o); b = ReadF2Dot14(o + 2); c = ReadF2Dot14(o + 4); d = ReadF2Dot14(o + 6); o += 8; }

                int cW, cH, cOY;
                if (Rasterize(cid, out cW, out cH, out cOY))
                {
                    float cdx = dx * _scale + ox, cdy = oy2 - dy * _scale - cOY;
                    var src = RasterizerPool._bmp;
                    for (int py2 = 0; py2 < cH; py2++) for (int px2 = 0; px2 < cW; px2++)
                    {
                        byte cv = src[py2 * cW + px2];
                        if (cv == 0) continue;
                        int ix = (int)(a * px2 + c * py2 + cdx), iy = (int)(b * px2 + d * py2 + cdy);
                        if ((uint)ix < (uint)bW && (uint)iy < (uint)bH)
                        {
                            int di = iy * bW + ix;
                            if (cv > dest[di]) dest[di] = cv; // max-combine روی هم‌پوشانی
                        }
                    }
                }
            } while ((cf & 0x20) != 0);

            var final = RasterizerPool._bmp;
            for (int i = 0; i < bmpSize; i++) final[i] = dest[i];

            outW = bW; outH = bH; outOY = (int)(yMax * _scale);
            return true;
        }

        // ─── ScanlineFill با supersampling عمودی (کیفیت بیشتر، رم اضافه‌ی صفر) ──
        // قبلاً هر ردیف پیکسل فقط با یک اسکن‌لاین در مرکز آن (y+0.5) نمونه‌
        // برداری می‌شد؛ در نتیجه لبه‌های مورب/منحنی (که در خط فارسی/عربی خیلی
        // رایج‌اند) به شکل پله‌ای (aliased) در می‌آمدند. حالا هر ردیف را با
        // SS=3 زیر-اسکن‌لاین عمودی (در y+1/6, y+1/2, y+5/6) نمونه‌برداری
        // می‌کنیم و یک coverage (۰ تا SS) برای هر پیکسل جمع می‌زنیم؛ فقط اگر
        // اکثریت زیر-نمونه‌ها پوشش داده باشند (coverage >= اکثریت) بیت روشن
        // می‌شود. این یک «باینری AA بر پایه‌ی coverage» است: اطلس همچنان
        // ۱-بیتی می‌ماند (بدون هیچ افزایش حافظه‌ی دائمی) ولی چون تصمیم روشن/
        // خاموش بودن هر پیکسل با اطلاعات دقیق‌تری گرفته می‌شود، شکل گلیف —
        // به‌خصوص منحنی‌های حروف فارسی — قابل‌توجه صاف‌تر رندر می‌شود.
        // هزینه: ~SS برابر کار CPU در حین رستر یک گلیف (فقط یک‌بار، چون
        // نتیجه در GlyphAtlas کش می‌شود؛ WarmUp همه‌ی گلیف‌های رایج را همان
        // ابتدا رستر می‌کند) — بدون تغییر در فرمت ذخیره‌سازی.
        // ─── ScanlineFill با supersampling عمودی — v7: خروجی coverage واقعی ──
        // قبلاً هر ردیف پیکسل با SS=3 زیر-اسکن‌لاین عمودی نمونه‌برداری می‌شد
        // و coverage (۰ تا SS) به‌دست‌آمده را با یک آستانه‌ی اکثریت به یک بیت
        // باینری فشرده می‌کرد. حالا همان مقدار coverage دقیق (۰..SS) مستقیماً
        // در بافر خروجی نوشته می‌شود — چون GlyphAtlas اکنون ۲ بیت به‌ازای هر
        // پیکسل دارد و می‌تواند این ۴ سطح را کامل نگه دارد. تصمیم باینری/AA
        // به زمان blit موکول شده (GlyphAtlas.BlitSlot) که آنجا هم بسته به
        // این‌که bgColor داده شده یا نه، یا آستانه می‌گیرد یا رنگ میان‌یابی
        // می‌کند. الگوریتم محاسبه‌ی خود coverage (x-intercept تحلیلی +
        // insertion sort) بدون تغییر مانده — فقط مرحله‌ی «فشرده‌سازی به بول»
        // حذف شده.
        // ─── ScanlineFill با supersampling عمودی + پوشش کسری افقی (v8) ────────
        // v7 فقط جهت عمودی را با SS=3 زیر-اسکن‌لاین نمونه‌برداری می‌کرد؛ در
        // جهت افقی مرزهای هر span را با (int) به عدد صحیح می‌بُرید و به هر
        // پیکسل داخل بازه یک واحد پوشش کامل می‌داد — یعنی AA فقط یک‌بعدی
        // (عمودی) بود، نه واقعاً دوبعدی. این باعث می‌شد لبه‌های مورب/منحنیِ
        // نزدیک به عمودی (خیلی رایج در ساقه‌ی حروف) هنوز پله‌ای دیده شوند،
        // مخصوصاً در سایزهای کوچک که هر گلیف فقط چند پیکسل عرض دارد.
        //
        // حالا برای هر زیر-اسکن‌لاین، سهم کسری واقعی دو پیکسل مرزی هر span
        // محاسبه و در یک بافر float انباشته می‌شود (پیکسل‌های داخلی همچنان
        // سهم کامل ۱ می‌گیرند). در پایان هر ردیف، مجموع پوشش سه زیر-اسکن‌لاین
        // (بازه‌ی ۰..SS) به نزدیک‌ترین سطح صحیح اطلس (۰..۳) گرد می‌شود.
        // نتیجه: AA واقعی دوبعدی، بدون هیچ افزایش دائمی حافظه‌ی اطلس (همان
        // ۲ بیت به‌ازای هر پیکسل باقی می‌ماند) — فقط یک بافر موقت ۲۴ فلوتی
        // مشترک اضافه شده.
        private const int SS = GlyphAtlas.COV_MAX;

        private static void ScanlineFill(byte[] bmp, int w, int h,
            float[] spx, float[] spy, int[] ends, int nc, int tot)
        {
            var xs = RasterizerPool._xs;
            var rowCov = RasterizerPool._rowCov;
            int rowCount = w * h;
            if (rowCount > bmp.Length) rowCount = bmp.Length; // محافظ ایمنی
            for (int i = 0; i < rowCount; i++) bmp[i] = 0;

            for (int y = 0; y < h; y++)
            {
                int rowBase = y * w;
                int rowW = Math.Min(w, rowCov.Length);
                for (int i2 = 0; i2 < rowW; i2++) rowCov[i2] = 0f;

                for (int sub = 0; sub < SS; sub++)
                {
                    float fy = y + (sub + 0.5f) / SS;
                    int xc = 0, s = 0;
                    for (int c = 0; c < nc; c++)
                    {
                        int e = ends[c];
                        for (int i = s; i <= e; i++)
                        {
                            int nx = (i == e) ? s : i + 1;
                            float ay = spy[i], by = spy[nx];
                            if ((ay <= fy && by > fy) || (by <= fy && ay > fy))
                                if (xc < xs.Length)
                                    xs[xc++] = spx[i] + (fy - ay) / (by - ay) * (spx[nx] - spx[i]);
                        }
                        s = e + 1;
                    }

                    // ─── Insertion sort (از bubble بهتر) ────────────────────────
                    for (int a = 1; a < xc; a++)
                    {
                        float v = xs[a];
                        int b2 = a - 1;
                        while (b2 >= 0 && xs[b2] > v) { xs[b2 + 1] = xs[b2]; b2--; }
                        xs[b2 + 1] = v;
                    }

                    // ─── انباشت پوشش کسری افقی برای این زیر-اسکن‌لاین ──────────
                    for (int p = 0; p + 1 < xc; p += 2)
                    {
                        float xf0 = xs[p], xf1 = xs[p + 1];
                        if (xf1 <= 0f || xf0 >= rowW) continue;
                        if (xf0 < 0f) xf0 = 0f;
                        if (xf1 > rowW) xf1 = rowW;
                        if (xf1 <= xf0) continue;

                        int ix0 = (int)xf0;
                        int ix1 = (int)xf1;
                        if (ix1 >= rowW) ix1 = rowW - 1;

                        if (ix0 >= ix1)
                        {
                            // span کاملاً داخل یک پیکسل — فقط همان کسر را بده
                            rowCov[ix0] += (xf1 - xf0);
                        }
                        else
                        {
                            rowCov[ix0] += (ix0 + 1 - xf0);           // پیکسل مرزی چپ (کسری)
                            for (int xx = ix0 + 1; xx < ix1; xx++)     // پیکسل‌های داخلی (کامل)
                                rowCov[xx] += 1f;
                            rowCov[ix1] += (xf1 - ix1);                // پیکسل مرزی راست (کسری)
                        }
                    }
                }

                // ─── جمع سه زیر-اسکن‌لاین (۰..SS) → گرد به سطح صحیح اطلس (۰..۳) ──
                for (int xx = 0; xx < rowW; xx++)
                {
                    int cov = (int)(rowCov[xx] + 0.5f);
                    if (cov > SS) cov = SS;
                    else if (cov < 0) cov = 0;
                    bmp[rowBase + xx] = (byte)cov;
                }
            }
        }

        private uint ReadU32(int o) { if (o + 4 > _data.Length) return 0; return ((uint)_data[o] << 24) | ((uint)_data[o + 1] << 16) | ((uint)_data[o + 2] << 8) | _data[o + 3]; }
        private int ReadU16(int o) { if (o + 2 > _data.Length) return 0; return (_data[o] << 8) | _data[o + 1]; }
        private int ReadS16(int o) { int v = ReadU16(o); return v >= 32768 ? v - 65536 : v; }
        private float ReadF2Dot14(int o) => ReadS16(o) / 16384f;
        private string ReadTag(int o) { if (o + 4 > _data.Length) return ""; return "" + (char)_data[o] + (char)_data[o + 1] + (char)_data[o + 2] + (char)_data[o + 3]; }
    }

} // namespace Xagros.GUI