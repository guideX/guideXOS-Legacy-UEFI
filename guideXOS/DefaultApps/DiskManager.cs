using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using System;
using System.Windows.Forms;
using System.Collections.Generic;
using guideXOS.GUI;

namespace guideXOS.DefaultApps {
    // A more Windows-like Disk Management UI with multi-disk support (System IDE + USB MSC),
    // left disk list, right volumes grid, and partition map. Buttons to switch filesystem drivers.
    internal class DiskManager : Window {
        private string _status = string.Empty;
        private string _detected = "Unknown";
        private bool _clickLock;

        // Layout constants
        private const int Pad = 12;
        private const int LeftPaneW = 200;
        private const int HeaderH = 26;
        private const int RowH = 24;
        private const int BtnH = 26;
        private const int Gap = 8;

        // Disks model
        private class DiskEntry {
            public string Name;
            public bool IsSystem;
            public USBMSCBot.USBDisk UsbDisk; // null for system
            public bool HaveInfo;
            public ulong TotalSectors;
            public uint BytesPerSector;
            public Part[] Parts; // 4 MBR primary entries
        }

        private struct Part { public uint Status; public byte Type; public uint LbaStart; public uint LbaCount; public string Fs; }

        private List<DiskEntry> _disks = new List<DiskEntry>(8);
        private int _selectedDiskIndex = 0;

        // Cache
        private string _cachedTotalCaption;

        // Action buttons coords
        private int _bxDetectX, _bxDetectY;
        private int _bxAutoX, _bxAutoY;
        private int _bxSwitchFatX, _bxSwitchFatY;
        private int _bxSwitchTarX, _bxSwitchTarY;
        private int _bxSwitchExtX, _bxSwitchExtY;
        private int _bxFormatExfatX, _bxFormatExfatY;
        private int _bxCreatePartX, _bxCreatePartY;
        private int _bxRefreshX, _bxRefreshY;

        public DiskManager(int x, int y, int w = 920, int h = 560) : base(x, y, w, h) {
            IsResizable = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowInTaskbar = true;
            Title = "Disk Management";
            _status = BuildStatus();
            RefreshDisks();
        }

        private string BuildStatus() {
            string driver;
            if (File.Instance == null) driver = "<none>";
            else if (File.Instance is FAT) driver = "FAT";
            else if (File.Instance is TarFS) driver = "TarFS";
            else if (File.Instance is EXT2) driver = "EXT2";
            else if (File.Instance is CloudFS) driver = "Cloud";
            else driver = "Unknown";
            return $"Driver: {driver}\nDetected media: {_detected}";
        }

        private void RefreshDisks() {
            _disks.Clear();
            // System disk (IDE-backed Disk.Instance)
            var sys = new DiskEntry(); sys.Name = "Disk 0 (System)"; sys.IsSystem = true; sys.Parts = new Part[4];
            // Try gather size
            if (Disk.Instance is IDEDevice ide) {
                sys.BytesPerSector = IDEDevice.SectorSize;
                sys.TotalSectors = ide.Size / IDEDevice.SectorSize;
                sys.HaveInfo = true;
            }
            ReadMBRForEntry(sys);
            _disks.Add(sys);
            // USB MSC disks
            var devs = USBStorage.GetAll();
            if (devs != null) {
                int idx = 1;
                for (int i = 0; i < devs.Length; i++) {
                    var d = devs[i]; if (d == null) continue;
                    if (!(d.Class == 0x08 && d.SubClass == 0x06 && d.Protocol == 0x50)) continue; // MSC BOT
                    var u = USBMSC.TryOpenDisk(d);
                    if (u == null || !u.IsReady) continue;
                    var entry = new DiskEntry();
                    entry.Name = "Disk " + idx.ToString() + " (USB)"; idx++;
                    entry.IsSystem = false; entry.UsbDisk = u; entry.Parts = new Part[4];
                    entry.BytesPerSector = u.LogicalBlockSize; entry.TotalSectors = u.TotalBlocks; entry.HaveInfo = true;
                    ReadMBRForEntry(entry);
                    _disks.Add(entry);
                }
            }
            if (_selectedDiskIndex >= _disks.Count) _selectedDiskIndex = _disks.Count - 1; if (_selectedDiskIndex < 0) _selectedDiskIndex = 0;
            var sel = GetSelected();
            _cachedTotalCaption = sel != null && sel.HaveInfo ? $"Total: {FmtSize(sel.TotalSectors * (sel.BytesPerSector == 0 ? 512UL : sel.BytesPerSector))}" : null;
        }

