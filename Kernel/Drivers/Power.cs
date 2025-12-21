namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// Power
    /// </summary>
    public static class Power {
        /// <summary>
        /// Reboot
        /// </summary>
        public static void Reboot() {
            while ((Native.In8(0x64) & 0x02) != 0) ;
            Native.Out8(0x64, 0xFE);
            Native.Hlt();
        }
        /// <summary>
        /// Shutdown
        /// </summary>
        public static void Shutdown() {
            ACPI.Shutdown();
        }
    }
}