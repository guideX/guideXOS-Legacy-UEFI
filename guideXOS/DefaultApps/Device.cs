using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.DefaultApps;
using System;
using System.Windows.Forms;

namespace guideXOS.DefaultApps {
    // Simple Device Manager-like window showing expected and detected devices
    internal class Devices : Window {
        private struct Row { 
            public string Name; 
            public string Status; 
            public string Detail; 
            public uint Color;
            public bool IsNetworkDevice; // Track if this row is a network device
            public bool IsEnabled; // Track if device is detected
        }
        private Row[] _rows;
        private int _count;
        private int _scroll;
        private bool _scrollDrag;
        private int _scrollStartY;
        private int _scrollStartScroll;
        
        // Hover state for configure buttons
        private int _hoverConfigRow = -1;

        private const int RowH = 26;
        private const int Pad = 12;

        public Devices(int X, int Y, int W = 700, int H = 520) : base(X, Y, W, H) {
            Title = "Devices";
            IsResizable = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            BuildRows();
        }

        private void EnsureRowsCapacity(int need) {
            if (_rows == null) _rows = new Row[need < 32 ? 32 : need];
            if (_rows.Length < need) {
                var nr = new Row[need * 2];
                for (int i = 0; i < _count; i++) nr[i] = _rows[i];
                _rows = nr;
            }
        }

        private void AddRow(string name, string status, string detail, uint color, bool isNetworkDevice = false, bool isEnabled = false) {
            EnsureRowsCapacity(_count + 1);
            _rows[_count].Name = name;
            _rows[_count].Status = status;
            _rows[_count].Detail = detail;
            _rows[_count].Color = color;
            _rows[_count].IsNetworkDevice = isNetworkDevice;
            _rows[_count].IsEnabled = isEnabled;
            _count++;
        }

        private static string SafeStr(string s) { return s ?? string.Empty; }

