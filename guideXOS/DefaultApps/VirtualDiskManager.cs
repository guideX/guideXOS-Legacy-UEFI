using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// Virtual Disk Manager - Mount and manage .img disk images
    /// </summary>
    internal class VirtualDiskManager : Window {
        private Disk _originalDisk;
        private FileSystem _originalFS;
        private FileDisk _currentVirtualDisk;
        private string _currentImagePath;
        
        private List<string> _availableImages = new List<string>(8);
        private int _selectedIndex = 0;
        private int _scrollOffset = 0;
        private bool _clickLock;
        
        private const int Pad = 12;
        private const int RowH = 24;

        public VirtualDiskManager(int x, int y, int w = 700, int h = 600) : base(x, y, w, h) {
            IsResizable = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowInTaskbar = true;
            Title = "Virtual Disk Manager";
            ScanForDiskImages();
        }

        private void ScanForDiskImages() {
            _availableImages.Clear();
            
            try {
                var files = File.GetFiles("/disks/");
                for (int i = 0; i < files.Count; i++) {
                    var file = files[i];
                    if (file.Name.Length > 4) {
                        int lastDot = file.Name.LastIndexOf('.');
                        if (lastDot > 0) {
                            string ext = file.Name.Substring(lastDot);
                            if (ext == ".img") {
                                _availableImages.Add("/disks/" + file.Name);
                            }
                        }
                    }
                }
            } catch {
                // Directory doesn't exist or can't be read
            }

            if (_availableImages.Count == 0) {
                _availableImages.Add("[No .img files found in /disks/]");
            }
        }

        public override void OnDraw() {
            base.OnDraw();
            
            int x = X + Pad;
            int y = Y + Pad + BarHeight;
            int w = Width - 2 * Pad;
            
            // Background
            Framebuffer.Graphics.FillRectangle(X + 1, Y + BarHeight, Width - 2, Height - BarHeight - 1, 0xFF2B2B2B);
            
            // Title
            WindowManager.font.DrawString(x, y, "Available Disk Images:");
            y += RowH + 8;
            
            // List of images
            int listH = 300;
            int listY = y;
            for (int i = _scrollOffset; i < _availableImages.Count && i < _scrollOffset + 12; i++) {
                uint bgColor = (i == _selectedIndex) ? 0xFF404040 : 0xFF2B2B2B;
                uint textColor = (i == _selectedIndex) ? 0xFFFFFF00 : 0xFFCCCCCC;
                
                Framebuffer.Graphics.FillRectangle(x, y, w, RowH - 2, bgColor);
                WindowManager.font.DrawString(x + 4, y + 4, _availableImages[i], w - 8, 16);
                y += RowH;
            }
            
            y = listY + listH + 16;
            
            // Status section
            WindowManager.font.DrawString(x, y, "Current Mount Status:");
            y += RowH;
            
            if (_currentVirtualDisk != null) {
                WindowManager.font.DrawString(x + 10, y, "Mounted: " + _currentImagePath, w - 20, 16);
                y += RowH;
                string sizeStr = _currentVirtualDisk.Size.ToString() + " bytes (" + _currentVirtualDisk.SectorCount.ToString() + " sectors)";
                WindowManager.font.DrawString(x + 10, y, "Size: " + sizeStr, w - 20, 16);
            } else {
                WindowManager.font.DrawString(x + 10, y, "No virtual disk mounted", w - 20, 16);
            }
            
            y += RowH + 24;
            
            // Instructions
            WindowManager.font.DrawString(x, y, "Controls:");
            y += RowH;
            WindowManager.font.DrawString(x + 10, y, "Up/Down - Select  |  M - FAT32  |  E - EXT4  |  U - Unmount  |  S - Sync  |  R - Refresh  |  L - List", w - 20, 16);
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
                
                if (mx >= x && mx < x + w && my >= listStartY && my < listStartY + 12 * RowH) {
                    int idx = (my - listStartY) / RowH + _scrollOffset;
                    if (idx >= 0 && idx < _availableImages.Count) {
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
                    if (_selectedIndex < _scrollOffset) _scrollOffset = _selectedIndex;
                }
            } else if (key.Key == ConsoleKey.Down) {
                if (_selectedIndex < _availableImages.Count - 1) {
                    _selectedIndex++;
                    if (_selectedIndex >= _scrollOffset + 12) _scrollOffset = _selectedIndex - 11;
                }
            } else if (key.Key == ConsoleKey.M) {
                MountImage("FAT32");
            } else if (key.Key == ConsoleKey.E) {
                MountImage("EXT4");
            } else if (key.Key == ConsoleKey.U) {
                UnmountImage();
            } else if (key.Key == ConsoleKey.S) {
                SyncImage();
            } else if (key.Key == ConsoleKey.R) {
                ScanForDiskImages();
                _selectedIndex = 0;
                _scrollOffset = 0;
            } else if (key.Key == ConsoleKey.L) {
                ListFiles();
            }
        }

        private void MountImage(string fsType) {
            if (_selectedIndex >= _availableImages.Count) return;
            
            string selected = _availableImages[_selectedIndex];
            if (selected.Length > 0 && selected[0] == '[') {
                System.Windows.Forms.MessageBox.Show("No valid image selected");
                return;
            }
            
            try {
                // Save original disk and filesystem if this is first mount
                if (_currentVirtualDisk == null) {
                    _originalDisk = Disk.Instance;
                    _originalFS = File.Instance;
                }
                
                // Create file-backed virtual disk
                var fileDisk = new FileDisk(selected);
                
                // Set as active disk
                Disk.Instance = fileDisk;
                
                // Mount appropriate filesystem
                if (fsType == "FAT32") {
                    File.Instance = new FAT(fileDisk);
                } else if (fsType == "EXT4") {
                    File.Instance = new EXT2(fileDisk);
                }
                
                _currentVirtualDisk = fileDisk;
                _currentImagePath = selected;
                
                System.Windows.Forms.MessageBox.Show("Mounted " + selected + " as " + fsType);
            } catch {
                System.Windows.Forms.MessageBox.Show("Failed to mount image");
            }
        }

        private void UnmountImage() {
            if (_currentVirtualDisk == null) {
                System.Windows.Forms.MessageBox.Show("No virtual disk mounted");
                return;
            }
            
            // Restore original disk and filesystem
            Disk.Instance = _originalDisk;
            File.Instance = _originalFS;
            
            _currentVirtualDisk = null;
            _currentImagePath = null;
            
            System.Windows.Forms.MessageBox.Show("Virtual disk unmounted");
        }

        private void SyncImage() {
            if (_currentVirtualDisk == null) {
                System.Windows.Forms.MessageBox.Show("No virtual disk mounted");
                return;
            }
            
            try {
                _currentVirtualDisk.Sync();
                System.Windows.Forms.MessageBox.Show("Changes synced to disk image");
            } catch {
                System.Windows.Forms.MessageBox.Show("Failed to sync changes");
            }
        }

        private void ListFiles() {
            try {
                var files = File.GetFiles("/");
                string msg = "Files in root: " + files.Count.ToString() + "\n\n";
                
                int count = 0;
                for (int i = 0; i < files.Count; i++) {
                    var file = files[i];
                    msg += file.Name;
                    if ((file.Attribute & FileAttribute.Directory) != 0) {
                        msg += " [DIR]";
                    }
                    msg += "\n";
                    
                    count++;
                    if (count >= 15) {
                        msg += "...(showing first 15)";
                        break;
                    }
                }
                
                System.Windows.Forms.MessageBox.Show(msg);
            } catch {
                System.Windows.Forms.MessageBox.Show("Failed to list files");
            }
        }
    }
}


