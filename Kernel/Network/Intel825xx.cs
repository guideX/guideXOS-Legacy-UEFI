//Reference: https://www.intel.com/content/dam/doc/manual/pci-pci-x-family-gbe-controllers-software-dev-manual.pdf
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Runtime.InteropServices;
using static guideXOS.Misc.MMIO;
using static guideXOS.NETv4;
namespace guideXOS {
    internal unsafe class Intel825xx {
        public static uint IOBase;

        public static RXDescriptor* RXDescs;
        public static TXDescriptor* TXDescs;

        public static uint RXCurrentIndex = 0;
        public static uint TXCurrentIndex = 0;

        const int NumRX = 32;
        const int NumTX = 32;

        public static void Initialize() {
            PCIDevice device = null;

            BootConsole.Write("[Intel825xx] Scanning for Intel NIC...\n");
            BootConsole.Write("[Intel825xx] Total PCI devices: ");
            BootConsole.Write(PCI.Devices.Count.ToString());
            BootConsole.Write("\n");

            for (int i = 0; i < PCI.Devices.Count; i++) {
                if (PCI.Devices[i] != null) {
                    ushort vendorID = PCI.Devices[i].VendorID;
                    ushort deviceID = PCI.Devices[i].DeviceID;
                    
                    // Debug: Print all Intel devices
                    if (vendorID == 0x8086) {
                        BootConsole.Write("[Intel825xx] Found Intel device: Vendor=0x");
                        BootConsole.Write(vendorID.ToString("X4"));
                        BootConsole.Write(" Device=0x");
                        BootConsole.Write(deviceID.ToString("X4"));
                        BootConsole.Write("\n");
                    }
                    
                    if (
                     vendorID == 0x8086 &&
                     (
                        deviceID == 0x1000 || //Intel82542
                        deviceID == 0x1001 || //Intel82543GC
                        deviceID == 0x1004 || //Intel82543GC
                        deviceID == 0x1008 || //Intel82544EI
                        deviceID == 0x1009 || //Intel82544EI
                        deviceID == 0x100C || //Intel82543EI
                        deviceID == 0x100D || //Intel82544GC
                        deviceID == 0x100E || //Intel82540EM
                        deviceID == 0x100F || //Intel82545EM
                        deviceID == 0x1010 || //Intel82546EB
                        deviceID == 0x1011 || //Intel82545EM
                        deviceID == 0x1012 || //Intel82546EB
                        deviceID == 0x1013 || //Intel82541EI
                        deviceID == 0x1014 || //Intel82541ER
                        deviceID == 0x1015 || //Intel82540EM
                        deviceID == 0x1016 || //Intel82540EP
                        deviceID == 0x1017 || //Intel82540EP
                        deviceID == 0x1018 || //Intel82541EI
                        deviceID == 0x1019 || //Intel82547EI
                        deviceID == 0x101A || //Intel82547EI
                        deviceID == 0x101D || //Intel82546EB
                        deviceID == 0x101E || //Intel82540EP
                        deviceID == 0x1026 || //Intel82545GM
                        deviceID == 0x1027 || //Intel82545GM
                        deviceID == 0x1028 || //Intel82545GM
                        deviceID == 0x1049 || //Intel82566MM_ICH8
                        deviceID == 0x104A || //Intel82566DM_ICH8
                        deviceID == 0x104B || //Intel82566DC_ICH8
                        deviceID == 0x104C || //Intel82562V_ICH8
                        deviceID == 0x104D || //Intel82566MC_ICH8
                        deviceID == 0x105E || //Intel82571EB
                        deviceID == 0x105F || //Intel82571EB
                        deviceID == 0x1060 || //Intel82571EB
                        deviceID == 0x1075 || //Intel82547EI
                        deviceID == 0x1076 || //Intel82541GI
                        deviceID == 0x1077 || //Intel82547EI
                        deviceID == 0x1078 || //Intel82541ER
                        deviceID == 0x1079 || //Intel82546EB
                        deviceID == 0x107A || //Intel82546EB
                        deviceID == 0x107B || //Intel82546EB
                        deviceID == 0x107C || //Intel82541PI
                        deviceID == 0x107D || //Intel82572EI
                        deviceID == 0x107E || //Intel82572EI
                        deviceID == 0x107F || //Intel82572EI
                        deviceID == 0x108A || //Intel82546GB
                        deviceID == 0x108B || //Intel82573E
                        deviceID == 0x108C || //Intel82573E
                        deviceID == 0x1096 || //Intel80003ES2LAN
                        deviceID == 0x1098 || //Intel80003ES2LAN
                        deviceID == 0x1099 || //Intel82546GB
                        deviceID == 0x109A || //Intel82573L
                        deviceID == 0x10A4 || //Intel82571EB
                        deviceID == 0x10A7 || //Intel82575
                        deviceID == 0x10A9 || //Intel82575_serdes
                        deviceID == 0x10B5 || //Intel82546GB
                        deviceID == 0x10B9 || //Intel82572EI
                        deviceID == 0x10BA || //Intel80003ES2LAN
                        deviceID == 0x10BB || //Intel80003ES2LAN
                        deviceID == 0x10BC || //Intel82571EB
                        deviceID == 0x10BD || //Intel82566DM_ICH9
                        deviceID == 0x10C4 || //Intel82562GT_ICH8
                        deviceID == 0x10C5 || //Intel82562G_ICH8
                        deviceID == 0x10C9 || //Intel82576
                        deviceID == 0x10D3 || //Intel82574L
                        deviceID == 0x10A9 || //Intel82575_quadcopper
                        deviceID == 0x10CB || //Intel82567V_ICH9
                        deviceID == 0x10E5 || //Intel82567LM_4_ICH9
                        deviceID == 0x10EA || //Intel82577LM
                        deviceID == 0x10EB || //Intel82577LC
                        deviceID == 0x10EF || //Intel82578DM
                        deviceID == 0x10F0 || //Intel82578DC
                        deviceID == 0x10F5 || //Intel82567LM_ICH9_egDellE6400Notebook
                        deviceID == 0x1502 || //Intel82579LM
                        deviceID == 0x1503 || //Intel82579V
                        deviceID == 0x150A || //Intel82576NS
                        deviceID == 0x150E || //Intel82580
                        deviceID == 0x1521 || //IntelI350
                        deviceID == 0x1533 || //IntelI210
                        deviceID == 0x157B || //IntelI210
                        deviceID == 0x153A || //IntelI217LM
                        deviceID == 0x153B || //IntelI217VA
                        deviceID == 0x1559 || //IntelI218V
                        deviceID == 0x155A || //IntelI218LM
                        deviceID == 0x15A0 || //IntelI218LM2
                        deviceID == 0x15A1 || //IntelI218V
                        deviceID == 0x15A2 || //IntelI218LM3
                        deviceID == 0x15A3 || //IntelI218V3
                        deviceID == 0x156F || //IntelI219LM
                        deviceID == 0x1570 || //IntelI219V
                        deviceID == 0x15B7 || //IntelI219LM2
                        deviceID == 0x15B8 || //IntelI219V2
                        deviceID == 0x15BB || //IntelI219LM3
                        deviceID == 0x15D7 || //IntelI219LM
                        deviceID == 0x15E3    //IntelI219LM
                        )
                    ) {
                        device = PCI.Devices[i];
                        BootConsole.Write("[Intel825xx] Matched device: 0x");
                        BootConsole.Write(deviceID.ToString("X4"));
                        BootConsole.Write("\n");
                        break;
                    }
                }
            }

            if (device == null) {
                BootConsole.Write("[Intel825xx] No Intel NIC found\n");
                return;
            }

            RXCurrentIndex = 0;
            TXCurrentIndex = 0;

            device.WriteRegister(0x04, 0x04 | 0x02 | 0x01);

            IOBase = (uint)(device.Bar0 & (~0xF));

            Reset();

            WriteRegister(0x14, 0x1);
            bool HasEEPROM = false;
            for (int j = 0; j < 1024; j++) {
                if ((ReadRegister(0x14) & 0x10) != 0) {
                    HasEEPROM = true;
                    break;
                }
            }

            if (!HasEEPROM) {
                NETv4.MAC = new MACAddress(
                    In8((byte*)(IOBase + 0x5400)),
                    In8((byte*)(IOBase + 0x5401)),
                    In8((byte*)(IOBase + 0x5402)),
                    In8((byte*)(IOBase + 0x5403)),
                    In8((byte*)(IOBase + 0x5404)),
                    In8((byte*)(IOBase + 0x5405))
                    );
                BootConsole.Write("[Intel825xx] This controller has no EEPROM\n");
            } else {
                NETv4.MAC = new MACAddress(
                    (byte)(ReadROM(0) & 0xFF),
                    (byte)(ReadROM(0) >> 8),
                    (byte)(ReadROM(1) & 0xFF),
                    (byte)(ReadROM(1) >> 8),
                    (byte)(ReadROM(2) & 0xFF),
                    (byte)(ReadROM(2) >> 8)
                );
                BootConsole.Write("[Intel825xx] EEPROM on this controller\n");
            }

            Linkup();
            for (int k = 0; k < 0x80; k++)
                WriteRegister((ushort)(0x5200 + k * 4), 0);

            RXsetup();
            TXsetup();

            WriteRegister(0x00D0, 0x1F6DC);
            ReadRegister(0xC0);

            BootConsole.Write("[Intel825xx] Configuration Done \n");

            //Bug. IRQ of device doesn't work
            Interrupts.EnableInterrupt(0x20, &OnInterrupt);

            NETv4.Sender = &Send;
        }

