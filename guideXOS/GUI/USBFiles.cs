using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Graph;
using System.Windows.Forms;
using guideXOS.Misc;
using System.Collections.Generic;

namespace guideXOS.GUI {
    // Isolated file explorer view for a single USB MSC device. It never swaps the global File System.
    // It mounts a local FAT reader against the USBDisk and reads files safely. Read-only view.
    internal unsafe class USBFiles : Window {
        private readonly USBMSCBot.USBDisk _disk;
        private FileSystem _fs; // local filesystem instance backed by _disk
        private bool _fsReady;
        private string _currentPath = "";
        private List<FileInfo> _entries;
        private int _scroll;
        private System.Drawing.Image _iconFolder;
        private System.Drawing.Image _iconDoc;

        public USBFiles(USBMSCBot.USBDisk disk, int X, int Y, int W = 640, int H = 480) : base(X, Y, W, H) {
            Title = "USB Files (Read-only)";
            _disk = disk;
            _fsReady = false;
            _entries = new List<FileInfo>();
            LoadIcons();
            InitLocalFS();
            ShowInTaskbar = true;
        }

        private void LoadIcons() {
            try { _iconFolder = Icons.FolderIcon(32); } catch { }
            try { _iconDoc = Icons.DocumentIcon(32); } catch { }
        }

        private void InitLocalFS() {
            // Build a tiny wrapper that temporarily swaps Disk.Instance for constructing FAT/TAR, then restores it.
            Disk previousDisk = Disk.Instance;
            var previousFs = File.Instance;
            try {
                Disk.Instance = _disk;
                _fs = new AutoFS();
                // Restore the global FS immediately; keep _fs only for local calls
                File.Instance = previousFs;
                _fsReady = true;
            } catch { _fsReady = false; }
            finally {
                Disk.Instance = previousDisk;
            }
            RefreshEntries();
        }

        private void RefreshEntries() {
            // Populate entries using the local _fs but without touching global File
            _entries.Clear();
            if (!_fsReady || _fs == null) return;
            Disk prevDisk = Disk.Instance; var prevFs = File.Instance;
            try {
                Disk.Instance = _disk;
                var list = _fs.GetFiles(_currentPath);
                for (int i = 0; i < list.Count; i++) _entries.Add(list[i]);
            } finally {
                Disk.Instance = prevDisk; File.Instance = prevFs;
            }
        }

        public override void OnInput() {
            base.OnInput(); if (!Visible) return;
            int contentX = X + 8; int contentY = Y + 8 + (WindowManager.font.FontSize + 12);
            int contentW = Width - 16; int contentH = Height - 16 - (WindowManager.font.FontSize + 12);
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            if (Control.MouseButtons == MouseButtons.Left) {
                // Simple click to open directories
                int pad = 12; int icon = _iconFolder != null ? _iconFolder.Width : 48; int tileW = icon + pad * 2; int tileH = (icon + WindowManager.font.FontSize + pad * 2);
                int cols = tileW > 0 ? (contentW / tileW) : 1; if (cols < 1) cols = 1;
                for (int i = 0; i < _entries.Count; i++) {
                    int gx = contentX + (i % cols) * tileW + pad;
                    int gy = contentY + (i / cols) * tileH + pad - _scroll;
                    if (my >= gy && my <= gy + tileH && mx >= gx && mx <= gx + tileW) {
                        bool isDir = (_entries[i].Attribute & FileAttribute.Directory) != 0;
                        if (isDir) { _currentPath = _currentPath + _entries[i].Name + "/"; RefreshEntries(); }
                        break;
                    }
                }
            }
        }

        public override void OnDraw() {
            base.OnDraw(); if (WindowManager.font == null) return;
            int tbH = WindowManager.font.FontSize + 12; int tbY = Y + 6;
            Framebuffer.Graphics.FillRectangle(X + 6, Y + 6, Width - 12, tbH, 0xFF1E1E1E);
            WindowManager.font.DrawString(X + 12, tbY + 4, _fsReady ? "Mounted" : "Not Ready");
            // content
            int contentX = X + 8; int contentY = Y + 8 + tbH; int contentW = Width - 16; int contentH = Height - 16 - tbH;
            Framebuffer.Graphics.FillRectangle(contentX, contentY, contentW, contentH, 0xFF202020);
            int pad = 12; int icon = _iconFolder != null ? _iconFolder.Width : 48; int tileW = icon + pad * 2; int tileH = (icon + WindowManager.font.FontSize + pad * 2);
            int cols = tileW > 0 ? (contentW / tileW) : 1; if (cols < 1) cols = 1;
            for (int i = 0; i < _entries.Count; i++) {
                int gx = contentX + (i % cols) * tileW + pad; int gy = contentY + (i / cols) * tileH + pad - _scroll;
                if (gy + tileH < contentY || gy > contentY + contentH) continue;
                bool isDir = (_entries[i].Attribute & FileAttribute.Directory) != 0;
                if (isDir) Framebuffer.Graphics.DrawImage(gx, gy, _iconFolder); else Framebuffer.Graphics.DrawImage(gx, gy, _iconDoc);
                string name = _entries[i].Name; WindowManager.font.DrawString(gx, gy + icon + 6, name);
            }
        }
    }
}
