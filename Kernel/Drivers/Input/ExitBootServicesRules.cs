namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// ExitBootServices Rules and Enforcement for Mouse Input
    /// 
    /// ============================================================================
    /// UEFI BOOT SERVICES LIFECYCLE
    /// ============================================================================
    /// 
    /// UEFI firmware provides two types of services:
    /// 
    /// 1. BOOT SERVICES (available only before ExitBootServices):
    ///    - Memory allocation (AllocatePool, AllocatePages)
    ///    - Protocol location (LocateProtocol, HandleProtocol)
    ///    - Event creation/signaling (CreateEvent, SetTimer)
    ///    - EFI_SIMPLE_POINTER_PROTOCOL access
    ///    - EFI_ABSOLUTE_POINTER_PROTOCOL access
    ///    - Console I/O protocols
    /// 
    /// 2. RUNTIME SERVICES (available after ExitBootServices):
    ///    - GetTime/SetTime
    ///    - GetVariable/SetVariable
    ///    - ResetSystem
    ///    - Virtual address conversion
    /// 
    /// ExitBootServices is called when the OS takes control of the machine.
    /// After this call:
    /// - ALL Boot Services become INVALID
    /// - Calling any Boot Service causes UNDEFINED BEHAVIOR (crash/hang)
    /// - UEFI firmware reclaims boot-time memory
    /// - Protocol pointers become INVALID
    /// 
    /// ============================================================================
    /// MOUSE INPUT RULES
    /// ============================================================================
    /// 
    /// BEFORE ExitBootServices:
    /// -------------------------
    /// ? Poll EFI_SIMPLE_POINTER_PROTOCOL freely
    /// ? Buffer mouse events in kernel-allocated memory
    /// ? Initialize all mouse-related data structures
    /// ? Register UEFI pointer provider with MouseInputManager
    /// 
    /// AT ExitBootServices:
    /// --------------------
    /// 1. Call MouseInputManager.OnExitBootServices()
    /// 2. All providers mark themselves unavailable
    /// 3. UEFI protocol pointers are invalidated (but not nulled)
    /// 4. Buffered events remain valid (in kernel memory)
    /// 
    /// AFTER ExitBootServices:
    /// -----------------------
    /// ? NEVER call EFI_SIMPLE_POINTER_PROTOCOL.GetState()
    /// ? NEVER call EFI_SIMPLE_POINTER_PROTOCOL.Reset()
    /// ? NEVER dereference protocol function pointers
    /// ? Consume buffered events from MouseEventRingBuffer
    /// ? Switch to USB HID or other post-boot input source
    /// ? Continue processing events through MouseEventDispatcher
    /// 
    /// ============================================================================
    /// DATA STRUCTURES THAT MUST PERSIST
    /// ============================================================================
    /// 
    /// These structures are allocated in KERNEL memory (not UEFI memory) and
    /// remain valid after ExitBootServices:
    /// 
    /// 1. MouseEventRingBuffer (lock-free SPSC buffer)
    ///    - Fixed-size array allocated at construction
    ///    - Contains MouseEvent structs (value types)
    ///    - Safe to read/write after ExitBootServices
    /// 
    /// 2. MouseInputManager state
    ///    - Provider references (concrete types, not interface)
    ///    - Configuration (sensitivity, thresholds)
    ///    - Statistics counters
    /// 
    /// 3. MouseEventDispatcher state
    ///    - Event history array
    ///    - Filtering configuration
    ///    - Statistics counters
    /// 
    /// 4. Control.MousePosition / Control.MouseButtons
    ///    - Global mouse state
    ///    - Updated by dispatcher
    /// 
    /// ============================================================================
    /// COMPONENTS THAT MUST STOP
    /// ============================================================================
    /// 
    /// 1. UefiPointerProvider.Poll()
    ///    - Must not call _protocol->GetState() after ExitBootServices
    ///    - Guard: Check _isAvailable before any protocol access
    /// 
    /// 2. UefiPointerProvider._protocol pointer
    ///    - Becomes invalid (points to reclaimed memory)
    ///    - Must not be dereferenced
    ///    - Guard: _isAvailable flag prevents access
    /// 
    /// 3. Any Boot Services calls in mouse code
    ///    - AllocatePool (use kernel allocator instead)
    ///    - Events/timers (use kernel timer instead)
    /// 
    /// ============================================================================
    /// </summary>
    public static class ExitBootServicesRules {
        
        // ====================================================================
        // STATE TRACKING
        // ====================================================================
        
        /// <summary>
        /// True after ExitBootServices has been called.
        /// Once set, NEVER cleared.
        /// </summary>
        private static bool _exitBootServicesOccurred = false;
        
        /// <summary>
        /// Timestamp (kernel ticks) when ExitBootServices occurred.
        /// </summary>
        private static ulong _exitBootServicesTimestamp = 0;
        
        /// <summary>
        /// Number of Boot Services access attempts blocked after ExitBootServices.
        /// Non-zero indicates a bug in the code.
        /// </summary>
        private static ulong _blockedAccessAttempts = 0;
        
        // ====================================================================
        // PUBLIC PROPERTIES
        // ====================================================================
        
        /// <summary>
        /// True if ExitBootServices has been called.
        /// Check this BEFORE any UEFI protocol access.
        /// </summary>
        public static bool HasExitedBootServices => _exitBootServicesOccurred;
        
        /// <summary>
        /// Timestamp when ExitBootServices occurred (0 if not yet).
        /// </summary>
        public static ulong ExitBootServicesTimestamp => _exitBootServicesTimestamp;
        
        /// <summary>
        /// Number of blocked access attempts (should be 0 in correct code).
        /// </summary>
        public static ulong BlockedAccessAttempts => _blockedAccessAttempts;
        
        // ====================================================================
        // TRANSITION API
        // ====================================================================
        
        /// <summary>
        /// Mark that ExitBootServices has occurred.
        /// Called by kernel during boot sequence.
        /// This is a ONE-WAY transition - cannot be undone.
        /// </summary>
        public static void MarkExitBootServices() {
            if (_exitBootServicesOccurred) {
                // Already marked - this is a bug (double call)
                MouseDebug.LogError("EBS", "ExitBootServices marked twice!");
                return;
            }
            
            _exitBootServicesOccurred = true;
            
            // Try to get timestamp
            try {
                _exitBootServicesTimestamp = Timer.Ticks;
            } catch {
                _exitBootServicesTimestamp = 0;
            }
            
            MouseDebug.LogExitBootServicesStart();
            
            // Notify MouseInputManager
            MouseInputManager.OnExitBootServices();
            
            MouseDebug.LogExitBootServicesComplete();
        }
        
        // ====================================================================
        // GUARD METHODS
        // ====================================================================
        
        /// <summary>
        /// Assert that Boot Services are still available.
        /// Call this before any UEFI protocol access.
        /// </summary>
        /// <param name="operation">Description of the operation being attempted</param>
        /// <returns>True if Boot Services are available, false if not</returns>
        public static bool AssertBootServicesAvailable(string operation) {
            if (_exitBootServicesOccurred) {
                _blockedAccessAttempts++;
                
                MouseDebug.LogError("EBS-GUARD", 
                    "BLOCKED: " + operation + " (attempt #" + _blockedAccessAttempts + ")");
                
                // In debug builds, this could panic
                // In release builds, we just log and return false
#if DEBUG
                // Could add a panic here for debug builds:
                // Panic.Error("Boot Services access after ExitBootServices: " + operation);
#endif
                
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if a UEFI protocol pointer is safe to use.
        /// Returns false if ExitBootServices has occurred OR pointer is null.
        /// </summary>
        /// <param name="protocolPtr">The protocol pointer to check</param>
        /// <param name="protocolName">Name for logging</param>
        /// <returns>True if safe to use, false otherwise</returns>
        public static unsafe bool IsProtocolSafe(void* protocolPtr, string protocolName) {
            if (_exitBootServicesOccurred) {
                _blockedAccessAttempts++;
                
                MouseDebug.LogError("EBS-GUARD", 
                    "Protocol access blocked: " + protocolName + 
                    " (ExitBootServices occurred)");
                
                return false;
            }
            
            if (protocolPtr == null) {
                MouseDebug.LogError("EBS-GUARD", 
                    "Protocol is null: " + protocolName);
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Check if a UEFI function pointer is safe to call.
        /// </summary>
        public static unsafe bool IsFunctionSafe(void* funcPtr, string functionName) {
            if (_exitBootServicesOccurred) {
                _blockedAccessAttempts++;
                
                MouseDebug.LogError("EBS-GUARD", 
                    "Function call blocked: " + functionName + 
                    " (ExitBootServices occurred)");
                
                return false;
            }
            
            if (funcPtr == null) {
                MouseDebug.LogError("EBS-GUARD", 
                    "Function pointer is null: " + functionName);
                return false;
            }
            
            return true;
        }
        
        // ====================================================================
        // DIAGNOSTICS
        // ====================================================================
        
        /// <summary>
        /// Get diagnostic string for ExitBootServices state.
        /// </summary>
        public static string GetDiagnostics() {
            return "[ExitBootServices Rules]\n" +
                   "  HasExitedBootServices: " + _exitBootServicesOccurred + "\n" +
                   "  ExitTimestamp: " + _exitBootServicesTimestamp + "\n" +
                   "  BlockedAttempts: " + _blockedAccessAttempts + "\n" +
                   "\n" +
                   "  Rules Enforcement:\n" +
                   "    - UEFI protocols: " + (_exitBootServicesOccurred ? "BLOCKED" : "ALLOWED") + "\n" +
                   "    - Boot Services: " + (_exitBootServicesOccurred ? "BLOCKED" : "ALLOWED") + "\n" +
                   "    - Buffered events: ALWAYS ALLOWED\n" +
                   "    - USB HID: ALWAYS ALLOWED\n";
        }
        
        /// <summary>
        /// Reset blocked attempts counter (for testing).
        /// </summary>
        public static void ResetBlockedAttempts() {
            _blockedAccessAttempts = 0;
        }
    }
}
