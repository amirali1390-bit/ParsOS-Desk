// AppInterpreterWasm.cs
//
// ✅ تکمیل‌شده: این نسخه decoder باینری WASM و ماشین اجرا (VM) را به‌طور کامل
// پیاده می‌کند (زیرمجموعه‌ی MVP: i32 arithmetic، control flow کامل block/loop/if/
// else/br/br_if، حافظه‌ی خطی با bounds-check و memory.grow، توابع/importها،
// globalهای ساده). طبق درخواست، این فایل هنوز به هیچ‌جای دیگری از سیستم
// (Kernel، GraphicsManager) وصل نشده — فقط خودِ مفسر کامل شده است.
//
// 🔌 اتصال واقعی: PapAppRuntime.cs (کنار همین فایل) حالا این موتور را به یک
// پنجره‌ی واقعی وصل می‌کند — یک WasmAppInstance به‌ازای هر پنجره‌ی برنامه‌ی
// .pap، با importهای draw_text/draw_rect/read_file که مستقیم به بومِ همان
// پنجره و پوشه‌ی sandbox خودش محدودند. برای آن اتصال، این فایل یک متد کوچک
// اضافه دارد: WasmInterpreter.WriteMemory (قرینه‌ی ReadUtf8String، برای
// importهایی مثل read_file که باید نتیجه را در حافظه‌ی خودِ اپ بنویسند).
//
// چرا WASM به‌جای Jint بهتر است برای کاسموس:
//   Jint یک مفسر tree-walking روی AST جاوااسکریپت است که به‌شدت به Reflection،
//   closures پویا، boxing/unboxing، و گاهی Expression Trees وابسته است — این‌ها
//   دقیقاً همان چیزهایی هستند که زیر IL2CPU (کامپایلر AOT کاسموس، بدون JIT واقعی)
//   پرخطرترین‌اند و به plugs دستی نیاز دارند.
//   WASM برعکس، از پایه یک بایت‌کد ساده‌ی stack-machine است — یعنی «مفسر» آن در
//   عمل یک حلقه‌ی switch روی opcode هاست، بدون reflection، بدون dynamic dispatch،
//   بدون GC فشار زیاد. این با محیط Cosmos خیلی سازگارتر است.
//
// محدودیت‌های شناخته‌شده‌ی این MVP (عمداً پیاده نشده‌اند):
//   - بدون f32/f64 واقعی (فقط i32؛ مقادیر i64 هم فقط برای const/global به‌صورت
//     محدود جا می‌گیرند چون استک عملیاتی int است، نه long)
//   - بدون جدول/call_indirect واقعی (Table section پارس می‌شود ولی نادیده گرفته
//     می‌شود؛ call_indirect در زمان اجرا trap می‌کند)
//   - بدون multi-value (بلوک‌ها حداکثر یک نوع نتیجه دارند، توابع حداکثر یک
//     مقدار برمی‌گردانند — دقیقاً مطابق اسپک اصلی WASM MVP)
//   - بدون bulk-memory (memory.copy/fill) و بدون reference types
//   - importها فقط از نوع تابع پشتیبانی می‌شوند (import حافظه/جدول/global پارس
//     می‌شود تا استریم درست بماند ولی عملاً استفاده نمی‌شود)

using System;
using System.Collections.Generic;
using System.Text;

namespace ParsOS.Apps.Scripting
{
    // ═══════════════════════════════════════════════════════════
    //  مسیر پیشنهادی: هر برنامه یک فایل .wasm است (مثلاً 0:\Apps\*.wasm).
    //  اپ باید سه تابع export کند: init، update(dt: i32)، draw(). تمام
    //  ارتباط با «دنیای بیرون» (رسم، فایل، ...) از طریق importهایی است که
    //  خودمان در HostImportTable تعریف می‌کنیم — یعنی هیچ WASI کامل و
    //  هیچ syscall خامی در کار نیست، فقط یک ABI حداقلی و امن.
    // ═══════════════════════════════════════════════════════════
    public sealed class WasmAppInstance
    {
        public string ModulePath;
        public bool Faulted;
        public string LastError;

        private WasmModule _module;
        private WasmInterpreter _vm;

        public WasmAppInstance(string path) => ModulePath = path;

        // ─── بارگذاری و اعتبارسنجی باینری .wasm ────────────────────────
        public bool Load(byte[] wasmBytes, HostImportTable hostApi)
        {
            try
            {
                _module = WasmModule.Parse(wasmBytes);

                // حافظه‌ی خطی سقف سخت دارد (پیش‌فرض ۲ مگابایت) که هرگز از سقف
                // سیستم بیشتر نمی‌شود؛ اگر خودِ ماژول min pages بیشتری بخواهد،
                // WasmInterpreter در سازنده‌اش Trap می‌کند (Load آن را می‌گیرد).
                _vm = new WasmInterpreter(_module, hostApi, maxMemoryBytes: 2 * 1024 * 1024);

                if (_module.HasExport("init"))
                {
                    // قبلاً این فراخوانی هیچ محافظت سوختی نداشت — یعنی یک init()
                    // خراب (حلقه‌ی بی‌نهایت) کل سیستم‌عامل تک‌رشته‌ای را برای
                    // همیشه فریز می‌کرد، نه فقط همین اپ را trap می‌کرد.
                    _vm.ResetFuel(200_000);
                    _vm.CallExport("init");
                }

                return true;
            }
            catch (WasmTrapException ex)
            {
                Faulted = true;
                LastError = "trap: " + ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                Faulted = true;
                LastError = ex.Message;
                return false;
            }
        }

        public void Update(int dtMs)
        {
            if (Faulted || _module == null || !_module.HasExport("update")) return;
            SafeCall("update", dtMs);
        }

        public void Draw()
        {
            if (Faulted || _module == null || !_module.HasExport("draw")) return;
            SafeCall("draw");
        }

        // on_click(x, y) — اختیاری؛ اپ‌هایی که تعاملی نیستند (مثلاً یک
        // ساعت یا انیمیشن) لازم نیست این export را داشته باشند.
        public void Click(int x, int y)
        {
            if (Faulted || _module == null || !_module.HasExport("on_click")) return;
            SafeCall("on_click", x, y);
        }

