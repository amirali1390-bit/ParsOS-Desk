// FileExplorerApp.cs
// پوشه: Apps/FileExplorerApp.cs
// فایل اکسپلورر کامل برای ParsOS
// قابلیت‌ها: مرور پوشه‌ها، باز کردن فایل‌ها، ایجاد/حذف، نمایش جزئیات

using Cosmos.System.FileSystem;
using Cosmos.System.Graphics;
using Cosmos.System.Graphics.Fonts;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using ParsOS.GUI;   // WindowInfo, GraphicsManager
using ParsOS.Utils;   // BitmapScaler

namespace ParsOS.Apps
{
    // ═══════════════════════════════════════════════════════════
    //  وضعیت مشترک بین پنجره‌های مختلف File Explorer
    //  هر پنجره یک نمونه FileExplorerState دارد
    // ═══════════════════════════════════════════════════════════
    public class FileExplorerState
    {
        // مسیر جاری
        public string CurrentPath = @"0:\";

        // لیست آیتم‌ها در پوشه جاری
        public List<FileSystemEntry> Entries = new List<FileSystemEntry>();

        // آیتم انتخاب‌شده (ایندکس در Entries) - برای highlight
        public int SelectedIndex = -1;

        // اسکرول
        public int ScrollOffset = 0;

        // نوار آدرس: آیا در حال ویرایش است؟
        public bool EditingAddress = false;
        public string AddressBuffer = "";

        // نمایش پیام خطا
        public string ErrorMessage = "";
        public int ErrorTimer = 0; // فریم تا پاک شدن خطا

        // پنل جزئیات فایل (سمت راست)
        public bool ShowDetails = true;

        // حالت view: 0=لیست، 1=grid
        public int ViewMode = 0;

        // breadcrumb cache
        public string[] Breadcrumbs = new string[0];
        public string[] BreadcrumbPaths = new string[0];

        // تاریخچه مسیر برای دکمه Back/Forward
        public List<string> History = new List<string>();
        public int HistoryIndex = -1;

        // flag برای نمایش dialog ایجاد فایل/پوشه جدید
        public bool ShowNewDialog = false;
        public bool NewDialogIsFolder = false; // true=پوشه، false=فایل
        public string NewNameBuffer = "";

        // flag برای تأیید حذف
        public bool ShowDeleteConfirm = false;
    }

    // ─── ساختار یک آیتم در لیست فایل‌ها ─────────────────────────
    public class FileSystemEntry
    {
        public string Name;
        public bool IsDirectory;
        public long Size;       // برای فایل‌ها (بایت)
        public string Extension;
        public string DisplaySize; // رشته آماده برای نمایش
    }

    // ═══════════════════════════════════════════════════════════
    //  برنامه File Explorer
    // ═══════════════════════════════════════════════════════════
    public static class FileExplorerApp
    {
        // Flag شناسایی پنجره
        public const string ContentFlag = "FILEEXPLORER_APP";

        // ─── شمارنده یکتا برای ID پنجره‌ها ─────────────────────
        // هر پنجره File Explorer یک ID ثابت دریافت می‌کند که
        // با جابجایی پنجره تغییر نمی‌کند → مشکل state گم شدن حل می‌شود
        private static int _nextWindowId = 1;

        // ابعاد پانل‌ها
        private const int SidebarW = 130;   // عرض sidebar چپ
        private const int DetailsW = 180;   // عرض پانل جزئیات راست
        private const int RowH = 22;        // ارتفاع هر ردیف در لیست
        private const int ToolbarH = 32;    // ارتفاع نوار ابزار
        private const int AddrBarH = 26;    // ارتفاع نوار آدرس
        private const int BreadH = 24;      // ارتفاع breadcrumb

        // ─── نگاشت پنجره ← وضعیت (چون WindowInfo.Content یک string است)
        // Key = Title+X+Y (منحصربه‌فرد کافی است)
        private static Dictionary<string, FileExplorerState> _states
            = new Dictionary<string, FileExplorerState>();

        // ─── Cache آیکون پوشه در اندازه‌های مختلف ───────────────────────────
        // اندازه لیست (List view): ارتفاع ردیف = 22px → آیکون 20×20 (بزرگ‌تر برای خوانایی)
        // اندازه Details panel:    آیکون بزرگ 40×40
        // یک‌بار محاسبه، بارها استفاده
        private static Bitmap _folderIconList = null;  // 20×20
        private static Bitmap _folderIconDetails = null;  // 40×40
        private static bool _folderIconsLoaded = false;

        /// <summary>
        /// آیکون‌های cache‌شده را (اگر هنوز ساخته نشده‌اند) می‌سازد.
        /// فقط یک‌بار اجرا می‌شود.
        /// </summary>
        private static void EnsureFolderIcons()
        {
            if (_folderIconsLoaded) return;
            _folderIconsLoaded = true;

            var src = Kernel.IconFolder20; // Bitmap اصلی (هر اندازه‌ای)
            if (src == null) return;

            // 20×20 برای لیست — ScaleSquare نسبت را حفظ می‌کند
            _folderIconList = BitmapScaler.ScaleSquare(src, 20);

            // 40×40 برای پانل Details
            _folderIconDetails = BitmapScaler.ScaleSquare(src, 40);
        }


        // ─── رشته‌های ثابت — جلوگیری از allocation هر فریم ──────────────────
        private static readonly string _strDir = "<DIR>";
        private static readonly string _strFolder = "Folder";
        private static readonly string _strFile = "File";
        private static readonly string _strItems = " items";
        private static readonly string _strSelected = "  |  Selected: ";
        private static readonly string _strDetails = "Details";
        private static readonly string _strPlaces = "PLACES";
        private static readonly string _strNoSel = "No selection";
        private static readonly string _strCurrent = "Current:";
        private static readonly string _strItemsColon = "Items:";
        private static readonly string _strType = "Type:";
        private static readonly string _strSize = "Size:";
        private static readonly string _strPath = "Path:";
        private static readonly string _strEllipsis = "..";
        private static readonly string _strDotDotDot = "...";
        // اولین حرف هر نام برنامه — ثابت، نه Substring هر فریم
        private static readonly string[] _appInitials = { "S", "N", "F", "T" };

