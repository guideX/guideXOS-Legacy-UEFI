using guideXOS.Misc;
using System;
using System.Windows.Forms;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Central mouse input manager
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY: FULLY COMPATIBLE
    /// ============================================================================
    /// 
    /// This manager coordinates all mouse input sources and routes their output
    /// to the GUI system via Control.MousePosition/MouseButtons.
    /// 
    /// NOTE: Interface-based provider dispatch (IMouseInputProvider) was removed
    /// because NativeAOT bare-metal mode doesn't support RhpInitialDynamicInterfaceDispatch.
    /// Instead, we use direct references to concrete provider types.
    /// 
    /// PROVIDER PRIORITY:
    /// - UEFI pointer (before ExitBootServices only) - highest priority
    /// - USB HID mouse (future)
    /// - PS/2 mouse (Legacy boot only)
    /// 
    /// DATA FLOW:
    /// 1. Main loop calls MouseInputManager.Poll()
    /// 2. Manager checks each provider directly
    /// 3. First provider with valid data wins
    /// 4. Filtering is applied (if enabled)
    /// 5. Control.MousePosition/MouseButtons are updated
    /// 
    /// ExitBootServices HANDLING:
    /// - Call OnExitBootServices() before ExitBootServices
    /// - UEFI providers mark themselves unavailable
    /// - Buffered events can still be consumed
    /// - USB HID becomes primary source
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class MouseInputManager {
        // Direct references to concrete provider types (no interface dispatch)
        private static UefiPointerProvider _uefiProvider;
        // Future: Add HIDMouseProvider, PS2MouseProvider, etc. as concrete fields
        
        // Provider count for diagnostics
        private static int _providerCount;
        
        // Initialization state
        private static bool _isInitialized;
        private static bool _exitBootServicesOccurred;
        
        // Filtering settings (extracted from PS2Mouse for reuse)
        private static bool _enableFiltering;
        private static float _sensitivity;
        private static int _noiseThreshold;
        private static int _maxDeltaPerPoll;
        
        // Debug counters
        public static ulong TotalPolls { get; private set; }
        public static ulong TotalEvents { get; private set; }
        public static string LastActiveProvider { get; private set; }

        /// <summary>
        /// True if manager is initialized and ready
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// True only while a UEFI pointer provider exists and may still be polled.
        /// Once ExitBootServices has occurred, firmware protocol pointers are invalid.
        /// </summary>
        public static bool HasActiveUefiPointerProvider =>
            _uefiProvider != null &&
            _uefiProvider.IsAvailable &&
            !ExitBootServicesRules.HasExitedBootServices;

        /// <summary>
        /// True when at least one provider can currently produce mouse events.
        /// </summary>
        public static bool HasAnyActiveProvider => HasActiveUefiPointerProvider;

        /// <summary>
        /// True after ExitBootServices has been called.
        /// Uses the centralized ExitBootServicesRules state.
        /// </summary>
        public static bool AfterExitBootServices => ExitBootServicesRules.HasExitedBootServices;

        /// <summary>
        /// Initialize the mouse input manager
        /// Must be called early in boot, before GUI initialization
        /// </summary>
        public static void Initialize() {
            if (_isInitialized) return;

            _uefiProvider = null;
            _providerCount = 0;
            _exitBootServicesOccurred = false;
            
            // Default filtering settings
            _enableFiltering = true;
            _sensitivity = 1.0f;
            _noiseThreshold = 2;
            _maxDeltaPerPoll = 50;
            
            TotalPolls = 0;
            TotalEvents = 0;
            LastActiveProvider = "None";
            
            _isInitialized = true;
            
            DebugLog("[MouseInputManager] Initialized");
        }

        /// <summary>
        /// Initialize with UefiBootInfo
        /// Automatically sets up UEFI pointer provider if available
        /// </summary>
        public static void Initialize(UefiBootInfo* bootInfo) {
            Initialize();
            
            if (bootInfo == null) {
                DebugLog("[MouseInputManager] No boot info provided");
                return;
            }

            DebugLog("[MouseInputManager] UEFI boot info received");

            // ----------------------------------------------------------------
            // Hex dump of BootInfo for debugging struct layout mismatches
            // ----------------------------------------------------------------
            DebugLog("[MouseInputManager] BootInfo hex dump (input protocol region):");
            DumpBootInfoInputRegion(bootInfo);

            // ----------------------------------------------------------------
            // Explicit diagnostics for missing UEFI pointer protocol fields
            // ----------------------------------------------------------------
            // NOTE: These reads are safe post-EBS because BootInfo is kernel-owned memory.
            // However, SimplePointerProtocol points to UEFI memory and MUST NOT be used
            // after ExitBootServices.
            
            // Log the raw Flags value to check which bits are set
            uint flags = bootInfo->Flags;
            DebugLog("[MouseInputManager] BootInfo.Flags = 0x" + ToHex8(flags));
            DebugLog("[MouseInputManager]   Bit 0 (MemMap): " + ((flags & 0x1) != 0 ? "SET" : "clear"));
            DebugLog("[MouseInputManager]   Bit 1 (FB): " + ((flags & 0x2) != 0 ? "SET" : "clear"));
            DebugLog("[MouseInputManager]   Bit 2 (Ramdisk): " + ((flags & 0x4) != 0 ? "SET" : "clear"));
            DebugLog("[MouseInputManager]   Bit 3 (SimplePtr): " + ((flags & 0x8) != 0 ? "SET" : "clear"));
            DebugLog("[MouseInputManager]   Bit 4 (AbsPtr): " + ((flags & 0x10) != 0 ? "SET" : "clear"));
            DebugLog("[MouseInputManager]   Bit 5 (TextIn): " + ((flags & 0x20) != 0 ? "SET" : "clear"));
            
            bool hasSimple = false;
            ulong simplePtr = 0;
            try {
                hasSimple = bootInfo->HasSimplePointer;
                simplePtr = bootInfo->SimplePointerProtocol;
            } catch {
                DebugLog("[MouseInputManager] WARNING: Exception reading boot info mouse fields");
            }

            if (!hasSimple) {
                DebugLog("[MouseInputManager] UEFI BootInfo: HasSimplePointer == false (bootloader did not report EFI_SIMPLE_POINTER_PROTOCOL)");
            } else {
                DebugLog("[MouseInputManager] UEFI BootInfo: HasSimplePointer == true");
            }

            if (simplePtr == 0) {
                DebugLog("[MouseInputManager] UEFI BootInfo: SimplePointerProtocol == 0 (bootloader did not provide protocol pointer)");
            } else {
                DebugLog("[MouseInputManager] UEFI BootInfo: SimplePointerProtocol = 0x" + ToHex16(simplePtr));
            }

            if (ExitBootServicesRules.HasExitedBootServices) {
                DebugLog("[MouseInputManager] ExitBootServices already occurred; skipping UEFI pointer protocol");
            }
            // Check if simple pointer protocol is available
            else if (hasSimple && simplePtr != 0) {
                DebugLog("[MouseInputManager] Simple pointer protocol found (BootInfo fields valid)");
                if (SetUefiPointerProtocol(simplePtr)) {
                    DebugLog("[MouseInputManager] UEFI mouse initialized from boot info");
                }
            } else {
                // More explicit reason in logs
                if (!hasSimple && simplePtr == 0) {
                    DebugLog("[MouseInputManager] No UEFI mouse: HasSimplePointer=false AND SimplePointerProtocol=0");
                } else if (!hasSimple) {
                    DebugLog("[MouseInputManager] No UEFI mouse: HasSimplePointer=false (even though pointer may be non-zero)");
                } else {
                    DebugLog("[MouseInputManager] No UEFI mouse: SimplePointerProtocol=0 (even though HasSimplePointer=true)");
                }
            }
        }
        
        /// <summary>
        /// Dump the input protocol region of BootInfo as hex for debugging
        /// Shows bytes around the SimplePointerProtocol field to detect struct mismatches
        /// </summary>
        private static void DumpBootInfoInputRegion(UefiBootInfo* bootInfo) {
            // UefiBootInfo layout with Pack=1 (verified offsets from hex dump):
            // ============================================================================
            // Header (28 bytes):
            //   0x00: Magic (4)
            //   0x04: Version (2)
            //   0x06: Size (2)
            //   0x08: Flags (4)
            //   0x0C: HeaderChecksum (4)
            //   0x10: Reserved0 (4)
            //   0x14: BootMode (4)
            //   0x18: Reserved1 (4)
            // Memory Map (24 bytes @ 0x1C):
            //   0x1C: MemoryMap (8)
            //   0x24: MemoryMapEntryCount (8)
            //   0x2C: MemoryMapDescriptorSize (8)
            // Framebuffer (36 bytes @ 0x34):
            //   0x34: FramebufferBase (8)
            //   0x3C: FramebufferSize (8)
            //   0x44: FramebufferWidth (4)
            //   0x48: FramebufferHeight (4)
            //   0x4C: FramebufferPitch (4)
            //   0x50: FramebufferFormat (4)
            //   0x54: Reserved2 (4)
            // ACPI (8 bytes @ 0x58):
            //   0x58: AcpiRsdp (8)
            // CommandLine (8 bytes @ 0x60):
            //   0x60: CommandLine (8)
            // Ramdisk (16 bytes @ 0x68):
            //   0x68: RamdiskBase (8)
            //   0x70: RamdiskSize (8)
            // Input Protocols (24 bytes @ 0x78):
            //   0x78: SimplePointerProtocol (8)  <-- CORRECT OFFSET!
            //   0x80: AbsolutePointerProtocol (8)
            //   0x88: SimpleTextInputEx (8)
            // Reserved (24 bytes @ 0x90):
            //   0x90: Reserved[3] (24)
            // ============================================================================
            // Total: 0xA8 = 168 bytes
            
            byte* ptr = (byte*)bootInfo;
            
            // Dump bytes from offset 0x68 to 0x98 (includes Ramdisk and Input protocols)
            DebugLog("[MouseInputManager] Bytes at offset 0x68-0x97 (Ramdisk + Input Protocols):");
            
            for (int row = 0; row < 3; row++) {
                int offset = 0x68 + (row * 16);
                string line = "  0x" + ToHex2(offset) + ": ";
                
                for (int i = 0; i < 16; i++) {
                    byte b = ptr[offset + i];
                    line += ToHex2(b) + " ";
                }
                
                DebugLog(line);
            }
            
            // Read the ACTUAL correct offsets based on C++ struct layout
            // SimplePointerProtocol is at offset 0x78 (not 0x80!)
            ulong* ptr64 = (ulong*)ptr;
            
            // 0x78 / 8 = 15
            DebugLog("[MouseInputManager] Raw uint64 at offset 0x78 (SimplePointerProtocol): 0x" + ToHex16(*(ulong*)(ptr + 0x78)));
            DebugLog("[MouseInputManager] Raw uint64 at offset 0x80 (AbsolutePointerProtocol): 0x" + ToHex16(*(ulong*)(ptr + 0x80)));
            DebugLog("[MouseInputManager] Raw uint64 at offset 0x88 (SimpleTextInputEx): 0x" + ToHex16(*(ulong*)(ptr + 0x88)));
            
            // Also show what the struct field reads as (to compare)
            DebugLog("[MouseInputManager] bootInfo->SimplePointerProtocol reads as: 0x" + ToHex16(bootInfo->SimplePointerProtocol));
        }
        
        /// <summary>
        /// Convert byte to 2-digit hex string (no format string support in bare metal)
        /// </summary>
        private static string ToHex2(int value) {
            char[] hex = new char[2];
            hex[0] = HexChar((value >> 4) & 0xF);
            hex[1] = HexChar(value & 0xF);
            return new string(hex);
        }
        
        /// <summary>
        /// Convert uint to 8-digit hex string
        /// </summary>
        private static string ToHex8(uint value) {
            char[] hex = new char[8];
            for (int i = 7; i >= 0; i--) {
                hex[i] = HexChar((int)(value & 0xF));
                value >>= 4;
            }
            return new string(hex);
        }
        
        /// <summary>
        /// Convert ulong to 16-digit hex string
        /// </summary>
        private static string ToHex16(ulong value) {
            char[] hex = new char[16];
            for (int i = 15; i >= 0; i--) {
                hex[i] = HexChar((int)(value & 0xF));
                value >>= 4;
            }
            return new string(hex);
        }
        
        /// <summary>
        /// Get hex character for nibble value 0-15
        /// </summary>
        private static char HexChar(int nibble) {
            if (nibble < 10) return (char)('0' + nibble);
            return (char)('A' + nibble - 10);
        }

        /// <summary>
        /// Set the UEFI Simple Pointer Protocol address from bootloader
        /// Call this early in boot if bootloader provides the protocol
        /// </summary>
        public static bool SetUefiPointerProtocol(ulong protocolPtr) {
            if (!_isInitialized) Initialize();
            
            if (protocolPtr == 0) {
                DebugLog("[MouseInputManager] UEFI pointer protocol is null");
                return false;
            }

            if (_uefiProvider != null) {
                DebugLog("[MouseInputManager] UEFI provider already registered");
                return false;
            }

            _uefiProvider = new UefiPointerProvider();
            if (_uefiProvider.Initialize(protocolPtr)) {
                _providerCount++;
                DebugLog("[MouseInputManager] UEFI pointer provider registered");
                return true;
            } else {
                _uefiProvider = null;
                DebugLog("[MouseInputManager] UEFI pointer provider init failed");
                return false;
            }
        }

        /// <summary>
        /// Poll all providers for new mouse data
        /// Call this once per frame in the main loop
        /// Updates Control.MousePosition and Control.MouseButtons
        /// </summary>
        public static void Poll() {
            if (!_isInitialized) return;
            
            TotalPolls++;
            
            MouseEvent evt = MouseEvent.Empty;
            string activeProviderName = null;
            
            // Poll UEFI provider first (highest priority)
            if (_uefiProvider != null && _uefiProvider.IsAvailable) {
                if (_uefiProvider.Poll(out MouseEvent uefiEvt) && uefiEvt.IsValid) {
                    evt = uefiEvt;
                    activeProviderName = _uefiProvider.ProviderName;
                }
            }
            
            // Future: Poll other providers here if UEFI didn't provide data
            // if (!evt.IsValid && _hidProvider != null && _hidProvider.IsAvailable) { ... }
            // if (!evt.IsValid && _ps2Provider != null && _ps2Provider.IsAvailable) { ... }
            
            // If we got a valid event, process it
            if (evt.IsValid) {
                TotalEvents++;
                if (activeProviderName != null) {
                    LastActiveProvider = activeProviderName;
                }
                
                ProcessEvent(evt);
            }
        }

        /// <summary>
        /// Process a mouse event and update global state
        /// Routes through MouseEventDispatcher for unified processing
        /// </summary>
        private static void ProcessEvent(MouseEvent evt) {
            // Dispatch through unified event dispatcher
            // This handles filtering, state updates, and event history
            MouseEventDispatcher.DispatchEvent(evt);
        }

        /// <summary>
        /// Notify all providers that ExitBootServices is about to be called.
        /// Must be called BEFORE ExitBootServices.
        /// 
        /// EXITBOOTSERVICES RULE:
        /// - This triggers the transition
        /// - All UEFI providers will mark themselves unavailable
        /// - Buffered events can still be consumed
        /// - No UEFI protocol access after this
        /// </summary>
        public static void OnExitBootServices() {
            if (!_isInitialized) return;
            
            // Prevent double-call
            if (_exitBootServicesOccurred) {
                MouseDebug.LogWarn("MouseInputManager", 
                    "OnExitBootServices called twice - ignoring");
                return;
            }
            
            DebugLog("[MouseInputManager] OnExitBootServices");
            
            // Debug: Log ExitBootServices start
            MouseDebug.LogExitBootServicesStart();
            
            _exitBootServicesOccurred = true;
            
            // Notify UEFI provider
            if (_uefiProvider != null) {
                try {
                    MouseDebug.LogExitBootServicesProvider(
                        _uefiProvider.ProviderName, 
                        _uefiProvider.BufferedEventCount);
                    
                    _uefiProvider.OnExitBootServices();
                } catch {
                    MouseDebug.LogError("MouseInputManager", 
                        "Exception notifying UEFI provider");
                }
            }
            
            // Future: Notify other providers here
            // if (_hidProvider != null) { ... }
            // if (_ps2Provider != null) { ... }
            
            // Debug: Log ExitBootServices complete
            MouseDebug.LogExitBootServicesComplete();
            
            DebugLog("[MouseInputManager] ExitBootServices notification complete");
        }

        /// <summary>
        /// Shutdown the input manager and all providers
        /// </summary>
        public static void Shutdown() {
            if (!_isInitialized) return;
            
            // Shutdown UEFI provider
            if (_uefiProvider != null) {
                try {
                    _uefiProvider.Shutdown();
                } catch {
                    // Ignore errors during shutdown
                }
                _uefiProvider = null;
            }
            
            // Future: Shutdown other providers here
            
            _providerCount = 0;
            _isInitialized = false;
            
            DebugLog("[MouseInputManager] Shutdown complete");
        }

        /// <summary>
        /// Configure filtering parameters
        /// Forwards to MouseEventDispatcher for unified configuration
        /// </summary>
        public static void ConfigureFiltering(bool enabled, float sensitivity = 1.0f, 
                                              int noiseThreshold = 2, int maxDelta = 50) {
            _enableFiltering = enabled;
            _sensitivity = sensitivity;
            _noiseThreshold = noiseThreshold;
            _maxDeltaPerPoll = maxDelta;
            
            // Also configure the dispatcher
            MouseEventDispatcher.EnableFiltering = enabled;
            MouseEventDispatcher.Sensitivity = sensitivity;
            MouseEventDispatcher.NoiseThreshold = noiseThreshold;
            MouseEventDispatcher.MaxDeltaPerUpdate = maxDelta;
        }

        /// <summary>
        /// Get diagnostic information
        /// </summary>
        public static string GetDiagnostics() {
            string diag = "[MouseInputManager Diagnostics]\n";
            diag += "  Initialized: " + _isInitialized + "\n";
            diag += "  ExitBootServices: " + _exitBootServicesOccurred + "\n";
            diag += "  Total Polls: " + TotalPolls + "\n";
            diag += "  Total Events: " + TotalEvents + "\n";
            diag += "  Last Provider: " + LastActiveProvider + "\n";
            diag += "  Provider Count: " + _providerCount + "\n";
            
            if (_uefiProvider != null) {
                diag += "  UEFI Provider:\n";
                diag += "    Name: " + _uefiProvider.ProviderName + "\n";
                diag += "    Available: " + _uefiProvider.IsAvailable + "\n";
                diag += "    Priority: " + _uefiProvider.Priority + "\n";
                diag += "    Polls: " + _uefiProvider.PollCount + "\n";
                diag += "    Events: " + _uefiProvider.EventCount + "\n";
                diag += "    Errors: " + _uefiProvider.ErrorCount + "\n";
                diag += "    Buffered: " + _uefiProvider.BufferedEventCount + "\n";
                diag += "    Dropped: " + _uefiProvider.DroppedEventCount + "\n";
                diag += _uefiProvider.GetBufferDiagnostics();
            }
            
            // Include dispatcher diagnostics
            diag += "\n";
            diag += MouseEventDispatcher.GetDiagnostics();
            
            // Include ExitBootServices rules diagnostics
            diag += "\n";
            diag += ExitBootServicesRules.GetDiagnostics();
            
            return diag;
        }

        /// <summary>
        /// Debug logging helper - uses for loop to avoid IEnumerable dependency
        /// </summary>
        private static void DebugLog(string message) {
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
