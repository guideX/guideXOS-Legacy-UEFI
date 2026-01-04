namespace guideXOS.Kernel.Drivers {
/// <summary>
/// 8259 Programmable Interrupt Controller (PIC) Driver
/// 
/// ============================================================================
/// UEFI COMPATIBILITY AUDIT
/// ============================================================================
/// 
/// CLASSIFICATION: BIOS-ERA ONLY - Potentially UEFI-incompatible
/// 
/// The 8259 PIC is a legacy interrupt controller from the IBM PC era.
/// UEFI systems may:
/// - Have PIC disabled in favor of APIC/xAPIC/x2APIC
/// - Have PIC in an unknown state after ExitBootServices
/// - Not respond to PIC programming at all
/// 
/// PORTS USED (BIOS-ERA):
/// - 0x20/0x21: Master PIC (IRQ 0-7)
/// - 0xA0/0xA1: Slave PIC (IRQ 8-15)
/// 
/// ENTRY POINTS:
/// - EntryPoint.KMain() -> PIC.Enable() (when UseAPIC not defined)
/// - PS2Mouse.Initialize() -> Interrupts.EnableInterrupt() -> PIC.ClearMask()
/// - PS2Keyboard.Initialize() -> Interrupts.EnableInterrupt() -> PIC.ClearMask()
/// 
/// UEFI CONSIDERATIONS:
/// - Modern UEFI systems prefer APIC (see LocalAPIC.cs, IOAPIC.cs)
/// - PIC.Enable() does work on most systems (VMs always have PIC)
/// - For real UEFI hardware: Check ACPI MADT for PIC presence
/// 
/// RECOMMENDED UEFI APPROACH:
/// - Use APIC instead (#define UseAPIC)
/// - Or poll-based input (no interrupts needed)
/// 
/// ============================================================================
/// </summary>
public static class PIC {
        /// <summary>
        /// Initialize and enable the 8259 PIC
        /// 
        /// BIOS-ERA: Programs master/slave PIC to remap IRQs to vectors 0x20-0x2F.
        /// UEFI: May still work but APIC is preferred on modern systems.
        /// 
        /// Standard PC PIC remapping:
        /// - IRQ0-7 -> vectors 0x20-0x27 (master)
        /// - IRQ8-15 -> vectors 0x28-0x2F (slave)
        /// </summary>
        public static void Enable() {
            //Initialize PIC
            Native.Out8(0x20, 0x11);
            Native.Out8(0xA0, 0x11);
            Native.Out8(0x21, 0x20);
            Native.Out8(0xA1, 40);
            Native.Out8(0x21, 0x04);
            Native.Out8(0xA1, 0x02);
            Native.Out8(0x21, 0x01);
            Native.Out8(0xA1, 0x01);

            Native.Out8(0x21, 0x0);
            Native.Out8(0xA1, 0x0);
        }

        public static void Disable() {
            //Initialize PIC
            Native.Out8(0x20, 0x11);
            Native.Out8(0xA0, 0x11);
            Native.Out8(0x21, 0x20);
            Native.Out8(0xA1, 40);
            Native.Out8(0x21, 0x04);
            Native.Out8(0xA1, 0x02);
            Native.Out8(0x21, 0x01);
            Native.Out8(0xA1, 0x01);

            Native.Out8(0x21, 0xFF);
            Native.Out8(0xA1, 0xFF);
        }

        public static void EndOfInterrupt(int irq) {
            if (irq >= 40)
                Native.Out8(0xA0, 0x20);

            Native.Out8(0x20, 0x20);
        }

        /// <summary>
        /// Unmask (enable) a specific IRQ
        /// 
        /// BIOS-ERA: Used by PS2Mouse (IRQ12) and PS2Keyboard (IRQ1)
        /// UEFI: Only works if PIC is being used instead of APIC
        /// </summary>
        public static void ClearMask(byte irq) {
            ushort port;
            byte value;

            if (irq < 8) {
                port = 0x21;
            } else {
                port = 0xA1;
                irq -= 8;
            }
            value = (byte)(Native.In8(port) & ~(1 << irq));
            Native.Out8(port, value);
        }
    }
}