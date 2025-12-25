using guideXOS.FS;
using System.Collections.Generic;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// IDE is a widely used interface standard in computing that allows for the connection and communication between a computer's motherboard and its storage devices. It is commonly used for connecting hard disk drives (HDDs) and optical disc drives (ODDs) to the computer system.
    /// </summary>
    public unsafe static class IDE {
        /// <summary>
        /// Ports
        /// </summary>
        public static List<IDEDevice> Ports;
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            // Debug: entering IDE.Initialize
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'i');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'d');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'e');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'1');
            
            Ports = new();
            
            // Debug: after list creation
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'i');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'d');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'e');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'2');
            
            ScanPorts(Channels.Primary);
            
            // Debug: after primary scan
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'i');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'d');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'e');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'3');
            
            ScanPorts(Channels.Secondary);
            
            // Debug: after secondary scan
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'i');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'d');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'e');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'4');
            
            //if (Ports.Count == 0)
            Console.WriteLine("[IDE] IDE controller initialized");
        }
        /// <summary>
        /// Channels
        /// </summary>
        public enum Channels {
            Primary,
            Secondary
        }
        /// <summary>
        /// Scan Ports
        /// </summary>
        /// <param name="index"></param>
        public static void ScanPorts(Channels index) {
            // Debug: entering ScanPorts
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'S');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'C');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'A');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'N');
            
            ushort LBALowPort;
            ushort LBAMidPort;
            ushort LBAHighPort;
            ushort DeviceHeadPort;
            ushort StatusPort;
            ushort CommandPort;
            ushort AltStatusPort;
            ushort DataPort;
            ushort FeaturePort;
            ushort ErrorPort;
            ushort SectorCountPort;
            ushort BasePort = 0, ControlPort = 0;
            
            bool Available() {
                Native.Out8(LBALowPort, 0x88);
                return Native.In8(LBALowPort) == 0x88;
            }
            
            bool WaitForReadyStatus() {
                byte status;
                int timeout = 100000; // Timeout counter
                do {
                    status = Native.In8(StatusPort);
                    timeout--;
                    if (timeout == 0) {
                        // Debug: timeout in WaitForReadyStatus
                        while ((Native.In8(0x3FD) & 0x20) == 0) { }
                        Native.Out8(0x3F8, (byte)'T');
                        while ((Native.In8(0x3FD) & 0x20) == 0) { }
                        Native.Out8(0x3F8, (byte)'O');
                        return false;
                    }
                }
                while ((status & IDEDevice.Busy) == IDEDevice.Busy);

                return true;
            }
            
            bool WaitForIdentifyData() {
                byte status;
                int timeout = 100000; // Timeout counter
                do {
                    status = Native.In8(StatusPort);
                    timeout--;
                    if (timeout == 0) {
                        // Debug: timeout in WaitForIdentifyData
                        while ((Native.In8(0x3FD) & 0x20) == 0) { }
                        Native.Out8(0x3F8, (byte)'T');
                        while ((Native.In8(0x3FD) & 0x20) == 0) { }
                        Native.Out8(0x3F8, (byte)'I');
                        return false;
                    }
                }
                while ((status & IDEDevice.DataRequest) != IDEDevice.DataRequest && (status & IDEDevice.Error) != IDEDevice.Error);
                return ((status & IDEDevice.Error) != IDEDevice.Error);
            }

            // Debug: before switch
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'1');

            switch (index) {
                case Channels.Primary:
                    BasePort = 0x1F0;
                    ControlPort = 0x3F6;
                    break;
                case Channels.Secondary:
                    BasePort = 0x170;
                    ControlPort = 0x376;
                    break;
            }

            // Debug: after switch, before port setup
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'2');

            DataPort = (ushort)(BasePort + 0);
            ErrorPort = (ushort)(BasePort + 1);
            FeaturePort = (ushort)(BasePort + 1);
            SectorCountPort = (ushort)(BasePort + 2);
            LBALowPort = (ushort)(BasePort + 3);
            LBAMidPort = (ushort)(BasePort + 4);
            LBAHighPort = (ushort)(BasePort + 5);
            DeviceHeadPort = (ushort)(BasePort + 6);
            CommandPort = (ushort)(BasePort + 7);
            StatusPort = (ushort)(BasePort + 7);
            AltStatusPort = (ushort)(ControlPort + 6);

            // Debug: before Available() check
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'3');

            if (!Available()) {
                // Debug: not available
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'N');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'A');
                return;
            }

            // Debug: available, continuing
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'4');

            Native.Out8(ControlPort, 0);

            // Debug: before device scan loop
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'5');

            for (byte port = 0; port < 2; port++) {
                // Debug: scanning port
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'P');
                
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)((byte)'0' + port)); // Print port number
                
                Native.Out8(DeviceHeadPort, (byte)((port == 0) ? 0xA0 : 0xB0));
                
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'a');
                
                Native.Out8(SectorCountPort, 0);
                Native.Out8(LBALowPort, 0);
                Native.Out8(LBAMidPort, 0);
                Native.Out8(LBAHighPort, 0);
                
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'b');
                
                Native.Out8(CommandPort, IDEDevice.IdentifyDrive);
                
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'c');
                
                // Add a small delay after sending command
                for (int d = 0; d < 1000; d++) {
                    Native.In8(StatusPort); // Poll status as a delay
                }
                
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'d');

                if (
                    Native.In8(StatusPort) == 0 ||
                    !WaitForReadyStatus() ||
                    Native.In8(LBAMidPort) != 0 && Native.In8(LBAHighPort) != 0 || //ATAPI
                    !WaitForIdentifyData()
                    ) {
                    // Debug: device not found/not compatible
                    while ((Native.In8(0x3FD) & 0x20) == 0) { }
                    Native.Out8(0x3F8, (byte)'X');
                    continue;
                }

                // Debug: device found!
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'F');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'O');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'U');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'N');
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'D');

                ulong Size = 0;
                var DriveInfo = new byte[4096];
                fixed (byte* p = DriveInfo) {
                    Native.Insw(DataPort, (ushort*)p, (ulong)(DriveInfo.Length / 2));

                    Size = (*(uint*)(p + IDEDevice.MaxLBA28)) * IDEDevice.SectorSize;

                    // Skip verbose Console.WriteLine - just note device found
                }
                DriveInfo.Dispose();

                var drv = new IDEDevice();

                drv.LBALowPort = LBALowPort;
                drv.LBAMidPort = LBAMidPort;
                drv.LBAHighPort = LBAHighPort;
                drv.DeviceHeadPort = DeviceHeadPort;
                drv.StatusPort = StatusPort;
                drv.CommandPort = CommandPort;


                drv.DataPort = DataPort;
                drv.SectorCountPort = SectorCountPort;

                drv.Drive = port;

                drv.Size = Size;

                Ports.Add(drv);
            }
            
            // Debug: scan complete
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'D');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'O');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'N');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'E');
        }
    }

    public unsafe class IDEDevice : Disk {
        public ushort LBALowPort;
        public ushort LBAMidPort;
        public ushort LBAHighPort;
        public ushort DeviceHeadPort;
        public ushort StatusPort;
        public ushort CommandPort;

        public const byte ReadSectorsWithRetry = 0x20;
        public const byte WriteSectorsWithRetry = 0x30;
        public const byte IdentifyDrive = 0xEC;
        public const byte CacheFlush = 0xE7;

        public const byte Busy = 1 << 7;
        public const byte DataRequest = 1 << 3;
        public const byte Error = 1 << 0;

        public const uint ModelNumber = 0x1B * 2;
        public const uint MaxLBA28 = 60 * 2;

        public const uint DrivesPerConroller = 2;

        public ushort DataPort;
        public ushort SectorCountPort;

        public const uint SectorSize = 512;

        public uint Drive;

        public ulong Size;

        public bool ReadOrWrite(uint sector, byte* data, bool write) {
            Native.Out8(DeviceHeadPort, (byte)(0xE0 | (Drive << 4) | ((sector >> 24) & 0x0F)));
            //Native.Out8(FeaturePort, 0);
            Native.Out8(SectorCountPort, 1);
            Native.Out8(LBAHighPort, (byte)((sector >> 16) & 0xFF));
            Native.Out8(LBAMidPort, (byte)((sector >> 8) & 0xFF));
            Native.Out8(LBALowPort, (byte)(sector & 0xFF));

            Native.Out8(CommandPort, (write) ? WriteSectorsWithRetry : ReadSectorsWithRetry);

            if (!WaitForReadyStatus())
                return false;

            while ((Native.In8(StatusPort) & 0x80) != 0) ;

            if (write) {
                Native.Outsw(DataPort, (ushort*)data, SectorSize / 2);

                Native.Out8(CommandPort, CacheFlush);

                WaitForReadyStatus();
            } else {
                Native.Insw(DataPort, (ushort*)data, SectorSize / 2);
            }

            if ((Native.In8(StatusPort) & 0x1) != 0) {
                Console.WriteLine($"IDE bad status");
                return false;
            }

            return true;
        }

        private bool WaitForReadyStatus() {
            byte status;
            do {
                status = Native.In8(StatusPort);
            }
            while ((status & Busy) == Busy);

            return true;
        }


        public override bool Read(ulong sector, uint count, byte* p) {
            for (ulong i = 0; i < count; i++) {
                bool b = ReadOrWrite((uint)(sector + i), p + (i * SectorSize), false);
                if (!b) return false;
            }
            return true;
        }

        public override bool Write(ulong sector, uint count, byte* p) {
            for (ulong i = 0; i < count; i++) {
                bool b = ReadOrWrite((uint)(sector + i), p + (i * SectorSize), true);
                if (!b) return false;
            }
            return true;
        }
    }
}