        public static void Reset() {
            BootConsole.Write("[Intel825xx] Reseting controller...\n");

            WriteRegister(0, 1 << 26);
            while (BitHelpers.IsBitSet(ReadRegister(0), 26)) ;
        }

        private static void TXsetup() {
            TXDescs = Allocator.ClearAllocate<TXDescriptor>(NumTX);

            WriteRegister(0x3800, (uint)(ulong)TXDescs);
            WriteRegister(0x3804, (uint)(((ulong)TXDescs) >> 32));
            WriteRegister(0x3808, (uint)(NumTX * sizeof(TXDescriptor)));
            WriteRegister(0x3810, 0);
            WriteRegister(0x3818, 0);

            WriteRegister(0x0400, (1 << 1) | (1 << 3));
        }

        private static void RXsetup() {
            RXDescs = Allocator.ClearAllocate<RXDescriptor>(NumRX);

            for (uint i = 0; i < NumRX; i++) {
                RXDescriptor* desc = &RXDescs[i];
                desc->addr = (ulong)(void*)Allocator.Allocate(2048 + 16);
            }

            WriteRegister(0x2800, (uint)(ulong)RXDescs);
            WriteRegister(0x2804, (uint)(((ulong)RXDescs) >> 32));

            WriteRegister(0x2808, (uint)(NumRX * sizeof(RXDescriptor)));
            WriteRegister(0x2810, 0);
            WriteRegister(0x2818, NumRX - 1);

            WriteRegister(0x0100,
                     (1 << 1) |
                     (1 << 2) |
                     (1 << 3) |
                     (1 << 4) |
                     (0 << 6) |
                     (0 << 8) |
                    (1 << 15) |
                    (1 << 26) |
                    (0 << 16)
                );
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct RXDescriptor {
            public ulong addr;
            public ushort length;
            public ushort checksum;
            public byte status;
            public byte errors;
            public ushort special;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TXDescriptor {
            public ulong addr;
            public ushort length;
            public byte cso;
            public byte cmd;
            public byte status;
            public byte css;
            public ushort special;
        }

        public static void WriteRegister(ushort Reg, uint Val) {
            Out32((uint*)(IOBase + Reg), Val);
        }

        public static uint ReadRegister(ushort Reg) {
            return In32((uint*)(IOBase + Reg));
        }

        public static ushort ReadROM(uint Addr) {
            uint Temp;
            WriteRegister(0x14, 1 | (Addr << 8));
            while (((Temp = ReadRegister(0x14)) & 0x10) == 0) ;
            return ((ushort)((Temp >> 16) & 0xFFFF));
        }

        public static void OnInterrupt() {
            uint Status = ReadRegister(0xC0);

            if ((Status & 0x04) != 0) {
                Linkup();
            }

            if ((Status & 0x80) != 0) {
                uint _RXCurr = RXCurrentIndex;
                RXDescriptor* desc = &RXDescs[RXCurrentIndex];
                while ((desc->status & 0x1) != 0) {
                    // Use the new overload that accepts packet size
                    int packetSize = desc->length;
                    if (packetSize > 0 && packetSize <= 2048) {
                        NETv4.OnData((byte*)desc->addr, packetSize);
                    }
                    desc->status = 0;
                    RXCurrentIndex = (RXCurrentIndex + 1) % NumRX;
                    WriteRegister(0x2818, _RXCurr);
                }
            }
        }

        private static void Linkup() {
            WriteRegister(0, ReadRegister(0) | 0x40);

            BootConsole.Write("[Intel825xx] Waiting for network connection \n");
            while (!BitHelpers.IsBitSet(*(uint*)(IOBase + 0x08), 1)) ;
        }

        public static void Send(byte* Buffer, int Length) {
            TXDescriptor* desc = &TXDescs[TXCurrentIndex];
            desc->addr = (ulong)Buffer;
            desc->length = (ushort)Length;
            desc->cmd = (1 << 0) | (1 << 1) | (1 << 3);
            desc->status = 0;

            TXCurrentIndex = (TXCurrentIndex + 1) % NumTX;
            WriteRegister(0x3818, TXCurrentIndex);
            for (; ; )
            {
                if (desc->status != 0) break;
                ACPITimer.Sleep(1);
            }
        }
    }
}