        private void SafeCall(string export, params int[] args)
        {
            try
            {
                // هر فراخوانی سوخت (fuel/instruction budget) جدا دارد — حلقه‌ی
                // بی‌نهایت داخل update() یک اپ خراب نباید بقیه‌ی OS کوآپریتیو
                // را فریز کند.
                _vm.ResetFuel(200_000);
                _vm.CallExport(export, args);
            }
            catch (WasmTrapException ex)
            {
                // trap یعنی خود ماشین WASM یک خطای غیرقابل‌بازیافت گزارش داده
                // (out-of-bounds memory access، div by zero، fuel تمام‌شده، ...)
                // — طبق طراحی باید فقط همین اپ/پنجره بسته شود.
                Faulted = true;
                LastError = "trap: " + ex.Message;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  HostImportTable — تنها دریچه‌ای که کد WASM از آن به سیستم دسترسی دارد.
    //  چون WASM فقط عدد صحیح/اعشاری رد و بدل می‌کند (نه string/object)، رشته‌ها
    //  با قرارداد (ptr, len) داخل حافظه‌ی خطیِ همان ماژول پاس داده می‌شوند —
    //  یعنی هر امپورت باید خودش از WasmInterpreter.ReadUtf8String بخواند.
    //
    //  ⚠️ پیاده‌سازی واقعی این importها (اتصال به GraphicsManager/Canvas واقعی)
    //  عمداً همچنان TODO است — طبق درخواست، فعلاً به جایی وصل نمی‌کنیم؛ فقط
    //  خودِ موتور WASM (parser + VM) کامل شده تا این importها بتوانند واقعاً
    //  فراخوانی شوند وقتی آماده‌ی اتصال شدیم.
    // ═══════════════════════════════════════════════════════════
    public sealed class HostImportTable
    {
        public delegate long HostFn(WasmInterpreter vm, int[] args);

        private readonly Dictionary<string, HostFn> _fns = new Dictionary<string, HostFn>();

        public void Register(string name, HostFn fn) => _fns[name] = fn;

        public bool TryGet(string name, out HostFn fn) => _fns.TryGetValue(name, out fn);

        // ─── نمونه‌ی یک مجموعه‌ی import پایه — این‌جا فقط امضا مشخص است،
        // پیاده‌سازی واقعی (clip به کادر پنجره، اعتبارسنجی رنگ/مسیر) باید جدا
        // نوشته شود، دقیقاً مثل AppApi در نسخه‌ی قبلیِ Jint.
        public static HostImportTable CreateDefault(/* WindowInfo window, TtfFont font */)
        {
            var t = new HostImportTable();

            // print(ptr, len) — رشته را از حافظه‌ی ماژول می‌خواند
            t.Register("print", (vm, args) =>
            {
                string s = vm.ReadUtf8String(args[0], args[1]);
                // TODO: اتصال به کنسول/لاگ داخلی اپ
                return 0;
            });

            // draw_text(ptr, len, x, y, colorRgb)
            t.Register("draw_text", (vm, args) =>
            {
                string s = vm.ReadUtf8String(args[0], args[1]);
                int x = args[2], y = args[3], color = args[4];
                // TODO: clip به کادر پنجره‌ی همین اپ + فراخوانی canvas.DrawTtf واقعی
                return 0;
            });

            // draw_rect(x, y, w, h, colorRgb)
            t.Register("draw_rect", (vm, args) =>
            {
                // TODO
                return 0;
            });

            // read_file(pathPtr, pathLen, outBufPtr, outBufCap) → تعداد بایت خوانده‌شده یا -1
            t.Register("read_file", (vm, args) =>
            {
                // TODO: مسیر باید محدود به sandbox directory خودِ همین اپ باشد
                return -1;
            });

            return t;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  WasmModule — پارسر باینری .wasm (بخش‌های Type/Import/Function/Table/
    //  Memory/Global/Export/Start/Element/Code/Data). فقط MVP: بدون
    //  bulk-memory، بدون multi-value، بدون reference types.
    // ═══════════════════════════════════════════════════════════
    public sealed class WasmModule
    {
        public byte[] LinearMemoryInit;
        public int MemoryMinPages, MemoryMaxPages; // هر page = 64KB طبق اسپک
        public Dictionary<string, int> Exports = new Dictionary<string, int>(); // name → internal func index
        public List<WasmFunction> Functions = new List<WasmFunction>(); // فقط توابع داخلی (import نیستند)
        public List<WasmImport> Imports = new List<WasmImport>();       // فقط importهای نوع تابع
        public List<WasmFuncType> Types = new List<WasmFuncType>();
        public List<WasmGlobal> Globals = new List<WasmGlobal>();

        public const int PageSize = 64 * 1024;

        public bool HasExport(string name) => Exports.ContainsKey(name);

        public static WasmModule Parse(byte[] bytes)
        {
            var r = new WasmReader(bytes);
            var m = new WasmModule();

            // ۱) magic number + version
            uint magic = r.ReadU32Fixed();
            if (magic != 0x6D736100u) // "\0asm" little-endian
                throw new WasmParseException("magic number نامعتبر — فایل .wasm نیست");
            uint version = r.ReadU32Fixed();
            if (version != 1u)
                throw new WasmParseException("نسخه‌ی WASM پشتیبانی‌نشده: " + version);

            int lastSectionId = -1;
            while (!r.Eof)
            {
                byte id = r.ReadByte();
                uint size = r.ReadVarU32();
                int sectionStart = r.Position;

                // بخش‌های custom (id=0) می‌توانند هر جایی بیایند و ترتیب را رعایت
                // نمی‌کنند؛ بقیه باید صعودی باشند (طبق اسپک) — نقض این یعنی فایل
                // خراب/دستکاری‌شده است.
                if (id != 0)
                {
                    if (id <= lastSectionId)
                        throw new WasmParseException("ترتیب بخش‌های ماژول نامعتبر است (id=" + id + ")");
                    lastSectionId = id;
                }

                switch (id)
                {
                    case 0: // custom — نادیده گرفته می‌شود
                        r.ReadBytes((int)size);
                        break;
                    case 1: ParseTypeSection(r, m); break;
                    case 2: ParseImportSection(r, m); break;
                    case 3: ParseFunctionSection(r, m); break;
                    case 4: ParseTableSection(r, m); break;
                    case 5: ParseMemorySection(r, m); break;
                    case 6: ParseGlobalSection(r, m); break;
                    case 7: ParseExportSection(r, m); break;
                    case 8: r.ReadVarU32(); break; // start section — فقط شماره را می‌خوانیم، خودمان صریحاً init را صدا می‌زنیم
                    case 9: ParseElementSection(r, m); break;
                    case 10: ParseCodeSection(r, m); break;
                    case 11: ParseDataSection(r, m); break;
                    default:
                        // بخش ناشناخته/آینده — برای سازگاری رو به جلو فقط رد شویم
                        r.ReadBytes((int)size);
                        break;
                }

                if (r.Position - sectionStart != size)
                    throw new WasmParseException("طول بخش id=" + id + " با محتوای واقعی مطابقت ندارد (فایل خراب است)");
            }

            if (m.MemoryMinPages > 0 && m.LinearMemoryInit == null)
                m.LinearMemoryInit = new byte[(long)m.MemoryMinPages * PageSize];

            return m;
        }

        // ─── Type section: vec(functype); functype = 0x60 params results ──────
        private static void ParseTypeSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                byte form = r.ReadByte();
                if (form != 0x60)
                    throw new WasmParseException("functype نامعتبر (انتظار 0x60)");

                var ft = new WasmFuncType
                {
                    Params = ReadValTypeVec(r),
                    Results = ReadValTypeVec(r)
                };
                if (ft.Results.Length > 1)
                    throw new WasmParseException("multi-value (بیش از یک مقدار بازگشتی) در این MVP پشتیبانی نمی‌شود");
                m.Types.Add(ft);
            }
        }

        private static WasmValueType[] ReadValTypeVec(WasmReader r)
        {
            uint n = r.ReadVarU32();
            var arr = new WasmValueType[n];
            for (uint i = 0; i < n; i++) arr[i] = MapValType(r.ReadByte());
            return arr;
        }

        private static WasmValueType MapValType(byte b)
        {
            switch (b)
            {
                case 0x7F: return WasmValueType.I32;
                case 0x7E: return WasmValueType.I64;
                case 0x7D: return WasmValueType.F32;
                case 0x7C: return WasmValueType.F64;
                default: throw new WasmParseException("valtype ناشناخته: 0x" + b.ToString("X2"));
            }
        }

        // ─── Import section ────────────────────────────────────────────────────
        private static void ParseImportSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                string mod = r.ReadName();
                string name = r.ReadName();
                byte kind = r.ReadByte();
                switch (kind)
                {
                    case 0x00: // func
                        {
                            uint typeIdx = r.ReadVarU32();
                            if (typeIdx >= (uint)m.Types.Count)
                                throw new WasmParseException("typeidx نامعتبر در import تابع");
                            m.Imports.Add(new WasmImport { Module = mod, Name = name, TypeIndex = (int)typeIdx });
                        }
                        break;
                    case 0x01: // table — فقط پارس، نادیده گرفته می‌شود (بدون call_indirect واقعی)
                        r.ReadByte(); // elemtype
                        ReadLimits(r, out _, out _);
                        break;
                    case 0x02: // memory — MVP: حافظه‌ی import‌شده پشتیبانی نمی‌شود، فقط برای درستیِ استریم می‌خوانیم
                        ReadLimits(r, out _, out _);
                        break;
                    case 0x03: // global
                        r.ReadByte(); // valtype
                        r.ReadByte(); // mutability
                        break;
                    default:
                        throw new WasmParseException("نوع import ناشناخته: " + kind);
                }
            }
        }

        // از (int min, int max) ValueTuple عمداً استفاده نشده — هرچند در C#
        // مدرن مشکلی ندارد، ولی چون بقیه‌ی این پروژه زیر IL2CPU کامپایل
        // می‌شود و همان‌جا هرجا ممکن بوده از ساده‌ترین IL استفاده شده، همان
        // سبک out-parameter رعایت شده است.
        private static void ReadLimits(WasmReader r, out int min, out int max)
        {
            byte flag = r.ReadByte();
            min = (int)r.ReadVarU32();
            max = flag == 0x01 ? (int)r.ReadVarU32() : 0;
        }

        // ─── Function section: vec(typeidx) برای توابع داخلی ─────────────────
        private static void ParseFunctionSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                uint typeIdx = r.ReadVarU32();
                if (typeIdx >= (uint)m.Types.Count)
                    throw new WasmParseException("typeidx نامعتبر در function section");
                var ft = m.Types[(int)typeIdx];
                m.Functions.Add(new WasmFunction
                {
                    ParamTypes = ft.Params,
                    ReturnTypes = ft.Results,
                    ParamCount = ft.Params.Length
                });
            }
        }

