namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// PS2 Controller
    /// </summary>
    public static class PS2Controller {
        /// <summary>
        /// Initialize
        /// </summary>
        public static unsafe void Initialize() {
            PS2Keyboard.Initialize();
            PS2Mouse.Initialize();
        }
    }
}