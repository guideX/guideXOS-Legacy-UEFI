using guideXOS.Kernel.Drivers.Input.Uefi;
using guideXOS.Misc;
using System.Windows.Forms;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// UEFI Simple Pointer Protocol input provider
    /// 
    /// ============================================================================
    /// UEFI LIFECYCLE
    /// ============================================================================
    /// 
    /// This provider uses EFI_SIMPLE_POINTER_PROTOCOL which is ONLY available
    /// BEFORE ExitBootServices is called by the kernel.
    /// 
    /// BEFORE ExitBootServices:
    /// - Protocol is valid and can be polled
    /// - Events are captured and buffered
    /// - IsAvailable returns true
    /// 
    /// AFTER ExitBootServices:
    /// - Protocol pointer becomes invalid
    /// - No new events can be captured
    /// - IsAvailable returns false
    /// - Buffered events can still be consumed
    /// - MouseInputManager should fall back to USB HID
    /// 
    /// ============================================================================
    /// INITIALIZATION
    /// ============================================================================
    /// 
    /// The protocol pointer must be obtained by the bootloader and passed via
    /// UefiBootInfo. The kernel cannot locate protocols after entering long mode
    /// because Boot Services are designed for the bootloader's address space.
    /// 
    /// Required UefiBootInfo fields (need to be added):
    /// - SimplePointerProtocol: EFI_SIMPLE_POINTER_PROTOCOL*
    /// - HasSimplePointer flag (Flags bit 0x8)
    /// 
    /// ============================================================================
    /// </summary>
    public unsafe class UefiPointerProvider {
        // Protocol pointer from bootloader
        private EfiSimplePointerProtocol* _protocol;
        
        // Device mode/capabilities
        private EfiSimplePointerMode* _mode;
        
        // Current state buffer
        private EfiSimplePointerState _lastState;
        
        // Lock-free ring buffer for events that survive ExitBootServices
        // Uses SPSC design - producer (this class) / consumer (MouseInputManager)
        private MouseEventRingBuffer _eventBuffer;
        
        // Availability flag
        private bool _isAvailable;
        private bool _isInitialized;
        
        // Resolution scaling factors (counts per pixel approximation)
        private float _scaleX;
        private float _scaleY;
        private float _scaleZ;
        
        // Sensitivity settings (reused from PS2Mouse concepts)
        private float _sensitivity;
        private int _noiseThreshold;
        private int _maxDeltaPerPoll;
        
        // Button debouncing
        private byte _lastButtonState;
        private int _buttonStableCount;
        private int _buttonDebounceThreshold;
        
        // Debug counters
        public ulong PollCount { get; private set; }
        public ulong EventCount { get; private set; }
        public ulong ErrorCount { get; private set; }

        public string ProviderName => "UEFI Simple Pointer";
        
        public bool IsAvailable => _isAvailable && _isInitialized && _protocol != null;
        
        public int Priority => 0; // Highest priority (used before ExitBootServices)

        /// <summary>
        /// Create a new UEFI pointer provider
        /// Call Initialize() with boot info to set up the protocol
        /// </summary>
        public UefiPointerProvider() {
            _protocol = null;
            _mode = null;
            _isAvailable = false;
            _isInitialized = false;
            
            // Create lock-free ring buffer with 64 event capacity (2^6)
            // This is allocated once and never reallocated
            _eventBuffer = new MouseEventRingBuffer(6);
            
            // Default filtering settings (similar to PS2Mouse)
            _sensitivity = 1.0f;  // UEFI pointer usually already scaled
            _noiseThreshold = 1;   // Ignore very tiny movements
            _maxDeltaPerPoll = 100; // Clamp extreme values
            
            // Button debouncing
            _lastButtonState = 0;
            _buttonStableCount = 0;
            _buttonDebounceThreshold = 1; // Less debouncing needed for UEFI
            
            // Default scale (will be updated from mode if available)
            _scaleX = 1.0f;
            _scaleY = 1.0f;
            _scaleZ = 1.0f;
            
            PollCount = 0;
            EventCount = 0;
            ErrorCount = 0;
        }

        /// <summary>
        /// Initialize from UefiBootInfo
        /// </summary>
        public bool Initialize() {
            // This version requires SetProtocol to be called with the actual pointer
            // Cannot auto-initialize without bootloader passing the protocol
            return _isInitialized;
        }

        /// <summary>
        /// Initialize with protocol pointer from bootloader
        /// 
        /// EXITBOOTSERVICES RULE: This method MUST be called BEFORE ExitBootServices.
        /// The protocol pointer becomes invalid after ExitBootServices.
        /// </summary>
        /// <param name="protocolPtr">EFI_SIMPLE_POINTER_PROTOCOL* from bootloader</param>
        /// <returns>True if initialization succeeded</returns>
        public bool Initialize(ulong protocolPtr) {
            // ================================================================
            // EXITBOOTSERVICES GUARD: Cannot initialize after ExitBootServices
            // ================================================================
            if (ExitBootServicesRules.HasExitedBootServices) {
                MouseDebug.LogError("UefiPointer", 
                    "Cannot initialize after ExitBootServices!");
                _isInitialized = false;
                _isAvailable = false;
                return false;
            }
            
            // Debug: Log protocol discovery attempt
            MouseDebug.LogProtocolDiscovery("EFI_SIMPLE_POINTER_PROTOCOL", 
                protocolPtr != 0, 
                "addr=0x" + protocolPtr.ToString("X16"));
            
            if (protocolPtr == 0) {
                DebugLog("UEFI Pointer: Protocol pointer is null");
                MouseDebug.LogProtocolInit("UefiPointerProvider", false, "null protocol pointer");
                _isInitialized = false;
                _isAvailable = false;
                return false;
            }

            _protocol = (EfiSimplePointerProtocol*)protocolPtr;
            
            // Debug: Log protocol validation
            MouseDebug.LogProtocolValidation("EFI_SIMPLE_POINTER_PROTOCOL", protocolPtr, true);
            
            // Validate protocol structure
            if (!ValidateProtocol()) {
                DebugLog("UEFI Pointer: Protocol validation failed");
                MouseDebug.LogProtocolInit("UefiPointerProvider", false, "validation failed");
                _protocol = null;
                _isInitialized = false;
                _isAvailable = false;
                return false;
            }

            // Get mode information
            _mode = _protocol->Mode;
            if (_mode != null) {
                // Calculate scale factors from resolution
                // Resolution is in counts per mm, we want pixels per count
                // Assume ~3.78 pixels per mm (96 DPI) as baseline
                const float pixelsPerMm = 3.78f;
                
                if (_mode->ResolutionX > 0) {
                    _scaleX = pixelsPerMm / _mode->ResolutionX;
                }
                if (_mode->ResolutionY > 0) {
                    _scaleY = pixelsPerMm / _mode->ResolutionY;
                }
                if (_mode->ResolutionZ > 0) {
                    _scaleZ = 1.0f / _mode->ResolutionZ; // Scroll wheel
                }
                
                DebugLog("UEFI Pointer: Mode available");
                MouseDebug.LogInfo("UefiPointer", "Mode: ResX=" + _mode->ResolutionX + 
                    " ResY=" + _mode->ResolutionY + " ResZ=" + _mode->ResolutionZ);
            }

            // Reset the device
            if (_protocol->Reset != null) {
                ulong status = _protocol->CallReset(0); // No extended verification
                if (EfiStatus.IsError(status)) {
                    DebugLog("UEFI Pointer: Reset returned error");
                    MouseDebug.LogWarn("UefiPointer", "Reset returned error status");
                    // Continue anyway - some firmware doesn't implement reset properly
                }
            }

            _isInitialized = true;
            _isAvailable = true;
            
            DebugLog("UEFI Pointer: Initialized successfully");
            MouseDebug.LogProtocolInit("UefiPointerProvider", true, "ready");
            return true;
        }

        /// <summary>
        /// Validate that the protocol structure looks reasonable
        /// </summary>
        private bool ValidateProtocol() {
            if (_protocol == null) return false;
            
            // Check that GetState function pointer is valid
            // (Not null and not obviously garbage)
            if (_protocol->GetState == null) {
                return false;
            }
            
            // Basic sanity check - the function pointer should be in a reasonable range
            // This is a heuristic to catch obviously invalid pointers
            ulong funcAddr = (ulong)_protocol->GetState;
            if (funcAddr < 0x1000 || funcAddr > 0xFFFFFFFFFFFF0000) {
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Poll for new mouse data from UEFI protocol
        /// Non-blocking - returns immediately if no new data
        /// 
        /// EXITBOOTSERVICES RULE: This method MUST NOT call protocol functions
        /// after ExitBootServices. The _isAvailable flag and ExitBootServicesRules
        /// guards prevent this.
        /// </summary>
        public bool Poll(out MouseEvent mouseEvent) {
            mouseEvent = MouseEvent.Empty;
            PollCount++;

            // ================================================================
            // EXITBOOTSERVICES GUARD: Check if Boot Services are still available
            // ================================================================
            if (ExitBootServicesRules.HasExitedBootServices) {
                // Boot Services are gone - only return buffered events
                return _eventBuffer.TryDequeue(out mouseEvent);
            }

            // Check if we're still available
            if (!_isAvailable || _protocol == null || _protocol->GetState == null) {
                // Try to return buffered events if any
                return _eventBuffer.TryDequeue(out mouseEvent);
            }
            
            // ================================================================
            // EXITBOOTSERVICES GUARD: Verify protocol is safe to use
            // ================================================================
            if (!ExitBootServicesRules.IsProtocolSafe(_protocol, "EFI_SIMPLE_POINTER_PROTOCOL")) {
                _isAvailable = false;
                return _eventBuffer.TryDequeue(out mouseEvent);
            }
            
            if (!ExitBootServicesRules.IsFunctionSafe(_protocol->GetState, "GetState")) {
                _isAvailable = false;
                return _eventBuffer.TryDequeue(out mouseEvent);
            }

            // Poll the UEFI protocol
            EfiSimplePointerState state;
            ulong status;
            
            try {
                status = _protocol->CallGetState(&state);
            } catch {
                // If we get an exception, the protocol is probably invalid
                // This could happen if ExitBootServices was called between our check and the call
                ErrorCount++;
                _isAvailable = false;
                MouseDebug.LogError("UefiPointer", "Exception in GetState - marking unavailable");
                return _eventBuffer.TryDequeue(out mouseEvent);
            }

            // Check status
            if (EfiStatus.IsNotReady(status)) {
                // No new data - return buffered event if any
                return _eventBuffer.TryDequeue(out mouseEvent);
            }

            if (EfiStatus.IsError(status)) {
                ErrorCount++;
                // Try buffered events
                return _eventBuffer.TryDequeue(out mouseEvent);
            }

            // Got new data - process it
            EventCount++;
            
            // Apply scaling and filtering
            int deltaX = (int)(state.RelativeMovementX * _scaleX * _sensitivity);
            int deltaY = (int)(state.RelativeMovementY * _scaleY * _sensitivity);
            int deltaZ = (int)(state.RelativeMovementZ * _scaleZ);

            // Invert Y axis for screen coordinates (UEFI Y+ is typically down already, but verify)
            // Most UEFI implementations follow screen coordinates, no inversion needed
            
            // Apply noise filtering
            if (System.Math.Abs(deltaX) < _noiseThreshold) deltaX = 0;
            if (System.Math.Abs(deltaY) < _noiseThreshold) deltaY = 0;
            
            // Clamp extreme values
            deltaX = System.Math.Clamp(deltaX, -_maxDeltaPerPoll, _maxDeltaPerPoll);
            deltaY = System.Math.Clamp(deltaY, -_maxDeltaPerPoll, _maxDeltaPerPoll);

            // Process buttons with debouncing
            byte currentButtonState = 0;
            if (state.LeftButton != 0) currentButtonState |= 0x01;
            if (state.RightButton != 0) currentButtonState |= 0x02;
            
            MouseButtons buttons = MouseButtons.None;
            
            if (currentButtonState == _lastButtonState) {
                _buttonStableCount++;
            } else {
                _buttonStableCount = 0;
                _lastButtonState = currentButtonState;
            }
            
            // Only update buttons if stable
            if (_buttonStableCount >= _buttonDebounceThreshold) {
                if ((currentButtonState & 0x01) != 0) buttons |= MouseButtons.Left;
                if ((currentButtonState & 0x02) != 0) buttons |= MouseButtons.Right;
            }

            // Get timestamp
            ulong timestamp = GetTimestamp();

            // Create event using source-specific factory method
            mouseEvent = MouseEvent.FromUefiSimplePointer(
                deltaX, deltaY, deltaZ,
                (currentButtonState & 0x01) != 0,  // Left button
                (currentButtonState & 0x02) != 0,  // Right button
                timestamp);
            
            // Debug: Log state change
            MouseDebug.LogStateChange(deltaX, deltaY, deltaZ, mouseEvent.Buttons, 
                MouseInputSource.UefiSimplePointer);
            
            // Also buffer the event using lock-free enqueue (for ExitBootServices transition)
            // This uses drop-oldest policy if buffer is full
            _eventBuffer.TryEnqueue(mouseEvent);
            
            // Store last state
            _lastState = state;
            
            return true;
        }

        /// <summary>
        /// Called when ExitBootServices is about to be called.
        /// After this, the protocol becomes INVALID and MUST NOT be accessed.
        /// 
        /// EXITBOOTSERVICES RULE:
        /// - Mark _isAvailable = false
        /// - Do NOT null the protocol (buffered events can still be consumed)
        /// - Do NOT call any protocol functions after this
        /// - Buffered events in _eventBuffer remain valid (kernel memory)
        /// </summary>
        public void OnExitBootServices() {
            DebugLog("UEFI Pointer: ExitBootServices - marking unavailable");
            
            // Debug: Log ExitBootServices for this provider
            MouseDebug.LogExitBootServicesProvider(ProviderName, BufferedEventCount);
            
            // ================================================================
            // CRITICAL: Mark unavailable to prevent any further protocol access
            // ================================================================
            _isAvailable = false;
            
            // NOTE: We intentionally do NOT null _protocol here because:
            // 1. Buffered events in _eventBuffer are still valid
            // 2. The pointer value itself is useful for diagnostics
            // 3. The _isAvailable flag prevents any actual access
            //
            // After this point:
            // - Poll() will only return buffered events
            // - No protocol functions will be called
            // - The protocol pointer points to reclaimed memory (DO NOT DEREFERENCE)
        }

        /// <summary>
        /// Shutdown the provider
        /// </summary>
        public void Shutdown() {
            _isAvailable = false;
            _isInitialized = false;
            _protocol = null;
            _mode = null;
            _eventBuffer.Clear();
        }

        /// <summary>
        /// Get remaining buffered events count
        /// Useful for debugging transition period
        /// </summary>
        public int BufferedEventCount => _eventBuffer.Count;

        /// <summary>
        /// Number of events dropped due to buffer overflow
        /// </summary>
        public ulong DroppedEventCount => _eventBuffer.DroppedEvents;

        /// <summary>
        /// Consume all buffered events at once
        /// Returns combined/aggregated event
        /// </summary>
        public MouseEvent ConsumeAllBuffered() {
            return _eventBuffer.DequeueAllAggregated();
        }

        /// <summary>
        /// Get buffer diagnostics
        /// </summary>
        public string GetBufferDiagnostics() {
            return _eventBuffer.GetDiagnostics();
        }

        /// <summary>
        /// Get current timestamp (timer ticks)
        /// </summary>
        private ulong GetTimestamp() {
            // Try to use Timer.Ticks if available
            try {
                return Timer.Ticks;
            } catch {
                // Fallback to 0 if timer not initialized
                return 0;
            }
        }

        /// <summary>
        /// Debug logging helper - uses for loop to avoid IEnumerable dependency
        /// </summary>
        private void DebugLog(string message) {
            // Output to serial port for debugging
            if (message == null) return;
            
            try {
                // Use for loop instead of foreach to avoid IEnumerable
                for (int i = 0; i < message.Length; i++) {
                    char c = message[i];
                    // Wait for transmit buffer empty
                    while ((Native.In8(0x3FD) & 0x20) == 0) { }
                    Native.Out8(0x3F8, (byte)c);
                }
                // Send newline
                while ((Native.In8(0x3FD) & 0x20) == 0) { }
                Native.Out8(0x3F8, (byte)'\n');
            } catch {
                // Ignore logging errors
            }
        }
    }
}