        private DiskEntry GetSelected() { return _disks.Count > 0 && _selectedDiskIndex >= 0 && _selectedDiskIndex < _disks.Count ? _disks[_selectedDiskIndex] : null; }

        private void ProbeOnce() {
            try {
                var buf = new byte[FileSystem.SectorSize];
                unsafe { fixed (byte* p = buf) Disk.Instance.Read(0, 1, p); }
                if (buf.Length >= 512 && buf[257] == (byte)'u' && buf[258] == (byte)'s' && buf[259] == (byte)'t' && buf[260] == (byte)'a' && buf[261] == (byte)'r') _detected = "TAR (initrd)";
                else if (buf.Length >= 512 && buf[510] == 0x55 && buf[511] == 0xAA) _detected = "FAT (boot sector)";
                else _detected = "Unknown";
            } catch { _detected = "Unknown"; }
            _status = BuildStatus();
        }

        private static string FmtSize(ulong bytes) {
            const ulong KB = 1024; const ulong MB = 1024 * 1024; const ulong GB = 1024 * 1024 * 1024;
            if (bytes >= GB) return ((bytes + GB / 10) / GB).ToString() + " GB";
            if (bytes >= MB) return ((bytes + MB / 10) / MB).ToString() + " MB";
            if (bytes >= KB) return ((bytes + KB / 10) / KB).ToString() + " KB";
            return bytes.ToString() + " B";
        }

        private static string DetectFsAtLBA(uint lbaStart) {
            if (lbaStart == 0) return "<empty>";
            try {
                var sec = new byte[512];
                unsafe { fixed (byte* p = sec) Disk.Instance.Read(lbaStart, 1, p); }
                if (sec.Length >= 512 && sec[257] == (byte)'u' && sec[258] == (byte)'s' && sec[259] == (byte)'t' && sec[260] == (byte)'a' && sec[261] == (byte)'r') return "TarFS";
                if (sec.Length >= 512 && sec[510] == 0x55 && sec[511] == 0xAA) {
                    ushort bytsPerSec = (ushort)(sec[11] | sec[12] << 8); byte secPerClus = sec[13];
                    if (bytsPerSec == 512 || bytsPerSec == 1024 || bytsPerSec == 2048 || bytsPerSec == 4096) if (secPerClus != 0) return "FAT";
                }
                // EXT
                var sb = new byte[1024]; unsafe { fixed (byte* p2 = sb) Disk.Instance.Read(lbaStart + 2, 2, p2); }
                ushort magic = (ushort)(sb[56] | sb[57] << 8); if (magic == 0xEF53) return "EXT2";
            } catch { }
            return "Unknown";
        }

        private void WithDisk(DiskEntry entry, Action action) {
            if (entry == null || action == null) return;
            var prev = Disk.Instance;
            try {
                if (!entry.IsSystem && entry.UsbDisk != null) Disk.Instance = entry.UsbDisk;
                action();
            } finally {
                Disk.Instance = prev;
            }
        }

        private void ReadMBRForEntry(DiskEntry entry) {
            if (entry == null) return;
            if (entry.Parts == null) entry.Parts = new Part[4];
            for (int i = 0; i < 4; i++) entry.Parts[i] = default;
            WithDisk(entry, () => {
                try {
                    var mbr = new byte[512]; unsafe { fixed (byte* p = mbr) Disk.Instance.Read(0, 1, p); }
                    if (mbr[510] == 0x55 && mbr[511] == 0xAA) {
                        for (int i = 0; i < 4; i++) {
                            int off = 446 + i * 16;
                            Part part = new Part(); part.Status = mbr[off + 0]; part.Type = mbr[off + 4];
                            part.LbaStart = (uint)(mbr[off + 8] | mbr[off + 9] << 8 | mbr[off + 10] << 16 | mbr[off + 11] << 24);
                            part.LbaCount = (uint)(mbr[off + 12] | mbr[off + 13] << 8 | mbr[off + 14] << 16 | mbr[off + 15] << 24);
                            part.Fs = DetectFsAtLBA(part.LbaStart);
                            entry.Parts[i] = part;
                        }
                    }
                } catch { }
            });
        }