        // ─── پن‌های اختصاصی ──────────────────────────────────────
        private static readonly Pen _penSidebar = new Pen(Color.FromArgb(25, 25, 42));
        private static readonly Pen _penSidebarBorder = new Pen(Color.FromArgb(55, 55, 90));
        private static readonly Pen _penSidebarHdr = new Pen(Color.FromArgb(130, 130, 180));
        private static readonly Pen _penSidebarItem = new Pen(Color.FromArgb(200, 200, 220));
        private static readonly Pen _penSidebarActive = new Pen(Color.FromArgb(80, 80, 130));
        private static readonly Pen _penSidebarActiveText = new Pen(Color.FromArgb(160, 180, 255));
        private static readonly Pen _penRow = new Pen(Color.FromArgb(42, 42, 62));
        private static readonly Pen _penRowAlt = new Pen(Color.FromArgb(38, 38, 56));
        private static readonly Pen _penRowSelected = new Pen(Color.FromArgb(70, 80, 150));
        private static readonly Pen _penRowHover = new Pen(Color.FromArgb(52, 52, 78));
        private static readonly Pen _penFolder = new Pen(Color.FromArgb(250, 180, 60));
        private static readonly Pen _penFile = new Pen(Color.FromArgb(140, 180, 255));
        private static readonly Pen _penFileText = new Pen(Color.FromArgb(180, 210, 255));
        private static readonly Pen _penDirText = new Pen(Color.FromArgb(255, 195, 80));
        private static readonly Pen _penDetails = new Pen(Color.FromArgb(22, 22, 38));
        private static readonly Pen _penDetailsBorder = new Pen(Color.FromArgb(60, 60, 100));
        private static readonly Pen _penDetailsLabel = new Pen(Color.FromArgb(120, 130, 180));
        private static readonly Pen _penDetailsValue = new Pen(Color.FromArgb(210, 215, 235));
        private static readonly Pen _penToolbar = new Pen(Color.FromArgb(30, 30, 48));
        private static readonly Pen _penBtn = new Pen(Color.FromArgb(55, 55, 85));
        private static readonly Pen _penBtnHover = new Pen(Color.FromArgb(75, 75, 115));
        private static readonly Pen _penBtnText = new Pen(Color.FromArgb(200, 205, 230));
        private static readonly Pen _penAddrBar = new Pen(Color.FromArgb(35, 35, 55));
        private static readonly Pen _penAddrBorder = new Pen(Color.FromArgb(70, 70, 110));
        private static readonly Pen _penAddrText = new Pen(Color.FromArgb(215, 220, 240));
        private static readonly Pen _penBreadcrumb = new Pen(Color.FromArgb(28, 28, 46));
        private static readonly Pen _penCrumbSep = new Pen(Color.FromArgb(80, 80, 120));
        private static readonly Pen _penCrumbText = new Pen(Color.FromArgb(150, 160, 220));
        private static readonly Pen _penCrumbActive = new Pen(Color.FromArgb(200, 210, 255));
        private static readonly Pen _penStatusBar = new Pen(Color.FromArgb(20, 20, 34));
        private static readonly Pen _penStatusText = new Pen(Color.FromArgb(130, 140, 180));
        private static readonly Pen _penError = new Pen(Color.FromArgb(220, 60, 60));
        private static readonly Pen _penListBg = new Pen(Color.FromArgb(38, 38, 58));
        private static readonly Pen _penHeaderBg = new Pen(Color.FromArgb(28, 28, 46));
        private static readonly Pen _penHeaderText = new Pen(Color.FromArgb(160, 165, 210));
        private static readonly Pen _penDialogBg = new Pen(Color.FromArgb(35, 35, 55));
        private static readonly Pen _penDialogBorder = new Pen(Color.FromArgb(100, 100, 160));
        private static readonly Pen _penDialogBtn = new Pen(Color.FromArgb(70, 100, 200));
        private static readonly Pen _penDialogBtnCancel = new Pen(Color.FromArgb(100, 50, 50));
        private static readonly Pen _penDialogInput = new Pen(Color.FromArgb(45, 45, 70));
        private static readonly Pen _penDialogInputBorder = new Pen(Color.FromArgb(90, 90, 140));
        private static readonly Pen _penExtIcon = new Pen(Color.FromArgb(100, 120, 200));
        private static readonly Pen _penExtIconText = new Pen(Color.FromArgb(230, 235, 255));
        private static readonly Pen _penScrollBar = new Pen(Color.FromArgb(50, 50, 80));
        private static readonly Pen _penScrollThumb = new Pen(Color.FromArgb(90, 90, 140));
        private static readonly Pen _penNewFolder = new Pen(Color.FromArgb(60, 160, 80));
        private static readonly Pen _penNewFile = new Pen(Color.FromArgb(60, 100, 200));
        private static readonly Pen _penDelete = new Pen(Color.FromArgb(180, 50, 50));
        private static readonly Pen _penConfirmBg = new Pen(Color.FromArgb(40, 20, 20));
        private static readonly Pen _penConfirmBorder = new Pen(Color.FromArgb(180, 60, 60));

        // مسیرهای میانبر sidebar
        private static readonly string[] _sidebarLabels =
        {
            "Drive 0:", "Desktop", "Documents", "Downloads",
            "Music", "Pictures", "Videos", "System"
        };
        private static readonly string[] _sidebarPaths =
        {
            @"0:\",
            @"0:\Desktop",
            @"0:\Documents",
            @"0:\Downloads",
            @"0:\Music",
            @"0:\Pictures",
            @"0:\Videos",
            @"0:\System"
        };

        // ─── حالت انتخاب عکس برای Wallpaper ─────────────────────
        // وقتی true باشد File Explorer در حالت picker عمل می‌کند:
        // با دوبار کلیک روی یک فایل تصویری، callback را صدا می‌زند.
        public static bool WallpaperPickerMode = false;
        public static Action<string> OnWallpaperPicked = null;  // path → callback

        // پسوندهای تصویری پشتیبانی‌شده
        private static readonly string[] _imageExts = { "BMP", "bmp" };

        // ═══════════════════════════════════════════════════════
        //  دریافت/ساخت وضعیت برای یک پنجره
        // ═══════════════════════════════════════════════════════
        // کلید بر اساس Title منحصربه‌فرد است (نه موقعیت پنجره).
        // Title توسط GraphicsManager با یک شمارنده یکتا ست می‌شود.
        // → با جابجا کردن پنجره، state گم نمی‌شود.
        private static string StateKey(WindowInfo w) => w.Title;

