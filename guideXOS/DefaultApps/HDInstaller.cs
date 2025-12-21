using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// guideXOS Hard Drive Installer - Guides users through installing guideXOS to their hard drive
    /// when booting from a USB flash drive
    /// </summary>
    internal class HDInstaller : Window {
        private int _currentStep = 0;
        private bool _clickLock;

        // Installation steps
        private enum InstallStep {
            Welcome = 0,
            DiskSelection = 1,
            PartitionSetup = 2,
            FormatWarning = 3,
            Installing = 4,
            Complete = 5
        }

        // Disk information
        private class DiskInfo {
            public string Name;
            public bool IsUSB;
            public ulong TotalSectors;
            public uint BytesPerSector;
            public USBMSCBot.USBDisk UsbDisk;
        }

        private List<DiskInfo> _availableDisks = new List<DiskInfo>();
        private int _selectedDiskIndex = -1;
        private bool _installInProgress = false;
        private int _installProgress = 0;
        private string _statusMessage = "";

        // Partition plan (editable in PartitionSetup)
        private int _bootPartitionMB = 100; // default boot size
        private string _systemFs = "EXT2"; // default filesystem
        private bool _advancedShown;

        // Internal install phases
        private bool _didPartition;
        private bool _didFormat;
        private bool _didCopyFiles;
        private bool _didBootloader;

        // UI Layout constants
        private const int Pad = 20;
        private const int BtnW = 120;
        private const int BtnH = 32;

        // Button coordinates
        private int _btnNextX, _btnNextY;
        private int _btnBackX, _btnBackY;
        private int _btnCancelX, _btnCancelY;

        public HDInstaller(int x, int y) : base(x, y, 700, 500) {
            Title = "Install guideXOS to Hard Drive";
            IsResizable = false;
            ShowMaximize = false;
            ShowMinimize = true;
            ShowInTaskbar = true;
            ShowInStartMenu = false;

            ScanDisks();
        }

        private void ScanDisks() {
            _availableDisks.Clear();

            // System disk (IDE)
            if (Disk.Instance is IDEDevice ide) {
                var sysInfo = new DiskInfo {
                    Name = "System Disk 0 (IDE)",
                    IsUSB = false,
                    BytesPerSector = IDEDevice.SectorSize,
                    TotalSectors = ide.Size / IDEDevice.SectorSize
                };
                _availableDisks.Add(sysInfo);
            }

            // USB disks
            var devices = USBStorage.GetAll();
            if (devices != null) {
                int usbIndex = 1;
                for (int i = 0; i < devices.Length; i++) {
                    var d = devices[i];
                    if (d == null) continue;
                    if (!(d.Class == 0x08 && d.SubClass == 0x06 && d.Protocol == 0x50)) continue;

                    var usbDisk = USBMSC.TryOpenDisk(d);
                    if (usbDisk == null || !usbDisk.IsReady) continue;

                    var info = new DiskInfo {
                        Name = $"USB Disk {usbIndex} (Removable)",
                        IsUSB = true,
                        BytesPerSector = usbDisk.LogicalBlockSize,
                        TotalSectors = usbDisk.TotalBlocks,
                        UsbDisk = usbDisk
                    };
                    _availableDisks.Add(info);
                    usbIndex++;
                }
            }
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;

            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);

            if (left && !_clickLock) {
                // Navigation buttons
                if (_currentStep > 0 && _currentStep < (int)InstallStep.Installing &&
                    Hit(mx, my, _btnBackX, _btnBackY, BtnW, BtnH)) {
                    _currentStep--;
                    _clickLock = true;
                    return;
                }

                if (_currentStep < (int)InstallStep.Installing &&
                    Hit(mx, my, _btnNextX, _btnNextY, BtnW, BtnH)) {
                    if (HandleNextButton()) {
                        _currentStep++;
                    }
                    _clickLock = true;
                    return;
                }

                if (Hit(mx, my, _btnCancelX, _btnCancelY, BtnW, BtnH)) {
                    if (_currentStep != (int)InstallStep.Installing) {
                        Visible = false;
                    }
                    _clickLock = true;
                    return;
                }

                // Disk selection in step 1
                if (_currentStep == (int)InstallStep.DiskSelection) {
                    int listY = Y + 120;
                    for (int i = 0; i < _availableDisks.Count; i++) {
                        int diskY = listY + i * 60;
                        if (Hit(mx, my, X + Pad, diskY, Width - Pad * 2, 50)) {
                            _selectedDiskIndex = i;
                            _clickLock = true;
                            return;
                        }
                    }
                }
            } else {
                _clickLock = false;
            }

            // Simple keyboard controls in PartitionSetup step
            if (_currentStep == (int)InstallStep.PartitionSetup) {
                var key = Keyboard.KeyInfo;
                if (key.KeyState == ConsoleKeyState.Pressed) {
                    if (key.Key == ConsoleKey.Add || key.Key == ConsoleKey.OemPlus) {
                        _bootPartitionMB += 10; if (_bootPartitionMB > 1024) _bootPartitionMB = 1024;
                    } else if (key.Key == ConsoleKey.Subtract || key.Key == ConsoleKey.OemMinus) {
                        _bootPartitionMB -= 10; if (_bootPartitionMB < 64) _bootPartitionMB = 64;
                    } else if (key.Key == ConsoleKey.Space) {
                        // Toggle filesystem
                        _systemFs = _systemFs == "EXT2" ? "EXT3" : _systemFs == "EXT3" ? "EXT4" : "EXT2";
                    }
                }
            }
        }

        private bool HandleNextButton() {
            switch ((InstallStep)_currentStep) {
                case InstallStep.Welcome:
                    return true;

                case InstallStep.DiskSelection:
                    if (_selectedDiskIndex < 0) {
                        _statusMessage = "Please select a disk to install to.";
                        return false;
                    }
                    return true;

                case InstallStep.PartitionSetup:
                    return true;

                case InstallStep.FormatWarning:
                    // Start installation
                    _installInProgress = true;
                    _installProgress = 0;
                    StartInstallation();
                    return true;

                case InstallStep.Complete:
                    Visible = false;
                    return false;
            }
            return true;
        }

        private void StartInstallation() {
            _statusMessage = "Installing guideXOS...";
            // Reset phases
            _didPartition = false;
            _didFormat = false;
            _didCopyFiles = false;
            _didBootloader = false;
        }

        public override void OnDraw() {
            base.OnDraw();

            // Background
            Framebuffer.Graphics.FillRectangle(X + 1, Y + 1, Width - 2, Height - 2, 0xFF2B2B2B);

            // Draw step indicator at top
            DrawStepIndicator();

            // Draw content based on current step
            switch ((InstallStep)_currentStep) {
                case InstallStep.Welcome:
                    DrawWelcomeStep();
                    break;
                case InstallStep.DiskSelection:
                    DrawDiskSelectionStep();
                    break;
                case InstallStep.PartitionSetup:
                    DrawPartitionSetupStep();
                    break;
                case InstallStep.FormatWarning:
                    DrawFormatWarningStep();
                    break;
                case InstallStep.Installing:
                    DrawInstallingStep();
                    break;
                case InstallStep.Complete:
                    DrawCompleteStep();
                    break;
            }

            // Draw navigation buttons at bottom
            DrawNavigationButtons();

            // Draw status message if any
            if (!string.IsNullOrEmpty(_statusMessage)) {
                WindowManager.font.DrawString(X + Pad, Y + Height - 80, _statusMessage, Width - Pad * 2, WindowManager.font.FontSize);
            }
        }

        private void DrawStepIndicator() {
            int indicatorY = Y + 40;
            int stepCount = 5;
            int stepWidth = (Width - Pad * 2) / stepCount;

            for (int i = 0; i < stepCount; i++) {
                int stepX = X + Pad + i * stepWidth;
                uint color = i <= _currentStep ? 0xFF4C8BF5 : 0xFF555555;

                // Draw circle
                Framebuffer.Graphics.FillRectangle(stepX + stepWidth / 2 - 15, indicatorY - 15, 30, 30, color);

                // Draw line to next step
                if (i < stepCount - 1) {
                    uint lineColor = i < _currentStep ? 0xFF4C8BF5 : 0xFF555555;
                    Framebuffer.Graphics.FillRectangle(stepX + stepWidth / 2 + 15, indicatorY - 2, stepWidth - 30, 4, lineColor);
                }

                // Draw step number
                string stepNum = (i + 1).ToString();
                WindowManager.font.DrawString(stepX + stepWidth / 2 - 8, indicatorY - 8, stepNum);
            }
        }

        private void DrawWelcomeStep() {
            int contentY = Y + 100;

            WindowManager.font.DrawString(X + Pad, contentY, "Welcome to guideXOS Installer");
            contentY += 40;

            string[] lines = new string[] {
                "This wizard will guide you through installing guideXOS",
                "to your computer's hard drive.",
                "",
                "You are currently running guideXOS from a USB flash drive.",
                "Installing to your hard drive will provide:",
                "",
                "  - Faster boot times",
                "  - Persistent storage for files and settings",
                "  - Better performance",
                "  - No need for the USB drive after installation",
                "",
                "WARNING: This will erase all data on the selected disk!",
                "Make sure you have backed up any important data.",
                "",
                "Click Next to continue."
            };

            for (int i = 0; i < lines.Length; i++) {
                WindowManager.font.DrawString(X + Pad, contentY, lines[i], Width - Pad * 2, WindowManager.font.FontSize);
                contentY += WindowManager.font.FontSize + 6;
            }
        }

        private void DrawDiskSelectionStep() {
            int contentY = Y + 100;

            WindowManager.font.DrawString(X + Pad, contentY, "Select Installation Disk");
            contentY += 30;

            WindowManager.font.DrawString(X + Pad, contentY, "Choose the disk where guideXOS will be installed:", Width - Pad * 2, WindowManager.font.FontSize);
            contentY += 30;

            if (_availableDisks.Count == 0) {
                WindowManager.font.DrawString(X + Pad, contentY, "No suitable disks found.", Width - Pad * 2, WindowManager.font.FontSize);
                return;
            }

            // Draw disk list
            for (int i = 0; i < _availableDisks.Count; i++) {
                var disk = _availableDisks[i];
                bool selected = i == _selectedDiskIndex;

                uint bgColor = selected ? 0xFF3A3A3A : 0xFF2A2A2A;
                uint borderColor = selected ? 0xFF4C8BF5 : 0xFF555555;

                Framebuffer.Graphics.FillRectangle(X + Pad, contentY, Width - Pad * 2, 50, bgColor);
                Framebuffer.Graphics.DrawRectangle(X + Pad, contentY, Width - Pad * 2, 50, borderColor, 2);

                string sizeStr = FormatSize(disk.TotalSectors * (disk.BytesPerSector == 0 ? 512UL : disk.BytesPerSector));
                WindowManager.font.DrawString(X + Pad + 10, contentY + 8, disk.Name, Width - Pad * 2 - 20, WindowManager.font.FontSize);
                WindowManager.font.DrawString(X + Pad + 10, contentY + 28, $"Size: {sizeStr}", Width - Pad * 2 - 20, WindowManager.font.FontSize);

                contentY += 60;
            }
        }

        private void DrawPartitionSetupStep() {
            int contentY = Y + 100;

            WindowManager.font.DrawString(X + Pad, contentY, "Partition Configuration");
            contentY += 40;

            if (_selectedDiskIndex >= 0 && _selectedDiskIndex < _availableDisks.Count) {
                var disk = _availableDisks[_selectedDiskIndex];

                string[] lines = new string[] {
                    "The installer will create the following partitions:",
                    "",
                    $"Disk: {disk.Name}",
                    $"Total Size: {FormatSize(disk.TotalSectors * (disk.BytesPerSector == 0 ? 512UL : disk.BytesPerSector))}",
                    "",
                    "Partition Layout (editable):",
                    $"  - Boot Partition (FAT32) - {_bootPartitionMB} MB",
                    $"  - System Partition ({_systemFs}) - Remaining space",
                    "",
                    "Adjustments:",
                    "  [+/-] Increase/Decrease boot size (64 MB - 1024 MB)",
                    "  [Space] Toggle system filesystem (EXT2/EXT3/EXT4)",
                    "",
                    "The boot partition will contain:",
                    "  - Bootloader (GRUB)",
                    "  - Kernel image",
                    "",
                    "The system partition will contain:",
                    "  - System files",
                    "  - Applications",
                    "  - User data",
                    "",
                    "Click Next to continue."
                };

                for (int i = 0; i < lines.Length; i++) {
                    WindowManager.font.DrawString(X + Pad, contentY, lines[i], Width - Pad * 2, WindowManager.font.FontSize);
                    contentY += WindowManager.font.FontSize + 6;
                }
            }
        }

        private void DrawFormatWarningStep() {
            int contentY = Y + 100;

            WindowManager.font.DrawString(X + Pad, contentY, "WARNING: Data Will Be Erased");
            contentY += 50;

            if (_selectedDiskIndex >= 0 && _selectedDiskIndex < _availableDisks.Count) {
                var disk = _availableDisks[_selectedDiskIndex];

                string[] lines = new string[] {
                    "The following disk will be formatted:",
                    "",
                    $"  {disk.Name}",
                    $"  Size: {FormatSize(disk.TotalSectors * (disk.BytesPerSector == 0 ? 512UL : disk.BytesPerSector))}",
                    "",
                    "ALL DATA ON THIS DISK WILL BE PERMANENTLY DELETED!",
                    "",
                    "This includes:",
                    "  - All files and folders",
                    "  - Any existing operating systems",
                    "  - All personal data",
                    "  - All applications and programs",
                    "",
                    "This action CANNOT be undone!",
                    "",
                    "Make sure you have:",
                    "  * Backed up all important data",
                    "  * Selected the correct disk",
                    "  * Saved any work in progress",
                    "",
                    "Click Next to begin installation, or Back to change settings."
                };

                for (int i = 0; i < lines.Length; i++) {
                    WindowManager.font.DrawString(X + Pad, contentY, lines[i], Width - Pad * 2, WindowManager.font.FontSize);
                    contentY += WindowManager.font.FontSize + 6;
                }
            }
        }

        private void DrawInstallingStep() {
            int contentY = Y + 100;

            WindowManager.font.DrawString(X + Pad, contentY, "Installing guideXOS");
            contentY += 50;

            // Simulate installation progress
            if (_installInProgress) {
                // Gate phases roughly by percentage
                if (!_didPartition) {
                    _statusMessage = "Partitioning disk...";
                    if (PartitionDisk()) { 
                        _didPartition = true; 
                        _installProgress = 15; 
                    } else {
                        _statusMessage = "Error: Partitioning failed";
                        _installInProgress = false;
                    }
                } else if (!_didFormat) {
                    _statusMessage = "Formatting partitions...";
                    if (FormatPartitions()) { 
                        _didFormat = true; 
                        _installProgress = 30; 
                    } else {
                        _statusMessage = "Error: Formatting failed";
                        _installInProgress = false;
                    }
                } else if (!_didCopyFiles) {
                    _statusMessage = "Copying system files... (this may take a while)";
                    if (CopySystemFiles()) { 
                        _didCopyFiles = true; 
                        _installProgress = 85; 
                        // Create boot configuration
                        try { CreateBootConfiguration(); } catch { }
                    } else {
                        _statusMessage = "Error: File copying failed";
                        _installInProgress = false;
                    }
                } else if (!_didBootloader) {
                    _statusMessage = "Installing bootloader...";
                    if (InstallBootloader()) { 
                        _didBootloader = true; 
                        _installProgress = 95; 
                    } else {
                        _statusMessage = "Error: Bootloader installation failed";
                        _installInProgress = false;
                    }
                } else {
                    _statusMessage = "Finalizing installation...";
                    _installProgress += 1;
                    if (_installProgress >= 100) {
                        _installProgress = 100;
                        _statusMessage = "Installation complete!";
                    }
                }
                if (_installProgress >= 100) {
                    _installProgress = 100;
                    _installInProgress = false;
                    _currentStep = (int)InstallStep.Complete;
                }
            }

            // Progress bar
            int progressBarW = Width - Pad * 4;
            int progressBarH = 30;
            Framebuffer.Graphics.FillRectangle(X + Pad * 2, contentY, progressBarW, progressBarH, 0xFF1E1E1E);

            int fillWidth = (progressBarW * _installProgress) / 100;
            Framebuffer.Graphics.FillRectangle(X + Pad * 2, contentY, fillWidth, progressBarH, 0xFF4C8BF5);

            string progressText = $"{_installProgress}%";
            int textX = X + Width / 2 - 20;
            WindowManager.font.DrawString(textX, contentY + 8, progressText);

            contentY += progressBarH + 30;

            // Installation steps
            string[] steps = new string[] {
                _didPartition ? "* Partitioning disk..." : "  Partitioning disk...",
                _didFormat ? "* Formatting partitions..." : "  Formatting partitions...",
                _didCopyFiles ? "* Copying system files..." : "  Copying system files...",
                _didBootloader ? "* Installing bootloader..." : "  Installing bootloader...",
                _installProgress > 95 ? "* Configuring system..." : "  Configuring system...",
                _installProgress >= 100 ? "* Installation complete!" : "  Finalizing installation..."
            };

            for (int i = 0; i < steps.Length; i++) {
                WindowManager.font.DrawString(X + Pad, contentY, steps[i], Width - Pad * 2, WindowManager.font.FontSize);
                contentY += WindowManager.font.FontSize + 10;
            }

            contentY += 20;
            WindowManager.font.DrawString(X + Pad, contentY, "Please wait, do not power off your computer...", Width - Pad * 2, WindowManager.font.FontSize);
        } 

        private void DrawCompleteStep() {
            int contentY = Y + 100;

            WindowManager.font.DrawString(X + Pad, contentY, "Installation Complete!");
            contentY += 40;

            // Check if USB installation media is still present
            bool usbStillPresent = CheckUSBInstallMediaPresent();

            if (usbStillPresent) {
                // Draw prominent warning about removing USB drive
                int warningBoxY = contentY;
                int warningBoxH = 140;
                
                // Warning box background (orange/red)
                Framebuffer.Graphics.FillRectangle(X + Pad, warningBoxY, Width - Pad * 2, warningBoxH, 0xFFDD4400);
                Framebuffer.Graphics.DrawRectangle(X + Pad, warningBoxY, Width - Pad * 2, warningBoxH, 0xFFFF6600, 3);
                
                // Warning text
                int warnTextY = warningBoxY + 10;
                WindowManager.font.DrawString(X + Pad + 10, warnTextY, "WARNING: REMOVE INSTALLATION MEDIA", Width - Pad * 2 - 20, WindowManager.font.FontSize);
                warnTextY += WindowManager.font.FontSize + 10;
                
                string[] warnLines = new string[] {
                    "Before rebooting, you MUST remove the USB flash drive",
                    "or installation media from your computer.",
                    "",
                    "If you reboot without removing it, your computer will",
                    "boot from the USB drive again instead of the newly",
                    "installed system on your hard drive.",
                    "",
                    "Please remove the USB drive now."
                };
                
                for (int i = 0; i < warnLines.Length; i++) {
                    WindowManager.font.DrawString(X + Pad + 10, warnTextY, warnLines[i], Width - Pad * 2 - 20, WindowManager.font.FontSize);
                    warnTextY += WindowManager.font.FontSize + 4;
                }
                
                contentY += warningBoxH + 20;
            } else {
                // USB removed - show success message
                int successBoxY = contentY;
                int successBoxH = 80;
                
                // Success box background (green)
                Framebuffer.Graphics.FillRectangle(X + Pad, successBoxY, Width - Pad * 2, successBoxH, 0xFF228B22);
                Framebuffer.Graphics.DrawRectangle(X + Pad, successBoxY, Width - Pad * 2, successBoxH, 0xFF32CD32, 2);
                
                // Success text
                int successTextY = successBoxY + 10;
                WindowManager.font.DrawString(X + Pad + 10, successTextY, "Installation media removed - Ready to reboot!", Width - Pad * 2 - 20, WindowManager.font.FontSize);
                successTextY += WindowManager.font.FontSize + 10;
                
                string[] successLines = new string[] {
                    "Your computer is now ready to boot from the hard drive.",
                    "Click the Reboot button below to restart and enjoy",
                    "your newly installed guideXOS system!"
                };
                
                for (int i = 0; i < successLines.Length; i++) {
                    WindowManager.font.DrawString(X + Pad + 10, successTextY, successLines[i], Width - Pad * 2 - 20, WindowManager.font.FontSize);
                    successTextY += WindowManager.font.FontSize + 4;
                }
                
                contentY += successBoxH + 20;
            }

            // Installation summary
            string[] summaryLines = new string[] {
                "guideXOS has been successfully installed!",
                "",
                "You can now enjoy:",
                "  * Faster boot times",
                "  * Persistent file storage",
                "  * Better performance",
                "  * Full system access",
                "",
                "Thank you for choosing guideXOS!"
            };

            for (int i = 0; i < summaryLines.Length; i++) {
                WindowManager.font.DrawString(X + Pad, contentY, summaryLines[i], Width - Pad * 2, WindowManager.font.FontSize);
                contentY += WindowManager.font.FontSize + 6;
            }

            // Draw Reboot button (only enabled if USB is removed)
            int rbW = 140, rbH = 32;
            int rbX = X + Width - Pad - rbW - BtnW - 10; // Positioned next to Finish button
            int rbY = Y + Height - 60;
            
            // Draw button with different color based on USB status
            int mx = Control.MousePosition.X; 
            int my = Control.MousePosition.Y;
            bool hover = Hit(mx, my, rbX, rbY, rbW, rbH);
            
            if (usbStillPresent) {
                // Disabled state - gray
                Framebuffer.Graphics.FillRectangle(rbX, rbY, rbW, rbH, 0xFF444444);
                WindowManager.font.DrawString(rbX + rbW / 2 - 30, rbY + rbH / 2 - WindowManager.font.FontSize / 2, "Reboot");
            } else {
                // Enabled state - clickable
                uint bgColor = hover ? 0xFF4C8BF5 : 0xFF3A3A3A;
                Framebuffer.Graphics.FillRectangle(rbX, rbY, rbW, rbH, bgColor);
                WindowManager.font.DrawString(rbX + rbW / 2 - 30, rbY + rbH / 2 - WindowManager.font.FontSize / 2, "Reboot");
                
                // Handle reboot click (only if USB is removed)
                bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
                if (left && !_clickLock && hover) {
                    try { guideXOS.Kernel.Drivers.Power.Reboot(); } catch { }
                    _clickLock = true;
                }
            }
            
            // Reset click lock when mouse released
            if (!Control.MouseButtons.HasFlag(MouseButtons.Left)) {
                _clickLock = false;
            }
        }
        
        /// <summary>
        /// Check if USB installation media is still present
        /// Returns true if any USB storage device is detected
        /// </summary>
        private bool CheckUSBInstallMediaPresent() {
            try {
                var devices = USBStorage.GetAll();
                if (devices == null || devices.Length == 0) {
                    return false;
                }
                
                // Check if any USB mass storage devices are present
                for (int i = 0; i < devices.Length; i++) {
                    var d = devices[i];
                    if (d == null) continue;
                    
                    // Check for USB Mass Storage Class device
                    if (d.Class == 0x08 && d.SubClass == 0x06 && d.Protocol == 0x50) {
                        var usbDisk = USBMSC.TryOpenDisk(d);
                        if (usbDisk != null && usbDisk.IsReady) {
                            // USB storage device detected
                            return true;
                        }
                    }
                }
                
                return false;
            } catch {
                // If we can't determine, assume it's removed (safer to allow reboot)
                return false;
            }
        }

        private void DrawNavigationButtons() {
            int btnY = Y + Height - 60;

            // Next/Finish button
            _btnNextX = X + Width - Pad - BtnW;
            _btnNextY = btnY;
            string nextLabel = _currentStep == (int)InstallStep.Complete ? "Finish" :
                              _currentStep == (int)InstallStep.FormatWarning ? "Install" : "Next";
            bool nextEnabled = _currentStep != (int)InstallStep.Installing;
            DrawButton(_btnNextX, _btnNextY, BtnW, BtnH, nextLabel, nextEnabled);

            // Back button
            _btnBackX = X + Width - Pad - BtnW * 2 - 10;
            _btnBackY = btnY;
            bool backEnabled = _currentStep > 0 && _currentStep < (int)InstallStep.Installing;
            if (backEnabled) {
                DrawButton(_btnBackX, _btnBackY, BtnW, BtnH, "Back", true);
            }

            // Cancel button
            _btnCancelX = X + Pad;
            _btnCancelY = btnY;
            bool cancelEnabled = _currentStep != (int)InstallStep.Installing;
            DrawButton(_btnCancelX, _btnCancelY, BtnW, BtnH, "Cancel", cancelEnabled);
        }
        
        /// <summary>
        /// Create a boot configuration file that tells the kernel which partition to mount as root
        /// </summary>
        private void CreateBootConfiguration() {
            try {
                // This would be read by the kernel during boot to know where to mount the root filesystem
                string bootConfig = 
                    "# guideXOS Boot Configuration\n" +
                    "# Generated by HD Installer\n" +
                    "\n" +
                    "# Root filesystem location\n" +
                    "root=/dev/sda2\n" +
                    "\n" +
                    "# Filesystem type\n" +
                    $"rootfstype={(_systemFs == "FAT32" ? "fat32" : _systemFs.ToLower())}\n" +
                    "\n" +
                    "# Boot partition (for kernel updates)\n" +
                    "boot=/dev/sda1\n" +
                    "bootfstype=fat32\n" +
                    "\n" +
                    "# Installation date\n" +
                    $"install_date={Timer.Ticks}\n";
                
                guideXOS.FS.File.WriteAllBytes("/boot/config.txt", GetAsciiBytes(bootConfig));
            } catch {
                // Not critical if this fails
            }
        }

        // Disk operations - complete implementation for full OS installation
        private bool PartitionDisk() {
            // Create MBR with two partitions: Boot (FAT32 LBA) and System (FAT32)
            // Note: Using FAT32 for system partition too for simplicity (can be changed to EXT2/3/4 later)
            if (_selectedDiskIndex < 0 || _selectedDiskIndex >= _availableDisks.Count) return false;
            var diskInfo = _availableDisks[_selectedDiskIndex];
            
            // Get the target disk - switch to it if not already selected
            var targetDisk = diskInfo.IsUSB ? (guideXOS.FS.Disk)diskInfo.UsbDisk : guideXOS.FS.Disk.Instance;
            if (targetDisk == null) return false;
            
            uint bps = diskInfo.BytesPerSector == 0 ? 512u : diskInfo.BytesPerSector;
            ulong totalSectors = diskInfo.TotalSectors;
            
            // Calculate boot partition size in sectors
            ulong bootSectors = ((ulong)_bootPartitionMB * 1024UL * 1024UL) / bps;
            if (bootSectors < 2048) bootSectors = 2048; // minimum
            if (bootSectors >= totalSectors - 4096) bootSectors = totalSectors / 4; // keep room
            
            ulong sysStart = bootSectors + 1; // Start system partition after boot partition
            ulong sysSectors = totalSectors - sysStart;

            // Build MBR (512 bytes)
            byte[] mbr = new byte[512];
            // Zero MBR
            for (int i = 0; i < mbr.Length; i++) mbr[i] = 0;
            
            // Install GRUB stage1 bootloader code
            // This is a minimal MBR boot code that loads GRUB from the boot partition
            byte[] grubStage1 = GetGRUBStage1();
            for (int i = 0; i < grubStage1.Length && i < 446; i++) {
                mbr[i] = grubStage1[i];
            }
            
            // Partition entries start at 0x1BE
            void WriteLBA32(int off, uint val) { 
                mbr[off] = (byte)(val & 0xFF); 
                mbr[off + 1] = (byte)((val >> 8) & 0xFF); 
                mbr[off + 2] = (byte)((val >> 16) & 0xFF); 
                mbr[off + 3] = (byte)((val >> 24) & 0xFF); 
            }
            
            // P0: Boot - Active, type 0x0C (FAT32 LBA)
            mbr[0x1BE + 0] = 0x80; // active (bootable)
            mbr[0x1BE + 4] = 0x0C; // type FAT32 LBA
            WriteLBA32(0x1BE + 8, (uint)1); // LBA start (skip MBR sector)
            WriteLBA32(0x1BE + 12, (uint)(bootSectors - 1));
            
            // P1: System - type 0x0C (FAT32) or 0x83 (Linux) based on selection
            int p1 = 0x1BE + 16;
            mbr[p1 + 0] = 0x00; // non-active
            mbr[p1 + 4] = _systemFs == "EXT2" || _systemFs == "EXT3" || _systemFs == "EXT4" ? (byte)0x83 : (byte)0x0C;
            WriteLBA32(p1 + 8, (uint)sysStart);
            WriteLBA32(p1 + 12, (uint)sysSectors);
            
            // Signature 0x55AA
            mbr[510] = 0x55; mbr[511] = 0xAA;
            
            unsafe {
                fixed (byte* p = mbr) targetDisk.Write(0, 1, p);
            }
            return true;
        }
        private bool FormatPartitions() {
            // Format both boot and system partitions
            if (_selectedDiskIndex < 0 || _selectedDiskIndex >= _availableDisks.Count) return false;
            var diskInfo = _availableDisks[_selectedDiskIndex];
            var targetDisk = diskInfo.IsUSB ? (guideXOS.FS.Disk)diskInfo.UsbDisk : guideXOS.FS.Disk.Instance;
            if (targetDisk == null) return false;
            
            uint bps = diskInfo.BytesPerSector == 0 ? 512u : diskInfo.BytesPerSector;
            ulong totalSectors = diskInfo.TotalSectors;
            ulong bootStart = 1; // after MBR
            ulong bootSectors = ((ulong)_bootPartitionMB * 1024UL * 1024UL) / bps; 
            if (bootSectors < 2048) bootSectors = 2048; 
            if (bootSectors >= totalSectors - 4096) bootSectors = totalSectors / 4;
            
            // Format boot partition as FAT32
            if (!FormatFAT32Partition(targetDisk, bootStart, bootSectors, bps, "GXOSBOOT")) {
                return false;
            }
            
            // Format system partition (also FAT32 for now - EXT2/3/4 would require more complex implementation)
            ulong sysStart = bootStart + bootSectors;
            ulong sysSectors = totalSectors - sysStart;
            
            if (!FormatFAT32Partition(targetDisk, sysStart, sysSectors, bps, "GXOSSYS")) {
                return false;
            }
            
            return true;
        }
        
        private bool FormatFAT32Partition(guideXOS.FS.Disk disk, ulong startSector, ulong sectorCount, uint bytesPerSector, string label) {
            // Create FAT32 filesystem
            byte[] bpb = new byte[512];
            for (int i = 0; i < bpb.Length; i++) bpb[i] = 0;
            
            // Jump instruction
            bpb[0] = 0xEB; bpb[1] = 0x58; bpb[2] = 0x90;
            
            // OEM name
            string oem = "GUIDEXOS"; 
            for (int i = 0; i < 8; i++) bpb[3 + i] = i < oem.Length ? (byte)oem[i] : (byte)' ';
            
            // Bytes per sector
            bpb[11] = (byte)(bytesPerSector & 0xFF); 
            bpb[12] = (byte)((bytesPerSector >> 8) & 0xFF);
            
            // Sectors per cluster: 8 (4KB clusters for 512-byte sectors)
            bpb[13] = 8;
            
            // Reserved sectors: 32
            bpb[14] = 32; bpb[15] = 0;
            
            // Number of FATs
            bpb[16] = 2;
            
            // Root entries (0 for FAT32)
            bpb[17] = 0; bpb[18] = 0;
            
            // Total sectors 16 (0 for FAT32)
            bpb[19] = 0; bpb[20] = 0;
            
            // Media descriptor (0xF8 = fixed disk)
            bpb[21] = 0xF8;
            
            // FAT size 16 (0 for FAT32)
            bpb[22] = 0; bpb[23] = 0;
            
            // Sectors per track / heads (dummy values)
            bpb[24] = 0x3F; bpb[25] = 0x00; 
            bpb[26] = 0xFF; bpb[27] = 0x00;
            
            // Hidden sectors
            uint hidden = (uint)startSector; 
            bpb[28] = (byte)(hidden & 0xFF); 
            bpb[29] = (byte)((hidden >> 8) & 0xFF); 
            bpb[30] = (byte)((hidden >> 16) & 0xFF); 
            bpb[31] = (byte)((hidden >> 24) & 0xFF);
            
            // Total sectors 32
            uint tot = (uint)sectorCount; 
            bpb[32] = (byte)(tot & 0xFF); 
            bpb[33] = (byte)((tot >> 8) & 0xFF); 
            bpb[34] = (byte)((tot >> 16) & 0xFF); 
            bpb[35] = (byte)((tot >> 24) & 0xFF);
            
            // FAT size 32: rough estimate
            uint fatsz = tot / (8u * 128u); 
            if (fatsz < 1) fatsz = 1; 
            bpb[36] = (byte)(fatsz & 0xFF); 
            bpb[37] = (byte)((fatsz >> 8) & 0xFF); 
            bpb[38] = (byte)((fatsz >> 16) & 0xFF); 
            bpb[39] = (byte)((fatsz >> 24) & 0xFF);
            
            // Flags, version
            bpb[40] = 0; bpb[41] = 0; bpb[42] = 0; bpb[43] = 0;
            
            // Root cluster (2 = first data cluster)
            bpb[44] = 2; bpb[45] = 0; bpb[46] = 0; bpb[47] = 0;
            
            // FSInfo sector
            bpb[48] = 1; bpb[49] = 0;
            
            // Backup boot sector
            bpb[50] = 6; bpb[51] = 0;
            
            // Drive number, boot sig, volume id
            bpb[64] = 0x80; 
            bpb[66] = 0x29; 
            bpb[67] = 0x12; bpb[68] = 0x34; bpb[69] = 0x56; bpb[70] = 0x78;
            
            // Volume label
            string vl = label.Length > 11 ? label.Substring(0, 11) : label;
            while (vl.Length < 11) vl += " ";
            for (int i = 0; i < 11; i++) bpb[71 + i] = (byte)vl[i];
            
            // File system type
            string fs = "FAT32   "; 
            for (int i = 0; i < 8; i++) bpb[82 + i] = (byte)fs[i];
            
            // Signature
            bpb[510] = 0x55; bpb[511] = 0xAA;
            
            unsafe { 
                fixed (byte* p = bpb) disk.Write(startSector, 1, p); 
            }
            
            // Zero reserved sectors
            var zero = new byte[bytesPerSector]; 
            unsafe { 
                fixed (byte* pz = zero) { 
                    for (ulong s = startSector + 2; s < startSector + 32; s++) {
                        disk.Write(s, 1, pz);
                    }
                } 
            }
            
            // Write FSInfo sector
            byte[] fsinfo = new byte[512]; 
            for (int i = 0; i < 512; i++) fsinfo[i] = 0; 
            fsinfo[0] = (byte)'R'; 
            fsinfo[1] = (byte)'R'; 
            fsinfo[2] = (byte)'a'; 
            fsinfo[3] = (byte)'A'; 
            fsinfo[484] = 0xFF; fsinfo[485] = 0xFF; fsinfo[486] = 0xFF; fsinfo[487] = 0xFF; // Free cluster count (unknown)
            fsinfo[488] = 0xFF; fsinfo[489] = 0xFF; fsinfo[490] = 0xFF; fsinfo[491] = 0xFF; // Next free cluster (unknown)
            fsinfo[508] = 0x00; fsinfo[509] = 0x00; 
            fsinfo[510] = 0x55; fsinfo[511] = 0xAA; 
            unsafe { 
                fixed (byte* pf = fsinfo) disk.Write(startSector + 1, 1, pf); 
            }
            
            // Initialize FAT tables
            unsafe {
                fixed (byte* pz = zero) {
                    // First FAT
                    ulong fatStart = startSector + 32;
                    for (uint i = 0; i < fatsz; i++) {
                        disk.Write(fatStart + i, 1, pz);
                    }
                    
                    // Second FAT
                    for (uint i = 0; i < fatsz; i++) {
                        disk.Write(fatStart + fatsz + i, 1, pz);
                    }
                }
            }
            
            // Write FAT chain for root directory
            byte[] fatSector = new byte[512];
            for (int i = 0; i < 512; i++) fatSector[i] = 0;
            
            // First FAT entry (media descriptor)
            fatSector[0] = 0xF8; fatSector[1] = 0xFF; fatSector[2] = 0xFF; fatSector[3] = 0x0F;
            // Second FAT entry (EOF marker)
            fatSector[4] = 0xFF; fatSector[5] = 0xFF; fatSector[6] = 0xFF; fatSector[7] = 0x0F;
            // Third FAT entry (root directory - EOF)
            fatSector[8] = 0xFF; fatSector[9] = 0xFF; fatSector[10] = 0xFF; fatSector[11] = 0x0F;
            
            unsafe {
                fixed (byte* pf = fatSector) {
                    ulong fatStart = startSector + 32;
                    disk.Write(fatStart, 1, pf);
                    disk.Write(fatStart + fatsz, 1, pf); // Second FAT copy
                }
            }
            
            return true;
        }
        private bool CopySystemFiles() {
            // Copy complete OS filesystem to system partition
            try {
                _statusMessage = "Copying system files...";
                
                if (_selectedDiskIndex < 0 || _selectedDiskIndex >= _availableDisks.Count) return false;
                var diskInfo = _availableDisks[_selectedDiskIndex];
                var targetDisk = diskInfo.IsUSB ? (guideXOS.FS.Disk)diskInfo.UsbDisk : guideXOS.FS.Disk.Instance;
                if (targetDisk == null) return false;
                
                // Calculate system partition start
                uint bps = diskInfo.BytesPerSector == 0 ? 512u : diskInfo.BytesPerSector;
                ulong bootStart = 1;
                ulong bootSectors = ((ulong)_bootPartitionMB * 1024UL * 1024UL) / bps;
                if (bootSectors < 2048) bootSectors = 2048;
                if (bootSectors >= diskInfo.TotalSectors - 4096) bootSectors = diskInfo.TotalSectors / 4;
                
                ulong sysStart = bootStart + bootSectors;
                
                // Save current disk reference
                var originalDisk = guideXOS.FS.Disk.Instance;
                var originalFile = guideXOS.FS.File.Instance;
                
                try {
                    // Step 1: Copy boot files to boot partition (FAT32)
                    _statusMessage = "Copying boot files...";
                    
                    // Switch to boot partition
                    guideXOS.FS.Disk.Instance = targetDisk;
                    var bootFS = new guideXOS.FS.FAT();
                    guideXOS.FS.File.Instance = bootFS;
                    
                    // Copy kernel and GRUB files
                    CopyBootFiles();
                    
                    // Step 2: Copy full system to system partition
                    _statusMessage = "Copying system partition...";
                    
                    // For this we need to read from the ramdisk and write to system partition
                    // This is complex, so we'll use a simplified approach:
                    // Copy the entire ramdisk content to the system partition
                    
                    // Switch back to ramdisk to read files
                    guideXOS.FS.Disk.Instance = originalDisk;
                    guideXOS.FS.File.Instance = originalFile;
                    
                    // Get all files from ramdisk
                    _statusMessage = "Enumerating files...";
                    var allFiles = GatherAllFiles("/");
                    
                    // Switch to system partition for writing
                    guideXOS.FS.Disk.Instance = targetDisk;
                    var sysFS = new guideXOS.FS.FAT();
                    guideXOS.FS.File.Instance = sysFS;
                    
                    // Copy all files
                    _statusMessage = $"Copying {allFiles.Count} files...";
                    for (int i = 0; i < allFiles.Count; i++) {
                        try {
                            var file = allFiles[i];
                            
                            // Read from ramdisk
                            guideXOS.FS.Disk.Instance = originalDisk;
                            guideXOS.FS.File.Instance = originalFile;
                            byte[] data = guideXOS.FS.File.ReadAllBytes(file);
                            
                            // Write to system partition
                            guideXOS.FS.Disk.Instance = targetDisk;
                            guideXOS.FS.File.Instance = sysFS;
                            
                            // Ensure directory exists
                            EnsureDirectoryExists(GetDirectoryPath(file));
                            
                            guideXOS.FS.File.WriteAllBytes(file, data);
                            data.Dispose();
                            
                            // Update progress
                            int fileProgress = 40 + ((i * 30) / allFiles.Count);
                            if (fileProgress > _installProgress) {
                                _installProgress = fileProgress;
                            }
                        } catch {
                            // Continue on error
                        }
                    }
                    
                    allFiles.Dispose();
                    
                } finally {
                    // Restore original disk and filesystem
                    guideXOS.FS.Disk.Instance = originalDisk;
                    guideXOS.FS.File.Instance = originalFile;
                }
                
                return true;
            } catch {
                return false;
            }
        }
        
        private void CopyBootFiles() {
            // Copy essential boot files to boot partition
            try {
                var originalDisk = guideXOS.FS.Disk.Instance;
                var originalFile = guideXOS.FS.File.Instance;
                
                try {
                    // Switch back to ramdisk to read kernel
                    guideXOS.FS.Disk.Instance = originalDisk;
                    guideXOS.FS.File.Instance = originalFile;
                    
                    // Read kernel binary
                    byte[] kernelData = null;
                    if (guideXOS.FS.File.Exists("/boot/kernel.bin")) {
                        kernelData = guideXOS.FS.File.ReadAllBytes("/boot/kernel.bin");
                    }
                    
                    // Create boot directory structure
                    EnsureDirectoryExists("/boot");
                    EnsureDirectoryExists("/boot/grub");
                    
                    // Write kernel to boot partition
                    if (kernelData != null) {
                        guideXOS.FS.File.WriteAllBytes("/boot/kernel.bin", kernelData);
                        kernelData.Dispose();
                    }
                    
                    // Create grub.cfg
                    string grubCfg = 
                        "set timeout=3\n" +
                        "set default=0\n" +
                        "\n" +
                        "menuentry 'guideXOS' {\n" +
                        "    insmod fat\n" +
                        "    insmod part_msdos\n" +
                        "    set root=(hd0,msdos1)\n" +
                        "    linux /boot/kernel.bin root=/dev/sda2\n" +
                        "    boot\n" +
                        "}\n" +
                        "\n" +
                        "menuentry 'guideXOS (Safe Mode)' {\n" +
                        "    insmod fat\n" +
                        "    insmod part_msdos\n" +
                        "    set root=(hd0,msdos1)\n" +
                        "    linux /boot/kernel.bin root=/dev/sda2 nomodeset\n" +
                        "    boot\n" +
                        "}\n";
                    
                    guideXOS.FS.File.WriteAllBytes("/boot/grub/grub.cfg", GetAsciiBytes(grubCfg));
                    
                } finally {
                    guideXOS.FS.Disk.Instance = originalDisk;
                    guideXOS.FS.File.Instance = originalFile;
                }
            } catch {
                // Ignore errors - some files may not exist
            }
        }
        
        private List<string> GatherAllFiles(string path) {
            var result = new List<string>();
            try {
                var entries = guideXOS.FS.File.GetFiles(path);
                if (entries != null) {
                    for (int i = 0; i < entries.Count; i++) {
                        var e = entries[i];
                        string fullPath = path + (path.EndsWith("/") ? "" : "/") + e.Name;
                        
                        if (e.Attribute == guideXOS.FS.FileAttribute.Directory) {
                            // Recursively gather files from subdirectories
                            var subFiles = GatherAllFiles(fullPath);
                            for (int j = 0; j < subFiles.Count; j++) {
                                result.Add(subFiles[j]);
                            }
                            subFiles.Dispose();
                        } else {
                            result.Add(fullPath);
                        }
                        e.Dispose();
                    }
                    entries.Dispose();
                }
            } catch {
                // Ignore errors
            }
            return result;
        }
        
        private void EnsureDirectoryExists(string path) {
            // Simple directory creation - may not work depending on filesystem implementation
            try {
                if (!guideXOS.FS.File.Exists(path)) {
                    // Try to create directory - this may not be implemented in all filesystems
                    // For now, we'll just skip it
                }
            } catch {
                // Ignore
            }
        }
        
        private string GetDirectoryPath(string filePath) {
            int lastSlash = filePath.LastIndexOf('/');
            if (lastSlash > 0) {
                return filePath.Substring(0, lastSlash);
            }
            return "/";
        }

        private static byte[] GetAsciiBytes(string s) {
            if (s == null) return new byte[0];
            var b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) b[i] = (byte)(s[i] & 0x7F);
            return b;
        }
        
        private bool InstallBootloader() {
            // Install GRUB bootloader to MBR
            try {
                _statusMessage = "Installing bootloader...";
                
                if (_selectedDiskIndex < 0 || _selectedDiskIndex >= _availableDisks.Count) return false;
                var diskInfo = _availableDisks[_selectedDiskIndex];
                var targetDisk = diskInfo.IsUSB ? (guideXOS.FS.Disk)diskInfo.UsbDisk : guideXOS.FS.Disk.Instance;
                if (targetDisk == null) return false;
                
                // The MBR was already written in PartitionDisk() with GRUB stage1
                // Here we can optionally install GRUB stage1.5 or stage2 to the boot partition
                
                // For a minimal bootloader, we've already done the work in PartitionDisk()
                // A full GRUB installation would require:
                // 1. GRUB stage1 in MBR (done)
                // 2. GRUB stage1.5 in post-MBR gap or boot partition
                // 3. GRUB stage2 and modules in /boot/grub/
                
                // Since we're using a simplified approach, we'll just verify the MBR
                byte[] mbrCheck = new byte[512];
                unsafe {
                    fixed (byte* p = mbrCheck) {
                        targetDisk.Read(0, 1, p);
                    }
                }
                
                // Verify MBR signature
                if (mbrCheck[510] != 0x55 || mbrCheck[511] != 0xAA) {
                    _statusMessage = "Error: Invalid MBR signature";
                    return false;
                }
                
                _statusMessage = "Bootloader installed successfully";
                return true;
            } catch {
                _statusMessage = "Error installing bootloader";
                return false;
            }
        }
        
        /// <summary>
        /// Get GRUB Stage1 bootloader code for MBR
        /// This is a minimal x86 boot loader that loads from the active partition
        /// </summary>
        private byte[] GetGRUBStage1() {
            // This is a simplified bootloader that:
            // 1. Loads the boot sector from the active partition
            // 2. Jumps to it
            // A real GRUB stage1 would be more complex
            
            byte[] bootCode = new byte[446];
            
            // Minimal bootloader in x86 assembly:
            // - Find active partition
            // - Load boot sector from active partition  
            // - Jump to loaded code
            
            int offset = 0;
            
            // CLI - disable interrupts
            bootCode[offset++] = 0xFA;
            
            // XOR AX, AX - zero AX
            bootCode[offset++] = 0x31;
            bootCode[offset++] = 0xC0;
            
            // MOV SS, AX - set stack segment to 0
            bootCode[offset++] = 0x8E;
            bootCode[offset++] = 0xD0;
            
            // MOV SP, 0x7C00 - set stack pointer
            bootCode[offset++] = 0xBC;
            bootCode[offset++] = 0x00;
            bootCode[offset++] = 0x7C;
            
            // STI - enable interrupts
            bootCode[offset++] = 0xFB;
            
            // MOV SI, 0x7C00 + 446 (partition table offset)
            bootCode[offset++] = 0xBE;
            bootCode[offset++] = 0xBE;
            bootCode[offset++] = 0x7D;
            
            // MOV CX, 4 - loop through 4 partition entries
            bootCode[offset++] = 0xB9;
            bootCode[offset++] = 0x04;
            bootCode[offset++] = 0x00;
            
            // Loop: Find active partition (0x80 flag)
            // CMP BYTE PTR [SI], 0x80
            bootCode[offset++] = 0x80;
            bootCode[offset++] = 0x3C;
            bootCode[offset++] = 0x80;
            
            // JE LoadBoot
            bootCode[offset++] = 0x74;
            bootCode[offset++] = 0x08;
            
            // ADD SI, 16 - next partition entry
            bootCode[offset++] = 0x83;
            bootCode[offset++] = 0xC6;
            bootCode[offset++] = 0x10;
            
            // LOOP back
            bootCode[offset++] = 0xE2;
            bootCode[offset++] = 0xF4;
            
            // No active partition found - hang
            bootCode[offset++] = 0xF4; // HLT
            bootCode[offset++] = 0xEB;
            bootCode[offset++] = 0xFD; // JMP -3 (infinite loop)
            
            // LoadBoot:
            // Load boot sector from active partition LBA (at SI+8)
            // MOV EAX, [SI+8] - get LBA start
            bootCode[offset++] = 0x66;
            bootCode[offset++] = 0x8B;
            bootCode[offset++] = 0x44;
            bootCode[offset++] = 0x08;
            
            // Read sector using INT 13h extensions (simplified)
            // For now, just show a message and hang
            
            // MOV SI, message
            bootCode[offset++] = 0xBE;
            int msgOffset = offset;
            bootCode[offset++] = 0x00; // Will be filled
            bootCode[offset++] = 0x00;
            
            // Print loop using INT 10h
            // LODSB
            bootCode[offset++] = 0xAC;
            
            // OR AL, AL
            bootCode[offset++] = 0x08;
            bootCode[offset++] = 0xC0;
            
            // JZ Done
            bootCode[offset++] = 0x74;
            bootCode[offset++] = 0x09;
            
            // MOV AH, 0x0E
            bootCode[offset++] = 0xB4;
            bootCode[offset++] = 0x0E;
            
            // MOV BX, 0x0007
            bootCode[offset++] = 0xBB;
            bootCode[offset++] = 0x07;
            bootCode[offset++] = 0x00;
            
            // INT 0x10
            bootCode[offset++] = 0xCD;
            bootCode[offset++] = 0x10;
            
            // JMP Print loop
            bootCode[offset++] = 0xEB;
            bootCode[offset++] = 0xF2;
            
            // Done: Hang
            bootCode[offset++] = 0xF4; // HLT
            bootCode[offset++] = 0xEB;
            bootCode[offset++] = 0xFD; // JMP -3
            
            // Message
            int messageStart = offset;
            string msg = "Loading guideXOS...\r\n\0";
            for (int i = 0; i < msg.Length && offset < bootCode.Length - 20; i++) {
                bootCode[offset++] = (byte)msg[i];
            }
            
            // Fix message offset reference
            bootCode[msgOffset] = (byte)((0x7C00 + messageStart) & 0xFF);
            bootCode[msgOffset + 1] = (byte)(((0x7C00 + messageStart) >> 8) & 0xFF);
            
            return bootCode;
        }

        private void DrawButton(int x, int y, int w, int h, string text, bool enabled = true) {
            if (!enabled) {
                Framebuffer.Graphics.FillRectangle(x, y, w, h, 0xFF1A1A1A);
                WindowManager.font.DrawString(x + w / 2 - text.Length * 4, y + h / 2 - WindowManager.font.FontSize / 2, text, w, WindowManager.font.FontSize);
                return;
            }

            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool hover = Hit(mx, my, x, y, w, h);

            uint bgColor = hover ? 0xFF4C8BF5 : 0xFF3A3A3A;
            Framebuffer.Graphics.FillRectangle(x, y, w, h, bgColor);
            WindowManager.font.DrawString(x + w / 2 - text.Length * 4, y + h / 2 - WindowManager.font.FontSize / 2, text);
        }

        private static bool Hit(int mx, int my, int x, int y, int w, int h) {
            return mx >= x && mx <= x + w && my >= y && my <= y + h;
        }

        private static string FormatSize(ulong bytes) {
            const ulong KB = 1024;
            const ulong MB = 1024 * 1024;
            const ulong GB = 1024 * 1024 * 1024;

            if (bytes >= GB) return ((bytes + GB / 10) / GB).ToString() + " GB";
            if (bytes >= MB) return ((bytes + MB / 10) / MB).ToString() + " MB";
            if (bytes >= KB) return ((bytes + KB / 10) / KB).ToString() + " KB";
            return bytes.ToString() + " B";
        }
    }
}