        private void BuildRows() {
            _count = 0;
            // Header: Expected devices
            AddRow("Expected Devices", "", "", 0xFFEEEEEE);

            // Audio - Intel AC'97
            bool ac97Present = PCI.GetDevice(0x8086, 0x2415) != null || AC97.DeviceLocated;
            AddRow("Audio: Intel AC'97", ac97Present ? "Detected" : "Not found", ac97Present ? AC97.DeviceName : "Vendor 8086 Dev 2415", ac97Present ? 0xFF5FB878u : 0xFFD96C6Cu);

            // Audio - ES1371
            bool es1371Present = PCI.GetDevice(0x1274, 0x1371) != null;
            AddRow("Audio: ES1371", es1371Present ? "Detected" : "Not found", "Vendor 1274 Dev 1371", es1371Present ? 0xFF5FB878u : 0xFFD96C6Cu);

            // Network - Intel 825xx family (Class 0x02)
            bool intelNet = false;
            bool realtekNet = false;
            for (int i = 0; i < PCI.Devices.Count; i++) {
                var d = PCI.Devices[i]; if (d == null) continue;
                if (d.VendorID == 0x8086 && d.ClassID == 0x02) intelNet = true;
                if (d.VendorID == 0x10EC && (d.DeviceID == 0x8139 || d.DeviceID == 0x8161 || d.DeviceID == 0x8168 || d.DeviceID == 0x8169)) realtekNet = true;
            }
            AddRow("Network: Intel 825xx", intelNet ? "Detected" : "Not found", "Vendor 8086 Class 02", intelNet ? 0xFF5FB878u : 0xFFD96C6Cu, true, intelNet);
            AddRow("Network: Realtek 8111/8168", realtekNet ? "Detected" : "Not found", "Vendor 10EC Dev 8168/8169", realtekNet ? 0xFF5FB878u : 0xFFD96C6Cu, true, realtekNet);

            // USB Mass Storage count
            int usbCount = 0;
            try { usbCount = USBStorage.Count; } catch { usbCount = 0; }
            AddRow("USB Mass Storage", usbCount > 0 ? usbCount.ToString() + " device(s)" : "None", "", usbCount > 0 ? 0xFF5FB878u : 0xFFCCCCCCu);

            // Spacer
            AddRow("", "", "", 0xFFCCCCCC);
            // Detected PCI devices
            AddRow("Detected PCI Devices", "", "", 0xFFEEEEEE);
            for (int i = 0; i < PCI.Devices.Count; i++) {
                var d = PCI.Devices[i]; if (d == null) continue;
                string vendor = VendorID.GetName(d.VendorID);
                string cls = ClassID.GetName(d.ClassID);
                string name = SafeStr(vendor);
                string st = "Bus:" + d.Bus.ToString() + " Slot:" + d.Slot.ToString() + " Func:" + d.Function.ToString();
                string detail = "Class: " + SafeStr(cls) + " DevID:0x" + d.DeviceID.ToString();
                AddRow(name, st, detail, 0xFFDDDDDDu);
                vendor.Dispose(); cls.Dispose(); st.Dispose(); detail.Dispose();
            }
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;

            int cx = X + Pad; int cy = Y + Pad; int cw = Width - Pad * 2; int ch = Height - Pad * 2;
            int listY = cy + WindowManager.font.FontSize + 8;
            int listH = ch - (WindowManager.font.FontSize + 8);

            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;

            // Scrollbar drag start
            int sbW = 10; int sbX = X + Width - Pad - sbW;
            if (Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                if (mx >= sbX && mx <= sbX + sbW && my >= listY && my <= listY + listH) { 
                    _scrollDrag = true; 
                    _scrollStartY = my; 
                    _scrollStartScroll = _scroll; 
                    return; 
                }
            } else { 
                _scrollDrag = false; 
            }

            // Drag update
            if (_scrollDrag) {
                int total = _count * RowH;
                int maxScroll = total > listH ? total - listH : 0; if (maxScroll < 0) maxScroll = 0;
                int dy = my - _scrollStartY;
                int newScroll = _scrollStartScroll + dy;
                if (newScroll < 0) newScroll = 0; if (newScroll > maxScroll) newScroll = maxScroll;
                _scroll = newScroll;
            }
            
            // Check for configure button clicks
            _hoverConfigRow = -1;
            int nameW = (int)(cw * 0.34f);
            int statusW = (int)(cw * 0.22f);
            int detailsW = cw - nameW - statusW - 6;
            int btnWidth = 80;
            int btnHeight = 20;
            
            int y = listY - _scroll;
            for (int i = 0; i < _count; i++) {
                if (y + RowH < listY) { y += RowH; continue; }
                if (y > listY + listH) break;
                
                // Only show configure button for enabled network devices
                if (_rows[i].IsNetworkDevice && _rows[i].IsEnabled) {
                    int btnX = cx + nameW + statusW + detailsW - btnWidth - 8;
                    int btnY = y + (RowH - btnHeight) / 2;
                    
                    if (mx >= btnX && mx <= btnX + btnWidth && my >= btnY && my <= btnY + btnHeight) {
                        _hoverConfigRow = i;
                        
                        if (Control.MouseButtons.HasFlag(MouseButtons.Left) && !WindowManager.MouseHandled) {
                            // Open network configuration dialog
                            var configDialog = new NetworkConfigurationDialog();
                            WindowManager.MoveToEnd(configDialog);
                            configDialog.Visible = true;
                            WindowManager.MouseHandled = true;
                        }
                    }
                }
                
                y += RowH;
            }
        }