        public override void OnInput() {
            base.OnInput(); if (!Visible) return;
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            if (left) {
                if (_clickLock) return;

                // Left pane disk selection
                int listX = X + Pad; int firstY = Y + Pad + HeaderH; int rowW = LeftPaneW - Pad * 2; int rowX = listX + Pad;
                for (int i = 0; i < _disks.Count; i++) {
                    int ry = firstY + i * (RowH + 4);
                    if (Hit(mx, my, rowX, ry, rowW, RowH)) { _selectedDiskIndex = i; _clickLock = true; return; }
                }

                // Actions (button widths computed in DrawActions)
                if (Hit(mx, my, _bxDetectX, _bxDetectY, 180, BtnH)) { ProbeOnce(); _clickLock = true; return; }
                if (Hit(mx, my, _bxAutoX, _bxAutoY, 180, BtnH)) { TrySetFS_Auto(); _clickLock = true; return; }
                if (Hit(mx, my, _bxSwitchFatX, _bxSwitchFatY, 180, BtnH)) { TrySetFS_FAT(); _clickLock = true; return; }
                if (Hit(mx, my, _bxSwitchTarX, _bxSwitchTarY, 180, BtnH)) { TrySetFS_TAR(); _clickLock = true; return; }
                if (Hit(mx, my, _bxSwitchExtX, _bxSwitchExtY, 180, BtnH)) { TrySetFS_EXT2(); _clickLock = true; return; }
                if (Hit(mx, my, _bxFormatExfatX, _bxFormatExfatY, 200, BtnH)) { TryFormatFAT(); _clickLock = true; return; }
                if (Hit(mx, my, _bxCreatePartX, _bxCreatePartY, 220, BtnH)) { TryCreatePartitionLargestFree(); _clickLock = true; return; }
                if (Hit(mx, my, _bxRefreshX, _bxRefreshY, 160, BtnH)) { RefreshDisks(); _clickLock = true; return; }
            } else {
                _clickLock = false;
            }
        }

        private static bool Hit(int mx, int my, int x, int y, int w, int h) { return mx >= x && mx <= x + w && my >= y && my <= y + h; }

        public override void OnDraw() {
            base.OnDraw();
            // Background
            Framebuffer.Graphics.FillRectangle(X + 1, Y + 1, Width - 2, Height - 2, 0xFF2B2B2B);

            // Left pane: Disks list
            DrawLeftPane();

            // Right pane split
            int rightX = X + LeftPaneW + Pad; int rightW = Width - (rightX - X) - Pad;
            if (rightW < 100) rightW = 100; // guard
            int topH = 200;
            int bottomY = Y + Pad + topH + Gap;
            int bottomH = Height - (bottomY - Y) - (Pad + 180); // leave space for actions block
            if (bottomH < 80) bottomH = 80;

            DrawVolumesGrid(rightX, Y + Pad, rightW, topH);
            DrawPartitionMap(rightX, bottomY, rightW, bottomH);
            DrawActions(rightX, Y + Height - (Pad + 160), rightW, 160);
        }

        private void DrawLeftPane() {
            int lx = X + Pad; int ly = Y + Pad; int lw = LeftPaneW; int lh = Height - Pad * 2;
            // Title
            WindowManager.font.DrawString(lx, ly - 2, "Disks");
            int listX = lx; int listY = ly + HeaderH; int rowW = lw - Pad * 2; int rowX = listX + Pad;

            uint bg = 0xFF303030; uint bgSel = 0xFF3A3A3A;
            for (int i = 0; i < _disks.Count; i++) {
                int ry = listY + i * (RowH + 4);
                Framebuffer.Graphics.FillRectangle(rowX, ry, rowW, RowH, _selectedDiskIndex == i ? bgSel : bg);
                WindowManager.font.DrawString(rowX + 6, ry + (RowH / 2 - WindowManager.font.FontSize / 2), _disks[i].Name, rowW - 12, WindowManager.font.FontSize);
            }

            // Status bottom-left area (clamp to width)
            int statusMaxW = lw - Pad * 2;
            WindowManager.font.DrawString(lx, Y + Height - (Pad + WindowManager.font.FontSize * 2 + 6), _status, statusMaxW, WindowManager.font.FontSize * 2);
        }

