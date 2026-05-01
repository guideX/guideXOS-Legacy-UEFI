using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.OS;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// Auto-Mount Configuration Manager
    /// Configure which disk images are auto-mounted at boot
    /// </summary>
    internal class AutoMountConfig : Window {
        private List<string> _mountedDisks = new List<string>(8);
        private int _selectedIndex = 0;
        private bool _clickLock;
        
        private const int Pad = 12;
        private const int RowH = 24;

        public AutoMountConfig(int x, int y, int w = 700, int h = 500) : base(x, y, w, h) {
            IsResizable = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowInTaskbar = true;
            Title = "Auto-Mount Configuration";
            RefreshMountList();
        }

        private void RefreshMountList() {
            _mountedDisks = VirtualDiskAutoMount.GetMountedDisks();
        }

        public override void OnDraw() {
            base.OnDraw();
            
            int x = X + Pad;
            int y = Y + Pad + BarHeight;
            int w = Width - 2 * Pad;
            
            // Background
            Framebuffer.Graphics.FillRectangle(X + 1, Y + BarHeight, Width - 2, Height - BarHeight - 1, 0xFF2B2B2B);
            
            // Title
            WindowManager.font.DrawString(x, y, "Currently Auto-Mounted Virtual Disks:");
            y += RowH + 8;
            
            // List mounted disks
            if (_mountedDisks.Count == 0) {
                WindowManager.font.DrawString(x + 10, y, "No virtual disks auto-mounted", w - 20, 16);
            } else {
                for (int i = 0; i < _mountedDisks.Count; i++) {
                    uint bgColor = (i == _selectedIndex) ? 0xFF404040 : 0xFF2B2B2B;
                    
                    Framebuffer.Graphics.FillRectangle(x, y, w, RowH - 2, bgColor);
                    
                    string mountPoint = _mountedDisks[i];
                    
                    // Get mount info
                    string imagePath, fsType;
                    ulong sizeBytes;
                    if (VirtualDiskAutoMount.GetMountInfo(mountPoint, out imagePath, out fsType, out sizeBytes)) {
                        string displayText = mountPoint + " -> " + imagePath + " (" + fsType + ", " + FormatSize(sizeBytes) + ")";
                        WindowManager.font.DrawString(x + 4, y + 4, displayText, w - 8, 16);
                    } else {
                        WindowManager.font.DrawString(x + 4, y + 4, mountPoint, w - 8, 16);
                    }
                    
                    y += RowH;
                }
            }
            
            y += RowH + 16;
            
            // Info section
            WindowManager.font.DrawString(x, y, "Auto-Mount Information:");
            y += RowH;
            
            WindowManager.font.DrawString(x + 10, y, "Virtual disks are automatically mounted at boot time", w - 20, 16);
            y += 18;
            WindowManager.font.DrawString(x + 10, y, "Configuration file: /etc/guidexos/automount.conf", w - 20, 16);
            y += 18;
            WindowManager.font.DrawString(x + 10, y, "You can switch between mounted disks instantly", w - 20, 16);
            
            y += RowH + 16;
            
            // Actions
            WindowManager.font.DrawString(x, y, "Actions:");
            y += RowH;
            WindowManager.font.DrawString(x + 10, y, "S - Switch to selected disk  |  A - Sync all disks  |  C - Create default config  |  R - Refresh", w - 20, 16);
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;
            
            int mx = System.Windows.Forms.Control.MousePosition.X;
            int my = System.Windows.Forms.Control.MousePosition.Y;
            bool left = System.Windows.Forms.Control.MouseButtons.HasFlag(System.Windows.Forms.MouseButtons.Left);
            
            if (left) {
                if (_clickLock) return;
                _clickLock = true;
                
                // Handle list selection
                int listStartY = Y + Pad + BarHeight + RowH + 8;
                int x = X + Pad;
                int w = Width - 2 * Pad;
                
                if (mx >= x && mx < x + w && my >= listStartY && my < listStartY + _mountedDisks.Count * RowH) {
                    int idx = (my - listStartY) / RowH;
                    if (idx >= 0 && idx < _mountedDisks.Count) {
                        _selectedIndex = idx;
                    }
                }
            } else {
                _clickLock = false;
            }
        }

        public override void OnGlobalKey(ConsoleKeyInfo key) {
            base.OnGlobalKey(key);
            
            if (key.Key == ConsoleKey.Up) {
                if (_selectedIndex > 0) {
                    _selectedIndex--;
                }
            } else if (key.Key == ConsoleKey.Down) {
                if (_selectedIndex < _mountedDisks.Count - 1) {
                    _selectedIndex++;
                }
            } else if (key.Key == ConsoleKey.S) {
                SwitchToDisk();
            } else if (key.Key == ConsoleKey.A) {
                SyncAllDisks();
            } else if (key.Key == ConsoleKey.C) {
                CreateDefaultConfig();
            } else if (key.Key == ConsoleKey.R) {
                RefreshMountList();
            }
        }

        private void SwitchToDisk() {
            if (_selectedIndex >= _mountedDisks.Count) return;
            
            string mountPoint = _mountedDisks[_selectedIndex];
            
            if (VirtualDiskAutoMount.SwitchToVirtualDisk(mountPoint)) {
                System.Windows.Forms.MessageBox.Show("Switched to " + mountPoint);
            } else {
                System.Windows.Forms.MessageBox.Show("Failed to switch to " + mountPoint);
            }
        }

        private void SyncAllDisks() {
            VirtualDiskAutoMount.SyncAll();
            System.Windows.Forms.MessageBox.Show("All virtual disks synced");
        }

        private void CreateDefaultConfig() {
            VirtualDiskAutoMount.CreateDefaultConfig();
            System.Windows.Forms.MessageBox.Show("Created default auto-mount configuration");
        }

        private string FormatSize(ulong bytes) {
            if (bytes < 1024) {
                return bytes.ToString() + " B";
            } else if (bytes < 1024 * 1024) {
                return (bytes / 1024).ToString() + " KB";
            } else if (bytes < 1024 * 1024 * 1024) {
                return (bytes / (1024 * 1024)).ToString() + " MB";
            } else {
                return (bytes / (1024 * 1024 * 1024)).ToString() + " GB";
            }
        }
    }
}
