// TerminalApp.cs
// برنامه ترمینال برای ParsOS
// پشتیبانی از 12+ دستور پرکاربرد با پس‌زمینه مشکی

using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using ParsOS.GUI;
using Sys = Cosmos.System;

namespace ParsOS.Apps
{
    public static class TerminalApp
    {
        // ─── شناسه پنجره ────────────────────────────────────────
        public const string ContentFlag = "TERMINAL_APP";

        // ─── رنگ‌ها و Pen‌ها ────────────────────────────────────
        private static readonly Pen _penBg = new Pen(Color.FromArgb(10, 10, 14));
        private static readonly Pen _penPrompt = new Pen(Color.FromArgb(80, 220, 120));   // سبز روشن
        private static readonly Pen _penInput = new Pen(Color.FromArgb(220, 220, 220));  // خاکستری روشن
        private static readonly Pen _penOutput = new Pen(Color.FromArgb(190, 190, 190));  // خاکستری
        private static readonly Pen _penError = new Pen(Color.FromArgb(255, 90, 90));    // قرمز
        private static readonly Pen _penSuccess = new Pen(Color.FromArgb(80, 220, 120));   // سبز
        private static readonly Pen _penWarning = new Pen(Color.FromArgb(255, 200, 60));   // زرد
        private static readonly Pen _penAccent = new Pen(Color.FromArgb(100, 180, 255));  // آبی روشن
        private static readonly Pen _penDim = new Pen(Color.FromArgb(90, 90, 100));    // خاکستری تیره
        private static readonly Pen _penCursor = new Pen(Color.FromArgb(80, 220, 120));   // سبز (= cursor)
        private static readonly Pen _penBorder = new Pen(Color.FromArgb(30, 30, 45));     // حاشیه داخلی
        private static readonly Pen _penTitleLine = new Pen(Color.FromArgb(50, 120, 80));    // خط زیر header
        private static readonly Pen _penDir = new Pen(Color.FromArgb(100, 160, 255));  // آبی برای پوشه
        private static readonly Pen _penFile = new Pen(Color.FromArgb(190, 190, 190));  // خاکستری برای فایل
        private static readonly Pen _penHighlight = new Pen(Color.FromArgb(30, 50, 30));     // highlight input line bg

        // ─── تنظیمات ────────────────────────────────────────────
        private const int CharW = 8;    // عرض هر کاراکتر (PCScreenFont)
        private const int LineH = 16;   // ارتفاع هر خط
        private const int PadX = 10;   // padding افقی
        private const int PadY = 8;    // padding عمودی (بعد از titlebar)
        private const int InputH = 20;   // ارتفاع ناحیه ورودی
        private const int MaxLines = 200;  // حداکثر خطوط تاریخچه

        // ─── وضعیت ──────────────────────────────────────────────
        private static List<OutputLine> _output = new List<OutputLine>();
        private static string _input = "";
        private static int _cursorPos = 0;
        private static int _scrollOffset = 0;   // اسکرول به بالا

        // تاریخچه دستورات (↑↓)
        private static List<string> _history = new List<string>();
        private static int _historyIdx = -1;
        private static string _historyTemp = "";

        // cursor blink
        private static int _blinkTick = 0;
        private const int BlinkInterval = 28;
        private static bool _cursorVisible = true;

        // مسیر جاری
        private static string _cwd = @"0:\";

        // ساختار یک خط خروجی
        private struct OutputLine
        {
            public string Text;
            public LineType Type;
            public OutputLine(string t, LineType lt) { Text = t; Type = lt; }
        }

        private enum LineType
        {
            Normal,     // خروجی معمولی
            Error,      // خطا (قرمز)
            Success,    // موفقیت (سبز)
            Warning,    // هشدار (زرد)
            Accent,     // اطلاعات مهم (آبی)
            Prompt,     // خط دستور (سبز روشن)
            Dir,        // اسم پوشه
            Dim,        // متن کم‌رنگ
        }

        // ═══════════════════════════════════════════════════════
        //  راه‌اندازی اولیه
        // ═══════════════════════════════════════════════════════
        public static void Initialize()
        {
            _output.Clear();
            _input = "";
            _cursorPos = 0;
            _scrollOffset = 0;
            _history.Clear();
            _historyIdx = -1;
            _cwd = @"0:\";

            // پیام خوش‌آمد
            PrintLine("", LineType.Normal);
            PrintLine("  ParsOS Terminal v1.0", LineType.Accent);
            PrintLine("  " + new string('─', 36), LineType.Dim);
            PrintLine("  Type 'help' to see available commands.", LineType.Dim);
            PrintLine("", LineType.Normal);
        }