        private void DrawVolumesGrid(int x, int y, int w, int h) {
            // Header title
            WindowManager.font.DrawString(x, y, "Volumes");
            int gridY = y + HeaderH;

            // Column widths scaled to fit w exactly
            int[] cw = new int[] { 180, 100, 90, 120, 120, 120, 70 }; // base
            int sum = 0; for (int i = 0; i < cw.Length; i++) sum += cw[i];
            if (sum != w) {
                // scale down if necessary
                if (sum > w) {
                    float scale = w / (float)sum;
                    int newsum = 0;
                    for (int i = 0; i < cw.Length; i++) { cw[i] = (int)(cw[i] * scale); if (cw[i] < 60) cw[i] = 60; newsum += cw[i]; }
                    // adjust last column to absorb diff
                    int diff = w - newsum; cw[cw.Length - 1] += diff;
                } else {
                    cw[cw.Length - 1] += w - sum;
                }
            }

            int cx = x; for (int i = 0; i < cw.Length; i++) { DrawHeaderCell(cx, gridY, cw[i], RowH, i switch { 0 => "Volume", 1 => "Layout", 2 => "Type", 3 => "Status", 4 => "Capacity", 5 => "Free Space", _ => "% Free" }); cx += cw[i]; }

            // Rows
            int rowY = gridY + RowH;
            var sel = GetSelected(); if (sel == null) return;
            if (sel.HaveInfo) {
                for (int i = 0; i < 4; i++) {
                    var p = sel.Parts[i]; if (p.LbaCount == 0) continue;
                    cx = x;
                    string vol = sel.Name + ", Partition " + (i + 1).ToString();
                    string layout = "Simple";
                    string type = p.Fs ?? "Unknown";
                    string status = p.Status == 0x80 ? "Healthy (Active)" : "Healthy";
                    ulong capB = p.LbaCount * (sel.BytesPerSector == 0 ? 512UL : sel.BytesPerSector);
                    string cap = FmtSize(capB);
                    string free = "N/A"; string pct = "N/A";
                    DrawCell(cx, rowY, cw[0], RowH, vol); cx += cw[0];
                    DrawCell(cx, rowY, cw[1], RowH, layout); cx += cw[1];
                    DrawCell(cx, rowY, cw[2], RowH, type); cx += cw[2];
                    DrawCell(cx, rowY, cw[3], RowH, status); cx += cw[3];
                    DrawCell(cx, rowY, cw[4], RowH, cap); cx += cw[4];
                    DrawCell(cx, rowY, cw[5], RowH, free); cx += cw[5];
                    DrawCell(cx, rowY, cw[6], RowH, pct);
                    rowY += RowH;
                }
            }
        }

        private void DrawPartitionMap(int x, int y, int w, int h) {
            var sel = GetSelected(); if (sel == null) return;
            WindowManager.font.DrawString(x, y, sel.Name);
            int barY = y + HeaderH; int barH = 36;
            Framebuffer.Graphics.FillRectangle(x, barY, w, barH, 0xFF1E1E1E);
            if (!sel.HaveInfo) { WindowManager.font.DrawString(x, barY + barH + 6, "Disk size unavailable"); return; }
            ulong total = sel.TotalSectors; if (total == 0) return;
            for (int i = 0; i < 4; i++) {
                var p = sel.Parts[i]; if (p.LbaCount == 0) continue;
                ulong start = p.LbaStart; ulong count = p.LbaCount; if (start > total) continue; if (start + count > total) count = total - start;
                int segX = x + (int)(start * (ulong)w / total); int segW = (int)(count * (ulong)w / total); if (segW <= 0) segW = 1;
                uint color = 0xFF4C8BF5; Framebuffer.Graphics.FillRectangle(segX, barY, segW, barH, color);
                string lbl = (p.Fs ?? "Unknown") + ", " + FmtSize(p.LbaCount * (sel.BytesPerSector == 0 ? 512UL : sel.BytesPerSector));
                // clip label within bar
                int maxW = segW - 8; if (maxW > 20) WindowManager.font.DrawString(segX + 4, barY + (barH / 2 - WindowManager.font.FontSize / 2), lbl, maxW, WindowManager.font.FontSize);
            }
            string cap = _cachedTotalCaption ?? $"Total: {FmtSize(sel.TotalSectors * (sel.BytesPerSector == 0 ? 512UL : sel.BytesPerSector))}";
            // ensure stays within right pane width
            WindowManager.font.DrawString(x, barY + barH + 6, cap, w, WindowManager.font.FontSize);
        }

