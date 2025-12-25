using System;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// On Key Handler
    /// </summary>
    /// <param name="key"></param>
    public delegate void OnKeyHandler(ConsoleKeyInfo key);
    /// <summary>
    /// Keyboard
    /// </summary>
    public static class Keyboard {
        /// <summary>
        /// Key Info
        /// </summary>
        public static ConsoleKeyInfo KeyInfo;
        /// <summary>
        /// On key Changed
        /// </summary>
        public static EventHandler<ConsoleKeyInfo> OnKeyChanged;
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            // Debug: entering Keyboard.Initialize
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'<');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'1');
            
            OnKeyChanged = null;
            
            // Debug: after OnKeyChanged = null
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'2');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'>');
        }
        /// <summary>
        /// Invoke On Key Changed
        /// </summary>
        /// <param name="info"></param>
        internal static void InvokeOnKeyChanged(ConsoleKeyInfo info) {
            OnKeyChanged?.Invoke(null, info);
        }
        /// <summary>
        /// Simulate Key - for On-Screen Keyboard
        /// </summary>
        /// <param name="info"></param>
        public static void SimulateKey(ConsoleKeyInfo info) {
            // Fire pressed event
            KeyInfo = info;
            OnKeyChanged?.Invoke(null, info);
            
            // Immediately fire released event to reset state
            var releasedInfo = new ConsoleKeyInfo {
                Key = info.Key,
                KeyChar = info.KeyChar,
                Modifiers = info.Modifiers,
                KeyState = ConsoleKeyState.Released,
                ScanCode = info.ScanCode
            };
            KeyInfo = releasedInfo;
            OnKeyChanged?.Invoke(null, releasedInfo);
            
            // Clean up to prevent interference with physical keyboard
            CleanKeyInfo(false);
        }
        /// <summary>
        /// Clean Key Info
        /// </summary>
        /// <param name="NoModifiers"></param>
        public static void CleanKeyInfo(bool NoModifiers = false) {
            KeyInfo.KeyChar = '\0';
            KeyInfo.ScanCode = 0;
            KeyInfo.KeyState = ConsoleKeyState.None;
            if (!NoModifiers) {
                KeyInfo.Modifiers = ConsoleModifiers.None;
            }
        }
    }
}