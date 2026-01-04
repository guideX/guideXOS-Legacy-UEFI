using System;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// On Key Handler delegate
    /// </summary>
    /// <param name="key">Key information</param>
    public delegate void OnKeyHandler(ConsoleKeyInfo key);
    
    /// <summary>
    /// Keyboard Event Dispatch - Central keyboard input routing
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION: UEFI-COMPATIBLE (Pure event dispatch)
    /// 
    /// This class does NOT perform any hardware access.
    /// It is a pure software event dispatcher that:
    /// - Stores current key state (KeyInfo)
    /// - Provides OnKeyChanged event for subscribers
    /// - Routes key events from any input source
    /// 
    /// REUSABLE FOR ALL INPUT SOURCES:
    /// - PS/2 keyboard (Legacy via PS2Keyboard.cs)
    /// - USB HID keyboard (via HID.cs -> USB.OnInterrupt)
    /// - On-screen keyboard (via SimulateKey)
    /// - UEFI keyboard protocol (future)
    /// 
    /// The InvokeOnKeyChanged() method is the universal entry point.
    /// Any new input source just needs to:
    /// 1. Build a ConsoleKeyInfo struct
    /// 2. Call Keyboard.InvokeOnKeyChanged(info)
    /// 
    /// ============================================================================
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
            OnKeyChanged = null;
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