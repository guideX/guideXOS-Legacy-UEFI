// #define MOUSE_DEBUG  // Uncomment to enable mouse pipeline debug logging

using guideXOS.Misc;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Debug logging for mouse input pipeline
    /// 
    /// ============================================================================
    /// COMPILE-TIME DEBUG FLAG
    /// ============================================================================
    /// 
    /// To enable debug logging, uncomment the #define MOUSE_DEBUG at the top
    /// of this file, or add MOUSE_DEBUG to project-wide defines.
    /// 
    /// When disabled (default):
    /// - All Log* methods are empty stubs (minimal overhead)
    /// - No serial output
    /// - Statistics are still tracked
    /// 
    /// When enabled:
    /// - Detailed logging to serial port (0x3F8)
    /// - Protocol discovery success/failure
    /// - Pointer state changes
    /// - Event buffer overruns
    /// - ExitBootServices transitions
    /// 
    /// LOG LEVELS:
    /// - INFO: Normal operation milestones
    /// - WARN: Unexpected but recoverable conditions
    /// - ERROR: Failures that affect functionality
    /// - EVENT: Individual mouse events (very verbose)
    /// - STATE: State machine transitions
    /// 
    /// ============================================================================
    /// </summary>
    public static class MouseDebug {
        
        // Statistics (always tracked, even without debug output)
        private static ulong _protocolDiscoveryAttempts = 0;
        private static ulong _protocolDiscoverySuccesses = 0;
        private static ulong _protocolDiscoveryFailures = 0;
        private static ulong _stateChanges = 0;
        private static ulong _bufferOverruns = 0;
        private static ulong _exitBootServicesCount = 0;
        private static ulong _eventsLogged = 0;

        /// <summary>
        /// Whether debug logging is enabled at compile time
        /// </summary>
        public static bool IsEnabled {
            get {
#if MOUSE_DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        // ====================================================================
        // PROTOCOL DISCOVERY LOGGING
        // ====================================================================

        /// <summary>
        /// Log protocol discovery attempt
        /// </summary>
        public static void LogProtocolDiscovery(string protocolName, bool success, string details = null) {
            _protocolDiscoveryAttempts++;
            if (success) {
                _protocolDiscoverySuccesses++;
            } else {
                _protocolDiscoveryFailures++;
            }
            
#if MOUSE_DEBUG
            if (success) {
                LogInternal("PROTO", "FOUND: " + protocolName);
            } else {
                LogInternal("PROTO", "NOT FOUND: " + protocolName);
            }
            if (details != null) {
                LogInternal("PROTO", "  Details: " + details);
            }
#endif
        }

        /// <summary>
        /// Log protocol initialization
        /// </summary>
        public static void LogProtocolInit(string protocolName, bool success, string reason = null) {
#if MOUSE_DEBUG
            if (success) {
                LogInternal("INIT", "SUCCESS: " + protocolName);
            } else {
                LogInternal("INIT", "FAILED: " + protocolName);
            }
            if (reason != null) {
                LogInternal("INIT", "  Reason: " + reason);
            }
#endif
        }

        /// <summary>
        /// Log protocol pointer validation
        /// </summary>
        public static void LogProtocolValidation(string protocolName, ulong address, bool valid) {
#if MOUSE_DEBUG
            string addrStr = "0x" + address.ToString("X16");
            if (valid) {
                LogInternal("VALID", protocolName + " @ " + addrStr + " OK");
            } else {
                LogInternal("VALID", protocolName + " @ " + addrStr + " INVALID");
            }
#endif
        }

        // ====================================================================
        // POINTER STATE LOGGING
        // ====================================================================

        /// <summary>
        /// Log pointer state change (position/buttons)
        /// </summary>
        public static void LogStateChange(int deltaX, int deltaY, int deltaZ, 
                                          System.Windows.Forms.MouseButtons buttons,
                                          MouseInputSource source) {
            _stateChanges++;
            
#if MOUSE_DEBUG
            // Only log if there's actual movement or button activity
            if (deltaX == 0 && deltaY == 0 && deltaZ == 0 && 
                buttons == System.Windows.Forms.MouseButtons.None) {
                return;
            }
            
            string srcName = GetSourceShortName(source);
            string btnStr = GetButtonString(buttons);
            
            LogInternal("STATE", srcName + " dX=" + deltaX + " dY=" + deltaY + 
                       " dZ=" + deltaZ + " btn=" + btnStr);
#endif
        }

        /// <summary>
        /// Log absolute position change
        /// </summary>
        public static void LogAbsolutePosition(int absX, int absY, 
                                               System.Windows.Forms.MouseButtons buttons,
                                               MouseInputSource source) {
            _stateChanges++;
            
#if MOUSE_DEBUG
            string srcName = GetSourceShortName(source);
            string btnStr = GetButtonString(buttons);
            
            LogInternal("STATE", srcName + " ABS X=" + absX + " Y=" + absY + " btn=" + btnStr);
#endif
        }

        /// <summary>
        /// Log button state change
        /// </summary>
        public static void LogButtonChange(System.Windows.Forms.MouseButtons oldState,
                                           System.Windows.Forms.MouseButtons newState,
                                           MouseInputSource source) {
#if MOUSE_DEBUG
            if (oldState == newState) return;
            
            string srcName = GetSourceShortName(source);
            string oldStr = GetButtonString(oldState);
            string newStr = GetButtonString(newState);
            
            LogInternal("BTN", srcName + " " + oldStr + " -> " + newStr);
#endif
        }

        // ====================================================================
        // EVENT BUFFER LOGGING
        // ====================================================================

        /// <summary>
        /// Log event enqueue
        /// </summary>
        public static void LogEventEnqueue(int bufferCount, int bufferCapacity, 
                                           MouseInputSource source) {
            _eventsLogged++;
            
#if MOUSE_DEBUG
            // Only log periodically to avoid flooding
            if (_eventsLogged % 100 == 0) {
                string srcName = GetSourceShortName(source);
                LogInternal("BUF", "ENQ #" + _eventsLogged + " from " + srcName + 
                           " [" + bufferCount + "/" + bufferCapacity + "]");
            }
#endif
        }

        /// <summary>
        /// Log event buffer overrun (oldest event dropped)
        /// </summary>
        public static void LogBufferOverrun(int bufferCapacity, ulong totalDropped) {
            _bufferOverruns++;
            
#if MOUSE_DEBUG
            LogInternal("WARN", "BUFFER OVERRUN! Cap=" + bufferCapacity + 
                       " Dropped=" + totalDropped);
#endif
        }

        /// <summary>
        /// Log event dequeue
        /// </summary>
        public static void LogEventDequeue(int bufferCount, bool success) {
#if MOUSE_DEBUG
            // Only log failures or periodic successes
            if (!success) {
                LogInternal("BUF", "DEQ failed (empty)");
            }
#endif
        }

        /// <summary>
        /// Log buffer aggregation
        /// </summary>
        public static void LogBufferAggregation(int eventCount, int totalDeltaX, 
                                                int totalDeltaY, bool hasAbsolute) {
#if MOUSE_DEBUG
            LogInternal("BUF", "AGG " + eventCount + " events: dX=" + totalDeltaX + 
                       " dY=" + totalDeltaY + " abs=" + hasAbsolute);
#endif
        }

        // ====================================================================
        // EXIT BOOT SERVICES LOGGING
        // ====================================================================

        /// <summary>
        /// Log ExitBootServices transition start
        /// </summary>
        public static void LogExitBootServicesStart() {
            _exitBootServicesCount++;
            
#if MOUSE_DEBUG
            LogInternal("EBS", "========================================");
            LogInternal("EBS", "ExitBootServices STARTING");
            LogInternal("EBS", "========================================");
#endif
        }

        /// <summary>
        /// Log ExitBootServices provider notification
        /// </summary>
        public static void LogExitBootServicesProvider(string providerName, 
                                                       int bufferedEvents) {
#if MOUSE_DEBUG
            LogInternal("EBS", "Notifying: " + providerName + 
                       " (buffered: " + bufferedEvents + ")");
#endif
        }

        /// <summary>
        /// Log ExitBootServices transition complete
        /// </summary>
        public static void LogExitBootServicesComplete() {
#if MOUSE_DEBUG
            LogInternal("EBS", "========================================");
            LogInternal("EBS", "ExitBootServices COMPLETE");
            LogInternal("EBS", "========================================");
#endif
        }

        // ====================================================================
        // GENERAL LOGGING
        // ====================================================================

        /// <summary>
        /// Log informational message
        /// </summary>
        public static void LogInfo(string component, string message) {
#if MOUSE_DEBUG
            LogInternal("INFO", "[" + component + "] " + message);
#endif
        }

        /// <summary>
        /// Log warning message
        /// </summary>
        public static void LogWarn(string component, string message) {
#if MOUSE_DEBUG
            LogInternal("WARN", "[" + component + "] " + message);
#endif
        }

        /// <summary>
        /// Log error message
        /// </summary>
        public static void LogError(string component, string message) {
#if MOUSE_DEBUG
            LogInternal("ERR", "[" + component + "] " + message);
#endif
        }

        // ====================================================================
        // STATISTICS (always available)
        // ====================================================================

        /// <summary>
        /// Get debug statistics string
        /// </summary>
        public static string GetStatistics() {
#if MOUSE_DEBUG
            bool debugEnabled = true;
#else
            bool debugEnabled = false;
#endif
            return "[MouseDebug Statistics]\n" +
                   "  Debug Enabled: " + debugEnabled + "\n" +
                   "  Protocol Discovery:\n" +
                   "    Attempts: " + _protocolDiscoveryAttempts + "\n" +
                   "    Successes: " + _protocolDiscoverySuccesses + "\n" +
                   "    Failures: " + _protocolDiscoveryFailures + "\n" +
                   "  State Changes: " + _stateChanges + "\n" +
                   "  Buffer Overruns: " + _bufferOverruns + "\n" +
                   "  ExitBootServices: " + _exitBootServicesCount + "\n" +
                   "  Events Logged: " + _eventsLogged + "\n";
        }

        /// <summary>
        /// Reset all statistics
        /// </summary>
        public static void ResetStatistics() {
            _protocolDiscoveryAttempts = 0;
            _protocolDiscoverySuccesses = 0;
            _protocolDiscoveryFailures = 0;
            _stateChanges = 0;
            _bufferOverruns = 0;
            _exitBootServicesCount = 0;
            _eventsLogged = 0;
        }

        // ====================================================================
        // INTERNAL HELPERS
        // ====================================================================

#if MOUSE_DEBUG
        /// <summary>
        /// Internal log output to serial port
        /// </summary>
        private static void LogInternal(string level, string message) {
            if (message == null) return;

            try {
                // Write prefix: [MOUSE:LEVEL]
                WriteSerial('[');
                WriteSerial('M');
                WriteSerial('O');
                WriteSerial('U');
                WriteSerial('S');
                WriteSerial('E');
                WriteSerial(':');
                
                for (int i = 0; i < level.Length && i < 5; i++) {
                    WriteSerial(level[i]);
                }
                
                WriteSerial(']');
                WriteSerial(' ');
                
                // Write message
                for (int i = 0; i < message.Length; i++) {
                    WriteSerial(message[i]);
                }
                
                WriteSerial('\n');
            } catch {
                // Ignore serial errors
            }
        }

        /// <summary>
        /// Write single character to serial port
        /// </summary>
        private static void WriteSerial(char c) {
            // Wait for transmit buffer empty
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)c);
        }

        /// <summary>
        /// Get short source name for compact logging
        /// </summary>
        private static string GetSourceShortName(MouseInputSource source) {
            switch (source) {
                case MouseInputSource.UefiSimplePointer: return "UEFI-S";
                case MouseInputSource.UefiAbsolutePointer: return "UEFI-A";
                case MouseInputSource.LegacyPS2: return "PS2";
                case MouseInputSource.UsbHid: return "USB";
                case MouseInputSource.VMwareBackdoor: return "VMW";
                case MouseInputSource.KeyboardEmulation: return "KBD";
                case MouseInputSource.Aggregated: return "AGG";
                default: return "UNK";
            }
        }

        /// <summary>
        /// Get button state string for logging
        /// </summary>
        private static string GetButtonString(System.Windows.Forms.MouseButtons buttons) {
            if (buttons == System.Windows.Forms.MouseButtons.None) return "---";
            
            string result = "";
            if ((buttons & System.Windows.Forms.MouseButtons.Left) != 0) result += "L";
            else result += "-";
            if ((buttons & System.Windows.Forms.MouseButtons.Middle) != 0) result += "M";
            else result += "-";
            if ((buttons & System.Windows.Forms.MouseButtons.Right) != 0) result += "R";
            else result += "-";
            
            return result;
        }
#endif
    }
}