        private void DrawActions(int x, int y, int w, int h) {
            WindowManager.font.DrawString(x, y, "Actions");
            int colGap = 16; int half = (w - colGap) / 2; if (half < 100) half = 100;
            int leftX = x; int rightX = x + half + colGap;
            int btnWLeft = half - 20; int btnWRight = half - 20; if (btnWLeft < 120) btnWLeft = 120; if (btnWRight < 120) btnWRight = 120;
            int byL = y + HeaderH; int byR = y + HeaderH;
            _bxDetectX = leftX; _bxDetectY = byL; DrawButton(_bxDetectX, _bxDetectY, btnWLeft, BtnH, "Detect media"); byL += BtnH + Gap;
            _bxAutoX = leftX; _bxAutoY = byL; DrawButton(_bxAutoX, _bxAutoY, btnWLeft, BtnH, "Set FS: Auto"); byL += BtnH + Gap;
            _bxSwitchFatX = leftX; _bxSwitchFatY = byL; DrawButton(_bxSwitchFatX, _bxSwitchFatY, btnWLeft, BtnH, "Set FS: FAT"); byL += BtnH + Gap;
            _bxSwitchTarX = leftX; _bxSwitchTarY = byL; DrawButton(_bxSwitchTarX, _bxSwitchTarY, btnWLeft, BtnH, "Set FS: TarFS"); byL += BtnH + Gap;
            _bxSwitchExtX = leftX; _bxSwitchExtY = byL; DrawButton(_bxSwitchExtX, _bxSwitchExtY, btnWLeft, BtnH, "Set FS: EXT2");

            _bxFormatExfatX = rightX; _bxFormatExfatY = byR; DrawButton(_bxFormatExfatX, _bxFormatExfatY, btnWRight, BtnH, "Format as FAT"); byR += BtnH + Gap;
            _bxCreatePartX = rightX; _bxCreatePartY = byR; DrawButton(_bxCreatePartX, _bxCreatePartY, btnWRight, BtnH, "Create partition (largest free)"); byR += BtnH + Gap;
            _bxRefreshX = rightX; _bxRefreshY = byR; DrawButton(_bxRefreshX, _bxRefreshY, btnWRight, BtnH, "Refresh");
        }

        private void DrawHeaderCell(int x, int y, int w, int h, string text) {
            Framebuffer.Graphics.FillRectangle(x, y, w, h, 0xFF252525);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF333333, 1);
            WindowManager.font.DrawString(x + 6, y + (h / 2 - WindowManager.font.FontSize / 2), text);
        }

        private void DrawCell(int x, int y, int w, int h, string text) {
            Framebuffer.Graphics.FillRectangle(x, y, w, h, 0xFF2A2A2A);
            WindowManager.font.DrawString(x + 6, y + (h / 2 - WindowManager.font.FontSize / 2), text, w - 10, WindowManager.font.FontSize);
        }

        private void DrawButton(int x, int y, int w, int h, string text) {
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y; bool hover = Hit(mx, my, x, y, w, h);
            uint bg = hover ? 0xFF3A3A3A : 0xFF323232;
            Framebuffer.Graphics.FillRectangle(x, y, w, h, bg);
            WindowManager.font.DrawString(x + 10, y + (h / 2 - WindowManager.font.FontSize / 2), text, w - 20, WindowManager.font.FontSize);
        }

        private void TrySetFS_Auto() {
            try { File.Instance = null; var fs = new AutoFS(); Desktop.InvalidateDirCache(); _status = BuildStatus(); } catch { _status = "Auto FS detect failed."; }
        }
        private void TrySetFS_FAT() { try { File.Instance = new FAT(); Desktop.InvalidateDirCache(); _status = BuildStatus(); } catch { _status = "Switch to FAT failed."; } }
        private void TrySetFS_TAR() { try { File.Instance = new TarFS(); Desktop.InvalidateDirCache(); _status = BuildStatus(); } catch { _status = "Switch to TAR failed."; } }
        private void TrySetFS_EXT2() { try { File.Instance = new EXT2(); Desktop.InvalidateDirCache(); _status = BuildStatus(); } catch { _status = "Switch to EXT2 failed."; } }
        private void TryFormatFAT() { try { var fs = new FAT(); fs.Format(); File.Instance = fs; Desktop.InvalidateDirCache(); _detected = "FAT (boot sector)"; _status = BuildStatus(); } catch { _status = "Format failed."; } }

