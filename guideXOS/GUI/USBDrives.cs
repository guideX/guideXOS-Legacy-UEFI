using guideXOS.Kernel.Drivers;
using guideXOS.Graph;
using guideXOS.Misc;
using System.Windows.Forms;

namespace guideXOS.GUI {
    // Simple window that lists connected USB mass storage devices and opens a per-drive browser
    internal unsafe class USBDrives : Window {
        private struct Entry { public USBDevice Dev; public string Label; public ulong SizeBytes; public bool Ready; }
        private Entry[] _items;
        private int _count;
        private int _scroll;
        private const int RowH = 42;

        public USBDrives(int X, int Y, int W = 420, int H = 360) : base(X, Y, W, H) {
            Title = "USB Drives";
            ShowInTaskbar = true;
            RefreshList();
        }

        private void RefreshList() {
            var devs = USBStorage.GetAll();
            if (devs == null) { _items = new Entry[0]; _count = 0; return; }
            if (_items == null || _items.Length < devs.Length) _items = new Entry[devs.Length];
            _count = 0;
            for (int i = 0; i < devs.Length; i++) {
                var d = devs[i]; if (d == null) continue;
                if (!(d.Class == 0x08 && d.SubClass == 0x06 && d.Protocol == 0x50)) continue;
                // Try initialize disk to get size
                USBMSCBot.USBDisk disk = USBMSC.TryOpenDisk(d);
                ulong size = 0; bool ready = false;
                if (disk != null && disk.IsReady) {
                    size = disk.TotalBlocks * disk.LogicalBlockSize;
                    ready = true;
                }
                // Format label
                string label = ready ? FormatSize(size) : "Not Ready";
                _items[_count].Dev = d; _items[_count].Label = label; _items[_count].SizeBytes = size; _items[_count].Ready = ready; _count++;
            }
        }

        private static string FormatSize(ulong bytes) {
            const ulong KB = 1024; const ulong MB = KB * 1024; const ulong GB = MB * 1024; const ulong TB = GB * 1024;
            if (bytes >= TB) return (bytes / TB).ToString() + " TB";
            if (bytes >= GB) return (bytes / GB).ToString() + " GB";
            if (bytes >= MB) return (bytes / MB).ToString() + " MB";
            if (bytes >= KB) return (bytes / KB).ToString() + " KB";
            return bytes.ToString() + " B";
        }

        public override void OnInput() {
            base.OnInput(); if (!Visible) return;
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            if (Control.MouseButtons == MouseButtons.Left) {
                int contentX = X + 10; int contentY = Y + 10; int w = Width - 20;
                for (int i = 0; i < _count; i++) {
                    int ry = contentY + i * RowH - _scroll;
                    if (my >= ry && my <= ry + RowH && mx >= contentX && mx <= contentX + w) {
                        var ent = _items[i];
                        if (ent.Ready) {
                            var disk = USBMSC.TryOpenDisk(ent.Dev);
                            if (disk != null && disk.IsReady) {
                                var win = new USBFiles(disk, X + 20, Y + 20, 560, 400);
                                WindowManager.MoveToEnd(win);
                                win.Visible = true;
                            }
                        }
                        break;
                    }
                }
            }
        }

        public override void OnDraw() {
            base.OnDraw();
            int contentX = X + 10; int contentY = Y + 10; int w = Width - 20; int h = Height - 20;
            Framebuffer.Graphics.FillRectangle(contentX, contentY, w, h, 0xFF202020);
            // Header
            WindowManager.font.DrawString(contentX + 6, contentY + 6, "Connected USB Mass Storage:");
            int y = contentY + 6 + WindowManager.font.FontSize + 6;
            for (int i = 0; i < _count; i++) {
                int ry = y + i * RowH - _scroll; if (ry + RowH < contentY || ry > contentY + h) continue;
                // Icon
                Framebuffer.Graphics.DrawImage(contentX + 6, ry + 4, Icons.FolderIcon(32));
                // Name and size
                string name = "Drive " + (i + 1).ToString();
                WindowManager.font.DrawString(contentX + 6 + Icons.FolderIcon(32).Width + 8, ry + 6, name);
                string label = _items[i].Label;
                WindowManager.font.DrawString(contentX + w - 10 - WindowManager.font.MeasureString(label), ry + 6, label);
            }
        }
    }
}
