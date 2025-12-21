namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// The ACPI Power Management Timer is a very simple timer which runs at 3.579545 MHz and generates a SCI when the counter has overflown.
    /// </summary>
    public static unsafe class ACPITimer {
        /// <summary>
        /// Clock
        /// </summary>
        private const int Clock = 3579545;
        /// <summary>
        /// Sleep
        /// </summary>
        /// <param name="ms"></param>
        public static void Sleep(ulong ms) {
            if (ACPI.FADT->PMTimerLength != 4) {
                Console.Write("ACPI Timer is not present!\n");
                for (; ; );
            }
            ulong delta = 0;
            ulong count = ms * (Clock / 1000);
            ulong last = Native.In32((ushort)ACPI.FADT->PMTimerBlock) & 0xFFFFFF;
            while (count != 0) {
                ulong curr = Native.In32((ushort)ACPI.FADT->PMTimerBlock) & 0xFFFFFF;
                if (curr > last) {
                    delta = curr - last;
                }
                if (last > curr) {
                    delta = (curr + 0xFFFFFF) - last;
                }
                last = curr;

                if (count > delta) {
                    count -= delta;
                } else {
                    count = 0;
                }
            }
        }
        /// <summary>
        /// Sleep Microseconds
        /// </summary>
        /// <param name="Microseconds"></param>
        public static void SleepMicroseconds(ulong Microseconds) {
            if (ACPI.FADT->PMTimerLength != 4) return;
            ulong Clock;
            ulong Counter;
            ulong Last;
            Clock = ACPITimer.Clock * Microseconds / 1000000;
            Last = Native.In32(ACPI.FADT->PMTimerBlock);
            Counter = 0;
            while (Counter < Clock) {
                ulong Current = Native.In32(ACPI.FADT->PMTimerBlock);
                if (Current < Last) {
                    Counter += ((((ACPI.FADT->Flags >> 8) & 0x01) & 0x01) ? 0x100000000ul : 0x1000000) + Current - Last;
                } else {
                    Counter += Current - Last;
                }
                Last = Current;
                Native.Nop();
            }
        }
    }
}
