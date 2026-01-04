namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Interface for mouse input providers
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY: FULLY COMPATIBLE (Interface only)
    /// ============================================================================
    /// 
    /// All mouse input sources (PS/2, USB HID, UEFI Pointer) implement this interface
    /// to provide a unified way to poll for mouse events.
    /// 
    /// Design principles:
    /// - Non-blocking: Poll() returns immediately with current state
    /// - Provider-agnostic: Caller doesn't need to know underlying hardware
    /// - Lifecycle-aware: IsAvailable indicates if provider can be used
    /// </summary>
    public interface IMouseInputProvider {
        /// <summary>
        /// Provider name for debugging/logging
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// True if this provider is initialized and can provide input
        /// May become false after ExitBootServices for UEFI providers
        /// </summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Priority for polling order (lower = higher priority)
        /// 0 = UEFI pointer (before ExitBootServices)
        /// 5 = USB HID mouse
        /// 10 = PS/2 mouse (Legacy only)
        /// 100 = Keyboard emulation (Kbd2Mouse)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Initialize the provider
        /// Returns true if initialization succeeded
        /// </summary>
        bool Initialize();

        /// <summary>
        /// Poll for new mouse data
        /// This method must be non-blocking
        /// </summary>
        /// <param name="mouseEvent">Output: the current mouse state/event</param>
        /// <returns>True if new data is available, false otherwise</returns>
        bool Poll(out MouseEvent mouseEvent);

        /// <summary>
        /// Called when ExitBootServices is about to be called
        /// UEFI providers should mark themselves as unavailable
        /// </summary>
        void OnExitBootServices();

        /// <summary>
        /// Cleanup and release resources
        /// </summary>
        void Shutdown();
    }
}