        private void TryCreatePartitionLargestFree() {
            var sel = GetSelected(); if (sel == null || !sel.IsSystem) { _status = "Partitioning supported only on System disk"; return; }
            if (!sel.HaveInfo) { _status = "Disk info unavailable"; return; }
            var mbr = new byte[512];
            WithDisk(sel, () => { unsafe { fixed (byte* p = mbr) Disk.Instance.Read(0, 1, p); } });

            ulong total = sel.TotalSectors; ulong firstUsable = 2048; if (firstUsable >= total) { _status = "Disk too small"; return; }
            ulong[] usedStart = new ulong[4]; ulong[] usedEnd = new ulong[4]; int usedCount = 0;
            for (int i = 0; i < 4; i++) { var p = sel.Parts[i]; if (p.Type != 0 && p.LbaCount != 0) { ulong s = p.LbaStart; if (s < firstUsable) s = firstUsable; ulong e = p.LbaStart + p.LbaCount; if (e > total) e = total; if (s < e) { usedStart[usedCount] = s; usedEnd[usedCount] = e; usedCount++; } } }
            int freeSlot = -1; for (int i = 0; i < 4; i++) if (sel.Parts[i].Type == 0 || sel.Parts[i].LbaCount == 0) { freeSlot = i; break; }
            if (freeSlot < 0) { _status = "No free MBR slots"; return; }
            for (int a = 0; a < usedCount - 1; a++) for (int b = a + 1; b < usedCount; b++) if (usedStart[b] < usedStart[a]) { var ts = usedStart[a]; usedStart[a] = usedStart[b]; usedStart[b] = ts; var te = usedEnd[a]; usedEnd[a] = usedEnd[b]; usedEnd[b] = te; }
            ulong cursor = firstUsable; ulong bestS = firstUsable; ulong bestE = total; ulong bestLen = 0;
            for (int i = 0; i < usedCount; i++) { if (usedStart[i] > cursor) { ulong gapS = cursor; ulong gapE = usedStart[i]; ulong gapLen = gapE - gapS; if (gapLen > bestLen) { bestLen = gapLen; bestS = gapS; bestE = gapE; } } if (usedEnd[i] > cursor) cursor = usedEnd[i]; }
            if (cursor < total) { ulong gapS = cursor; ulong gapE = total; ulong gapLen = gapE - gapS; if (gapLen > bestLen) { bestLen = gapLen; bestS = gapS; bestE = gapE; } }
            if (bestLen < 2048) { _status = "No sufficient free space"; return; }

            int off = 446 + freeSlot * 16; mbr[off + 0] = 0x00; mbr[off + 1] = 0xFE; mbr[off + 2] = 0xFF; mbr[off + 3] = 0xFF; mbr[off + 4] = 0x07; mbr[off + 5] = 0xFE; mbr[off + 6] = 0xFF; mbr[off + 7] = 0xFF;
            uint start = (uint)bestS; uint count = (uint)(bestE - bestS);
            mbr[off + 8] = (byte)(start & 0xFF); mbr[off + 9] = (byte)(start >> 8 & 0xFF); mbr[off + 10] = (byte)(start >> 16 & 0xFF); mbr[off + 11] = (byte)(start >> 24 & 0xFF);
            mbr[off + 12] = (byte)(count & 0xFF); mbr[off + 13] = (byte)(count >> 8 & 0xFF); mbr[off + 14] = (byte)(count >> 16 & 0xFF); mbr[off + 15] = (byte)(count >> 24 & 0xFF);
            mbr[510] = 0x55; mbr[511] = 0xAA;
            try { WithDisk(sel, () => { unsafe { fixed (byte* p = mbr) Disk.Instance.Write(0, 1, p); } }); _status = "Partition created. Refresh to view."; RefreshDisks(); } catch { _status = "Failed to write MBR"; }
        }
    }
}