        // ─── Table section — پارس می‌شود اما نادیده گرفته می‌شود (بدون جدول واقعی) ──
        private static void ParseTableSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                r.ReadByte(); // elemtype (0x70 = funcref)
                ReadLimits(r, out _, out _);
            }
        }

        // ─── Memory section: MVP فقط یک حافظه را می‌پذیرد ─────────────────────
        private static void ParseMemorySection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            if (count > 1)
                throw new WasmParseException("بیش از یک حافظه‌ی خطی در این MVP پشتیبانی نمی‌شود");
            for (uint i = 0; i < count; i++)
            {
                ReadLimits(r, out int min, out int max);
                m.MemoryMinPages = min;
                m.MemoryMaxPages = max;
            }
        }

        // ─── Global section: global = globaltype + init-expr + 0x0B ──────────
        private static void ParseGlobalSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                var type = MapValType(r.ReadByte());
                byte mutFlag = r.ReadByte();
                int init = ReadConstI32InitExpr(r);
                m.Globals.Add(new WasmGlobal { Type = type, Mutable = mutFlag == 0x01, InitValue = init });
            }
        }

        // فقط i32.const N به‌دنبال 0x0B پشتیبانی می‌شود (رایج‌ترین حالت واقعی).
        private static int ReadConstI32InitExpr(WasmReader r)
        {
            byte op = r.ReadByte();
            if (op != 0x41) // i32.const
                throw new WasmParseException("init-expr فقط i32.const را در این MVP پشتیبانی می‌کند (op=0x" + op.ToString("X2") + ")");
            int val = (int)r.ReadVarI32();
            byte end = r.ReadByte();
            if (end != 0x0B)
                throw new WasmParseException("init-expr باید با end (0x0B) تمام شود");
            return val;
        }

        // ─── Export section ────────────────────────────────────────────────────
        private static void ParseExportSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                string name = r.ReadName();
                byte kind = r.ReadByte();
                uint idx = r.ReadVarU32();
                if (kind == 0x00) // func
                {
                    int importFuncCount = m.Imports.Count;
                    if (idx < (uint)importFuncCount)
                        throw new WasmParseException("export مستقیم یک تابع import‌شده در این MVP پشتیبانی نمی‌شود: " + name);
                    int internalIdx = (int)idx - importFuncCount;
                    if (internalIdx < 0 || internalIdx >= m.Functions.Count)
                        throw new WasmParseException("funcidx نامعتبر در export section: " + name);
                    m.Exports[name] = internalIdx;
                }
                // kind های table/mem/global نادیده گرفته می‌شوند (خارج از دامنه‌ی MVP)
            }
        }

        // ─── Element section — بدون جدول واقعی، فقط برای درستیِ استریم پارس می‌شود ──
        private static void ParseElementSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                r.ReadVarU32(); // tableidx (معمولاً ۰)
                ReadConstI32InitExpr(r); // offset expr
                uint n = r.ReadVarU32();
                for (uint j = 0; j < n; j++) r.ReadVarU32(); // funcidx ها
            }
        }

        // ─── Code section: هر entry دقیقاً با Functions[i] مطابقت دارد ─────────
        private static void ParseCodeSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            if (count != (uint)m.Functions.Count)
                throw new WasmParseException("تعداد code entries با function section مطابقت ندارد");

            for (int i = 0; i < count; i++)
            {
                uint bodySize = r.ReadVarU32();
                int bodyStart = r.Position;

                var fn = m.Functions[i];

                // local declarations: vec(count:varuint32, type:byte)
                uint numDecls = r.ReadVarU32();
                var localTypes = new List<WasmValueType>();
                for (uint d = 0; d < numDecls; d++)
                {
                    uint cnt = r.ReadVarU32();
                    var t = MapValType(r.ReadByte());
                    for (uint k = 0; k < cnt; k++) localTypes.Add(t);
                }
                fn.LocalTypes = localTypes.ToArray();
                fn.LocalCount = localTypes.Count;

                fn.Body = DecodeInstructions(r, m);

                if (r.Position - bodyStart != bodySize)
                    throw new WasmParseException("طول بدنه‌ی تابع #" + i + " مطابقت ندارد (فایل خراب است)");
            }
        }

        // ─── Data section: memidx + offset-expr + vec(byte) ───────────────────
        private static void ParseDataSection(WasmReader r, WasmModule m)
        {
            uint count = r.ReadVarU32();
            for (uint i = 0; i < count; i++)
            {
                uint memIdx = r.ReadVarU32();
                if (memIdx != 0)
                    throw new WasmParseException("این MVP فقط memory index صفر را پشتیبانی می‌کند");
                int offset = ReadConstI32InitExpr(r);
                uint len = r.ReadVarU32();
                byte[] segment = r.ReadBytes((int)len);

                if (m.LinearMemoryInit == null)
                    m.LinearMemoryInit = new byte[(long)m.MemoryMinPages * PageSize];

                long end = (long)offset + segment.Length;
                if (offset < 0 || end > m.LinearMemoryInit.Length)
                    throw new WasmParseException("data segment خارج از محدوده‌ی حافظه‌ی min pages است");

                Array.Copy(segment, 0, m.LinearMemoryInit, offset, segment.Length);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Instruction decoding — یک تابع را به لیست تخت WasmInstruction تبدیل
        //  می‌کند و همزمان اهداف پرش (Target/Target2) برای block/loop/if/else
        //  را resolve می‌کند، تا حلقه‌ی اجرا هیچ‌وقت مجبور به اسکن مجدد نباشد.
        // ═══════════════════════════════════════════════════════════
        private static List<WasmInstruction> DecodeInstructions(WasmReader r, WasmModule m)
        {
            var list = new List<WasmInstruction>();
            var ctrlStack = new List<int>();           // ایندکس‌های Block/Loop/If باز
            var elseOfIf = new Dictionary<int, int>();  // ایندکس If → ایندکس Else متناظرش (اگر باشد)
            int importFuncCount = m.Imports.Count;

            while (true)
            {
                byte op = r.ReadByte();
                switch (op)
                {
                    case 0x00: list.Add(new WasmInstruction { Op = WasmOp.Unreachable }); break;
                    case 0x01: list.Add(new WasmInstruction { Op = WasmOp.Nop }); break;

                    case 0x02: // block
                        ReadBlockType(r);
                        ctrlStack.Add(list.Count);
                        list.Add(new WasmInstruction { Op = WasmOp.Block });
                        break;

                    case 0x03: // loop
                        ReadBlockType(r);
                        ctrlStack.Add(list.Count);
                        list.Add(new WasmInstruction { Op = WasmOp.Loop });
                        break;

                    case 0x04: // if
                        ReadBlockType(r);
                        ctrlStack.Add(list.Count);
                        list.Add(new WasmInstruction { Op = WasmOp.If });
                        break;

                    case 0x05: // else
                        {
                            if (ctrlStack.Count == 0)
                                throw new WasmParseException("else بدون if متناظر");
                            int ifIdx = ctrlStack[ctrlStack.Count - 1];
                            if (list[ifIdx].Op != WasmOp.If)
                                throw new WasmParseException("else بدون if متناظر");
                            elseOfIf[ifIdx] = list.Count;
                            list.Add(new WasmInstruction { Op = WasmOp.Else });
                        }
                        break;

                    case 0x0B: // end
                        {
                            int endPos = list.Count;
                            list.Add(new WasmInstruction { Op = WasmOp.End });
                            int afterEnd = list.Count;

                            if (ctrlStack.Count == 0)
                            {
                                // انتهای خودِ تابع — پایان دیکد
                                return list;
                            }

                            int openIdx = ctrlStack[ctrlStack.Count - 1];
                            ctrlStack.RemoveAt(ctrlStack.Count - 1);
                            var openInstr = list[openIdx];

                            if (openInstr.Op == WasmOp.Block)
                            {
                                openInstr.Target = afterEnd;
                            }
                            else if (openInstr.Op == WasmOp.Loop)
                            {
                                openInstr.Target = openIdx; // نقطه‌ی ادامه = خودِ loop
                            }
                            else if (openInstr.Op == WasmOp.If)
                            {
                                openInstr.Target2 = afterEnd; // نقطه‌ی خروج (برای فریمِ اجرا)
                                if (elseOfIf.TryGetValue(openIdx, out int elseIdx))
                                {
                                    openInstr.Target = elseIdx + 1; // اگر شرط نادرست بود، به شروع else برو
                                    var elseInstr = list[elseIdx];
                                    elseInstr.Target = afterEnd; // عبور از then به انتهای if وقتی به else می‌رسیم به‌صورت خطی
                                    list[elseIdx] = elseInstr;
                                }
                                else
                                {
                                    openInstr.Target = afterEnd; // بدون else: شرط نادرست یعنی کلاً رد شو
                                }
                            }
                            list[openIdx] = openInstr;
                        }
                        break;

                    case 0x0C: // br
                        list.Add(new WasmInstruction { Op = WasmOp.Br, Imm = r.ReadVarU32() });
                        break;

                    case 0x0D: // br_if
                        list.Add(new WasmInstruction { Op = WasmOp.BrIf, Imm = r.ReadVarU32() });
                        break;

                    case 0x0E: // br_table — پشتیبانی نمی‌شود، ولی باید عملوندها را درست بخوانیم
                        {
                            uint n = r.ReadVarU32();
                            for (uint k = 0; k < n; k++) r.ReadVarU32();
                            r.ReadVarU32(); // default label
                            list.Add(new WasmInstruction { Op = WasmOp.BrTableUnsupported });
                        }
                        break;

                    case 0x0F: list.Add(new WasmInstruction { Op = WasmOp.Return }); break;

                    case 0x10: // call
                        {
                            uint funcIdx = r.ReadVarU32();
                            if (funcIdx < (uint)importFuncCount)
                                list.Add(new WasmInstruction { Op = WasmOp.CallImport, Imm = funcIdx });
                            else
                                list.Add(new WasmInstruction { Op = WasmOp.Call, Imm = funcIdx - importFuncCount });
                        }
                        break;

                    case 0x11: // call_indirect — پشتیبانی نمی‌شود (بدون جدول)
                        r.ReadVarU32(); // typeidx
                        r.ReadByte();   // reserved tableidx byte
                        list.Add(new WasmInstruction { Op = WasmOp.CallIndirectUnsupported });
                        break;

                    case 0x1A: list.Add(new WasmInstruction { Op = WasmOp.Drop }); break;
                    case 0x1B: list.Add(new WasmInstruction { Op = WasmOp.Select }); break;

                    case 0x20: list.Add(new WasmInstruction { Op = WasmOp.LocalGet, Imm = r.ReadVarU32() }); break;
                    case 0x21: list.Add(new WasmInstruction { Op = WasmOp.LocalSet, Imm = r.ReadVarU32() }); break;
                    case 0x22: list.Add(new WasmInstruction { Op = WasmOp.LocalTee, Imm = r.ReadVarU32() }); break;
                    case 0x23: list.Add(new WasmInstruction { Op = WasmOp.GlobalGet, Imm = r.ReadVarU32() }); break;
                    case 0x24: list.Add(new WasmInstruction { Op = WasmOp.GlobalSet, Imm = r.ReadVarU32() }); break;

                    case 0x28: // i32.load
                        {
                            r.ReadVarU32(); // align (نادیده گرفته می‌شود)
                            uint offset = r.ReadVarU32();
                            list.Add(new WasmInstruction { Op = WasmOp.I32Load, Imm = offset });
                        }
                        break;

                    case 0x36: // i32.store
                        {
                            r.ReadVarU32(); // align
                            uint offset = r.ReadVarU32();
                            list.Add(new WasmInstruction { Op = WasmOp.I32Store, Imm = offset });
                        }
                        break;

                    case 0x3F: // memory.size
                        r.ReadByte(); // reserved 0x00
                        list.Add(new WasmInstruction { Op = WasmOp.MemorySize });
                        break;

                    case 0x40: // memory.grow
                        r.ReadByte(); // reserved 0x00
                        list.Add(new WasmInstruction { Op = WasmOp.MemoryGrow });
                        break;

                    case 0x41: // i32.const
                        list.Add(new WasmInstruction { Op = WasmOp.I32Const, Imm = r.ReadVarI32() });
                        break;

                    case 0x45: list.Add(new WasmInstruction { Op = WasmOp.I32Eqz }); break;
                    case 0x46: list.Add(new WasmInstruction { Op = WasmOp.I32Eq }); break;
                    case 0x47: list.Add(new WasmInstruction { Op = WasmOp.I32Ne }); break;
                    case 0x48: list.Add(new WasmInstruction { Op = WasmOp.I32LtS }); break;
                    case 0x4A: list.Add(new WasmInstruction { Op = WasmOp.I32GtS }); break;
                    case 0x4C: list.Add(new WasmInstruction { Op = WasmOp.I32LeS }); break;
                    case 0x4E: list.Add(new WasmInstruction { Op = WasmOp.I32GeS }); break;

                    case 0x6A: list.Add(new WasmInstruction { Op = WasmOp.I32Add }); break;
                    case 0x6B: list.Add(new WasmInstruction { Op = WasmOp.I32Sub }); break;
                    case 0x6C: list.Add(new WasmInstruction { Op = WasmOp.I32Mul }); break;
                    case 0x6D: list.Add(new WasmInstruction { Op = WasmOp.I32DivS }); break;
                    case 0x6F: list.Add(new WasmInstruction { Op = WasmOp.I32RemS }); break;
                    case 0x71: list.Add(new WasmInstruction { Op = WasmOp.I32And }); break;
                    case 0x72: list.Add(new WasmInstruction { Op = WasmOp.I32Or }); break;
                    case 0x73: list.Add(new WasmInstruction { Op = WasmOp.I32Xor }); break;
                    case 0x74: list.Add(new WasmInstruction { Op = WasmOp.I32Shl }); break;
                    case 0x75: list.Add(new WasmInstruction { Op = WasmOp.I32ShrS }); break;

                    default:
                        throw new WasmParseException("opcode پشتیبانی‌نشده در این MVP: 0x" + op.ToString("X2"));
                }
            }
        }

        // blocktype: 0x40 (خالی) یا یک valtype تک‌بایتی. تایپ‌های چندمقداری
        // (LEB چندبایتی با بیت بالا ست) در این MVP پشتیبانی نمی‌شوند.
        private static void ReadBlockType(WasmReader r)
        {
            byte b = r.PeekByte();
            if ((b & 0x80) != 0)
                throw new WasmParseException("blocktype چندبایتی (multi-value) در این MVP پشتیبانی نمی‌شود");
            r.ReadByte();
        }
    }

    public sealed class WasmFuncType
    {
        public WasmValueType[] Params = Array.Empty<WasmValueType>();
        public WasmValueType[] Results = Array.Empty<WasmValueType>();
    }

    public sealed class WasmGlobal
    {
        public WasmValueType Type;
        public bool Mutable;
        public int InitValue; // MVP: فقط init-expr از نوع i32.const پشتیبانی می‌شود
    }

    public sealed class WasmFunction
    {
        public int ParamCount, LocalCount;
        public WasmValueType[] ParamTypes = Array.Empty<WasmValueType>();
        public WasmValueType[] LocalTypes = Array.Empty<WasmValueType>();
        public WasmValueType[] ReturnTypes = Array.Empty<WasmValueType>();
        public List<WasmInstruction> Body = new List<WasmInstruction>();
    }

    public sealed class WasmImport
    {
        public string Module, Name;
        public int TypeIndex;
    }

    public struct WasmInstruction
    {
        public WasmOp Op;
        public long Imm;    // عملوند ثابت (const، ایندکس local/global/func، آفست حافظه، ...)
        public int Target;  // مقصد پرش (معنی‌اش به Op بستگی دارد؛ نگاه کنید به DecodeInstructions)
        public int Target2; // فقط برای If: نقطه‌ی خروج نهایی (بعد از End)، صرف‌نظر از مسیر then/else
    }

    // زیرمجموعه‌ای از opcodeهای اسپک WASM MVP — کافی برای برنامه‌های ساده،
    // بدون f32/f64 واقعی، بدون call_indirect واقعی.
    public enum WasmOp
    {
        Unreachable, Nop, Block, Loop, If, Else, End, Br, BrIf, BrTableUnsupported, Return,
        Call, CallImport, CallIndirectUnsupported,
        Drop, Select,
        LocalGet, LocalSet, LocalTee,
        GlobalGet, GlobalSet,
        I32Load, I32Store,
        MemorySize, MemoryGrow,
        I32Const,
        I32Add, I32Sub, I32Mul, I32DivS, I32RemS,
        I32And, I32Or, I32Xor, I32Shl, I32ShrS,
        I32Eq, I32Ne, I32LtS, I32GtS, I32LeS, I32GeS, I32Eqz,
    }

    public enum WasmValueType { I32, I64, F32, F64 }

    // ═══════════════════════════════════════════════════════════
    //  WasmInterpreter — ماشین اجرای stack-based. بدون Reflection، بدون
    //  Dynamic — فقط یک حلقه‌ی switch؛ همین ویژگی است که زیر IL2CPU مطمئن‌تر
    //  از Jint می‌شود.
    //
    //  استک عملیاتی (_stack) بین فراخوانی‌های تودرتو (call/call_import) به
    //  اشتراک گذاشته می‌شود — این دقیقاً همان قرارداد استاندارد ماشین‌های
    //  استکی ساده است: تابع فراخوانی‌شونده آرگومان‌هایش را از همان جایی که
    //  فراخواننده روی استک گذاشته برمی‌دارد، و مقدار بازگشتی‌اش را دقیقاً
    //  همان‌جا می‌گذارد — بدون کپی/marshalling اضافه.
    // ═══════════════════════════════════════════════════════════
    public sealed class WasmInterpreter
    {
        private readonly WasmModule _module;
        private readonly HostImportTable _hostApi;
        private byte[] _memory;
        private int _memoryPages;
        private readonly int _maxMemoryBytes;
        private readonly int[] _globals;

        private readonly int[] _stack = new int[1024]; // TODO: سقف واقعی باید تنظیم/اندازه‌گیری شود
        private int _sp;
        private long _fuel;

        public WasmInterpreter(WasmModule module, HostImportTable hostApi, int maxMemoryBytes)
        {
            _module = module;
            _hostApi = hostApi;
            _maxMemoryBytes = maxMemoryBytes;

            long minBytes = (long)module.MemoryMinPages * WasmModule.PageSize;
            if (minBytes > maxMemoryBytes)
                throw new WasmTrapException(
                    "ماژول به " + minBytes + " بایت حافظه نیاز دارد ولی سقف sandbox " + maxMemoryBytes + " بایت است");

            _memory = new byte[minBytes];
            if (module.LinearMemoryInit != null)
                Array.Copy(module.LinearMemoryInit, _memory,
                    Math.Min(module.LinearMemoryInit.Length, _memory.Length));
            _memoryPages = module.MemoryMinPages;

            _globals = new int[module.Globals.Count];
            for (int i = 0; i < module.Globals.Count; i++)
                _globals[i] = module.Globals[i].InitValue;
        }

        public void ResetFuel(long amount) => _fuel = amount;

        public void CallExport(string name, params int[] args)
        {
            if (!_module.Exports.TryGetValue(name, out int funcIdx))
                throw new WasmTrapException("export not found: " + name);

            var fn = _module.Functions[funcIdx];
            int[] locals = new int[fn.ParamCount + fn.LocalCount];
            int n = Math.Min(args.Length, fn.ParamCount);
            for (int i = 0; i < n; i++) locals[i] = args[i];

            ExecuteBody(fn, locals);
        }

        // فریم کنترلِ اجرا برای block/loop/if — فقط محلیِ همین فراخوانی است
        // (recursion طبیعیِ C# باعث می‌شود هر تماس تودرتو فریم مستقل خودش را
        // داشته باشد، بدون نیاز به مدیریت دستی استک فراخوانی).
        private struct Frame
        {
            public int Target;
            public bool IsLoop;
            public Frame(int target, bool isLoop) { Target = target; IsLoop = isLoop; }
        }

        private void ExecuteBody(WasmFunction fn, int[] locals)
        {
            var body = fn.Body;
            var blockStack = new List<Frame>();
            int ip = 0;

            while (ip < body.Count)
            {
                if (--_fuel <= 0)
                    throw new WasmTrapException("fuel exhausted (احتمالاً حلقه‌ی بی‌نهایت در اسکریپت)");

                var instr = body[ip];
                switch (instr.Op)
                {
                    case WasmOp.Unreachable:
                        throw new WasmTrapException("unreachable instruction اجرا شد");

                    case WasmOp.Nop:
                        ip++;
                        break;

                    case WasmOp.Block:
                        blockStack.Add(new Frame(instr.Target, false));
                        ip++;
                        break;

                    case WasmOp.Loop:
                        blockStack.Add(new Frame(instr.Target, true));
                        ip++;
                        break;

                    case WasmOp.If:
                        {
                            int cond = Pop();
                            if (cond != 0)
                            {
                                blockStack.Add(new Frame(instr.Target2, false));
                                ip++;
                            }
                            else
                            {
                                ip = instr.Target;
                                if (instr.Target != instr.Target2)
                                    blockStack.Add(new Frame(instr.Target2, false)); // else-branch وجود دارد
                                // اگر Target == Target2: else‌ای نیست، مستقیم از کل if رد شدیم، فریمی لازم نیست
                            }
                        }
                        break;

                    case WasmOp.Else:
                        // به این نقطه فقط از طریق اجرای خطیِ انتهای then می‌رسیم —
                        // یعنی then تمام شده، باید کل else را رد کنیم.
                        ip = instr.Target;
                        if (blockStack.Count > 0) blockStack.RemoveAt(blockStack.Count - 1);
                        break;

                    case WasmOp.End:
                        if (blockStack.Count > 0) blockStack.RemoveAt(blockStack.Count - 1);
                        ip++;
                        break;

                    case WasmOp.Br:
                        ip = BranchTo(blockStack, (int)instr.Imm);
                        break;

                    case WasmOp.BrIf:
                        {
                            int c = Pop();
                            if (c != 0) ip = BranchTo(blockStack, (int)instr.Imm);
                            else ip++;
                        }
                        break;

                    case WasmOp.BrTableUnsupported:
                        throw new WasmTrapException("br_table در این MVP پشتیبانی نمی‌شود");

                    case WasmOp.Return:
                        return;

                    case WasmOp.Call:
                        CallInternal((int)instr.Imm);
                        ip++;
                        break;

                    case WasmOp.CallImport:
                        CallImportByIndex((int)instr.Imm);
                        ip++;
                        break;

                    case WasmOp.CallIndirectUnsupported:
                        throw new WasmTrapException("call_indirect در این MVP پشتیبانی نمی‌شود (بدون جدول)");

                    case WasmOp.Drop:
                        Pop();
                        ip++;
                        break;

                    case WasmOp.Select:
                        {
                            int c = Pop(), b = Pop(), a = Pop();
                            Push(c != 0 ? a : b);
                            ip++;
                        }
                        break;

                    case WasmOp.LocalGet:
                        Push(locals[instr.Imm]);
                        ip++;
                        break;

                    case WasmOp.LocalSet:
                        locals[instr.Imm] = Pop();
                        ip++;
                        break;

                    case WasmOp.LocalTee:
                        {
                            int v = Pop();
                            locals[instr.Imm] = v;
                            Push(v);
                            ip++;
                        }
                        break;

                    case WasmOp.GlobalGet:
                        Push(_globals[instr.Imm]);
                        ip++;
                        break;

                    case WasmOp.GlobalSet:
                        {
                            int gi = (int)instr.Imm;
                            if (!_module.Globals[gi].Mutable)
                                throw new WasmTrapException("نوشتن روی global غیرقابل‌تغییر");
                            _globals[gi] = Pop();
                            ip++;
                        }
                        break;

                    case WasmOp.I32Load:
                        {
                            int baseAddr = Pop();
                            long eff = (long)baseAddr + instr.Imm;
                            if (eff < 0 || eff + 4 > _memory.Length)
                                throw new WasmTrapException("out-of-bounds memory access در i32.load");
                            int val = _memory[eff] | (_memory[eff + 1] << 8)
                                    | (_memory[eff + 2] << 16) | (_memory[eff + 3] << 24);
                            Push(val);
                            ip++;
                        }
                        break;

                    case WasmOp.I32Store:
                        {
                            int value = Pop();
                            int baseAddr = Pop();
                            long eff = (long)baseAddr + instr.Imm;
                            if (eff < 0 || eff + 4 > _memory.Length)
                                throw new WasmTrapException("out-of-bounds memory access در i32.store");
                            _memory[eff] = (byte)value;
                            _memory[eff + 1] = (byte)(value >> 8);
                            _memory[eff + 2] = (byte)(value >> 16);
                            _memory[eff + 3] = (byte)(value >> 24);
                            ip++;
                        }
                        break;

                    case WasmOp.MemorySize:
                        Push(_memoryPages);
                        ip++;
                        break;

                    case WasmOp.MemoryGrow:
                        {
                            int delta = Pop();
                            int oldPages = _memoryPages;
                            bool invalid = delta < 0;
                            long newBytes = invalid ? 0 : (long)(_memoryPages + delta) * WasmModule.PageSize;
                            bool exceedsCap = newBytes > _maxMemoryBytes;
                            bool exceedsModuleMax = _module.MemoryMaxPages > 0 && (_memoryPages + delta) > _module.MemoryMaxPages;

                            if (invalid || exceedsCap || exceedsModuleMax)
                            {
                                Push(-1); // طبق اسپک: رد شدن رشد یعنی -1 برگردد، نه trap
                            }
                            else
                            {
                                var newMem = new byte[newBytes];
                                Array.Copy(_memory, newMem, _memory.Length);
                                _memory = newMem;
                                _memoryPages += delta;
                                Push(oldPages);
                            }
                            ip++;
                        }
                        break;

                    case WasmOp.I32Const:
                        Push((int)instr.Imm);
                        ip++;
                        break;

                    case WasmOp.I32Add: { int b = Pop(), a = Pop(); Push(a + b); ip++; } break;
                    case WasmOp.I32Sub: { int b = Pop(), a = Pop(); Push(a - b); ip++; } break;
                    case WasmOp.I32Mul: { int b = Pop(), a = Pop(); Push(a * b); ip++; } break;

                    case WasmOp.I32DivS:
                        {
                            int b = Pop(), a = Pop();
                            if (b == 0) throw new WasmTrapException("integer divide by zero (i32.div_s)");
                            if (a == int.MinValue && b == -1) throw new WasmTrapException("integer overflow (i32.div_s)");
                            Push(a / b);
                            ip++;
                        }
                        break;

                    case WasmOp.I32RemS:
                        {
                            int b = Pop(), a = Pop();
                            if (b == 0) throw new WasmTrapException("integer divide by zero (i32.rem_s)");
                            Push(a == int.MinValue && b == -1 ? 0 : a % b);
                            ip++;
                        }
                        break;

                    case WasmOp.I32And: { int b = Pop(), a = Pop(); Push(a & b); ip++; } break;
                    case WasmOp.I32Or: { int b = Pop(), a = Pop(); Push(a | b); ip++; } break;
                    case WasmOp.I32Xor: { int b = Pop(), a = Pop(); Push(a ^ b); ip++; } break;
                    case WasmOp.I32Shl: { int b = Pop(), a = Pop(); Push(a << (b & 31)); ip++; } break;
                    case WasmOp.I32ShrS: { int b = Pop(), a = Pop(); Push(a >> (b & 31)); ip++; } break;

                    case WasmOp.I32Eq: { int b = Pop(), a = Pop(); Push(a == b ? 1 : 0); ip++; } break;
                    case WasmOp.I32Ne: { int b = Pop(), a = Pop(); Push(a != b ? 1 : 0); ip++; } break;
                    case WasmOp.I32LtS: { int b = Pop(), a = Pop(); Push(a < b ? 1 : 0); ip++; } break;
                    case WasmOp.I32GtS: { int b = Pop(), a = Pop(); Push(a > b ? 1 : 0); ip++; } break;
                    case WasmOp.I32LeS: { int b = Pop(), a = Pop(); Push(a <= b ? 1 : 0); ip++; } break;
                    case WasmOp.I32GeS: { int b = Pop(), a = Pop(); Push(a >= b ? 1 : 0); ip++; } break;
                    case WasmOp.I32Eqz: { int a = Pop(); Push(a == 0 ? 1 : 0); ip++; } break;

                    default:
                        throw new WasmTrapException("opcode پیاده‌سازی نشده: " + instr.Op);
                }
            }
        }

        // br L یعنی: از L لایه‌ی کنترلیِ تودرتوی فعلی خارج شو. اگر مقصد یک
        // Loop باشد یعنی «ادامه» (فریمش می‌ماند)، وگرنه یعنی «خروج» (فریمش هم
        // برداشته می‌شود) — این دقیقاً معنایی است که در WASM به br نسبت داده
        // می‌شود.
        private int BranchTo(List<Frame> blockStack, int depth)
        {
            int idx = blockStack.Count - 1 - depth;
            if (idx < 0)
                throw new WasmTrapException("عمق br نامعتبر است");

            var frame = blockStack[idx];
            // فریم‌های بالاتر از idx همه در حال خروج‌اند، باید برداشته شوند
            if (idx + 1 <= blockStack.Count - 1)
                blockStack.RemoveRange(idx + 1, blockStack.Count - 1 - idx);
            if (!frame.IsLoop)
                blockStack.RemoveAt(idx); // خروج از block/if یعنی فریمش هم برداشته می‌شود

            return frame.Target;
        }

        private void CallInternal(int internalFuncIdx)
        {
            if (internalFuncIdx < 0 || internalFuncIdx >= _module.Functions.Count)
                throw new WasmTrapException("funcidx نامعتبر در call: " + internalFuncIdx);

            var fn = _module.Functions[internalFuncIdx];
            int[] locals = new int[fn.ParamCount + fn.LocalCount];
            // آرگومان‌ها را از همان استکِ مشترک برمی‌داریم (ترتیب معکوس چون
            // آخرین‌بار پوش‌شده اول پاپ می‌شود، ولی باید در جای درستِ locals
            // بنشیند).
            for (int p = fn.ParamCount - 1; p >= 0; p--)
                locals[p] = Pop();

            ExecuteBody(fn, locals);
        }

        private void CallImportByIndex(int importIdx)
        {
            if (importIdx < 0 || importIdx >= _module.Imports.Count)
                throw new WasmTrapException("importidx نامعتبر در call: " + importIdx);

            var imp = _module.Imports[importIdx];
            var ft = _module.Types[imp.TypeIndex];

            int[] args = new int[ft.Params.Length];
            for (int p = ft.Params.Length - 1; p >= 0; p--)
                args[p] = Pop();

            if (!_hostApi.TryGet(imp.Name, out var hostFn))
                throw new WasmTrapException("import resolve نشد: " + imp.Module + "." + imp.Name);

            long result = hostFn(this, args);

            if (ft.Results.Length > 0)
                Push((int)result);
        }

        private void Push(int v)
        {
            if (_sp >= _stack.Length) throw new WasmTrapException("stack overflow");
            _stack[_sp++] = v;
        }

        private int Pop()
        {
            if (_sp <= 0) throw new WasmTrapException("stack underflow");
            return _stack[--_sp];
        }

        // ─── دسترسی حافظه با bounds-check اجباری — این دقیقاً همان چیزی است
        // که اجازه می‌دهد به کد untrusted اعتماد کنیم: هیچ pointer خامی به
        // حافظه‌ی واقعی OS نمی‌رسد، فقط به این آرایه‌ی sandbox‌شده.
        public string ReadUtf8String(int ptr, int len)
        {
            if (ptr < 0 || len < 0 || (long)ptr + len > _memory.Length)
                throw new WasmTrapException("out-of-bounds memory access در ReadUtf8String");
            return Encoding.UTF8.GetString(_memory, ptr, len);
        }

        // ─── نوشتن به حافظه‌ی خطی از سمت host — قرینه‌ی ReadUtf8String، برای
        // importهایی مثل read_file که باید داده را در بافرِ خودِ اپ بگذارند.
        // بدون bounds-check اجازه نمی‌دهیم چون ptr/len از داخل خودِ اپ
        // (untrusted) می‌آید.
        public int WriteMemory(int ptr, byte[] data, int count)
        {
            if (data == null) return 0;
            if (count > data.Length) count = data.Length;
            if (ptr < 0 || count < 0 || (long)ptr + count > _memory.Length)
                throw new WasmTrapException("out-of-bounds memory access در WriteMemory");
            Array.Copy(data, 0, _memory, ptr, count);
            return count;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  WasmReader — خواننده‌ی خام باینری (LEB128 varint، نام‌ها، بایت خام).
    // ═══════════════════════════════════════════════════════════
    internal sealed class WasmReader
    {
        private readonly byte[] _buf;
        private int _pos;

        public WasmReader(byte[] buf) { _buf = buf; _pos = 0; }

        public bool Eof => _pos >= _buf.Length;
        public int Position => _pos;

        public byte ReadByte()
        {
            if (_pos >= _buf.Length) throw new WasmParseException("پایان غیرمنتظره‌ی فایل");
            return _buf[_pos++];
        }

        public byte PeekByte()
        {
            if (_pos >= _buf.Length) throw new WasmParseException("پایان غیرمنتظره‌ی فایل");
            return _buf[_pos];
        }

        public byte[] ReadBytes(int count)
        {
            if (count < 0 || _pos + count > _buf.Length) throw new WasmParseException("پایان غیرمنتظره‌ی فایل");
            var r = new byte[count];
            Array.Copy(_buf, _pos, r, 0, count);
            _pos += count;
            return r;
        }

        // magic/version در فرمت WASM به‌صورت ۴ بایت خامِ little-endian هستند
        // (نه LEB128).
        public uint ReadU32Fixed()
        {
            uint v = (uint)(ReadByte() | (ReadByte() << 8) | (ReadByte() << 16) | (ReadByte() << 24));
            return v;
        }

        public uint ReadVarU32()
        {
            uint result = 0;
            int shift = 0;
            byte b;
            do
            {
                b = ReadByte();
                result |= (uint)(b & 0x7F) << shift;
                shift += 7;
                if (shift > 35) throw new WasmParseException("varuint32 خیلی طولانی است");
            } while ((b & 0x80) != 0);
            return result;
        }

        public long ReadVarI32()
        {
            long result = 0;
            int shift = 0;
            byte b;
            do
            {
                b = ReadByte();
                result |= (long)(b & 0x7F) << shift;
                shift += 7;
                if (shift > 35) throw new WasmParseException("varint32 خیلی طولانی است");
            } while ((b & 0x80) != 0);

            if (shift < 32 && (b & 0x40) != 0)
                result |= -(1L << shift); // sign-extend

            return (int)result;
        }

        public string ReadName()
        {
            uint len = ReadVarU32();
            byte[] bytes = ReadBytes((int)len);
            return Encoding.UTF8.GetString(bytes);
        }
    }

    // خطای parse-time (فایل خراب/نامعتبر) — جدا از WasmTrapException که
    // خطای runtime است؛ هر دو در Load() به هم گرفته می‌شوند و اپ را faulted
    // علامت می‌زنند، نه این‌که کل OS را کرش کنند.
    public sealed class WasmParseException : Exception
    {
        public WasmParseException(string message) : base(message) { }
    }

    public sealed class WasmTrapException : Exception
    {
        public WasmTrapException(string message) : base(message) { }
    }
}