        public override void OnDraw() {
            base.OnDraw();
            if (!Visible || WindowManager.font == null) return;

            int cx = X + Pad; int cy = Y + Pad; int cw = Width - Pad * 2; int ch = Height - Pad * 2;

            // Title inside content
            string hdr = "Hardware Inventory";
            WindowManager.font.DrawString(cx, cy, hdr);
            hdr.Dispose();

            int listY = cy + WindowManager.font.FontSize + 8;
            int listH = ch - (WindowManager.font.FontSize + 8);

            // Background
            Framebuffer.Graphics.FillRectangle(cx, listY, cw, listH, 0xFF202020);

            // Columns: Name | Status | Details
            int nameW = (int)(cw * 0.34f);
            int statusW = (int)(cw * 0.22f);
            int detailsW = cw - nameW - statusW - 6;

            int y = listY - _scroll;
            for (int i = 0; i < _count; i++) {
                if (y + RowH < listY) { y += RowH; continue; }
                if (y > listY + listH) break;
                uint rowBg = (i % 2 == 0) ? 0xFF252525u : 0xFF232323u;
                Framebuffer.Graphics.FillRectangle(cx, y, cw, RowH - 1, rowBg);
                // Header rows (color emphasized)
                if (!string.IsNullOrEmpty(_rows[i].Name) && string.IsNullOrEmpty(_rows[i].Status) && string.IsNullOrEmpty(_rows[i].Detail)) {
                    Framebuffer.Graphics.FillRectangle(cx, y, cw, RowH - 1, 0xFF2D2D2D);
                }
                // name
                WindowManager.font.DrawString(cx + 6, y + (RowH / 2 - WindowManager.font.FontSize / 2), _rows[i].Name, nameW - 8, WindowManager.font.FontSize);
                // status
                uint col = _rows[i].Color == 0 ? 0xFFDDDDDDu : _rows[i].Color;
                int sx = cx + nameW + 4;
                int sy = y + (RowH / 2 - WindowManager.font.FontSize / 2);
                // badge-like status
                UIPrimitives.AFillRoundedRect(sx, sy - 2, statusW - 8, WindowManager.font.FontSize + 4, 0x33222222, 4);
                WindowManager.font.DrawString(sx + 6, sy, _rows[i].Status, statusW - 16, WindowManager.font.FontSize);
                // details
                int detailX = cx + nameW + statusW + 6;
                WindowManager.font.DrawString(detailX, y + (RowH / 2 - WindowManager.font.FontSize / 2), _rows[i].Detail, detailsW - 8, WindowManager.font.FontSize);
                
                // Draw configure button for enabled network devices
                if (_rows[i].IsNetworkDevice && _rows[i].IsEnabled) {
                    int btnWidth = 80;
                    int btnHeight = 20;
                    int btnX = cx + nameW + statusW + detailsW - btnWidth - 8;
                    int btnY = y + (RowH - btnHeight) / 2;
                    
                    bool isHover = (_hoverConfigRow == i);
                    uint btnBg = isHover ? 0xFF4A6A8Au : 0xFF3A5A7Au;
                    uint btnBorder = 0xFF2A4A6Au;
                    
                    Framebuffer.Graphics.FillRectangle(btnX, btnY, btnWidth, btnHeight, btnBg);
                    Framebuffer.Graphics.DrawRectangle(btnX, btnY, btnWidth, btnHeight, btnBorder);
                    
                    string btnText = "Configure";
                    int textX = btnX + (btnWidth - (btnText.Length * (WindowManager.font.FontSize / 2))) / 2;
                    int textY = btnY + (btnHeight - WindowManager.font.FontSize) / 2 + 2;
                    WindowManager.font.DrawString(textX, textY, btnText);
                }
                
                y += RowH;
            }

            // Scrollbar
            int sbW = 10; int sbX = X + Width - Pad - sbW;
            Framebuffer.Graphics.FillRectangle(sbX, listY, sbW, listH, 0xFF1A1A1A);
            int total = _count * RowH;
            if (total > 0 && total > listH) {
                int thumbH = listH * listH / total; if (thumbH < 16) thumbH = 16; if (thumbH > listH) thumbH = listH;
                int thumbY = listH * _scroll / total; if (thumbY + thumbH > listH) thumbY = listH - thumbH;
                Framebuffer.Graphics.FillRectangle(sbX + 1, listY + thumbY, sbW - 2, thumbH, 0xFF2F2F2F);
            }
        }
    }
}