        // ─── اضافه کردن خط به خروجی ──────────────────────────────
        private static void PrintLine(string text, LineType type = LineType.Normal)
        {
            _output.Add(new OutputLine(text, type));
            if (_output.Count > MaxLines)
                _output.RemoveAt(0);
            // اسکرول به پایین خودکار
            _scrollOffset = 0;
        }

        // ─── چند خط با یک نوع ──────────────────────────────────
        private static void PrintLines(string[] lines, LineType type = LineType.Normal)
        {
            foreach (var l in lines) PrintLine(l, type);
        }

        // ═══════════════════════════════════════════════════════
        //  ورودی کیبورد
        // ═══════════════════════════════════════════════════════
        // بازگشت true = تغییری رخ داد که نیاز به رندر دارد
        public static bool HandleKeyboard()
        {
            bool changed = false;

            while (Sys.KeyboardManager.TryReadKey(out var key))
            {
                ProcessKey(key);
                changed = true;
            }

            _blinkTick++;
            if (_blinkTick >= BlinkInterval)
            {
                _blinkTick = 0;
                _cursorVisible = !_cursorVisible;
                changed = true;  // blink واقعی رخ داد
            }

            return changed;
        }

        private static void ProcessKey(Sys.KeyEvent key)
        {
            _cursorVisible = true;
            _blinkTick = 0;

            switch (key.Key)
            {
                case Sys.ConsoleKeyEx.Enter:
                    ExecuteCommand(_input.Trim());
                    break;

                case Sys.ConsoleKeyEx.Backspace:
                    if (_cursorPos > 0 && _input.Length > 0)
                    {
                        _input = _input.Substring(0, _cursorPos - 1) + _input.Substring(_cursorPos);
                        _cursorPos--;
                    }
                    break;

                case Sys.ConsoleKeyEx.Delete:
                    if (_cursorPos < _input.Length)
                        _input = _input.Substring(0, _cursorPos) + _input.Substring(_cursorPos + 1);
                    break;

                case Sys.ConsoleKeyEx.LeftArrow:
                    if (_cursorPos > 0) _cursorPos--;
                    break;

                case Sys.ConsoleKeyEx.RightArrow:
                    if (_cursorPos < _input.Length) _cursorPos++;
                    break;

                case Sys.ConsoleKeyEx.Home:
                    _cursorPos = 0;
                    break;

                case Sys.ConsoleKeyEx.End:
                    _cursorPos = _input.Length;
                    break;

                case Sys.ConsoleKeyEx.UpArrow:
                    // تاریخچه قبلی
                    if (_history.Count > 0)
                    {
                        if (_historyIdx == -1)
                        {
                            _historyTemp = _input;
                            _historyIdx = _history.Count - 1;
                        }
                        else if (_historyIdx > 0)
                            _historyIdx--;
                        _input = _history[_historyIdx];
                        _cursorPos = _input.Length;
                    }
                    break;

                case Sys.ConsoleKeyEx.DownArrow:
                    if (_historyIdx >= 0)
                    {
                        if (_historyIdx < _history.Count - 1)
                        {
                            _historyIdx++;
                            _input = _history[_historyIdx];
                        }
                        else
                        {
                            _historyIdx = -1;
                            _input = _historyTemp;
                        }
                        _cursorPos = _input.Length;
                    }
                    break;

                case Sys.ConsoleKeyEx.PageUp:
                    _scrollOffset += 5;
                    break;

                case Sys.ConsoleKeyEx.PageDown:
                    if (_scrollOffset > 5) _scrollOffset -= 5;
                    else _scrollOffset = 0;
                    break;

                default:
                    char c = key.KeyChar;
                    if (c >= 32 && c < 127)
                    {
                        _input = _input.Substring(0, _cursorPos) + c.ToString() + _input.Substring(_cursorPos);
                        _cursorPos++;
                    }
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  پردازش دستور
        // ═══════════════════════════════════════════════════════
        private static void ExecuteCommand(string raw)
        {
            // نمایش دستور تایپ‌شده
            string promptStr = GetPrompt() + raw;
            PrintLine(promptStr, LineType.Prompt);

            // ثبت در تاریخچه (دستور غیرخالی و تکراری نباشد)
            if (!string.IsNullOrEmpty(raw))
            {
                if (_history.Count == 0 || _history[_history.Count - 1] != raw)
                    _history.Add(raw);
                if (_history.Count > 50) _history.RemoveAt(0);
            }

            // پاک کردن input
            _input = "";
            _cursorPos = 0;
            _historyIdx = -1;
            _historyTemp = "";

            if (string.IsNullOrEmpty(raw))
                return;

            // پارس دستور
            string[] parts = SplitArgs(raw);
            string cmd = parts[0].ToLower();
            string[] args = new string[parts.Length - 1];
            for (int i = 0; i < args.Length; i++) args[i] = parts[i + 1];

            // ─── دیسپچ دستورات ───────────────────────────────────
            switch (cmd)
            {
                case "help": CmdHelp(); break;
                case "clear":
                case "cls": CmdClear(); break;
                case "echo": CmdEcho(args); break;
                case "ls":
                case "dir": CmdLs(args); break;
                case "cd": CmdCd(args); break;
                case "pwd": CmdPwd(); break;
                case "mkdir": CmdMkdir(args); break;
                case "rmdir": CmdRmdir(args); break;
                case "touch": CmdTouch(args); break;
                case "rm":
                case "del": CmdRm(args); break;
                case "cat":
                case "type": CmdCat(args); break;
                case "write": CmdWrite(args); break;
                case "copy":
                case "cp": CmdCopy(args); break;
                case "move":
                case "mv": CmdMove(args); break;
                case "rename": CmdMove(args); break;
                case "mem":
                case "free": CmdMem(); break;
                case "sysinfo": CmdSysinfo(); break;
                case "ver":
                case "version": CmdVersion(); break;
                case "reboot": CmdReboot(); break;
                case "shutdown": CmdShutdown(); break;
                case "history": CmdHistory(); break;
                default:
                    PrintLine("'" + cmd + "': command not found. Type 'help'.", LineType.Error);
                    break;
            }
        }

        // ─── Prompt string ───────────────────────────────────────
        private static string GetPrompt()
        {
            return "ParsOS:" + _cwd + "$ ";
        }

        // ─── پارس آرگومان‌ها (با پشتیبانی از quote) ──────────────
        private static string[] SplitArgs(string raw)
        {
            var result = new List<string>();
            bool inQ = false;
            var cur = new System.Text.StringBuilder();
            foreach (char c in raw)
            {
                if (c == '"') { inQ = !inQ; continue; }
                if (c == ' ' && !inQ)
                {
                    if (cur.Length > 0) { result.Add(cur.ToString()); cur = new System.Text.StringBuilder(); }
                    continue;
                }
                cur.Append(c);
            }
            if (cur.Length > 0) result.Add(cur.ToString());
            if (result.Count == 0) result.Add("");
            return result.ToArray();
        }

        // ─── مسیر کامل از آرگومان ─────────────────────────────────
        private static string ResolvePath(string arg)
        {
            if (arg.Length >= 2 && arg[1] == ':') return arg;
            if (arg.StartsWith(@"\")) return _cwd.Substring(0, 2) + arg;
            string combined = _cwd.TrimEnd('\\') + @"\" + arg;
            return combined;
        }

        // ═══════════════════════════════════════════════════════
        //  دستورات
        // ═══════════════════════════════════════════════════════

        // 1. help
        private static void CmdHelp()
        {
            PrintLine("", LineType.Normal);
            PrintLine("  Available Commands:", LineType.Accent);
            PrintLine("  " + new string('─', 42), LineType.Dim);

            string[][] cmds = new string[][] {
                new[] { "  help          ", "Show this help message"             },
                new[] { "  cls / clear   ", "Clear the terminal screen"          },
                new[] { "  echo <text>   ", "Print text to terminal"             },
                new[] { "  pwd           ", "Print current working directory"    },
                new[] { "  ls / dir      ", "List files and folders"             },
                new[] { "  cd <path>     ", "Change directory"                   },
                new[] { "  mkdir <name>  ", "Create a new directory"             },
                new[] { "  rmdir <name>  ", "Remove an empty directory"          },
                new[] { "  touch <name>  ", "Create an empty file"               },
                new[] { "  rm / del <f>  ", "Delete a file"                      },
                new[] { "  cat / type <f>", "Display file contents"              },
                new[] { "  write <f> <t> ", "Write text into a file"             },
                new[] { "  cp <src> <dst>", "Copy a file"                        },
                new[] { "  mv <src> <dst>", "Move / rename a file"               },
                new[] { "  mem / free    ", "Show memory usage"                  },
                new[] { "  sysinfo       ", "Show system information"            },
                new[] { "  version / ver ", "Show OS version"                    },
                new[] { "  history       ", "Show command history"               },
                new[] { "  reboot        ", "Restart the system"                 },
                new[] { "  shutdown      ", "Shut down the system"               },
            };

            foreach (var row in cmds)
            {
                PrintLine(row[0] + row[1], LineType.Normal);
            }
            PrintLine("", LineType.Normal);
            PrintLine("  Tip: Use ↑↓ for history, PageUp/Down to scroll.", LineType.Dim);
            PrintLine("", LineType.Normal);
        }

        // 2. clear
        private static void CmdClear()
        {
            _output.Clear();
            _scrollOffset = 0;
        }

        // 3. echo
        private static void CmdEcho(string[] args)
        {
            if (args.Length == 0) { PrintLine("", LineType.Normal); return; }
            string text = string.Join(" ", args);
            PrintLine("  " + text, LineType.Normal);
        }

        // 4. pwd
        private static void CmdPwd()
        {
            PrintLine("  " + _cwd, LineType.Accent);
        }

        // 5. ls / dir
        private static void CmdLs(string[] args)
        {
            string path = args.Length > 0 ? ResolvePath(args[0]) : _cwd;
            try
            {
                if (!Directory.Exists(path))
                {
                    PrintLine("  ls: '" + path + "': No such directory", LineType.Error);
                    return;
                }

                PrintLine("", LineType.Normal);
                PrintLine("  Directory: " + path, LineType.Accent);
                PrintLine("  " + new string('─', 40), LineType.Dim);

                bool hasAny = false;

                // پوشه‌ها
                try
                {
                    string[] dirs = Directory.GetDirectories(path);
                    foreach (string d in dirs)
                    {
                        string name = d.Length > path.TrimEnd('\\').Length
                            ? d.Substring(path.TrimEnd('\\').Length + 1) : d;
                        PrintLine("  [DIR]  " + name, LineType.Dir);
                        hasAny = true;
                    }
                }
                catch { }

                // فایل‌ها
                try
                {
                    string[] files = Directory.GetFiles(path);
                    foreach (string f in files)
                    {
                        string name = f.Length > path.TrimEnd('\\').Length
                            ? f.Substring(path.TrimEnd('\\').Length + 1) : f;
                        long size = 0;
                        try { size = new FileInfo(f).Length; } catch { }
                        string sizeStr = size < 1024 ? size + " B"
                                       : size < 1024 * 1024 ? (size / 1024) + " KB"
                                       : (size / (1024 * 1024)) + " MB";
                        PrintLine("  [FILE] " + name.PadRight(24) + sizeStr, LineType.Normal);
                        hasAny = true;
                    }
                }
                catch { }

                if (!hasAny)
                    PrintLine("  (empty directory)", LineType.Dim);

                PrintLine("", LineType.Normal);
            }
            catch (Exception ex)
            {
                PrintLine("  ls: " + ex.Message, LineType.Error);
            }
        }

        // 6. cd
        private static void CmdCd(string[] args)
        {
            if (args.Length == 0) { PrintLine("  Usage: cd <path>", LineType.Warning); return; }

            string target = args[0];

            // cd .. → یک سطح بالاتر
            if (target == "..")
            {
                string cur = _cwd.TrimEnd('\\');
                int idx = cur.LastIndexOf('\\');
                if (idx >= 2) // حداقل 0:\ باشد
                    _cwd = cur.Substring(0, idx + 1);
                else
                    _cwd = cur.Substring(0, 3); // 0:\
                return;
            }

            // cd . → بی‌اثر
            if (target == ".") return;

            string full = ResolvePath(target);
            if (!full.EndsWith("\\")) full += "\\";

            try
            {
                if (Directory.Exists(full.TrimEnd('\\')))
                    _cwd = full;
                else
                    PrintLine("  cd: '" + full + "': No such directory", LineType.Error);
            }
            catch (Exception ex)
            {
                PrintLine("  cd: " + ex.Message, LineType.Error);
            }
        }

        // 7. mkdir
        private static void CmdMkdir(string[] args)
        {
            if (args.Length == 0) { PrintLine("  Usage: mkdir <name>", LineType.Warning); return; }
            string path = ResolvePath(args[0]);
            try
            {
                if (Directory.Exists(path))
                { PrintLine("  mkdir: '" + args[0] + "' already exists", LineType.Warning); return; }
                Directory.CreateDirectory(path);
                PrintLine("  Directory created: " + args[0], LineType.Success);
            }
            catch (Exception ex) { PrintLine("  mkdir: " + ex.Message, LineType.Error); }
        }

        // 8. rmdir
        private static void CmdRmdir(string[] args)
        {
            if (args.Length == 0) { PrintLine("  Usage: rmdir <name>", LineType.Warning); return; }
            string path = ResolvePath(args[0]);
            try
            {
                if (!Directory.Exists(path))
                { PrintLine("  rmdir: '" + args[0] + "': Not found", LineType.Error); return; }
                Directory.Delete(path);
                PrintLine("  Directory removed: " + args[0], LineType.Success);
            }
            catch (Exception ex) { PrintLine("  rmdir: " + ex.Message, LineType.Error); }
        }

        // 9. touch
        private static void CmdTouch(string[] args)
        {
            if (args.Length == 0) { PrintLine("  Usage: touch <filename>", LineType.Warning); return; }
            string path = ResolvePath(args[0]);
            try
            {
                if (File.Exists(path))
                { PrintLine("  touch: '" + args[0] + "' already exists", LineType.Warning); return; }
                File.WriteAllText(path, "");
                PrintLine("  File created: " + args[0], LineType.Success);
            }
            catch (Exception ex) { PrintLine("  touch: " + ex.Message, LineType.Error); }
        }

        // 10. rm / del
        private static void CmdRm(string[] args)
        {
            if (args.Length == 0) { PrintLine("  Usage: rm <filename>", LineType.Warning); return; }
            string path = ResolvePath(args[0]);
            try
            {
                if (!File.Exists(path))
                { PrintLine("  rm: '" + args[0] + "': File not found", LineType.Error); return; }
                File.Delete(path);
                PrintLine("  Deleted: " + args[0], LineType.Success);
            }
            catch (Exception ex) { PrintLine("  rm: " + ex.Message, LineType.Error); }
        }

        // 11. cat / type
        private static void CmdCat(string[] args)
        {
            if (args.Length == 0) { PrintLine("  Usage: cat <filename>", LineType.Warning); return; }
            string path = ResolvePath(args[0]);
            try
            {
                if (!File.Exists(path))
                { PrintLine("  cat: '" + args[0] + "': File not found", LineType.Error); return; }
                string content = File.ReadAllText(path);
                if (string.IsNullOrEmpty(content))
                { PrintLine("  (empty file)", LineType.Dim); return; }

                string[] lines = content.Split('\n');
                PrintLine("  ─── " + args[0] + " ───", LineType.Accent);
                foreach (string line in lines)
                    PrintLine("  " + line, LineType.Normal);
                PrintLine("  " + new string('─', 20), LineType.Dim);
            }
            catch (Exception ex) { PrintLine("  cat: " + ex.Message, LineType.Error); }
        }

        // 12. write
        private static void CmdWrite(string[] args)
        {
            if (args.Length < 2)
            {
                PrintLine("  Usage: write <filename> <text>", LineType.Warning);
                PrintLine("  Example: write notes.txt Hello World", LineType.Dim);
                return;
            }
            string path = ResolvePath(args[0]);
            var sb = new System.Text.StringBuilder();
            for (int i = 1; i < args.Length; i++)
            {
                if (i > 1) sb.Append(' ');
                sb.Append(args[i]);
            }
            try
            {
                // append اگر فایل وجود دارد
                if (File.Exists(path))
                    File.AppendAllText(path, "\n" + sb.ToString());
                else
                    File.WriteAllText(path, sb.ToString());
                PrintLine("  Written to: " + args[0], LineType.Success);
            }
            catch (Exception ex) { PrintLine("  write: " + ex.Message, LineType.Error); }
        }

        // 13. cp / copy
        private static void CmdCopy(string[] args)
        {
            if (args.Length < 2) { PrintLine("  Usage: cp <source> <dest>", LineType.Warning); return; }
            string src = ResolvePath(args[0]);
            string dst = ResolvePath(args[1]);
            try
            {
                if (!File.Exists(src))
                { PrintLine("  cp: source not found: " + args[0], LineType.Error); return; }
                string content = File.ReadAllText(src);
                File.WriteAllText(dst, content);
                PrintLine("  Copied: " + args[0] + " → " + args[1], LineType.Success);
            }
            catch (Exception ex) { PrintLine("  cp: " + ex.Message, LineType.Error); }
        }

        // 14. mv / move / rename
        private static void CmdMove(string[] args)
        {
            if (args.Length < 2) { PrintLine("  Usage: mv <source> <dest>", LineType.Warning); return; }
            string src = ResolvePath(args[0]);
            string dst = ResolvePath(args[1]);
            try
            {
                if (!File.Exists(src))
                { PrintLine("  mv: source not found: " + args[0], LineType.Error); return; }
                string content = File.ReadAllText(src);
                File.WriteAllText(dst, content);
                File.Delete(src);
                PrintLine("  Moved: " + args[0] + " → " + args[1], LineType.Success);
            }
            catch (Exception ex) { PrintLine("  mv: " + ex.Message, LineType.Error); }
        }

        // 15. mem / free
        private static void CmdMem()
        {
            try
            {
                ulong total = Cosmos.Core.CPU.GetAmountOfRAM() * 1024UL * 1024UL;
                ulong used = Cosmos.Core.GCImplementation.GetUsedRAM();
                ulong free = total > used ? total - used : 0;
                int pct = total > 0 ? (int)((float)used / (float)total * 100f) : 0;

                PrintLine("", LineType.Normal);
                PrintLine("  Memory Usage:", LineType.Accent);
                PrintLine("  Total : " + (total / (1024 * 1024)) + " MB", LineType.Normal);
                PrintLine("  Used  : " + (used / (1024 * 1024)) + " MB  (" + pct + "%)", LineType.Normal);
                PrintLine("  Free  : " + (free / (1024 * 1024)) + " MB", LineType.Success);

                // بار گرافیکی ساده
                int barLen = 30;
                int filled = (int)((float)pct / 100f * barLen);
                var bar = new System.Text.StringBuilder("  [");
                for (int i = 0; i < barLen; i++)
                    bar.Append(i < filled ? '#' : '.');
                bar.Append("]");
                PrintLine(bar.ToString(), pct > 80 ? LineType.Error : LineType.Normal);
                PrintLine("", LineType.Normal);
            }
            catch
            {
                PrintLine("  Unable to read memory info.", LineType.Error);
            }
        }

        // 16. sysinfo
        private static void CmdSysinfo()
        {
            PrintLine("", LineType.Normal);
            PrintLine("  System Information:", LineType.Accent);
            PrintLine("  " + new string('─', 34), LineType.Dim);
            PrintLine("  OS      : ParsOS v" + ParsOS.Kernel.Version, LineType.Normal);

            try
            {
                ulong ramMB = Cosmos.Core.CPU.GetAmountOfRAM();
                PrintLine("  RAM     : " + ramMB + " MB", LineType.Normal);
            }
            catch { PrintLine("  RAM     : N/A", LineType.Dim); }

            PrintLine("  Arch    : x86 (32-bit)", LineType.Normal);
            PrintLine("  Kernel  : Cosmos", LineType.Normal);
            PrintLine("  FS      : CosmosVFS", LineType.Normal);
            PrintLine("  Theme   : " + (Theme.DarkMode ? "Dark" : "Light"), LineType.Normal);
            PrintLine("  CWD     : " + _cwd, LineType.Normal);
            PrintLine("", LineType.Normal);
        }

        // 17. version / ver
        private static void CmdVersion()
        {
            PrintLine("  ParsOS OS  v" + ParsOS.Kernel.Version, LineType.Accent);
            PrintLine("  Terminal   v1.0", LineType.Dim);
        }

        // 18. history
        private static void CmdHistory()
        {
            if (_history.Count == 0) { PrintLine("  (no history)", LineType.Dim); return; }
            PrintLine("  Command History:", LineType.Accent);
            for (int i = 0; i < _history.Count; i++)
                PrintLine("  " + (i + 1).ToString().PadLeft(3) + "  " + _history[i], LineType.Normal);
        }

        // 19. reboot
        private static void CmdReboot()
        {
            PrintLine("  Rebooting...", LineType.Warning);
            Sys.Power.Reboot();
        }

        // 20. shutdown
        private static void CmdShutdown()
        {
            PrintLine("  Shutting down...", LineType.Warning);
            Sys.Power.Shutdown();
        }

        // ═══════════════════════════════════════════════════════
        //  رندر
        // ═══════════════════════════════════════════════════════
        public static void Draw(WindowInfo w, PCScreenFont font)
        {
            var canvas = GraphicsManager.WCanvas;
            int th = WindowInfo.TitleH;

            // ─── پس‌زمینه مشکی پنجره ─────────────────────────────
            int bgX = w.X + 1;
            int bgY = w.Y + th;
            int bgW = w.W - 2;
            int bgH = w.H - th - 1;
            canvas.DrawFilledRectangle(_penBg, bgX, bgY, bgW, bgH);

            // ─── محاسبه ناحیه خروجی و ورودی ─────────────────────
            int inputAreaH = InputH + 6;          // ناحیه ورودی در پایین
            int outputH = bgH - inputAreaH - PadY;
            int visLines = outputH / LineH;

            // ─── خطوط خروجی ──────────────────────────────────────
            int startLine = _output.Count - visLines - _scrollOffset;
            if (startLine < 0) startLine = 0;
            int endLine = startLine + visLines;
            if (endLine > _output.Count) endLine = _output.Count;

            for (int i = startLine; i < endLine; i++)
            {
                var line = _output[i];
                int drawY = bgY + PadY + (i - startLine) * LineH;
                Pen pen = GetLinePen(line.Type);

                // کوتاه کردن خط اگر از عرض پنجره بیشتر شد
                string text = line.Text;
                int maxChars = (bgW - PadX * 2) / CharW;
                if (text.Length > maxChars)
                    text = text.Substring(0, maxChars - 1) + "…";

                canvas.DrawString(text, font, pen, bgX + PadX, drawY);
            }

            // ─── خط جداکننده بالای ناحیه ورودی ──────────────────
            int inputLineY = w.Y + w.H - inputAreaH - 1;
            canvas.DrawLine(_penTitleLine, bgX, inputLineY, bgX + bgW, inputLineY);

            // ─── ناحیه ورودی: highlight پس‌زمینه ─────────────────
            canvas.DrawFilledRectangle(_penHighlight, bgX, inputLineY + 1, bgW, inputAreaH);

            // prompt + input text
            int inputY = inputLineY + (inputAreaH - LineH) / 2;
            string prompt = GetPrompt();
            canvas.DrawString(prompt, font, _penPrompt, bgX + PadX, inputY);

            int inputTextX = bgX + PadX + prompt.Length * CharW;
            canvas.DrawString(_input, font, _penInput, inputTextX, inputY);

            // ─── cursor ─────────────────────────────────────────
            if (_cursorVisible)
            {
                int curX = inputTextX + _cursorPos * CharW;
                canvas.DrawFilledRectangle(_penCursor, curX, inputY + 1, 2, LineH - 2);
            }

            // ─── scroll indicator ────────────────────────────────
            if (_scrollOffset > 0)
            {
                string scrollMsg = "↑ +" + _scrollOffset + " lines";
                canvas.DrawString(scrollMsg, font, _penWarning,
                    bgX + bgW - scrollMsg.Length * CharW - PadX, bgY + PadY);
            }
        }

        // ─── انتخاب pen بر اساس نوع خط ───────────────────────────
        private static Pen GetLinePen(LineType t)
        {
            switch (t)
            {
                case LineType.Error: return _penError;
                case LineType.Success: return _penSuccess;
                case LineType.Warning: return _penWarning;
                case LineType.Accent: return _penAccent;
                case LineType.Prompt: return _penPrompt;
                case LineType.Dir: return _penDir;
                case LineType.Dim: return _penDim;
                default: return _penOutput;
            }
        }
    }
}