        public static FileExplorerState GetOrCreateState(WindowInfo w)
        {
            string key = StateKey(w);
            if (!_states.ContainsKey(key))
            {
                var st = new FileExplorerState();
                _states[key] = st;
                NavigateTo(st, @"0:\");
            }
            return _states[key];
        }

        // ─── ساخت Title یکتا هنگام باز کردن پنجره (از GraphicsManager فراخوانی شود) ──
        public static string NewUniqueTitle()
        {
            return "File Explorer #" + (_nextWindowId++).ToString();
        }

        // ─── پاکسازی وضعیت پس از بستن پنجره ───────────────────
        public static void CleanupState(WindowInfo w)
        {
            string key = StateKey(w);
            if (_states.ContainsKey(key))
            {
                var st = _states[key];
                st.Entries.Clear();
                st.History.Clear();
                st.Breadcrumbs = new string[0];
                st.BreadcrumbPaths = new string[0];
                st.ErrorMessage = "";
                _states.Remove(key);
                // cache count را reset کنیم چون این پنجره بسته شد
                _cachedEntryCount = -1;
                Cosmos.Core.Memory.Heap.Collect();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  ناوبری در سیستم فایل
        // ═══════════════════════════════════════════════════════
        public static void NavigateTo(FileExplorerState st, string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    st.ErrorMessage = "Path not found: " + path;
                    st.ErrorTimer = 180;
                    return;
                }

                // ذخیره در تاریخچه
                if (st.HistoryIndex >= 0
                    && st.HistoryIndex < st.History.Count
                    && st.History[st.HistoryIndex] == path)
                {
                    // همان مسیر - بدون تغییر تاریخچه
                }
                else
                {
                    // حذف آینده اگر برگشته بودیم
                    while (st.History.Count > st.HistoryIndex + 1 && st.History.Count > 0)
                        st.History.RemoveAt(st.History.Count - 1);

                    st.History.Add(path);
                    st.HistoryIndex = st.History.Count - 1;

                    // ─── سقف تاریخچه: بیش از ۳۰ مسیر نگه نمی‌داریم ─────
                    if (st.History.Count > 30)
                    {
                        st.History.RemoveAt(0);
                        st.HistoryIndex = st.History.Count - 1;
                    }
                }

                st.CurrentPath = path;
                st.SelectedIndex = -1;
                st.ScrollOffset = 0;
                st.ErrorMessage = "";

                // بارگذاری محتوا
                LoadDirectory(st);

                // ساخت breadcrumb
                BuildBreadcrumbs(st);
            }
            catch (Exception ex)
            {
                st.ErrorMessage = "Error: " + ex.Message;
                st.ErrorTimer = 240;
            }
        }

        private static void LoadDirectory(FileExplorerState st)
        {
            // ─── reuse: Entries قدیمی را پاک می‌کنیم نه اینکه List جدید بسازیم ──
            // بدون List موقت — پاک کردن و افزودن مستقیم جلوگیری از GC فشار می‌کند
            var saved = new List<FileSystemEntry>(st.Entries); // snapshot برای rollback

            try
            {
                st.Entries.Clear();

                // پوشه‌ها اول
                string[] dirs = Directory.GetDirectories(st.CurrentPath);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string name = Path.GetFileName(dirs[i]);
                    if (string.IsNullOrEmpty(name)) name = dirs[i];
                    st.Entries.Add(new FileSystemEntry
                    {
                        Name = name,
                        IsDirectory = true,
                        Size = 0,
                        Extension = "",
                        DisplaySize = "<DIR>"
                    });
                }

                // فایل‌ها
                string[] files = Directory.GetFiles(st.CurrentPath);
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileName(files[i]);
                    long size = 0;
                    try { var fi = new FileInfo(files[i]); size = fi.Length; } catch { }

                    string ext = Path.GetExtension(name);
                    if (ext.Length > 0) ext = ext.Substring(1).ToUpper();

                    st.Entries.Add(new FileSystemEntry
                    {
                        Name = name,
                        IsDirectory = false,
                        Size = size,
                        Extension = ext,
                        DisplaySize = FormatSize(size)
                    });
                }

                if (st.SelectedIndex >= st.Entries.Count)
                    st.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                // rollback به وضعیت قبل
                st.Entries.Clear();
                for (int i = 0; i < saved.Count; i++) st.Entries.Add(saved[i]);
                st.ErrorMessage = "Read error: " + ex.Message;
                st.ErrorTimer = 240;
            }
            // saved برای GC آزاد می‌شود — اما این فقط در صورت خطا اتفاق می‌افتد
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes.ToString() + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024).ToString() + " KB";
            return (bytes / (1024 * 1024)).ToString() + " MB";
        }

        private static void BuildBreadcrumbs(FileExplorerState st)
        {
            // "0:\Documents\Music" → ["0:", "Documents", "Music"]
            string p = st.CurrentPath.Replace('/', '\\');
            if (p.EndsWith("\\") && p.Length > 3)
                p = p.Substring(0, p.Length - 1);

            string[] parts = p.Split('\\');
            st.Breadcrumbs = new string[parts.Length];
            st.BreadcrumbPaths = new string[parts.Length];

            string acc = "";
            for (int i = 0; i < parts.Length; i++)
            {
                if (i == 0)
                    acc = parts[0] + "\\";
                else
                    acc = acc + parts[i] + "\\";

                st.Breadcrumbs[i] = parts[i].Length == 0 ? "Root" : parts[i];
                st.BreadcrumbPaths[i] = acc;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  ورودی کیبورد
        // ═══════════════════════════════════════════════════════
        public static bool HandleKeyboard(FileExplorerState st)
        {
            if (Cosmos.System.KeyboardManager.TryReadKey(out var key))
            {
                // ─── Dialog ایجاد جدید ──────────────────────────
                if (st.ShowNewDialog)
                {
                    HandleDialogKey(st, key);
                    return true;
                }

                // ─── نوار آدرس ──────────────────────────────────
                if (st.EditingAddress)
                {
                    HandleAddressKey(st, key);
                    return true;
                }

                // ─── ناوبری با کیبورد ────────────────────────────
                switch (key.Key)
                {
                    case Cosmos.System.ConsoleKeyEx.UpArrow:
                        if (st.SelectedIndex > 0) st.SelectedIndex--;
                        EnsureVisible(st);
                        break;

                    case Cosmos.System.ConsoleKeyEx.DownArrow:
                        if (st.SelectedIndex < st.Entries.Count - 1) st.SelectedIndex++;
                        EnsureVisible(st);
                        break;

                    case Cosmos.System.ConsoleKeyEx.Enter:
                        if (st.SelectedIndex >= 0 && st.SelectedIndex < st.Entries.Count)
                            OpenEntry(st, st.SelectedIndex);
                        break;

                    case Cosmos.System.ConsoleKeyEx.Backspace:
                        // برو به پوشه والد
                        GoUp(st);
                        break;

                    case Cosmos.System.ConsoleKeyEx.Delete:
                        if (st.SelectedIndex >= 0 && st.SelectedIndex < st.Entries.Count)
                            st.ShowDeleteConfirm = true;
                        break;

                    case Cosmos.System.ConsoleKeyEx.F5:
                        // رفرش
                        LoadDirectory(st);
                        break;
                }
                return true;
            }
            return false;
        }

        private static void HandleDialogKey(FileExplorerState st, Cosmos.System.KeyEvent key)
        {
            if (key.Key == Cosmos.System.ConsoleKeyEx.Enter)
            {
                ConfirmNewDialog(st);
            }
            else if (key.Key == Cosmos.System.ConsoleKeyEx.Escape)
            {
                st.ShowNewDialog = false;
                st.NewNameBuffer = "";
            }
            else if (key.Key == Cosmos.System.ConsoleKeyEx.Backspace)
            {
                if (st.NewNameBuffer.Length > 0)
                    st.NewNameBuffer = st.NewNameBuffer.Substring(0, st.NewNameBuffer.Length - 1);
            }
            else if (key.KeyChar >= 32 && key.KeyChar < 127 && st.NewNameBuffer.Length < 32)
            {
                st.NewNameBuffer += key.KeyChar.ToString();
            }
        }

        private static void HandleAddressKey(FileExplorerState st, Cosmos.System.KeyEvent key)
        {
            if (key.Key == Cosmos.System.ConsoleKeyEx.Enter)
            {
                st.EditingAddress = false;
                if (!string.IsNullOrEmpty(st.AddressBuffer))
                    NavigateTo(st, st.AddressBuffer);
            }
            else if (key.Key == Cosmos.System.ConsoleKeyEx.Escape)
            {
                st.EditingAddress = false;
                st.AddressBuffer = st.CurrentPath;
            }
            else if (key.Key == Cosmos.System.ConsoleKeyEx.Backspace)
            {
                if (st.AddressBuffer.Length > 0)
                    st.AddressBuffer = st.AddressBuffer.Substring(0, st.AddressBuffer.Length - 1);
            }
            else if (key.KeyChar >= 32 && key.KeyChar < 127 && st.AddressBuffer.Length < 80)
            {
                st.AddressBuffer += key.KeyChar.ToString();
            }
        }

        private static void EnsureVisible(FileExplorerState st)
        {
            // محاسبه ظرفیت لیست را در اینجا نداریم، 10 تقریبی است
            const int visible = 10;
            if (st.SelectedIndex < st.ScrollOffset)
                st.ScrollOffset = st.SelectedIndex;
            if (st.SelectedIndex >= st.ScrollOffset + visible)
                st.ScrollOffset = st.SelectedIndex - visible + 1;
        }

        // ═══════════════════════════════════════════════════════
        //  عملیات فایل‌سیستم
        // ═══════════════════════════════════════════════════════
        private static void OpenEntry(FileExplorerState st, int idx)
        {
            var entry = st.Entries[idx];
            if (entry.IsDirectory)
            {
                string newPath = st.CurrentPath.TrimEnd('\\') + "\\" + entry.Name + "\\";
                NavigateTo(st, newPath);
            }
            else
            {
                string ext = entry.Extension.ToUpper();
                string fullPath = st.CurrentPath.TrimEnd('\\') + "\\" + entry.Name;

                // ─── حالت Wallpaper Picker ──────────────────────────
                if (WallpaperPickerMode && IsImageExt(ext))
                {
                    OnWallpaperPicked?.Invoke(fullPath);
                    return;
                }

                // باز کردن فایل‌های متنی در Notepad
                if (ext == "TXT" || ext == "LOG" || ext == "CS" || ext == "INI" || ext == "CFG")
                {
                    try
                    {
                        string content = File.ReadAllText(fullPath);
                        GraphicsManager.OpenNewWindow("Notepad - " + entry.Name,
                            NotepadApp.ContentFlag);
                        var wins = GraphicsManager.Windows;
                        if (wins.Count > 0)
                        {
                            var nw = wins[wins.Count - 1];
                            NotepadApp.SetContent(nw, content);
                        }
                    }
                    catch (Exception ex)
                    {
                        st.ErrorMessage = "Cannot open: " + ex.Message;
                        st.ErrorTimer = 180;
                    }
                }
                // باز کردن تصاویر BMP
                else if (IsImageExt(ext))
                {
                    try
                    {
                        byte[] data = File.ReadAllBytes(fullPath);
                        var bmp = new Bitmap(data);
                        Kernel.Wallpaper = bmp;
                        GraphicsManager.OnWallpaperChanged();
                        st.ErrorMessage = "Wallpaper set: " + entry.Name;
                        st.ErrorTimer = 180;
                    }
                    catch (Exception ex)
                    {
                        st.ErrorMessage = "Cannot load image: " + ex.Message;
                        st.ErrorTimer = 180;
                    }
                }
                // ─── نصب پکیج برنامه‌ی اختصاصی (.pap) با دوبار کلیک ──────
                // بدون wizard: نصب فوری + یک کارت کوچک نتیجه (PapInstallerUI)
                else if (ext == "PAP")
                {
                    PapInstallerUI.BeginInstall(fullPath);
                }
                else
                {
                    st.ErrorMessage = "No app for ." + entry.Extension + " files";
                    st.ErrorTimer = 150;
                }
            }
        }

        // ─── بررسی پسوند تصویری ─────────────────────────────────
        private static bool IsImageExt(string extUpper)
        {
            return extUpper == "BMP" || extUpper == "PNG" || extUpper == "JPG"
                || extUpper == "JPEG" || extUpper == "GIF";
        }

        private static void GoUp(FileExplorerState st)
        {
            string p = st.CurrentPath.TrimEnd('\\');
            int last = p.LastIndexOf('\\');
            if (last > 0)
                NavigateTo(st, p.Substring(0, last + 1));
            else if (last == 2) // "0:\"
                return;
        }

        private static void ConfirmNewDialog(FileExplorerState st)
        {
            if (string.IsNullOrEmpty(st.NewNameBuffer)) return;
            string fullPath = st.CurrentPath.TrimEnd('\\') + "\\" + st.NewNameBuffer;
            try
            {
                if (st.NewDialogIsFolder)
                    Directory.CreateDirectory(fullPath);
                else
                    File.WriteAllText(fullPath, "");

                st.ShowNewDialog = false;
                st.NewNameBuffer = "";
                LoadDirectory(st); // رفرش
            }
            catch (Exception ex)
            {
                st.ErrorMessage = "Create failed: " + ex.Message;
                st.ErrorTimer = 200;
                st.ShowNewDialog = false;
                st.NewNameBuffer = "";
            }
        }

        private static void DeleteSelected(FileExplorerState st)
        {
            if (st.SelectedIndex < 0 || st.SelectedIndex >= st.Entries.Count) return;
            var entry = st.Entries[st.SelectedIndex];
            string fullPath = st.CurrentPath.TrimEnd('\\') + "\\" + entry.Name;
            try
            {
                if (entry.IsDirectory)
                    Directory.Delete(fullPath, false); // recursive=false برای امنیت
                else
                    File.Delete(fullPath);

                st.SelectedIndex = -1;
                st.ShowDeleteConfirm = false;
                LoadDirectory(st);
            }
            catch (Exception ex)
            {
                st.ErrorMessage = "Delete failed: " + ex.Message;
                st.ErrorTimer = 200;
                st.ShowDeleteConfirm = false;
            }
        }

        // ═══════════════════════════════════════════════════════
        //  کلیک موس
        // ═══════════════════════════════════════════════════════
        public static bool HandleClick(WindowInfo w, int mx, int my)
        {
            var st = GetOrCreateState(w);
            int th = WindowInfo.TitleH;
            int wx = w.X, wy = w.Y, ww = w.W, wh = w.H;
            int contentY = wy + th;
            int contentH = wh - th;

            // ─── Dialog تأیید حذف ────────────────────────────────
            if (st.ShowDeleteConfirm)
            {
                int dw = 280, dh = 120;
                int dx = wx + (ww - dw) / 2;
                int dy = wy + th + (contentH - dh) / 2;
                // دکمه تأیید
                if (mx >= dx + 20 && mx <= dx + 100 && my >= dy + 80 && my <= dy + 105)
                { DeleteSelected(st); return true; }
                // دکمه لغو
                if (mx >= dx + 160 && mx <= dx + 260 && my >= dy + 80 && my <= dy + 105)
                { st.ShowDeleteConfirm = false; return true; }
                return true;
            }

            // ─── Dialog ایجاد جدید ───────────────────────────────
            if (st.ShowNewDialog)
            {
                int dw = 300, dh = 130;
                int dx = wx + (ww - dw) / 2;
                int dy = wy + th + (contentH - dh) / 2;
                // کلیک روی input
                if (mx >= dx + 10 && mx <= dx + 290 && my >= dy + 50 && my <= dy + 75)
                    return true; // focus در حال ویرایش است
                // تأیید
                if (mx >= dx + 10 && mx <= dx + 130 && my >= dy + 88 && my <= dy + 115)
                { ConfirmNewDialog(st); return true; }
                // لغو
                if (mx >= dx + 160 && mx <= dx + 290 && my >= dy + 88 && my <= dy + 115)
                { st.ShowNewDialog = false; st.NewNameBuffer = ""; return true; }
                return true;
            }

            // ─── نوار ابزار ──────────────────────────────────────
            int tbY = contentY;
            if (my >= tbY && my < tbY + ToolbarH)
            {
                HandleToolbarClick(st, w, mx - wx, my - tbY);
                return true;
            }

            // ─── نوار آدرس ───────────────────────────────────────
            int addrY = contentY + ToolbarH;
            if (my >= addrY && my < addrY + AddrBarH)
            {
                // کلیک روی آدرس بار → حالت ویرایش
                st.EditingAddress = true;
                st.AddressBuffer = st.CurrentPath;
                return true;
            }

            // ─── breadcrumb ──────────────────────────────────────
            int bcY = addrY + AddrBarH;
            if (my >= bcY && my < bcY + BreadH)
            {
                HandleBreadcrumbClick(st, w, mx);
                return true;
            }

            // ─── Sidebar ─────────────────────────────────────────
            int listY = bcY + BreadH;
            if (mx >= wx && mx < wx + SidebarW && my >= listY && my < wy + wh - 22)
            {
                int sideIdx = (my - listY - 30) / 20; // 30 = header height
                if (sideIdx >= 0 && sideIdx < _sidebarPaths.Length)
                    NavigateTo(st, _sidebarPaths[sideIdx]);
                return true;
            }

            // ─── لیست فایل‌ها ─────────────────────────────────────
            int listX = wx + SidebarW;
            int listW = ww - SidebarW - (st.ShowDetails ? DetailsW : 0);
            int listContentY = listY + RowH; // header row

            if (mx >= listX && mx < listX + listW && my >= listContentY && my < wy + wh - 22)
            {
                int clickedRow = (my - listContentY) / RowH + st.ScrollOffset;
                if (clickedRow >= 0 && clickedRow < st.Entries.Count)
                {
                    if (st.SelectedIndex == clickedRow)
                    {
                        // double-click شبیه‌سازی: یک بار کلیک روی آیتم انتخاب‌شده = باز کردن
                        OpenEntry(st, clickedRow);
                    }
                    else
                    {
                        st.SelectedIndex = clickedRow;
                    }
                }
                return true;
            }

            // ─── پانل جزئیات ─────────────────────────────────────
            int detX = wx + SidebarW + listW;
            if (st.ShowDetails && mx >= detX && mx < wx + ww && my >= listY && my < wy + wh - 22)
                return true;

            return false;
        }

        private static void HandleToolbarClick(FileExplorerState st, WindowInfo w, int rx, int ry)
        {
            // rx نسبت به سمت چپ پنجره است
            // دکمه Back: x=4
            if (rx >= 4 && rx < 36) { GoBack(st); return; }
            // دکمه Forward: x=40
            if (rx >= 40 && rx < 72) { GoForward(st); return; }
            // دکمه Up: x=76
            if (rx >= 76 && rx < 108) { GoUp(st); return; }
            // دکمه Refresh: x=112
            if (rx >= 112 && rx < 144) { LoadDirectory(st); return; }

            // در حالت picker، دکمه‌های ویرایش غیرفعال‌اند
            if (WallpaperPickerMode) return;

            // دکمه New Folder: x=155
            if (rx >= 155 && rx < 220)
            {
                st.ShowNewDialog = true; st.NewDialogIsFolder = true; st.NewNameBuffer = "";
                return;
            }
            // دکمه New File: x=225
            if (rx >= 225 && rx < 285)
            {
                st.ShowNewDialog = true; st.NewDialogIsFolder = false; st.NewNameBuffer = "";
                return;
            }
            // دکمه Delete: x=290
            if (rx >= 290 && rx < 340)
            {
                if (st.SelectedIndex >= 0 && st.SelectedIndex < st.Entries.Count)
                    st.ShowDeleteConfirm = true;
                return;
            }
            // دکمه Details toggle: آخر نوار
            int dBtn = w.W - 38;
            if (rx >= dBtn && rx < dBtn + 34)
            { st.ShowDetails = !st.ShowDetails; return; }
        }

        private static void HandleBreadcrumbClick(FileExplorerState st, WindowInfo w, int mx)
        {
            // هر قطعه breadcrumb را بررسی می‌کنیم — عرض باید دقیقاً با DrawBreadcrumbs
            // (که از همین فونت TTF استفاده می‌کند) هماهنگ باشد، وگرنه کلیک‌ها آفست می‌شوند
            var font = Kernel.VazirFont;
            int startX = w.X + SidebarW + 8;
            int x = startX;
            for (int i = 0; i < st.Breadcrumbs.Length; i++)
            {
                int segW = (font != null ? font.MeasureWidth(st.Breadcrumbs[i]) : st.Breadcrumbs[i].Length * 8) + 10;
                if (mx >= x && mx < x + segW)
                {
                    NavigateTo(st, st.BreadcrumbPaths[i]);
                    return;
                }
                int sepW = (font != null ? font.MeasureWidth(">") : 8) + 12;
                x += segW + sepW; // جداکننده ">"
                if (x > w.X + w.W - 20) break;
            }
        }

        private static void GoBack(FileExplorerState st)
        {
            if (st.HistoryIndex > 0)
            {
                st.HistoryIndex--;
                string path = st.History[st.HistoryIndex];
                st.CurrentPath = path;
                st.SelectedIndex = -1;
                st.ScrollOffset = 0;
                LoadDirectory(st);
                BuildBreadcrumbs(st);
            }
        }

        private static void GoForward(FileExplorerState st)
        {
            if (st.HistoryIndex < st.History.Count - 1)
            {
                st.HistoryIndex++;
                string path = st.History[st.HistoryIndex];
                st.CurrentPath = path;
                st.SelectedIndex = -1;
                st.ScrollOffset = 0;
                LoadDirectory(st);
                BuildBreadcrumbs(st);
            }
        }

        // ─── Pen‌های overlay — از پیش تخصیص‌یافته (جلوگیری از new Pen هر فریم) ──
        private static readonly Pen _penOverlayDim = new Pen(Color.FromArgb(120, 0, 0, 20));
        private static readonly Pen _penOverlayDelete = new Pen(Color.FromArgb(80, 40, 0, 0));

        // ─── Tick: فراخوانی از GraphicsManager هر فریم برای هر پنجره ──
        // مدیریت تایمرها اینجاست نه در Draw
        public static void Tick(WindowInfo w)
        {
            var st = GetOrCreateState(w);
            if (st.ErrorTimer > 0) st.ErrorTimer--;
        }

        // ═══════════════════════════════════════════════════════
        //  رندر اصلی
        // ═══════════════════════════════════════════════════════
        public static void Draw(WindowInfo w, int cx, int cy, TtfFont font)
        {
            var st = GetOrCreateState(w);
            var canvas = GraphicsManager.WCanvas;
            int th = WindowInfo.TitleH;
            int wx = w.X, wy = w.Y, ww = w.W, wh = w.H;
            int contentY = wy + th;
            int contentH = wh - th;
            int detW = st.ShowDetails ? DetailsW : 0;

            // تایمر خطا در Tick() کاهش می‌یابد، نه اینجا

            // ─── نوار ابزار ──────────────────────────────────────
            DrawToolbar(canvas, st, font, wx, contentY, ww, ToolbarH);

            // ─── نوار آدرس ───────────────────────────────────────
            int addrY = contentY + ToolbarH;
            DrawAddressBar(canvas, st, font, wx, addrY, ww, AddrBarH);

            // ─── breadcrumb ──────────────────────────────────────
            int bcY = addrY + AddrBarH;
            DrawBreadcrumbs(canvas, st, font, wx, bcY, ww, BreadH);

            // ─── ناحیه اصلی ──────────────────────────────────────
            int listY = bcY + BreadH;
            int listH = wh - (listY - wy) - 22; // 22 = status bar
            int listW = ww - SidebarW - detW;

            // Sidebar
            DrawSidebar(canvas, st, font, wx, listY, SidebarW, listH);

            // لیست فایل‌ها
            DrawFileList(canvas, st, font, wx + SidebarW, listY, listW, listH);

            // پانل جزئیات
            if (st.ShowDetails)
                DrawDetailsPanel(canvas, st, font, wx + SidebarW + listW, listY, detW, listH);

            // ─── نوار وضعیت ──────────────────────────────────────
            int statusY = wy + wh - 22;
            DrawStatusBar(canvas, st, font, wx, statusY, ww);

            // ─── Dialog ──────────────────────────────────────────
            if (st.ShowNewDialog)
                DrawNewDialog(canvas, st, font, wx, wy + th, ww, contentH);

            if (st.ShowDeleteConfirm)
                DrawDeleteConfirm(canvas, st, font, wx, wy + th, ww, contentH);
        }

        // ─── نوار ابزار ──────────────────────────────────────────
        private static void DrawToolbar(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                        int wx, int y, int ww, int h)
        {
            canvas.DrawFilledRectangle(_penToolbar, wx, y, ww, h);
            canvas.DrawLine(_penSidebarBorder, wx, y + h - 1, wx + ww, y + h - 1);

            bool canBack = st.HistoryIndex > 0;
            bool canFwd = st.HistoryIndex < st.History.Count - 1;

            DrawToolBtn(canvas, font, wx + 4, y + 4, 30, 24, "<", canBack ? _penBtn : _penSidebarBorder, canBack ? _penBtnText : _penSidebarHdr);
            DrawToolBtn(canvas, font, wx + 38, y + 4, 30, 24, ">", canFwd ? _penBtn : _penSidebarBorder, canFwd ? _penBtnText : _penSidebarHdr);
            DrawToolBtn(canvas, font, wx + 74, y + 4, 30, 24, "^", _penBtn, _penBtnText);
            DrawToolBtn(canvas, font, wx + 110, y + 4, 30, 24, "R", _penBtn, _penBtnText);

            // جداکننده
            canvas.DrawLine(_penSidebarBorder, wx + 148, y + 6, wx + 148, y + h - 6);

            if (WallpaperPickerMode)
            {
                // در حالت picker فقط یک banner نمایش می‌دهیم
                canvas.DrawFilledRectangle(_penNewFolder, wx + 154, y + 4, ww - 154 - 4, 24);
                canvas.DrawTtf(font, "Select image for wallpaper (double-click BMP)", _penBtnText, wx + 162, y + 9, _penNewFolder.Color);
            }
            else
            {
                DrawToolBtn(canvas, font, wx + 154, y + 4, 62, 24, "+Folder", _penNewFolder, _penBtnText);
                DrawToolBtn(canvas, font, wx + 220, y + 4, 56, 24, "+File", _penNewFile, _penBtnText);
                DrawToolBtn(canvas, font, wx + 280, y + 4, 52, 24, "DEL", _penDelete, _penBtnText);

                // دکمه Details در سمت راست
                int dBtn = wx + ww - 38;
                DrawToolBtn(canvas, font, dBtn, y + 4, 34, 24, st.ShowDetails ? ">|" : "|<", _penBtn, _penBtnText);
            }
        }

        private static void DrawToolBtn(WindowCanvas canvas, TtfFont font,
                                        int x, int y, int w, int h, string label,
                                        Pen bg, Pen fg)
        {
            canvas.DrawFilledRectangle(bg, x, y, w, h);
            canvas.DrawRectangle(_penSidebarBorder, x, y, w, h);
            int tx = x + (w - font.MeasureWidth(label)) / 2;
            int ty = y + (h - font.LineHeight) / 2;
            canvas.DrawTtf(font, label, fg, tx, ty, bg.Color);
        }

        // ─── نوار آدرس ───────────────────────────────────────────
        private static void DrawAddressBar(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                           int wx, int y, int ww, int h)
        {
            canvas.DrawFilledRectangle(_penAddrBar, wx, y, ww, h);
            canvas.DrawRectangle(_penAddrBorder, wx + 4, y + 2, ww - 8, h - 4);

            string text = st.EditingAddress ? st.AddressBuffer : st.CurrentPath;
            if (st.EditingAddress) text += "_"; // cursor

            // کوتاه کردن متن اگر طولانی باشد — با عرض واقعی گلیف‌های TTF (نه فرض ۸px مونواسپیس)
            int avail = ww - 16;
            if (font.MeasureWidth(text) > avail)
            {
                int keep = text.Length;
                string shown = "..." + text.Substring(text.Length - keep);
                while (keep > 1 && font.MeasureWidth(shown) > avail)
                {
                    keep--;
                    shown = "..." + text.Substring(text.Length - keep);
                }
                text = shown;
            }

            canvas.DrawTtf(font, text, _penAddrText, wx + 8, y + 5, _penAddrBar.Color);
        }

        // ─── breadcrumb ──────────────────────────────────────────
        private static void DrawBreadcrumbs(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                            int wx, int y, int ww, int h)
        {
            canvas.DrawFilledRectangle(_penBreadcrumb, wx, y, ww, h);
            canvas.DrawLine(_penSidebarBorder, wx, y + h - 1, wx + ww, y + h - 1);

            int x = wx + SidebarW + 8;
            for (int i = 0; i < st.Breadcrumbs.Length; i++)
            {
                bool isLast = (i == st.Breadcrumbs.Length - 1);
                Pen textPen = isLast ? _penCrumbActive : _penCrumbText;
                string seg = st.Breadcrumbs[i];
                canvas.DrawTtf(font, seg, textPen, x, y + 5, _penBreadcrumb.Color);
                x += font.MeasureWidth(seg) + 4;
                if (!isLast)
                {
                    canvas.DrawTtf(font, ">", _penCrumbSep, x, y + 5, _penBreadcrumb.Color);
                    x += font.MeasureWidth(">") + 8;
                }
                if (x > wx + ww - 20) break;
            }
        }

        // ─── Sidebar ─────────────────────────────────────────────
        private static void DrawSidebar(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                        int wx, int y, int w, int h)
        {
            canvas.DrawFilledRectangle(_penSidebar, wx, y, w, h);
            canvas.DrawLine(_penSidebarBorder, wx + w - 1, y, wx + w - 1, y + h);

            canvas.DrawTtf(font, "PLACES", _penSidebarHdr, wx + 8, y + 8, _penSidebar.Color);
            canvas.DrawLine(_penSidebarBorder, wx + 4, y + 24, wx + w - 4, y + 24);

            for (int i = 0; i < _sidebarLabels.Length; i++)
            {
                int iy = y + 30 + i * 20;
                bool isCurrent = (st.CurrentPath == _sidebarPaths[i] ||
                                  st.CurrentPath.StartsWith(_sidebarPaths[i]));
                Color rowBg = _penSidebar.Color;
                if (isCurrent)
                {
                    canvas.DrawFilledRectangle(_penSidebarActive, wx + 2, iy - 2, w - 4, 19);
                    rowBg = _penSidebarActive.Color;
                }

                // آیکون ساده
                string icon = i == 0 ? "[D]" : "[F]";
                canvas.DrawTtf(font, icon, _penFolder, wx + 6, iy, rowBg);
                canvas.DrawTtf(font, _sidebarLabels[i],
                    isCurrent ? _penSidebarActiveText : _penSidebarItem, wx + 32, iy, rowBg);
            }
        }

        // ─── لیست فایل‌ها ─────────────────────────────────────────
        private static void DrawFileList(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                         int x, int y, int w, int h)
        {
            // هدر ستون‌ها
            canvas.DrawFilledRectangle(_penHeaderBg, x, y, w, RowH);
            canvas.DrawTtf(font, "Name", _penHeaderText, x + 30, y + 4, _penHeaderBg.Color);
            canvas.DrawTtf(font, "Type", _penHeaderText, x + w - 100, y + 4, _penHeaderBg.Color);
            canvas.DrawTtf(font, "Size", _penHeaderText, x + w - 50, y + 4, _penHeaderBg.Color);
            canvas.DrawLine(_penSidebarBorder, x, y + RowH - 1, x + w, y + RowH - 1);

            int listH = h - RowH;
            int maxVisible = listH / RowH;
            canvas.DrawFilledRectangle(_penListBg, x, y + RowH, w, listH);

            for (int i = 0; i < maxVisible; i++)
            {
                int entryIdx = i + st.ScrollOffset;
                if (entryIdx >= st.Entries.Count) break;

                int ry = y + RowH + i * RowH;
                var entry = st.Entries[entryIdx];

                // پس‌زمینه ردیف
                Pen rowBgPen;
                if (entryIdx == st.SelectedIndex)
                    rowBgPen = _penRowSelected;
                else if (i % 2 == 0)
                    rowBgPen = _penRow;
                else
                    rowBgPen = _penRowAlt;
                canvas.DrawFilledRectangle(rowBgPen, x, ry, w, RowH);
                Color rowBg = rowBgPen.Color;

                // آیکون
                DrawFileIcon(canvas, font, x + 2, ry + 1, entry, rowBg);

                // نام — کوتاه‌سازی بر اساس عرض واقعی گلیف با TTF (نه فرض ۸px مونواسپیس)
                Pen namePen;
                if (entry.IsDirectory)
                    namePen = _penDirText;
                else if (IsImageExt(entry.Extension.ToUpper()))
                    namePen = _penNewFolder;
                else
                    namePen = _penFileText;

                int maxNameW = w - 130;
                string displayName = entry.Name;
                if (font.MeasureWidth(displayName) > maxNameW)
                {
                    int keep = displayName.Length;
                    while (keep > 1 && font.MeasureWidth(displayName.Substring(0, keep)) + font.MeasureWidth(_strEllipsis) > maxNameW)
                        keep--;
                    string head = displayName.Substring(0, keep);
                    canvas.DrawTtf(font, head, namePen, x + 26, ry + 4, rowBg);
                    canvas.DrawTtf(font, _strEllipsis, namePen, x + 26 + font.MeasureWidth(head), ry + 4, rowBg);
                }
                else
                    canvas.DrawTtf(font, displayName, namePen, x + 26, ry + 4, rowBg);

                // نوع/پسوند
                string typeStr = entry.IsDirectory ? _strFolder : (entry.Extension.Length > 0 ? entry.Extension : _strFile);
                canvas.DrawTtf(font, typeStr, _penStatusText, x + w - 100, ry + 4, rowBg);

                // اندازه
                canvas.DrawTtf(font, entry.DisplaySize, _penStatusText, x + w - 50, ry + 4, rowBg);
            }

            // scrollbar
            if (st.Entries.Count > maxVisible)
                DrawScrollbar(canvas, x + w - 8, y + RowH, 6, listH, st.Entries.Count, st.ScrollOffset, maxVisible);
        }

        private static void DrawFileIcon(WindowCanvas canvas, TtfFont font,
                                 int x, int y, FileSystemEntry entry, Color rowBg)
        {
            if (entry.IsDirectory)
            {
                // ── تلاش برای نمایش آیکون واقعی پوشه ──────────────────
                EnsureFolderIcons();

                if (_folderIconList != null)
                {
                    // آیکون 20×20 را مرکز‌چین عمودی در ردیف 22px رندر می‌کنیم
                    // y = ry+1 → آیکون 20px کاملاً جا می‌شود
                    canvas.DrawImageAlpha(_folderIconList, x, y);
                }
                else
                {
                    // fallback: آیکون برداری ساده (وقتی Bitmap موجود نباشد)
                    canvas.DrawFilledRectangle(_penFolder, x, y + 2, 20, 13);
                    canvas.DrawFilledRectangle(_penFolder, x, y, 8, 4);
                    canvas.DrawRectangle(_penDirText, x, y + 2, 20, 13);
                }
            }
            else
            {
                canvas.DrawFilledRectangle(_penExtIcon, x, y, 18, 16);
                canvas.DrawFilledRectangle(_penListBg, x + 12, y, 6, 5);
                canvas.DrawLine(_penSidebarHdr, x + 12, y, x + 18, y + 5);
                canvas.DrawLine(_penSidebarHdr, x + 12, y, x + 12, y + 5);
                canvas.DrawLine(_penSidebarHdr, x + 12, y + 5, x + 18, y + 5);
                if (entry.Extension.Length > 0)
                {
                    // حداکثر ۳ کاراکتر — بدون Substring اگر کوتاه‌تر است
                    if (entry.Extension.Length <= 3)
                        canvas.DrawTtf(font, entry.Extension, _penExtIconText, x + 1, y + 5, _penExtIcon.Color);
                    else
                        canvas.DrawTtf(font, entry.Extension.Substring(0, 3), _penExtIconText, x + 1, y + 5, _penExtIcon.Color);
                }
            }
        }


        // ─── Scrollbar ────────────────────────────────────────────
        private static void DrawScrollbar(WindowCanvas canvas, int x, int y, int w, int h,
                                          int total, int offset, int visible)
        {
            canvas.DrawFilledRectangle(_penScrollBar, x, y, w, h);
            if (total <= 0) return;
            int thumbH = Math.Max(20, h * visible / total);
            int thumbY = y + (h - thumbH) * offset / Math.Max(1, total - visible);
            canvas.DrawFilledRectangle(_penScrollThumb, x, thumbY, w, thumbH);
        }

        // ─── پانل جزئیات ─────────────────────────────────────────
        private static void DrawDetailsPanel(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                             int x, int y, int w, int h)
        {
            canvas.DrawFilledRectangle(_penDetails, x, y, w, h);
            canvas.DrawLine(_penDetailsBorder, x, y, x, y + h);

            Color panelBg = _penDetails.Color;
            canvas.DrawTtf(font, _strDetails, _penSidebarHdr, x + 8, y + 8, panelBg);
            canvas.DrawLine(_penDetailsBorder, x + 4, y + 24, x + w - 4, y + 24);

            if (st.SelectedIndex < 0 || st.SelectedIndex >= st.Entries.Count)
            {
                canvas.DrawTtf(font, _strNoSel, _penSidebarHdr, x + 8, y + 36, panelBg);
                canvas.DrawTtf(font, _strCurrent, _penDetailsLabel, x + 8, y + 60, panelBg);
                string pathDisp = st.CurrentPath;
                if (pathDisp.Length > 18)
                {
                    canvas.DrawTtf(font, _strDotDotDot, _penDetailsValue, x + 8, y + 76, panelBg);
                    canvas.DrawTtf(font, pathDisp.Substring(pathDisp.Length - 15), _penDetailsValue, x + 32, y + 76, panelBg);
                }
                else
                    canvas.DrawTtf(font, pathDisp, _penDetailsValue, x + 8, y + 76, panelBg);
                canvas.DrawTtf(font, _strItemsColon, _penDetailsLabel, x + 8, y + 96, panelBg);
                // فقط وقتی count تغییر کرده رشته را می‌سازیم (cache مشترک با StatusBar)
                canvas.DrawTtf(font, _cachedCountStr.Substring(0, _cachedCountStr.IndexOf(' ')),
                    _penDetailsValue, x + 8, y + 112, panelBg);
                return;
            }

            var entry = st.Entries[st.SelectedIndex];

            // آیکون بزرگ
            if (entry.IsDirectory)
            {
                EnsureFolderIcons();

                if (_folderIconDetails != null)
                {
                    // آیکون 40×40 مرکزچین در پانل Details
                    int iconX = x + (w - 40) / 2;
                    int iconY = y + 28;
                    canvas.DrawImageAlpha(_folderIconDetails, iconX, iconY);
                }
                else
                {
                    // fallback برداری بزرگ‌تر
                    canvas.DrawFilledRectangle(_penFolder, x + (w - 40) / 2, y + 36, 40, 28);
                    canvas.DrawFilledRectangle(_penFolder, x + (w - 40) / 2, y + 33, 18, 6);
                    canvas.DrawRectangle(_penDirText, x + (w - 40) / 2, y + 36, 40, 28);
                }
            }
            else
            {
                canvas.DrawFilledRectangle(_penExtIcon, x + (w - 36) / 2, y + 33, 36, 32);
                if (entry.Extension.Length > 0)
                {
                    string ext3 = entry.Extension.Length > 3 ? entry.Extension.Substring(0, 3) : entry.Extension;
                    canvas.DrawTtf(font, ext3, _penExtIconText, x + (w - 36) / 2 + 4, y + 44, _penExtIcon.Color);
                }
            }

            int dy = y + 80;  // کمی بیشتر فاصله از آیکون 40px
            // نام (شکست خط)
            int nameLines = DrawWrapped(canvas, font, entry.Name, _penDetailsValue, x + 6, dy, w - 12, panelBg);
            dy += nameLines * (font.LineHeight + 2) + 4;

            canvas.DrawTtf(font, _strType, _penDetailsLabel, x + 6, dy, panelBg);
            dy += 14;
            canvas.DrawTtf(font, entry.IsDirectory ? _strFolder : entry.Extension + " File",
                _penDetailsValue, x + 6, dy, panelBg);
            dy += 18;

            if (!entry.IsDirectory)
            {
                canvas.DrawTtf(font, _strSize, _penDetailsLabel, x + 6, dy, panelBg);
                dy += 14;
                canvas.DrawTtf(font, entry.DisplaySize, _penDetailsValue, x + 6, dy, panelBg);
                dy += 18;
            }

            canvas.DrawTtf(font, _strPath, _penDetailsLabel, x + 6, dy, panelBg);
            dy += 14;
            string shortPath = st.CurrentPath;
            if (shortPath.Length > 18)
            {
                canvas.DrawTtf(font, _strDotDotDot, _penDetailsValue, x + 6, dy, panelBg);
                canvas.DrawTtf(font, shortPath.Substring(shortPath.Length - 15), _penDetailsValue, x + 30, dy, panelBg);
            }
            else
                canvas.DrawTtf(font, shortPath, _penDetailsValue, x + 6, dy, panelBg);
        }

        // ─── شکست خط بر اساس عرض واقعی گلیف‌های TTF (نه فرض ۸px مونواسپیس) ──
        // تعداد خط رسم‌شده را برمی‌گرداند تا فراخوان بتواند dy بعدی را محاسبه کند.
        private static int DrawWrapped(WindowCanvas canvas, TtfFont font, string text,
                                        Pen pen, int x, int y, int maxW, Color bgColor)
        {
            if (font.MeasureWidth(text) <= maxW)
            {
                canvas.DrawTtf(font, text, pen, x, y, bgColor);
                return 1;
            }
            int start = 0;
            int lineY = y;
            int lines = 0;
            while (start < text.Length)
            {
                int len = 1;
                // بزرگ‌ترین طول از start که هنوز در maxW جا می‌شود
                while (start + len < text.Length &&
                       font.MeasureWidth(text.Substring(start, len + 1)) <= maxW)
                    len++;
                canvas.DrawTtf(font, text.Substring(start, len), pen, x, lineY, bgColor);
                start += len;
                lineY += font.LineHeight + 2;
                lines++;
            }
            return lines;
        }

        // ─── نوار وضعیت ──────────────────────────────────────────
        private static void DrawStatusBar(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                          int wx, int y, int ww)
        {
            canvas.DrawFilledRectangle(_penStatusBar, wx, y, ww, 22);
            canvas.DrawLine(_penSidebarBorder, wx, y, wx + ww, y);
            Color statusBg = _penStatusBar.Color;

            if (st.ErrorTimer > 0 && st.ErrorMessage.Length > 0)
            {
                canvas.DrawTtf(font, st.ErrorMessage, _penError, wx + 8, y + 4, statusBg);
            }
            else
            {
                // ─── count رشته — فقط وقتی Entries تغییر کرده cache می‌شود ──
                // چون Tick هر فریم اجرا می‌شود اما Entries به ندرت عوض می‌شود
                if (_cachedEntryCount != st.Entries.Count)
                {
                    _cachedEntryCount = st.Entries.Count;
                    _cachedCountStr = st.Entries.Count.ToString() + _strItems;
                }
                canvas.DrawTtf(font, _cachedCountStr, _penStatusText, wx + 8, y + 4, statusBg);

                if (st.SelectedIndex >= 0 && st.SelectedIndex < st.Entries.Count)
                {
                    // نام فایل را بدون concatenation رندر می‌کنیم (دو DrawTtf جداگانه)
                    // — فاصله با عرض واقعی گلیف‌ها محاسبه می‌شود، نه فرض ۸px
                    int afterCount = wx + 8 + font.MeasureWidth(_cachedCountStr);
                    canvas.DrawTtf(font, _strSelected, _penStatusText, afterCount, y + 4, statusBg);
                    canvas.DrawTtf(font, st.Entries[st.SelectedIndex].Name, _penStatusText,
                        afterCount + font.MeasureWidth(_strSelected), y + 4, statusBg);
                }
            }

            // مسیر سمت راست — با عرض واقعی گلیف تراز راست می‌شود
            string pathInfo = st.CurrentPath;
            if (pathInfo.Length > 30)
            {
                string tail = pathInfo.Substring(pathInfo.Length - 27);
                int tailW = font.MeasureWidth(tail);
                int ellW = font.MeasureWidth(_strDotDotDot);
                int pathX = wx + ww - tailW - ellW - 8;
                canvas.DrawTtf(font, _strDotDotDot, _penSidebarHdr, pathX, y + 4, statusBg);
                canvas.DrawTtf(font, tail, _penSidebarHdr, pathX + ellW, y + 4, statusBg);
            }
            else
            {
                int pathX = wx + ww - font.MeasureWidth(pathInfo) - 8;
                canvas.DrawTtf(font, pathInfo, _penSidebarHdr, pathX, y + 4, statusBg);
            }
        }

        // cache برای count رشته — جلوگیری از ToString() هر فریم
        private static int _cachedEntryCount = -1;
        private static string _cachedCountStr = "0 items";

        // ─── Dialog ایجاد جدید ────────────────────────────────────
        private static void DrawNewDialog(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                          int wx, int wy, int ww, int wh)
        {
            // dim overlay — از Pen از پیش ساخته‌شده استفاده می‌کند
            canvas.DrawFilledRectangle(_penOverlayDim, wx, wy, ww, wh);

            int dw = 300, dh = 130;
            int dx = wx + (ww - dw) / 2;
            int dy = wy + (wh - dh) / 2;

            canvas.DrawFilledRectangle(_penDialogBg, dx, dy, dw, dh);
            canvas.DrawRectangle(_penDialogBorder, dx, dy, dw, dh);

            string title = st.NewDialogIsFolder ? "New Folder" : "New File";
            canvas.DrawTtf(font, title, _penCrumbActive, dx + 10, dy + 10, _penDialogBg.Color);
            canvas.DrawLine(_penDialogBorder, dx, dy + 28, dx + dw, dy + 28);

            canvas.DrawTtf(font, "Enter name:", _penBtnText, dx + 10, dy + 36, _penDialogBg.Color);

            // Input
            canvas.DrawFilledRectangle(_penDialogInput, dx + 10, dy + 50, dw - 20, 25);
            canvas.DrawRectangle(_penDialogInputBorder, dx + 10, dy + 50, dw - 20, 25);
            string inputText = st.NewNameBuffer + "_";
            canvas.DrawTtf(font, inputText, _penAddrText, dx + 14, dy + 55, _penDialogInput.Color);

            // دکمه‌ها
            DrawToolBtn(canvas, font, dx + 10, dy + 88, 120, 27, "Create", _penDialogBtn, _penBtnText);
            DrawToolBtn(canvas, font, dx + 160, dy + 88, 130, 27, "Cancel", _penDialogBtnCancel, _penBtnText);
        }

        // ─── Dialog تأیید حذف ────────────────────────────────────
        private static void DrawDeleteConfirm(WindowCanvas canvas, FileExplorerState st, TtfFont font,
                                              int wx, int wy, int ww, int wh)
        {
            canvas.DrawFilledRectangle(_penOverlayDelete, wx, wy, ww, wh);

            int dw = 280, dh = 120;
            int dx = wx + (ww - dw) / 2;
            int dy = wy + (wh - dh) / 2;

            canvas.DrawFilledRectangle(_penConfirmBg, dx, dy, dw, dh);
            canvas.DrawRectangle(_penConfirmBorder, dx, dy, dw, dh);

            canvas.DrawTtf(font, "Delete?", _penError, dx + 10, dy + 10, _penConfirmBg.Color);
            canvas.DrawLine(_penConfirmBorder, dx, dy + 28, dx + dw, dy + 28);

            if (st.SelectedIndex >= 0 && st.SelectedIndex < st.Entries.Count)
            {
                string name = st.Entries[st.SelectedIndex].Name;
                if (name.Length > 28) name = name.Substring(0, 25) + "...";
                canvas.DrawTtf(font, "Delete: " + name, _penBtnText, dx + 10, dy + 38, _penConfirmBg.Color);
                string warn = st.Entries[st.SelectedIndex].IsDirectory
                    ? "Empty folders only!" : "This cannot be undone.";
                canvas.DrawTtf(font, warn, _penSidebarHdr, dx + 10, dy + 56, _penConfirmBg.Color);
            }

            DrawToolBtn(canvas, font, dx + 20, dy + 80, 80, 26, "Delete", _penDelete, _penBtnText);
            DrawToolBtn(canvas, font, dx + 160, dy + 80, 100, 26, "Cancel", _penBtn, _penBtnText);
        }
    }
}