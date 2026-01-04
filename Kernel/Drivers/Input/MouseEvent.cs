using System.Windows.Forms;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Mouse input source type identifier
    /// Used to track where events originate for debugging and diagnostics
    /// </summary>
    public enum MouseInputSource : byte {
        /// <summary>Unknown or unspecified source</summary>
        Unknown = 0,
        
        /// <summary>UEFI EFI_SIMPLE_POINTER_PROTOCOL (relative positioning)</summary>
        UefiSimplePointer = 1,
        
        /// <summary>UEFI EFI_ABSOLUTE_POINTER_PROTOCOL (absolute positioning, touchscreen)</summary>
        UefiAbsolutePointer = 2,
        
        /// <summary>Legacy PS/2 mouse via i8042 controller (BIOS-era only)</summary>
        LegacyPS2 = 3,
        
        /// <summary>USB HID boot protocol mouse</summary>
        UsbHid = 4,
        
        /// <summary>VMware backdoor absolute pointer</summary>
        VMwareBackdoor = 5,
        
        /// <summary>Keyboard emulation (arrow keys as mouse)</summary>
        KeyboardEmulation = 6,
        
        /// <summary>Aggregated from multiple sources</summary>
        Aggregated = 7
    }

    /// <summary>
    /// Unified mouse event structure for all input sources
    /// 
    /// ============================================================================
    /// SOURCE-AGNOSTIC DESIGN
    /// ============================================================================
    /// 
    /// This structure normalizes mouse input from ALL sources:
    /// - UEFI Simple Pointer Protocol
    /// - UEFI Absolute Pointer Protocol  
    /// - Legacy PS/2 mouse
    /// - USB HID mouse
    /// - VMware backdoor
    /// - Keyboard emulation
    /// 
    /// The rest of GuideXOS only sees MouseEvent, never raw hardware data.
    /// This enables:
    /// - Easy switching between input sources
    /// - Consistent filtering/processing
    /// - Source-agnostic GUI code
    /// - Unified event buffering
    /// 
    /// REQUIRED FIELDS:
    /// - DeltaX/DeltaY: Movement (relative) or position (absolute)
    /// - Buttons: Bitmask of pressed buttons
    /// - Timestamp: When event was captured (for ordering/debouncing)
    /// - Source: Where event originated (for diagnostics)
    /// 
    /// ============================================================================
    /// </summary>
    public struct MouseEvent {
        /// <summary>
        /// Relative X movement (pixels or protocol-specific units)
        /// Positive = right, Negative = left
        /// For absolute events, this is 0.
        /// </summary>
        public int DeltaX;

        /// <summary>
        /// Relative Y movement (pixels or protocol-specific units)
        /// Positive = down (after coordinate transform), Negative = up
        /// For absolute events, this is 0.
        /// </summary>
        public int DeltaY;

        /// <summary>
        /// Scroll wheel delta
        /// Positive = scroll up, Negative = scroll down
        /// </summary>
        public int DeltaZ;

        /// <summary>
        /// Current button state (flags)
        /// Multiple buttons can be pressed simultaneously.
        /// </summary>
        public MouseButtons Buttons;

        /// <summary>
        /// True if this event contains absolute coordinates
        /// False for relative movement deltas
        /// </summary>
        public bool IsAbsolute;

        /// <summary>
        /// Absolute X position (0-65535 range, scaled to screen)
        /// Only valid when IsAbsolute is true
        /// </summary>
        public int AbsoluteX;

        /// <summary>
        /// Absolute Y position (0-65535 range, scaled to screen)
        /// Only valid when IsAbsolute is true
        /// </summary>
        public int AbsoluteY;

        /// <summary>
        /// Timestamp in timer ticks when this event was captured
        /// Used for:
        /// - Event ordering in buffers
        /// - Button debouncing
        /// - Double-click detection
        /// - Gesture recognition
        /// </summary>
        public ulong Timestamp;

        /// <summary>
        /// Source of this mouse event
        /// Used for debugging, diagnostics, and source-specific handling
        /// </summary>
        public MouseInputSource Source;

        /// <summary>
        /// True if this event contains valid data
        /// False for empty/placeholder events
        /// </summary>
        public bool IsValid;

        /// <summary>
        /// Create an empty/invalid event
        /// </summary>
        public static MouseEvent Empty => new MouseEvent { 
            IsValid = false, 
            Source = MouseInputSource.Unknown 
        };

        /// <summary>
        /// Create a relative movement event from any source
        /// </summary>
        /// <param name="deltaX">X movement (positive = right)</param>
        /// <param name="deltaY">Y movement (positive = down)</param>
        /// <param name="deltaZ">Scroll wheel movement</param>
        /// <param name="buttons">Button state bitmask</param>
        /// <param name="timestamp">Timer ticks when captured</param>
        /// <param name="source">Input source identifier</param>
        public static MouseEvent CreateRelative(
            int deltaX, 
            int deltaY, 
            int deltaZ, 
            MouseButtons buttons, 
            ulong timestamp,
            MouseInputSource source = MouseInputSource.Unknown) {
            return new MouseEvent {
                DeltaX = deltaX,
                DeltaY = deltaY,
                DeltaZ = deltaZ,
                Buttons = buttons,
                IsAbsolute = false,
                AbsoluteX = 0,
                AbsoluteY = 0,
                Timestamp = timestamp,
                Source = source,
                IsValid = true
            };
        }

        /// <summary>
        /// Create an absolute position event from any source
        /// </summary>
        /// <param name="absoluteX">X position (0-65535 range)</param>
        /// <param name="absoluteY">Y position (0-65535 range)</param>
        /// <param name="deltaZ">Scroll wheel movement</param>
        /// <param name="buttons">Button state bitmask</param>
        /// <param name="timestamp">Timer ticks when captured</param>
        /// <param name="source">Input source identifier</param>
        public static MouseEvent CreateAbsolute(
            int absoluteX, 
            int absoluteY, 
            int deltaZ, 
            MouseButtons buttons, 
            ulong timestamp,
            MouseInputSource source = MouseInputSource.Unknown) {
            return new MouseEvent {
                DeltaX = 0,
                DeltaY = 0,
                DeltaZ = deltaZ,
                Buttons = buttons,
                IsAbsolute = true,
                AbsoluteX = absoluteX,
                AbsoluteY = absoluteY,
                Timestamp = timestamp,
                Source = source,
                IsValid = true
            };
        }

        // ====================================================================
        // SOURCE-SPECIFIC FACTORY METHODS
        // ====================================================================
        // These ensure consistent event creation from each source type

        /// <summary>
        /// Create event from UEFI Simple Pointer Protocol data
        /// </summary>
        public static MouseEvent FromUefiSimplePointer(
            int relativeX, int relativeY, int relativeZ,
            bool leftButton, bool rightButton,
            ulong timestamp) {
            MouseButtons buttons = MouseButtons.None;
            if (leftButton) buttons |= MouseButtons.Left;
            if (rightButton) buttons |= MouseButtons.Right;
            
            return CreateRelative(relativeX, relativeY, relativeZ, buttons, timestamp, 
                MouseInputSource.UefiSimplePointer);
        }

        /// <summary>
        /// Create event from UEFI Absolute Pointer Protocol data
        /// </summary>
        public static MouseEvent FromUefiAbsolutePointer(
            int absoluteX, int absoluteY, int absoluteZ,
            bool leftButton, bool rightButton,
            ulong timestamp) {
            MouseButtons buttons = MouseButtons.None;
            if (leftButton) buttons |= MouseButtons.Left;
            if (rightButton) buttons |= MouseButtons.Right;
            
            return CreateAbsolute(absoluteX, absoluteY, absoluteZ, buttons, timestamp,
                MouseInputSource.UefiAbsolutePointer);
        }

        /// <summary>
        /// Create event from Legacy PS/2 mouse packet data
        /// </summary>
        public static MouseEvent FromLegacyPS2(
            int deltaX, int deltaY, sbyte wheel,
            bool leftButton, bool rightButton, bool middleButton,
            ulong timestamp) {
            MouseButtons buttons = MouseButtons.None;
            if (leftButton) buttons |= MouseButtons.Left;
            if (rightButton) buttons |= MouseButtons.Right;
            if (middleButton) buttons |= MouseButtons.Middle;
            
            return CreateRelative(deltaX, deltaY, wheel, buttons, timestamp,
                MouseInputSource.LegacyPS2);
        }

        /// <summary>
        /// Create event from USB HID boot protocol mouse report
        /// </summary>
        public static MouseEvent FromUsbHid(
            sbyte axisX, sbyte axisY, sbyte wheel,
            byte buttonByte,
            ulong timestamp) {
            MouseButtons buttons = MouseButtons.None;
            if ((buttonByte & 0x01) != 0) buttons |= MouseButtons.Left;
            if ((buttonByte & 0x02) != 0) buttons |= MouseButtons.Right;
            if ((buttonByte & 0x04) != 0) buttons |= MouseButtons.Middle;
            
            return CreateRelative(axisX, axisY, wheel, buttons, timestamp,
                MouseInputSource.UsbHid);
        }

        /// <summary>
        /// Create event from VMware backdoor absolute pointer data
        /// </summary>
        public static MouseEvent FromVMwareBackdoor(
            uint x, uint y, sbyte z, uint buttonWord,
            ulong timestamp) {
            MouseButtons buttons = MouseButtons.None;
            // VMware buttons: 0x08 = Middle, 0x10 = Right, 0x20 = Left
            if ((buttonWord & 0x20) != 0) buttons |= MouseButtons.Left;
            if ((buttonWord & 0x10) != 0) buttons |= MouseButtons.Right;
            if ((buttonWord & 0x08) != 0) buttons |= MouseButtons.Middle;
            
            return CreateAbsolute((int)x, (int)y, z, buttons, timestamp,
                MouseInputSource.VMwareBackdoor);
        }

        /// <summary>
        /// Create event from keyboard emulation (arrow keys)
        /// </summary>
        public static MouseEvent FromKeyboardEmulation(
            int deltaX, int deltaY,
            bool leftButton, bool rightButton,
            ulong timestamp) {
            MouseButtons buttons = MouseButtons.None;
            if (leftButton) buttons |= MouseButtons.Left;
            if (rightButton) buttons |= MouseButtons.Right;
            
            return CreateRelative(deltaX, deltaY, 0, buttons, timestamp,
                MouseInputSource.KeyboardEmulation);
        }

        /// <summary>
        /// Create aggregated event from multiple events (for buffer flush)
        /// </summary>
        public static MouseEvent CreateAggregated(
            int totalDeltaX, int totalDeltaY, int totalDeltaZ,
            int absoluteX, int absoluteY, bool hasAbsolute,
            MouseButtons latestButtons,
            ulong latestTimestamp) {
            if (hasAbsolute) {
                return new MouseEvent {
                    DeltaX = 0,
                    DeltaY = 0,
                    DeltaZ = totalDeltaZ,
                    Buttons = latestButtons,
                    IsAbsolute = true,
                    AbsoluteX = absoluteX,
                    AbsoluteY = absoluteY,
                    Timestamp = latestTimestamp,
                    Source = MouseInputSource.Aggregated,
                    IsValid = true
                };
            } else {
                return new MouseEvent {
                    DeltaX = totalDeltaX,
                    DeltaY = totalDeltaY,
                    DeltaZ = totalDeltaZ,
                    Buttons = latestButtons,
                    IsAbsolute = false,
                    AbsoluteX = 0,
                    AbsoluteY = 0,
                    Timestamp = latestTimestamp,
                    Source = MouseInputSource.Aggregated,
                    IsValid = true
                };
            }
        }

        /// <summary>
        /// Get human-readable source name
        /// </summary>
        public string GetSourceName() {
            switch (Source) {
                case MouseInputSource.UefiSimplePointer: return "UEFI Simple Pointer";
                case MouseInputSource.UefiAbsolutePointer: return "UEFI Absolute Pointer";
                case MouseInputSource.LegacyPS2: return "Legacy PS/2";
                case MouseInputSource.UsbHid: return "USB HID";
                case MouseInputSource.VMwareBackdoor: return "VMware Backdoor";
                case MouseInputSource.KeyboardEmulation: return "Keyboard Emulation";
                case MouseInputSource.Aggregated: return "Aggregated";
                default: return "Unknown";
            }
        }
    }
}
