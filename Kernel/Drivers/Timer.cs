using guideXOS.Misc;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// Timer
    /// </summary>
    public static class Timer {
        /// <summary>
        /// Bus Clock in Hz
        /// </summary>
        public static ulong Bus_Clock;
        /// <summary>
        /// CPU Clock
        /// </summary>
        public static ulong CPU_Clock;
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            BootConsole.WriteLine("[TIMER] INITIALIZE");

            // Minimal timer init compatible with PIC IRQ0 scheduling.
            // Program PIT channel 0 for ~1000Hz.
            // PIT base frequency is 1193182 Hz.
            const uint pitHz = 1193182;
            const uint targetHz = 1000;
            uint divisor = pitHz / targetHz;
            if (divisor == 0) divisor = 1;

            // Command: channel 0, lobyte/hibyte, mode 2 (rate generator), binary
            Native.Out8(0x43, 0x34);
            Native.Out8(0x40, (byte)(divisor & 0xFF));
            Native.Out8(0x40, (byte)((divisor >> 8) & 0xFF));

            CPU_Clock = 1000000000; // placeholder
            Bus_Clock = 100000000;  // placeholder

            BootConsole.WriteLine("[TIMER] PIT programmed (1000Hz)");
        }
        private static ulong EstimateCPUSpeed() {
            ulong prev = Native.Rdtsc();
            ACPITimer.SleepMicroseconds(100000);
            ulong next = Native.Rdtsc();
            ulong cpuclock;
            if (next > prev) {
                cpuclock = next - prev;
            } else {
                //Overflow
                cpuclock = prev - next;
            }
            cpuclock *= 10;
            return cpuclock;
        }

        public static ulong Ticks { get; private set; }

        internal static void OnInterrupt() {
            //This method is only for bootstrap CPU
            if (SMP.ThisCPU == 0) {
                Ticks++;

                if (ThreadPool.Locked) {
                    Ticks--;
                }
            }
        }

        public static void Sleep(ulong millisecond) {
            ulong T = Ticks;
            while (Ticks < (T + millisecond)) Native.Hlt();
        }
    }
}