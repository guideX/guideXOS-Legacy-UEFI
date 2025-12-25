using guideXOS.Misc;
namespace guideXOS.Kernel.Drivers {
    public static class Timer {
        public static ulong Bus_Clock;
        public static ulong CPU_Clock;

        public static void Initialize() {
            // Debug: entering Timer.Initialize
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'t');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'m');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'1');
            
            // SKIP timer initialization entirely for now - just set default values
            // The timer interrupt seems to be causing crashes
            CPU_Clock = 1000000000; // Assume 1GHz
            Bus_Clock = 100000000;  // Assume 100MHz bus
            
            // Debug: done (no timer)
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'S');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'I');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'P');
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