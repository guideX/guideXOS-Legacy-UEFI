using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using System.Collections.Generic;
using System.Windows.Forms;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// Represents a drive/volume that can be displayed in ComputerFiles.
    /// </summary>
    public class DriveInfo {
        public string Name { get; set; }
        public string RootPath { get; set; }
        public DriveType Type { get; set; }
        public ulong TotalSize { get; set; }
        public bool IsReady { get; set; }
        public object Tag { get; set; } // For storing USBDevice or other identifiers
        public FileSystem FileSystem { get; set; }

        public enum DriveType {
            HardDisk,
            USB
        }

        public new void Dispose() {
            Name?.Dispose();
            RootPath?.Dispose();
            FileSystem?.Dispose();
        }
    }
}
