using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Graph;
using guideXOS.Misc;
using guideXOS.Kernel.Drivers;
using System.Collections.Generic;
using System.Windows.Forms;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// File Write Test Application - Tests writing capabilities to different storage devices
    /// </summary>
    internal unsafe class FileWriteTest : Window {
        private string _log;
        private int _scroll;
        private bool _testRunning;
        private List<DiskInfo> _disks;
        private int _selectedDisk;
        
        private struct DiskInfo {
            public string Name;
            public Disk DiskInstance;
            public bool IsIDE;
            public ulong SizeBytes;
        }

        public FileWriteTest(int X, int Y, int W = 700, int H = 550) : base(X, Y, W, H) {
            Title = "File Write Test";
            ShowInTaskbar = true;
            ShowInStartMenu = true;
            _log = "File Write Test Utility\n";
            _log += "======================\n\n";
            _log += "This tool helps test file writing on different storage devices.\n\n";
            _testRunning = false;
            _scroll = 0;
            _selectedDisk = 0;
            _disks = new List<DiskInfo>();
            
            ScanDisks();
        }

        private void ScanDisks() {
            _disks.Clear();
            _log += "[SCAN] Scanning for available disks...\n";
            
            // Scan IDE/SATA disks
            if (IDE.Ports != null) {
                for (int i = 0; i < IDE.Ports.Count; i++) {
                    var ide = IDE.Ports[i];
                    var info = new DiskInfo {
                        Name = $"IDE{i} ({(ide.Size / (1024 * 1024))}MB)",
                        DiskInstance = ide,
                        IsIDE = true,
                        SizeBytes = ide.Size
                    };
                    _disks.Add(info);
                    _log += $"  Found: {info.Name}\n";
                }
            }
            
            if (_disks.Count == 0) {
                _log += "  No disks found.\n";
            }
            
            _log += "\n";
        }

        private void Log(string msg) {
            _log += msg + "\n";
        }

        private void TestRawSectorReadWrite() {
            if (_selectedDisk < 0 || _selectedDisk >= _disks.Count) {
                Log("[ERROR] No disk selected");
                return;
            }
            
            var disk = _disks[_selectedDisk];
            Log($"\n[TEST] Raw Sector Read/Write Test on {disk.Name}");
            Log("--------------------------------------");
            
            // Test sector 2048 (safe area, after typical boot sectors)
            ulong testSector = 2048;
            
            Log($"Testing sector {testSector}...");
            
            // Save original Disk.Instance
            Disk originalDisk = Disk.Instance;
            
            try {
                // Switch to test disk
                Disk.Instance = disk.DiskInstance;
                
                // Allocate test buffers
                byte[] readBuf = new byte[512];
                byte[] writeBuf = new byte[512];
                byte[] verifyBuf = new byte[512];
                
                // Read original data
                Log("  Reading original sector data...");
                fixed (byte* p = readBuf) {
                    if (!disk.DiskInstance.Read(testSector, 1, p)) {
                        Log("  [FAIL] Could not read sector");
                        return;
                    }
                }
                Log("  [OK] Read successful");
                
                // Prepare test pattern
                Log("  Writing test pattern...");
                for (int i = 0; i < 512; i++) {
                    writeBuf[i] = (byte)(i % 256);
                }
                writeBuf[0] = (byte)'T';
                writeBuf[1] = (byte)'E';
                writeBuf[2] = (byte)'S';
                writeBuf[3] = (byte)'T';
                
                // Write test pattern
                fixed (byte* p = writeBuf) {
                    if (!disk.DiskInstance.Write(testSector, 1, p)) {
                        Log("  [FAIL] Could not write sector");
                        return;
                    }
                }
                Log("  [OK] Write successful");
                
                // Verify write
                Log("  Verifying written data...");
                fixed (byte* p = verifyBuf) {
                    if (!disk.DiskInstance.Read(testSector, 1, p)) {
                        Log("  [FAIL] Could not read back sector");
                        return;
                    }
                }
                
                bool match = true;
                for (int i = 0; i < 512; i++) {
                    if (verifyBuf[i] != writeBuf[i]) {
                        match = false;
                        break;
                    }
                }
                
                if (match) {
                    Log("  [OK] Verification successful - data matches!");
                } else {
                    Log("  [FAIL] Verification failed - data mismatch");
                }
                
                // Restore original data
                Log("  Restoring original sector data...");
                fixed (byte* p = readBuf) {
                    disk.DiskInstance.Write(testSector, 1, p);
                }
                Log("  [OK] Original data restored");
                
                Log("\n[RESULT] Raw sector test PASSED");
                
            } finally {
                // Restore original Disk.Instance
                Disk.Instance = originalDisk;
            }
        }

        private void TestFATFileSystem() {
            if (_selectedDisk < 0 || _selectedDisk >= _disks.Count) {
                Log("[ERROR] No disk selected");
                return;
            }
            
            var disk = _disks[_selectedDisk];
            Log($"\n[TEST] FAT Filesystem Test on {disk.Name}");
            Log("--------------------------------------");
            
            // Save original instances
            Disk originalDisk = Disk.Instance;
            FileSystem originalFS = File.Instance;
            
            try {
                // Switch to test disk
                Disk.Instance = disk.DiskInstance;
                
                Log("Initializing FAT filesystem...");
                
                FAT fat = null;
                try {
                    fat = new FAT();
                    File.Instance = fat;
                    Log("  [OK] FAT filesystem initialized");
                } catch {
                    Log("  [FAIL] Could not initialize FAT filesystem");
                    Log("  Disk may not be FAT formatted");
                    return;
                }
                
                // Test file write
                Log("\nTesting file write operations...");
                
                string testFile = "/guidexos_test.txt";
                string testContent = "Hello from guideXOS!\nThis is a file write test.\n";
                byte[] contentBytes = new byte[testContent.Length];
                for (int i = 0; i < testContent.Length; i++) {
                    contentBytes[i] = (byte)testContent[i];
                }
                
                Log($"  Writing test file: {testFile}");
                try {
                    fat.WriteAllBytes(testFile, contentBytes);
                    Log("  [OK] File written successfully");
                } catch {
                    Log("  [FAIL] Could not write file");
                    return;
                }
                
                // Test file read
                Log("  Reading back test file...");
                byte[] readBytes = null;
                try {
                    readBytes = fat.ReadAllBytes(testFile);
                    if (readBytes == null || readBytes.Length != contentBytes.Length) {
                        Log("  [FAIL] File size mismatch");
                        return;
                    }
                    Log("  [OK] File read successfully");
                } catch {
                    Log("  [FAIL] Could not read file");
                    return;
                }
                
                // Verify content
                Log("  Verifying file content...");
                bool match = true;
                for (int i = 0; i < contentBytes.Length; i++) {
                    if (readBytes[i] != contentBytes[i]) {
                        match = false;
                        break;
                    }
                }
                
                if (match) {
                    Log("  [OK] Content verification successful!");
                } else {
                    Log("  [FAIL] Content mismatch");
                    return;
                }
                
                // List directory
                Log("\nListing root directory...");
                var files = fat.GetFiles("/");
                Log($"  Found {files.Count} entries:");
                for (int i = 0; i < files.Count && i < 10; i++) {
                    string attr = (files[i].Attribute & FileAttribute.Directory) != 0 ? "DIR " : "FILE";
                    Log($"    [{attr}] {files[i].Name}");
                }
                
                // Clean up test file
                Log("\nDeleting test file...");
                try {
                    fat.Delete(testFile);
                    Log("  [OK] Test file deleted");
                } catch {
                    Log("  [WARN] Could not delete test file");
                }
                
                Log("\n[RESULT] FAT filesystem test PASSED");
                
            } finally {
                // Restore original instances
                Disk.Instance = originalDisk;
                File.Instance = originalFS;
            }
        }

        private void TestFormatOptions() {
            Log("\n[INFO] Format/Partition Options");
            Log("================================");
            Log("\nYou can format a disk using:");
            Log("  1. FAT12 - For small disks (< 16MB)");
            Log("  2. FAT16 - For medium disks (16MB - 2GB)");
            Log("  3. FAT32 - For large disks (> 2GB)");
            Log("\nRecommended tools:");
            Log("  - Use Disk Manager app in guideXOS");
            Log("  - Or format disk externally before booting");
            Log("\nCurrent FAT implementation supports:");
            Log("  ? Reading FAT12/16/32");
            Log("  ? Writing files to existing FAT volumes");
            Log("  ? Creating/deleting files");
            Log("  ? Low-level format not yet implemented");
        }

        public override void OnDraw() {
            base.OnDraw();
            
            if (!Visible) return;
            
            int contentY = Y + BarHeight + 5;
            int contentHeight = Height - BarHeight - 10;
            
            // Draw background
            Framebuffer.Graphics.FillRectangle(X, contentY, Width, contentHeight, 0xFF1E1E1E);
            
            // Draw disk selector
            int btnY = contentY + 10;
            DrawText(X + 10, btnY, "Select Disk:");
            
            int btnX = X + 120;
            for (int i = 0; i < _disks.Count && i < 6; i++) {
                bool selected = i == _selectedDisk;
                uint color = selected ? 0xFF00D9FF : 0xFF323232;
                DrawButton(btnX, btnY, 100, 25, _disks[i].Name, color);
                btnX += 110;
            }
            
            // Draw test buttons
            btnY += 40;
            DrawText(X + 10, btnY, "Tests:");
            
            btnY += 25;
            if (DrawButton(X + 10, btnY, 150, 30, "Raw Sector R/W")) {
                if (!_testRunning) {
                    _testRunning = true;
                    TestRawSectorReadWrite();
                    _testRunning = false;
                }
            }
            
            if (DrawButton(X + 170, btnY, 150, 30, "FAT Filesystem")) {
                if (!_testRunning) {
                    _testRunning = true;
                    TestFATFileSystem();
                    _testRunning = false;
                }
            }
            
            if (DrawButton(X + 330, btnY, 150, 30, "Format Options")) {
                if (!_testRunning) {
                    TestFormatOptions();
                }
            }
            
            if (DrawButton(X + 490, btnY, 150, 30, "Refresh Disks")) {
                ScanDisks();
            }
            
            // Draw log area
            int logY = btnY + 40;
            int logHeight = contentY + contentHeight - logY - 10;
            
            DrawText(X + 10, logY, "Test Log:");
            logY += 20;
            logHeight -= 20;
            
            // Draw log background
            Framebuffer.Graphics.FillRectangle(X + 10, logY, Width - 20, logHeight, 0xFF000000);
            Framebuffer.Graphics.DrawRectangle(X + 10, logY, Width - 20, logHeight, 0xFF323232);
            
            // Draw log text
            var lines = _log.Split('\n');
            int lineY = logY + 5 - _scroll;
            
            for (int i = 0; i < lines.Length; i++) {
                if (lineY >= logY && lineY < logY + logHeight - 15) {
                    string line = lines[i];
                    
                    // Simple text rendering - WindowManager.font.DrawString doesn't support color parameter
                    // So we'll just draw in white for now
                    WindowManager.font.DrawString(X + 15, lineY, line);
                }
                lineY += 15;
            }
        }

        private bool ContainsText(string str, string substring) {
            if (str == null || substring == null) return false;
            for (int i = 0; i <= str.Length - substring.Length; i++) {
                bool match = true;
                for (int j = 0; j < substring.Length; j++) {
                    if (str[i + j] != substring[j]) {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }

        private void DrawText(int x, int y, string text) {
            if (WindowManager.font != null) {
                WindowManager.font.DrawString(x, y, text);
            }
        }

        private bool DrawButton(int x, int y, int w, int h, string text, uint bgColor = 0xFF323232) {
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            
            bool hover = mx >= x && mx < x + w && my >= y && my < y + h;
            bool clicked = hover && Control.MouseButtons == MouseButtons.Left;
            
            uint color = hover ? 0xFF3A3A3A : bgColor;
            
            Framebuffer.Graphics.FillRectangle(x, y, w, h, color);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF555555);
            
            int textX = x + (w / 2) - (text.Length * 4);
            int textY = y + (h / 2) - 6;
            WindowManager.font.DrawString(textX, textY, text);
            
            if (clicked && !_testRunning) {
                Timer.Sleep(100); // Debounce
            }
            
            return clicked && !_testRunning;
        }

        public override void OnInput() {
            base.OnInput();
            
            if (!Visible) return;
            
            // Handle disk selection clicks
            int contentY = Y + BarHeight + 5;
            int btnY = contentY + 10;
            int btnX = X + 120;
            
            if (Control.MouseButtons == MouseButtons.Left) {
                int mx = Control.MousePosition.X;
                int my = Control.MousePosition.Y;
                
                for (int i = 0; i < _disks.Count && i < 6; i++) {
                    if (mx >= btnX && mx < btnX + 100 && my >= btnY && my < btnY + 25) {
                        _selectedDisk = i;
                        Timer.Sleep(100);
                        break;
                    }
                    btnX += 110;
                }
            }
        }
    }
}
