using guideXOS.Misc;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// The APIC is a split architecture design, with a local component (LAPIC) usually integrated into the processor itself, and an optional I/O APIC on a system bus
    /// </summary>
    public static unsafe partial class LocalAPIC {
        private const int LAPIC_ID = 0x0020;
        private const int LAPIC_VER = 0x0030;
        private const int LAPIC_TPR = 0x0080;
        private const int LAPIC_APR = 0x0090;
        private const int LAPIC_PPR = 0x00a0;
        private const int LAPIC_EOI = 0x00b0;
        private const int LAPIC_RRD = 0x00c0;
        private const int LAPIC_LDR = 0x00d0;
        private const int LAPIC_DFR = 0x00e0;
        private const int LAPIC_SVR = 0x00f0;
        private const int LAPIC_ISR = 0x0100;
        private const int LAPIC_TMR = 0x0180;
        private const int LAPIC_IRR = 0x0200;
        private const int LAPIC_ESR = 0x0280;
        private const int LAPIC_ICRLO = 0x0300;
        private const int LAPIC_ICRHI = 0x0310;
        private const int LAPIC_TIMER = 0x0320;
        private const int LAPIC_THERMAL = 0x0330;
        private const int LAPIC_PERF = 0x0340;
        private const int LAPIC_LINT0 = 0x0350;
        private const int LAPIC_LINT1 = 0x0360;
        private const int LAPIC_ERROR = 0x0370;
        private const int LAPIC_TICR = 0x0380;
        private const int LAPIC_TCCR = 0x0390;
        private const int LAPIC_TDCR = 0x03e0;
        private const int ICR_FIXED = 0x00000000;
        private const int ICR_LOWEST = 0x00000100;
        private const int ICR_SMI = 0x00000200;
        private const int ICR_NMI = 0x00000400;
        private const int ICR_INIT = 0x00000500;
        private const int ICR_STARTUP = 0x00000600;
        private const int ICR_PHYSICAL = 0x00000000;
        private const int ICR_LOGICAL = 0x00000800;
        private const int ICR_IDLE = 0x00000000;
        private const int ICR_SEND_PENDING = 0x00001000;
        private const int ICR_DEASSERT = 0x00000000;
        private const int ICR_ASSERT = 0x00004000;
        private const int ICR_EDGE = 0x00000000;
        private const int ICR_LEVEL = 0x00008000;
        private const int ICR_NO_SHORTHAND = 0x00000000;
        private const int ICR_SELF = 0x00040000;
        private const int ICR_ALL_INCLUDING_SELF = 0x00080000;
        private const int ICR_ALL_EXCLUDING_SELF = 0x000c0000;
        private const int ICR_DESTINATION_SHIFT = 24;
        private const uint IA32_APIC_BASE_MSR = 0x1B;
        private const ulong IA32_APIC_BASE_MASK = 0xFFFFF000UL;

        private static ulong ApicBase {
            get {
                // Prefer ACPI MADT address when present. This avoids rdmsr (#GP) issues
                // in bring-up environments where MSR access may not be permitted yet.
                if (ACPI.MADT != null && ACPI.MADT->LocalAPICAddress != 0)
                    return ACPI.MADT->LocalAPICAddress;

                // Fallback: IA32_APIC_BASE MSR
                ulong msr = Native.Rdmsr(IA32_APIC_BASE_MSR);
                ulong baseAddr = msr & IA32_APIC_BASE_MASK;
                return baseAddr;
            }
        }

        private static void SerialHex64(ulong v) {
            for (int shift = 60; shift >= 0; shift -= 4) {
                int nibble = (int)((v >> shift) & 0xF);
                byte c = (byte)(nibble < 10 ? ('0' + nibble) : ('A' + (nibble - 10)));
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, c);
            }
        }

        /// <summary>
        /// Read Registers
        /// </summary>
        public static uint ReadRegister(uint reg) {
            ulong baseAddr = ApicBase;
            if (baseAddr == 0) return 0;
            return MMIO.In32((uint*)(baseAddr + reg));
        }

        /// <summary>
        /// Write Registers
        /// </summary>
        public static void WriteRegister(uint reg, uint data) {
            ulong baseAddr = ApicBase;
            if (baseAddr == 0) return;
            MMIO.Out32((uint*)(baseAddr + reg), data);
        }

        /// <summary>
        /// End Of Interrupt
        /// </summary>
        public static void EndOfInterrupt() {
            WriteRegister((uint)LAPIC_EOI, 0);
        }

        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            ulong baseAddr = ApicBase;

            // Serial log APIC base early (BootConsole may be fragile around interrupt init)
            BootConsole.Write("APIC@");
            SerialHex64(baseAddr);
            BootConsole.NewLine();

            if (baseAddr == 0) {
                BootConsole.WriteLine("[Local APIC] ERROR: APIC base not found");
                return;
            }

            // Configure Spurious Interrupt Vector Register
            // Bit 8 enables the APIC. Low 8 bits = spurious vector.
            WriteRegister((uint)LAPIC_SVR, 0x100 | 0xFF);
            BootConsole.WriteLine("[Local APIC] Initialized");
        }
        /// <summary>
        /// GetID
        /// </summary>
        /// <returns></returns>
        public static uint GetId() {
            return ReadRegister((uint)LAPIC_ID) >> 24;
        }
        /// <summary>
        /// Send Init
        /// </summary>
        /// <param name="apic_id"></param>
        public static void SendInit(uint apic_id) {
            SendIPI(apic_id, (uint)(ICR_INIT | ICR_PHYSICAL
                | ICR_ASSERT | ICR_EDGE | ICR_NO_SHORTHAND));
        }
        /// <summary>
        /// Send All Interrupt
        /// </summary>
        /// <param name="vector"></param>
        public static void SendAllInterrupt(uint vector) {
            SendInterrupt(0, vector | ICR_ALL_EXCLUDING_SELF);
        }
        /// <summary>
        /// Send All Interrupt Including Self
        /// </summary>
        /// <param name="vector"></param>
        public static void SendAllInterruptIncludingSelf(uint vector) {
            SendInterrupt(0, vector | ICR_ALL_INCLUDING_SELF);
        }
        /// <summary>
        /// Send Interrupt
        /// </summary>
        /// <param name="apic_id"></param>
        /// <param name="vector"></param>
        public static void SendInterrupt(uint apic_id, uint vector) {
            SendIPI(apic_id, vector);
        }
        /// <summary>
        /// Send IPI
        /// </summary>
        /// <param name="apic_id"></param>
        /// <param name="vector"></param>
        public static void SendIPI(uint apic_id, uint vector) {
            WriteRegister((uint)LAPIC_ICRHI, apic_id << ICR_DESTINATION_SHIFT);
            WriteRegister((uint)LAPIC_ICRLO, vector);

            while ((ReadRegister((uint)LAPIC_ICRLO) & ICR_SEND_PENDING) != 0) ;
        }
        /// <summary>
        /// Send Startup
        /// </summary>
        /// <param name="apic_id"></param>
        /// <param name="vector"></param>
        public static void SendStartup(uint apic_id, uint vector) {
            SendIPI(apic_id, vector | (uint)ICR_STARTUP
                | (uint)ICR_PHYSICAL | (uint)ICR_ASSERT | (uint)ICR_EDGE | (uint)ICR_NO_SHORTHAND);
        }
    }
}