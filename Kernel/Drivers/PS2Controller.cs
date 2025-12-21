namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// PS2 Controller
    /// </summary>
    public static class PS2Controller {
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            PS2Keyboard.Initialize();
            PS2Mouse.Initialise();
        }
    }
}