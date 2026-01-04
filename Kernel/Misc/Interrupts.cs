using guideXOS.Kernel.Drivers;
using System.Collections.Generic;

namespace guideXOS.Misc {
    /// <summary>
    /// Interrupt Management - Central interrupt registration and dispatch
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION: PARTIALLY UEFI-COMPATIBLE
    /// 
    /// This class provides interrupt abstraction that works with either:
    /// - Legacy PIC (#else branch)
    /// - APIC (#if UseAPIC branch)
    /// 
    /// The abstraction itself is UEFI-compatible, but:
    /// - Legacy PIC mode may not work on all UEFI systems
    /// - APIC mode is recommended for UEFI
    /// - Interrupt-driven input may not be necessary (polling works)
    /// 
    /// ENTRY POINTS:
    /// - PS2Keyboard.Initialize() -> EnableInterrupt(0x21, handler)
    /// - PS2Mouse.Initialize() -> EnableInterrupt(0x2C, handler)
    /// - Timer.Initialize() -> EnableInterrupt(0x20, handler)
    /// 
    /// UEFI CONSIDERATIONS:
    /// - Interrupts are disabled after ExitBootServices
    /// - OS must set up own IDT and enable interrupts
    /// - Currently done in EntryPoint.KMain() via IDT.Initialize()
    /// - APIC mode (#define UseAPIC) is more UEFI-friendly
    /// 
    /// FOR UEFI INPUT WITHOUT INTERRUPTS:
    /// - Poll USB HID in main loop
    /// - No need to register interrupt handlers
    /// - Simplifies boot process
    /// 
    /// ============================================================================
    /// </summary>
    public static class Interrupts {
        public unsafe class INT {
            public int IRQ;
            public delegate*<void> Handler;
        }

        public static List<INT> INTs;

        public static void Initialize() {
            INTs = new List<INT>();
        }

        public static void EndOfInterrupt(byte irq) {
#if UseAPIC
            LocalAPIC.EndOfInterrupt();
#else
            PIC.EndOfInterrupt(irq);
#endif
        }

        public static void EnableInterrupt(byte irq) {
#if UseAPIC
            // In APIC mode, callers historically pass 0x20 for the timer.
            // IOAPIC.SetEntry contains a back-compat shim for that.
            IOAPIC.SetEntry(irq);
#else
            PIC.ClearMask(irq);
#endif
        }

        /// <summary>
        /// Enable an interrupt and register handler
        /// 
        /// BIOS-ERA: Uses PIC.ClearMask() or IOAPIC.SetEntry()
        /// UEFI: Works if using APIC mode or if PIC is accessible
        /// 
        /// For UEFI without interrupts: Skip this entirely and poll.
        /// </summary>
        public static unsafe void EnableInterrupt(byte irq, delegate*<void> handler) {
#if UseAPIC
            IOAPIC.SetEntry(irq);
#else
            PIC.ClearMask(irq);
#endif
            INTs.Add(new INT() { IRQ = irq, Handler = handler });
        }

        public static unsafe void HandleInterrupt(int irq) {
            for (int i = 0; i < INTs.Count; i++) {
                if (INTs[i].IRQ == irq) INTs[i].Handler();
            }
        }
    }
}