using guideXOS.DefaultApps;
using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.OS;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Desktop
    /// </summary>
    internal static class Desktop {
        // Helpers for fuzzy executable resolution
        private static bool HasDot(string s) {
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '.') return true;
            return false;
        }
        private static bool StartsWithStr(string s, string pref) {
            int l = pref.Length;
            if (s.Length < l) return false;
            for (int i = 0; i < l; i++)
                if (s[i] != pref[i]) return false;
            return true;
        }
        /// <summary>
        /// Try Fuzzy Exec
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="token"></param>
        /// <param name="match"></param>
        /// <param name="ambiguous"></param>
        /// <returns></returns>
        private static bool TryFuzzyExec(string dir, string token, out string match, out bool ambiguous) {
            match = null;
            ambiguous = false;
            var list = File.GetFiles(dir);
            int matches = 0;
            if (list != null) {
                for (int i = 0; i < list.Count; i++) {
                    var fi = list[i];
                    if (fi.Attribute != FileAttribute.Directory) {
                        string nm = fi.Name;
                        if (nm.Length > token.Length + 1 && StartsWithStr(nm, token) && nm[token.Length] == '.') {
                            int len = nm.Length;
                            bool gxm = len >= 4 && nm[len - 1] == 'm' && nm[len - 2] == 'x' && nm[len - 3] == 'g' && nm[len - 4] == '.';
                            bool mue = len >= 4 && nm[len - 1] == 'e' && nm[len - 2] == 'u' && nm[len - 3] == 'm' && nm[len - 4] == '.';
                            if (gxm || mue) {
                                matches++;
                                match = nm;
                            }
                        }
                    }
                    fi.Dispose();
                }
                list.Dispose();
            }
            if (matches == 1) return true;
            if (matches > 1) {
                ambiguous = true;
            }
            return false;
        }

        /// <summary>
        /// Dir
        /// </summary>
        public static string Dir;
        /// <summary>
        /// Home mode: when true, show app icons and special desktop icons. When false, show real filesystem entries for Dir.
        /// </summary>
        public static bool HomeMode;
        /// <summary>
        /// Taskbar
        /// </summary>
        public static Taskbar Taskbar;
        /// <summary>
        /// Image Viewer
        /// </summary>
        public static ImageViewer imageViewer;
        /// <summary>
        /// Message Box
        /// </summary>
        public static MessageBox msgbox;
        /// <summary>
        /// Wav Player
        /// </summary>
        public static WAVPlayer wavplayer;
        /// <summary>
        /// Apps
        /// </summary>
        public static AppCollection Apps;
        /// <summary>
        /// File Explorer window
        /// </summary>
        static ComputerFiles compFiles;
        /// <summary>
        /// Is At Root
        /// </summary>
        public static bool IsAtRoot {
            get => Desktop.Dir.Length < 1;
        }
        
        // FIXED: Cache USB Drive labels to prevent per-frame allocations
        private static string[] _usbDriveLabels = null;
        private static int _lastUSBCount = -1;
        
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            // CRITICAL: AppCollection() might try to read from disk which hangs!
            // Skip it for minimal boot test
            // Apps = new AppCollection();
            Apps = null; // Set to null for now
            IndexClicked = -1;
            
            // CRITICAL: Icons.TaskbarIcon(32) tries to load PNG from disk which hangs!
            // Create dummy icon instead
            //Taskbar = new Taskbar(40, Icons.TaskbarIcon(32));
            Image dummyTaskbarIcon = new Image(32, 32);  // Dummy taskbar icon
            Taskbar = new Taskbar(40, dummyTaskbarIcon);
            
            Dir = "";
            HomeMode = true;
            //imageViewer = new ImageViewer(400, 400);
            //msgbox = new MessageBox(100, 300);
            //wavplayer = new WAVPlayer(450, 200);
            //imageViewer.Visible = false;
            //msgbox.Visible = false;
            //wavplayer.Visible = false;
            LastPoint.X = -1;
            LastPoint.Y = -1;
            _dirCacheDirty = true;
            _dirCacheFor = null;
            _dirCache = null;
            compFiles = null;
            _lastUSBCount = -1;
            _usbDriveLabels = null;
            // DEBUG MARKER
            Native.Out8(0x3F8, (byte)'*');
        }
        /// <summary>
        /// Heuristic: if a USB MSC disk is present and marked ready, show installer icon.
        /// In future, this can check actual boot device id.
        /// </summary>
        private static bool IsBootFromUSB() {
            try {
                // If an IDE/SATA system disk exists and at least one USB MSC removable is present, assume live USB boot.
                bool hasIde = guideXOS.FS.Disk.Instance is guideXOS.Kernel.Drivers.IDEDevice;
                var dev = guideXOS.Kernel.Drivers.USBStorage.GetFirst();
                if (dev != null) {
                    var disk = guideXOS.Kernel.Drivers.USBMSC.TryOpenDisk(dev);
                    if (disk != null && disk.IsReady) return true;
                }
                // Fallback: if multiple USB storage devices, also suggest installer
                if (guideXOS.Kernel.Drivers.USBStorage.Count > 0 && !hasIde) return true;
            } catch { }
            return false;
        }
        /// <summary>
        /// Bar Height
        /// </summary>
        const int BarHeight = 40;
        static List<FileInfo> _dirCache;
        static string _dirCacheFor;
        static bool _dirCacheDirty;

        static void ClearDirCache() {
            if (_dirCache != null) {
                for (int i = 0; i < _dirCache.Count; i++) {
                    _dirCache[i].Dispose();
                }
                _dirCache.Dispose();
                _dirCache = null;
            }
            _dirCacheFor = null;
        }
        /// <summary>
        /// Get Directory Entries
        /// </summary>
        /// <returns></returns>
        static List<FileInfo> GetDirectoryEntries() {
            if (_dirCache == null || _dirCacheDirty || _dirCacheFor == null || _dirCacheFor != Dir) {
                // Dispose previous cache
                ClearDirCache();
                // Refresh cache for current Dir
                _dirCache = File.GetFiles(Dir);
                _dirCacheFor = Dir;
                _dirCacheDirty = false;
            }
            return _dirCache;
        }
        /// <summary>
        /// Is Mouse Over any Visible Window
        /// </summary>
        /// <returns></returns>
        static bool IsMouseOverAnyVisibleWindow() {
            for (int d = 0; d < WindowManager.Windows.Count; d++) {
                if (WindowManager.Windows[d].Visible && WindowManager.Windows[d].IsUnderMouse())
                    return true;
            }
            return false;
        }
        /*
        private static bool ShouldHideAppOnDesktop(string name) {
            // hide these app icons only on desktop
            // compare by lowercase name for safety
            string n = name;
            // Build a simple lowercase comparable copy
            int len = n.Length;
            bool match = false;
            // quick helpers
            bool Eq(string a) {
                if (a.Length != len) return false;
                for (int i = 0; i < len; i++) {
                    char ca = n[i]; if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32);
                    char cb = a[i]; if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32);
                    if (ca != cb) return false;
                }
                return true;
            }
            if (Eq("lock") || Eq("notepad") || Eq("task manager") || Eq("start menu") || Eq("monitor") || Eq("console") || Eq("paint") || Eq("clock") || Eq("calculator")) match = true;
            return match;
        }
        */
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="DocumentIcon32"></param>
        public static int IconSize = 48; // default desktop icon size changed from 32 to 48
        /// <summary>
        /// Set Icon Size
        /// </summary>
        /// <param name="size"></param>
        public static void SetIconSize(int size) {
            if (size != 16 && size != 24 && size != 32 && size != 48 && size != 128) return;
            IconSize = size;
        }
        /// <summary>
        /// Update
        /// </summary>
        /// <param name="documentIcon"></param>
        /// <param name="folderIcon"></param>
        /// <param name="imageIcon"></param>
        /// <param name="audioIcon"></param>
        /// <param name="iconSize"></param>
        public static void Update(Image documentIcon, Image folderIcon, Image imageIcon, Image audioIcon, int iconSize) {
            SetIconSize(iconSize);
            var docIcon = documentIcon;
            var names = GetDirectoryEntries();
            int devide = 60; // could scale later
            int fw = docIcon.Width;
            int fh = docIcon.Height;
            int screenH = Framebuffer.Graphics.Height;
            int x = devide;
            int y = devide;
            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            bool mouseOverWindow = IsMouseOverAnyVisibleWindow();
            bool mouseBlocked = WindowManager.HasWindowMoving || WindowManager.MouseHandled || mouseOverWindow;
            bool clickable = leftDown && !mouseBlocked;
            if (leftDown && mouseOverWindow) clickable = false;
            if (HomeMode) {
                if (y + fh + devide > screenH - devide) {
                    y = devide;
                    x += fw + devide;
                }
                ClickEvent("Computer Files", false, x, y, Apps.Length, clickable, leftDown);
                uint col98 = UI.ButtonFillColor(x, y, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                // Better padding: centered icon with transparent background
                Framebuffer.Graphics.DrawImage(x, y, folderIcon);
                // Draw text centered below icon with proper spacing
                int textWidth = WindowManager.font.MeasureString("Computer Files");
                int textX = x + (folderIcon.Width / 2) - (textWidth / 2);
                WindowManager.font.DrawString(textX, y + fh + 4, "Computer Files");
                y += documentIcon.Height + devide;

                // Auto-show installer icon if booting/live from USB media (heuristic)
                if (IsBootFromUSB()) {
                    if (y + fh + devide > screenH - devide) { y = devide; x += fw + devide; }
                    string installLabel = "Install to Hard Drive";
                    ClickEvent(installLabel, false, x, y, 50001, clickable, leftDown);
                    uint colInst = UI.ButtonFillColor(x, y, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                    // Use document icon for app-like entry
                    Framebuffer.Graphics.DrawImage(x, y, docIcon);
                    int instW = WindowManager.font.MeasureString(installLabel);
                    int instX = x + (docIcon.Width / 2) - (instW / 2);
                    WindowManager.font.DrawString(instX, y + fh + 4, installLabel);
                    y += documentIcon.Height + devide;
                }
                // USB mass storage icons, one per connected device
                if (Kernel.Drivers.USBStorage.Count > 0) {
                    int count = Kernel.Drivers.USBStorage.Count;
                    
                    // FIXED: Regenerate cached labels only if USB count changed
                    if (_lastUSBCount != count) {
                        // Dispose old labels
                        if (_usbDriveLabels != null) {
                            for (int i = 0; i < _usbDriveLabels.Length; i++) {
                                if (_usbDriveLabels[i] != null) {
                                    _usbDriveLabels[i].Dispose();
                                }
                            }
                            _usbDriveLabels.Dispose();
                        }
                        
                        // Create new labels
                        _usbDriveLabels = new string[count];
                        for (int i = 0; i < count; i++) {
                            if (count == 1) {
                                _usbDriveLabels[i] = "USB Drive";
                            } else {
                                string uNum = (i + 1).ToString();
                                string temp = "USB Drive " + uNum;
                                _usbDriveLabels[i] = temp;
                                uNum.Dispose(); // FIXED: Dispose intermediate string
                                // Note: temp becomes the cached label, so don't dispose it
                            }
                        }
                        _lastUSBCount = count;
                    }
                    
                    for (int u = 0; u < count; u++) {
                        if (y + fh + devide > screenH - devide) {
                            y = devide;
                            x += fw + devide;
                        }
                        // FIXED: Use only cached label - no duplicate creation!
                        string label = _usbDriveLabels[u];
                        ClickEvent(label, true, x, y, 20000 + u, clickable, leftDown);
                        uint col = UI.ButtonFillColor(x, y, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                        Framebuffer.Graphics.DrawImage(x, y, folderIcon);
                        // Draw text centered below icon
                        int labelWidth = WindowManager.font.MeasureString(label);
                        int labelX = x + (folderIcon.Width / 2) - (labelWidth / 2);
                        WindowManager.font.DrawString(labelX, y + fh + 4, label);
                        y += documentIcon.Height + devide;
                        // FIXED: NEVER dispose cached labels - they are reused every frame!
                    }
                }
                if (y + fh + devide > screenH - devide) {
                    y = devide;
                    x += fw + devide;
                }
                ClickEvent("Root", true, x, y, Apps.Length + 1, clickable, leftDown);
                uint colRoot = UI.ButtonFillColor(x, y, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                Framebuffer.Graphics.DrawImage(x, y, folderIcon);
                // Draw text centered below icon
                int rootTextWidth = WindowManager.font.MeasureString("Root");
                int rootTextX = x + (folderIcon.Width / 2) - (rootTextWidth / 2);
                WindowManager.font.DrawString(rootTextX, y + fh + 4, "Root");
                y += documentIcon.Height + devide;
            }
            if (!HomeMode) {
                if (y + fh + devide > screenH - devide) {
                    y = devide;
                    x += fw + devide;
                }
                ClickEvent("Desktop", false, x, y, -100, clickable, leftDown);
                uint cDesk = UI.ButtonFillColor(x, y, folderIcon.Width, folderIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                Framebuffer.Graphics.DrawImage(x, y, folderIcon);
                // Draw text centered below icon
                int deskTextWidth = WindowManager.font.MeasureString("Desktop");
                int deskTextX = x + (folderIcon.Width / 2) - (deskTextWidth / 2);
                WindowManager.font.DrawString(deskTextX, y + fh + 4, "Desktop");
                y += fh + devide;
                for (int i = 0; i < names.Count; i++) {
                    if (y + fh + devide > screenH - devide) {
                        y = devide;
                        x += fw + devide;
                    }
                    string n = names[i].Name;
                    bool isDir = names[i].Attribute == FileAttribute.Directory;
                    ClickEvent(n, isDir, x, y, i + 1000, clickable, leftDown);
                    uint bg = UI.ButtonFillColor(x, y, documentIcon.Width, documentIcon.Height, 0xFF2B2B2B, 0xFF343434, 0xFF3A3A3A);
                    // Draw appropriate icon based on file type
                    if (n.EndsWith(".png") || n.EndsWith(".bmp")) {
                        Framebuffer.Graphics.DrawImage(x, y, imageIcon);
                    } else if (n.EndsWith(".wav")) {
                        Framebuffer.Graphics.DrawImage(x, y, audioIcon);
                    } else if (isDir) {
                        Framebuffer.Graphics.DrawImage(x, y, folderIcon);
                    } else {
                        Framebuffer.Graphics.DrawImage(x, y, docIcon);
                    }
                    // Draw text centered below icon, truncate if too long
                    string displayName = n;
                    int maxTextWidth = fw + 32;
                    int nameWidth = WindowManager.font.MeasureString(displayName);
                    if (nameWidth > maxTextWidth) {
                        // Truncate name if too long
                        int ellipsisWidth = WindowManager.font.MeasureString("...");
                        int availWidth = maxTextWidth - ellipsisWidth;
                        int charCount = 0;
                        for (int c = 0; c < displayName.Length; c++) {
                            int w = WindowManager.font.MeasureString(displayName.Substring(0, c + 1));
                            if (w > availWidth) break;
                            charCount = c + 1;
                        }
                        displayName = displayName.Substring(0, charCount) + "...";
                        nameWidth = WindowManager.font.MeasureString(displayName);
                    }
                    int nameX = x + (documentIcon.Width / 2) - (nameWidth / 2);
                    WindowManager.font.DrawString(nameX, y + fh + 4, displayName);
                    y += fh + devide;
                }
            }
            // Selection marquee unchanged...
            // Handle widget container auto-hide (must run even when hidden)
            if (Program.WidgetsContainer != null) {
                Program.WidgetsContainer.UpdateAutoHide();
            }
            // Draw taskbar and handle Start Menu interactions
            Taskbar.Draw();
            // Right-click pinning for desktop items (apps/files)
            if (Control.MouseButtons.HasFlag(MouseButtons.Right)) {
                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                int scanX = devide;
                int scanY = devide;
                int iconW = docIcon.Width;
                int iconH = docIcon.Height;
                if (HomeMode) {
                    // Computer Files first icon
                    if (mx >= scanX && mx <= scanX + iconW && my >= scanY && my <= scanY + iconH) {
                        PinnedManager.PinComputerFiles();
                    }
                } else {
                    // iterate file entries in current directory
                    for (int i = 0; i < names.Count; i++) {
                        if (scanY + fh + devide > screenH - devide) {
                            scanY = devide;
                            scanX += fw + devide;
                        }
                        if (mx >= scanX && mx <= scanX + iconW && my >= scanY && my <= scanY + iconH) {
                            string fname = names[i].Name;
                            bool dir = names[i].Attribute == FileAttribute.Directory;
                            if (!dir) {
                                PinnedManager.PinFile(fname, Dir + fname, docIcon);
                            }
                            break;
                        }
                        scanY += fh + devide;
                    }
                }
            }
        }
        /// <summary>
        /// Last Point
        /// </summary>
        public static Point LastPoint;
        /// <summary>
        /// Click Event
        /// </summary>
        /// <param name="name"></param>
        /// <param name="isDirectory"></param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="i"></param>
        /// <param name="clickable"></param>
        /// <param name="leftDown"></param>
        private static bool _mouseClickLatch = false; // added latch to prevent repeated OnClick while mouse held
        private static void ClickEvent(string name, bool isDirectory, int X, int Y, int i, bool clickable, bool leftDown) {
            if (leftDown) {
                if (!WindowManager.HasWindowMoving && clickable && !ClickLock && !_mouseClickLatch &&
                  Control.MousePosition.X > X && Control.MousePosition.X < X + Icons.DocumentIcon(IconSize).Width &&
                  Control.MousePosition.Y > Y && Control.MousePosition.Y < Y + Icons.DocumentIcon(IconSize).Height) {
                    IndexClicked = i;
                    OnClick(name, isDirectory, X, Y);
                    _mouseClickLatch = true;
                }
            } else {
                ClickLock = false;
                _mouseClickLatch = false;
            }
            if (IndexClicked == i) {
                int w = (int)(Icons.DocumentIcon(IconSize).Width * 1.5f);
                Framebuffer.Graphics.AFillRectangle(X + ((Icons.DocumentIcon(IconSize).Width / 2) - (w / 2)), Y, w, Icons.DocumentIcon(IconSize).Height * 2, 0x7F2E86C1);
            }
        }
        static bool ClickLock = false;
        static int IndexClicked;
        /// <summary>
        /// On Click
        /// </summary>
        /// <param name="name"></param>
        /// <param name="isDirectory"></param>
        /// <param name="itemX"></param>
        /// <param name="itemY"></param>
        public static void OnClick(string name, bool isDirectory, int itemX, int itemY) {
            ClickLock = true;
            // Special desktop controls
            if (name == "Root" && HomeMode) {
                HomeMode = false;
                _dirCacheDirty = true;
                IndexClicked = -1;
                return;
            }
            // Launch HD installer from desktop icon
            if (name == "Install to Hard Drive" && HomeMode) {
                var installer = new guideXOS.DefaultApps.HDInstaller(itemX + 60, itemY + 60);
                WindowManager.MoveToEnd(installer);
                installer.Visible = true;
                IndexClicked = -1;
                return;
            }
            if (name == "Computer Files" && HomeMode) {
                // FIXED: Reuse existing ComputerFiles window instead of creating new one!
                if (compFiles == null) {
                    compFiles = new ComputerFiles(300, 200, 540, 380);
                }
                if (!compFiles.Visible) {
                    compFiles.Visible = true;
                }
                WindowManager.MoveToEnd(compFiles); // Bring to front
                return;
            }
            // Click on USB drive icon opens Computer Files too (when on Home desktop)
            if (HomeMode) {
                string usbPrefix = "USB Drive";
                bool isUsb = name.Length >= usbPrefix.Length;
                if (isUsb) {
                    for (int pi = 0; pi < usbPrefix.Length; pi++) {
                        if (name[pi] != usbPrefix[pi]) {
                            isUsb = false;
                            break;
                        }
                    }
                }
                // FIXED: NEVER dispose string literals - they are constants!
                if (isUsb) {
                    // FIXED: Reuse existing ComputerFiles window!
                    if (compFiles == null) {
                        compFiles = new ComputerFiles(300, 200, 540, 380);
                    }
                    if (!compFiles.Visible) {
                        compFiles.Visible = true;
                    }
                    WindowManager.MoveToEnd(compFiles); // Bring to front
                    return;
                }
            }
            if (name == "Desktop" && !HomeMode) {
                HomeMode = true;
                if (Dir.Length != 0) {
                    Dir.Dispose();
                }
                Dir = "";
                _dirCacheDirty = true;
                IndexClicked = -1;
                return;
            }

            string devider = "/";
            string path = Dir + name;
            if (isDirectory) {
                string newd = Dir + name + devider;
                Dir.Dispose();
                Dir = newd;
                _dirCacheDirty = true;
                IndexClicked = -1;
            } else if (name.EndsWith(".png")) {
                byte[] buffer = File.ReadAllBytes(path);
                PNG png = new(buffer);
                buffer.Dispose();
                imageViewer.SetImage(png);
                png.Dispose();
                WindowManager.MoveToEnd(imageViewer);
                imageViewer.Visible = true;
                RecentManager.AddDocument(path, Icons.ImageIcon(32));
            } else if (name.EndsWith(".bmp")) {
                byte[] buffer = File.ReadAllBytes(path);
                Bitmap png = new(buffer);
                buffer.Dispose();
                imageViewer.SetImage(png);
                png.Dispose();
                WindowManager.MoveToEnd(imageViewer);
                imageViewer.Visible = true;
                RecentManager.AddDocument(path, Icons.ImageIcon(32));
            } else if (name.EndsWith(".txt")) {
                // Open in Notepad
                var notepad = new Notepad(itemX + 40, itemY + 40);
                notepad.OpenFile(path);
                WindowManager.MoveToEnd(notepad);
                notepad.Visible = true;
                RecentManager.AddDocument(path, Icons.DocumentIcon(32));
            } else if (name.EndsWith(".gxm") || name.EndsWith(".mue")) {
                byte[] buffer = File.ReadAllBytes(path);
                string err;
                bool ok = GXMLoader.TryExecute(buffer, out err);
                if (!ok) {
                    msgbox.X = itemX + 60;
                    msgbox.Y = itemY + 60;
                    msgbox.SetText(err ?? "Failed to run executable");
                    WindowManager.MoveToEnd(msgbox);
                    msgbox.Visible = true;
                } else {
                    RecentManager.AddDocument(path, Icons.DocumentIcon(32));
                }
            } else if (name.EndsWith(".wav")) {
                if (Audio.HasAudioDevice) {
                    wavplayer.Visible = true;
                    byte[] buffer = File.ReadAllBytes(path);
                    unsafe {
                        fixed (char* ptr = name) wavplayer.Play(buffer, new string(ptr));
                    }
                    RecentManager.AddDocument(path, Icons.AudioIcon(32));
                } else {
                    msgbox.X = itemX + 75;
                    msgbox.Y = itemY + 75;
                    msgbox.SetText("Audio controller is unavailable!");
                    WindowManager.MoveToEnd(msgbox);
                    msgbox.Visible = true;
                }
            } else if (!Apps.Load(name)) {
                msgbox.X = itemX + 75;
                msgbox.Y = itemY + 75;
                msgbox.SetText("No application can open this file!");
                WindowManager.MoveToEnd(msgbox);
                msgbox.Visible = true;
            }
            path.Dispose();
            devider.Dispose();
        }
        /// <summary>
        /// Invalidate Directory Cache
        /// </summary>
        public static void InvalidateDirCache() {
            _dirCacheDirty = true;
        }
    }
}