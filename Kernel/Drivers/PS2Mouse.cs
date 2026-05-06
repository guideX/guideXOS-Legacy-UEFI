using guideXOS.GUI;
using guideXOS.Misc;
using System;
using System.Windows.Forms;
using static Native;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// PS/2 Mouse Driver
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION SUMMARY:
    /// -----------------------
    /// 1. BIOS-ERA ONLY (will NOT work in UEFI):
    ///    - Direct port I/O to 0x60/0x64 (i8042 controller)
    ///    - IRQ12 interrupt registration (0x2C vector)
    ///    - Assumes PS/2 controller is pre-initialized by BIOS
    ///    - Relies on legacy PIC for interrupt delivery
    /// 
    /// 2. UEFI-INCOMPATIBLE:
    ///    - Initialize() method: Uses raw port I/O commands (0xA8, 0xD4, etc.)
    ///    - WriteRegister(): Sends bytes directly to i8042 data port
    ///    - ReadRegister(): Reads from i8042 output buffer
    ///    - OnInterrupt(): Registered via legacy IRQ mechanism
    ///    
    ///    WHY: UEFI systems may not have PS/2 hardware. Even if present:
    ///    - UEFI firmware does NOT initialize the i8042 controller
    ///    - UEFI uses EFI_SIMPLE_POINTER_PROTOCOL instead
    ///    - Direct port I/O may access non-existent hardware or conflict with UEFI
    ///    - Legacy PIC may be disabled/replaced by APIC in UEFI mode
    /// 
    /// 3. POTENTIALLY REUSABLE LOGIC:
    ///    - Packet parsing: Phase state machine (MData[0..3])
    ///    - Sign bit extraction from byte 0 (bits 4,5)
    ///    - Button state debouncing logic
    ///    - Touchpad sensitivity filtering
    ///    - Delta clamping and noise rejection
    ///    - Coordinate transformation (Y-axis inversion)
    ///    - Control.MousePosition/MouseButtons update pattern
    /// 
    /// RECOMMENDED UEFI APPROACH:
    /// --------------------------
    /// Option A (Simple): Use USB HID mouse via existing HID.cs + EHCI.cs
    /// Option B (Full): Poll EFI_SIMPLE_POINTER_PROTOCOL from bootloader,
    ///                  pass events via shared memory or dedicated interrupt
    /// Option C (Hybrid): Store UEFI mouse protocol pointer in bootInfo,
    ///                    poll it from main loop (no interrupts needed)
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class PS2Mouse {
        // UEFI-INCOMPATIBLE: Direct i8042 controller ports
        // These only work when BIOS has initialized the PS/2 controller
        private const byte Data = 0x60;    // i8042 data port - BIOS-ERA ONLY
        private const byte Command = 0x64; // i8042 command port - BIOS-ERA ONLY

        // UEFI-INCOMPATIBLE: PS/2 mouse commands sent via i8042
        // These commands assume a PS/2 mouse is connected and controller is initialized
        private const byte SetDefaults = 0xF6;          // BIOS-ERA ONLY
        private const byte EnableDataReporting = 0xF4;  // BIOS-ERA ONLY

        // POTENTIALLY REUSABLE: Packet parsing state machine
        // This logic can be extracted and reused for any 4-byte mouse packet protocol
        private static int Phase = 0;
        public static byte[] MData;  // 4-byte packet buffer
        private static int aX;
        private static int aY;
        public static int DeltaZ;

        public static int ScreenWidth = 0;
        public static int ScreenHeight = 0;
        
        // Debug counters
        public static int InterruptCount = 0;
        public static int PacketCount = 0;
        
        // POTENTIALLY REUSABLE: Touchpad sensitivity and filtering
        // These parameters and filtering logic can be reused for any relative mouse input
        public static float TouchpadSensitivity = 0.25f; // Reduce touchpad sensitivity (was 1.0)
        public static int NoiseThreshold = 2; // Ignore tiny movements under this value
        public static int MaxDeltaPerPacket = 50; // Prevent huge jumps (touchpad noise)
        public static bool EnableTouchpadFiltering = true; // Master toggle for touchpad fixes
        
        // POTENTIALLY REUSABLE: Button debouncing to prevent phantom clicks
        // This pattern can be applied to any input source with noisy button state
        private static byte lastButtonState = 0;
        private static int buttonStableCount = 0;
        private static int ButtonDebounceThreshold = 2; // Require 2 consistent packets before accepting button change

        /// <summary>
        /// Initialize PS/2 Mouse
        /// 
        /// UEFI-INCOMPATIBLE: This entire method assumes BIOS-initialized PS/2 hardware.
        /// 
        /// Entry Points that depend on this:
        /// - EntryPoint.Entry() (Legacy Multiboot) -> PS2Controller.Initialize() -> PS2Mouse.Initialize()
        /// - Program.KMain() calls PS2Mouse.Initialize() in Legacy mode only
        /// 
        /// WHY this doesn't work in UEFI:
        /// 1. Port 0x64 command 0xA8 (enable aux device) assumes i8042 exists and is mapped
        /// 2. UEFI may not have initialized the PS/2 controller at all
        /// 3. IRQ12 (0x2C) registration assumes legacy PIC is active and configured
        /// 4. Many UEFI systems have no physical PS/2 ports (USB-only)
        /// 
        /// For UEFI: Use HID.cs USB mouse or implement EFI_SIMPLE_POINTER_PROTOCOL polling
        /// </summary>
        public static void Initialize() {
            // Debug: entering PS2Mouse.Initialize
            BootConsole.WriteLine("MS1");
            
            MData = new byte[4];
            Phase = 1;

            BootConsole.WriteLine("MS2");

            // UEFI-INCOMPATIBLE: Legacy IRQ12 registration via PIC
            // This only works when PIC is enabled and IRQ12 is unmasked
            Interrupts.EnableInterrupt(0x2c, &OnInterrupt);

            BootConsole.WriteLine("MS3");

            // Use default values - skip UISettings access during early boot
            EnableTouchpadFiltering = true;
            TouchpadSensitivity = 0.25f;
            NoiseThreshold = 2;
            MaxDeltaPerPacket = 50;
            ButtonDebounceThreshold = 2;

            BootConsole.WriteLine("MS4");

            // BIOS-ERA ASSUMPTION: PS/2 controller is pre-configured
            // UEFI-INCOMPATIBLE: UEFI firmware does NOT configure i8042
            // The comment "UEFI firmware likely already configured" is INCORRECT for pure UEFI
            // UEFI systems use EFI_SIMPLE_POINTER_PROTOCOL, not raw PS/2

            // Wait for controller input buffer to be empty before sending commands
            // UEFI-INCOMPATIBLE: Polling port 0x64 assumes i8042 exists
            WaitForInputBuffer();
            // UEFI-INCOMPATIBLE: Command 0xA8 to enable aux device
            Out8(Command, 0xA8); // Enable auxiliary device (mouse port)
            
            // Debug: after enable aux
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'a');
            
            // Simple mouse initialization - just enable data reporting
            for (int i = 0; i < 1000; i++) { } // Small delay
            WriteRegister(SetDefaults);
            
            // Debug: after set defaults
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'b');
            
            for (int i = 0; i < 1000; i++) { } // Small delay
            WriteRegister(EnableDataReporting);
            
            // Debug: after enable reporting
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'c');

            Control.MouseButtons = MouseButtons.None;
            DeltaZ = 0;

            BootConsole.WriteLine("MS6");
        }
        
        /// <summary>
        /// Enable full mouse processing after boot is complete
        /// </summary>
        public static void EnableFullProcessing() {
            _initComplete = true;
        }
        
        /// <summary>
        /// Wait for PS/2 controller input buffer to be empty (ready to receive command)
        /// 
        /// UEFI-INCOMPATIBLE: Polls i8042 status register (port 0x64)
        /// This assumes the i8042 controller exists and is memory-mapped.
        /// On UEFI systems without PS/2, this may:
        /// - Read garbage from unmapped I/O space
        /// - Hang if the port doesn't respond
        /// - Conflict with UEFI's own hardware management
        /// </summary>
        private static void WaitForInputBuffer() {
            int timeout = 10000;
            // UEFI-INCOMPATIBLE: Port 0x64 read assumes i8042 exists
            while ((In8(0x64) & 0x02) != 0 && timeout > 0) {
                timeout--;
            }
        }

        /// <summary>
        /// Send a command byte to the PS/2 mouse
        /// 
        /// UEFI-INCOMPATIBLE: Uses i8042 command 0xD4 to route data to mouse port
        /// This direct hardware access will not work without BIOS initialization.
        /// </summary>
        public static void WriteRegister(byte value) {
            // UEFI-INCOMPATIBLE: All port I/O here assumes i8042 is present
            WaitForInputBuffer();
            Out8(Command, 0xD4); // Tell controller to send to mouse - BIOS-ERA ONLY
            WaitForInputBuffer();
            Out8(Data, value);   // Send command to mouse - BIOS-ERA ONLY
            // Wait for ACK with timeout
            for (int i = 0; i < 10000; i++) {
                if ((In8(0x64) & 0x01) != 0) { // Output buffer full
                    In8(Data); // Read and discard ACK
                    break;
                }
            }
        }

        /// <summary>
        /// Read a byte from the PS/2 mouse
        /// 
        /// UEFI-INCOMPATIBLE: Reads directly from i8042 data port
        /// </summary>
        public static byte ReadRegister() {
            // UEFI-INCOMPATIBLE: Port 0x64/0x60 access
            for (int i = 0; i < 10000; i++) {
                if ((In8(0x64) & 0x01) != 0) {
                    return In8(Data);
                }
            }
            return 0; // Timeout
        }

        // Flag to indicate if full mouse processing is enabled
        private static bool _initComplete = false;

        /// <summary>
        /// PS/2 Mouse interrupt handler (IRQ12 / vector 0x2C)
        /// 
        /// UEFI-INCOMPATIBLE: This handler is registered via legacy PIC IRQ system.
        /// In UEFI, interrupts are typically disabled or routed through APIC.
        /// Even with APIC, there's no guarantee IRQ12 corresponds to mouse.
        /// 
        /// POTENTIALLY REUSABLE: The packet parsing logic inside (Phase state machine,
        /// sign bit handling, button debouncing, filtering) can be extracted and
        /// reused for any mouse input source that provides similar delta/button data.
        /// </summary>
        public static void OnInterrupt() {
            // UEFI-INCOMPATIBLE: Reads from i8042 data port
            byte D = In8(Data);
            InterruptCount++; // Count all interrupts for debugging
            
            // During early boot, just read and discard data
            // This prevents crashes from accessing uninitialized objects
            if (!_initComplete) {
                return;
            }
            
            if (VMwareTools.Available) return;

            if (Phase == 0) {
                if (D == 0xfa)
                    Phase = 1;
            } else if (Phase == 1) {
                if ((D & 8) == 8) {
                    MData[0] = D;
                    Phase = 2;
                }
            } else if (Phase == 2) {
                MData[1] = D;
                Phase = 3;
            } else if (Phase == 3) {
                MData[2] = D;
                Phase = 1;
                PacketCount++; // Count completed packets

                // POTENTIALLY REUSABLE: Packet validation
                // Bit 3 should always be set in PS/2 mouse byte 0
                if ((MData[0] & 0x08) == 0) {
                    // Invalid packet, ignore it
                    return;
                }

                // POTENTIALLY REUSABLE: Sign bit extraction for 2's complement deltas
                // This pattern applies to any protocol using 9-bit signed deltas
                bool xSign = (MData[0] & 0x10) != 0; // Bit 4: X sign (1 = negative)
                bool ySign = (MData[0] & 0x20) != 0; // Bit 5: Y sign (1 = negative)
                
                // Button debouncing to prevent phantom clicks on touchpads
                byte currentButtonState = (byte)(MData[0] & 0x07); // Extract button bits (bits 0-2)
                
                if (currentButtonState == lastButtonState) {
                    buttonStableCount++;
                } else {
                    buttonStableCount = 0;
                    lastButtonState = currentButtonState;
                }
                
                // Only update buttons if state has been stable for debounce threshold
                if (buttonStableCount >= ButtonDebounceThreshold || !EnableTouchpadFiltering) {
                    Control.MouseButtons = MouseButtons.None;
                    if ((currentButtonState & 0x01) != 0) {
                        Control.MouseButtons |= MouseButtons.Left;
                    }
                    if ((currentButtonState & 0x02) != 0) {
                        Control.MouseButtons |= MouseButtons.Right;
                    }
                    if ((currentButtonState & 0x04) != 0)
                    {
                        Control.MouseButtons |= MouseButtons.Middle;
                    }
                }

                // Read raw deltas as unsigned values
                aX = MData[1];
                aY = MData[2];
                
                // Apply sign based on the sign bits from byte 0
                if (xSign) {
                    aX = aX - 256; // Convert to negative if sign bit is set
                }
                if (ySign) {
                    aY = aY - 256; // Convert to negative if sign bit is set
                }
                
                aY = -aY; // Invert Y axis for screen coordinates

                // Apply touchpad filtering if enabled
                if (EnableTouchpadFiltering) {
                    // FIRST: Clamp extreme values BEFORE any other processing (touchpad noise can cause huge jumps)
                    aX = Math.Clamp(aX, -MaxDeltaPerPacket, MaxDeltaPerPacket);
                    aY = Math.Clamp(aY, -MaxDeltaPerPacket, MaxDeltaPerPacket);
                    
                    // SECOND: Filter out noise (very small movements) - do this before scaling
                    if (Math.Abs(aX) < NoiseThreshold) aX = 0;
                    if (Math.Abs(aY) < NoiseThreshold) aY = 0;
                    
                    // THIRD: Apply touchpad sensitivity scaling
                    aX = (int)(aX * TouchpadSensitivity);
                    aY = (int)(aY * TouchpadSensitivity);
                    
                    // FOURTH: Clamp again after scaling to ensure no overflow
                    aX = Math.Clamp(aX, -MaxDeltaPerPacket, MaxDeltaPerPacket);
                    aY = Math.Clamp(aY, -MaxDeltaPerPacket, MaxDeltaPerPacket);
                }

                DeltaZ = 0;

                // Additional sanity check: if both deltas are at their maximum (likely corrupted packet), ignore movement
                if (EnableTouchpadFiltering && Math.Abs(aX) >= MaxDeltaPerPacket && Math.Abs(aY) >= MaxDeltaPerPacket) {
                    // This is likely a corrupted packet causing corner jumping, ignore it
                    return;
                }

                // POTENTIALLY REUSABLE: Final position update with bounds clamping
                // This pattern can be used for any relative mouse input source
                Control.MousePosition.X = Math.Clamp(Control.MousePosition.X + aX, 0, Framebuffer.Width);
                Control.MousePosition.Y = Math.Clamp(Control.MousePosition.Y + aY, 0, Framebuffer.Height);
            }
        }
        
        // ============================================================================
        // UEFI MIGRATION NOTES
        // ============================================================================
        // 
        // To support mouse input in UEFI mode, consider these approaches:
        // 
        // 1. USB HID Mouse (RECOMMENDED - already partially implemented):
        //    - HID.cs already has GetMouse() for USB HID devices
        //    - USB.cs polls HID devices in a background thread
        //    - Need to ensure EHCI/XHCI is initialized in UEFI mode
        //    - Reuse filtering logic from this file
        // 
        // 2. EFI_SIMPLE_POINTER_PROTOCOL (for UEFI firmware mouse):
        //    - Bootloader stores protocol pointer in UefiBootInfo
        //    - Main loop calls into protocol to get absolute/relative position
        //    - No interrupts needed - pure polling
        //    - Example structure:
        //      struct EfiPointerState { int RelX, RelY, RelZ; bool LBtn, RBtn; }
        // 
        // 3. Hybrid approach:
        //    - Use USB HID when available (modern hardware)
        //    - Fall back to EFI_SIMPLE_POINTER_PROTOCOL
        //    - Never use PS/2 ports in UEFI mode
        // 
        // The reusable components from this driver:
        //    - Filtering constants (TouchpadSensitivity, NoiseThreshold, etc.)
        //    - Button debouncing pattern
        //    - Delta clamping logic
        //    - Control.MousePosition update pattern
        // ============================================================================
    }
}