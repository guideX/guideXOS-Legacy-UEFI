namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// PS2 Controller
    /// </summary>
    public static class PS2Controller {
        /// <summary>
        /// Initialize
        /// </summary>
        public static unsafe void Initialize() {
            // Debug: entering PS2Controller.Initialize
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'p');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'s');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'2');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'1');
            
            PS2Keyboard.Initialize();
            
            // Debug: after PS2Keyboard
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'p');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'s');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'2');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'2');
            
            PS2Mouse.Initialise();
            
            // Debug: after PS2Mouse
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'p');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'s');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'2');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'3');
        }
    }
}