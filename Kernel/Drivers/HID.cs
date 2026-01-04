using guideXOS.Misc;
using System;
using System.Windows.Forms;
using static System.ConsoleKey;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// USB Human Interface Device (HID) Driver
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION SUMMARY:
    /// -----------------------
    /// 1. POTENTIALLY UEFI-COMPATIBLE:
    ///    - GetMouse(): Extracts deltas and buttons from HID report
    ///    - GetKeyboard(): Extracts scancodes from HID report
    ///    - GetHIDPacket(): Generic HID report fetching
    ///    - ConsoleKeys[] translation table
    ///    
    ///    These DO NOT use legacy ports (0x60/0x64) or PIC IRQs.
    ///    They communicate via USB control transfers.
    /// 
    /// 2. DEPENDENCIES (must work for HID to work in UEFI):
    ///    - USB.cs: USB request/response handling
    ///    - EHCI.cs: USB 2.0 host controller driver
    ///    - Hub.cs: USB hub enumeration
    ///    - PCI.cs: Must detect USB controllers
    ///    
    ///    UEFI STATUS: Currently skipped in UEFI mode (Program.KMain)
    ///    but the code itself is NOT inherently UEFI-incompatible.
    /// 
    /// 3. ENTRY POINTS:
    ///    - Program.KMain() -> Hub.Initialize(), HID.Initialize(), EHCI.Initialize()
    ///    - USB.StartPolling() -> USB.LoopPoll() -> USB.OnInterrupt() -> HID.GetMouse/GetKeyboard
    ///    
    ///    Currently gated by: BootConsole.CurrentMode == BootMode.Legacy
    /// 
    /// 4. REUSABLE COMPONENTS:
    ///    - HID report parsing (boot protocol keyboard/mouse)
    ///    - ConsoleKey mapping table
    ///    - Mouse delta extraction pattern
    /// 
    /// RECOMMENDED UEFI APPROACH:
    /// --------------------------
    /// USB HID is the BEST option for UEFI mouse/keyboard:
    /// 1. Enable USB initialization in UEFI mode (requires EHCI/XHCI to work)
    /// 2. EHCI.Initialize() must handle UEFI memory mapping
    /// 3. USB.StartPolling() can run in main thread (no interrupts needed)
    /// 4. Alternative: Use XHCI for USB 3.0 hosts (more common on modern UEFI)
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class HID {
        static USBRequest* _usbRequest;
        
        // POTENTIALLY UEFI-COMPATIBLE: USB HID device references
        public static ConsoleKey[] ConsoleKeys;
        public static USBDevice Mouse;
        public static USBDevice Keyboard;
        public static void Initialize() {
            Mouse = null;
            Keyboard = null;
            _usbRequest = (USBRequest*)Allocator.Allocate((ulong)sizeof(USBRequest));
        }
        /// <summary>
        /// Get HID report packet from USB device
        /// 
        /// POTENTIALLY UEFI-COMPATIBLE: Uses USB control transfers, not legacy ports.
        /// Requires USB stack (EHCI/XHCI) to be working.
        /// </summary>
        public static bool GetHIDPacket(USBDevice device, void* buffer, ushort length) {
            (*_usbRequest).Clean(); 
            _usbRequest->Request = 1; // GET_REPORT
            _usbRequest->RequestType = 0xA1; // Host-to-device, Class, Interface
            _usbRequest->Index = device.Interface; 
            _usbRequest->Length = length; 
            _usbRequest->Value = 0x0100; // Input Report, Report ID 0
            bool res = USB.SendAndReceive(device, _usbRequest, buffer, device.Parent); 
            return res;
        }
        /// <summary>
        /// Get keyboard input from USB HID keyboard
        /// 
        /// POTENTIALLY UEFI-COMPATIBLE: USB-based, no legacy ports.
        /// REUSABLE: HID boot protocol keyboard report parsing.
        /// </summary>
        public static void GetKeyboard(USBDevice device, out byte ScanCode, out ConsoleKey Key) {
            Key = None; ScanCode = 0; 
            byte* desc = stackalloc byte[8]; // Standard boot keyboard report is 8 bytes
            bool res = GetHIDPacket(device, desc, 8);
            if (res) { 
                // desc[0] is modifier keys
                // desc[1] is reserved
                // desc[2]..desc[7] are keycodes
                if (desc[2] != 0) { 
                    ScanCode = desc[2]; 
                    if (ScanCode < ConsoleKeys.Length) 
                        Key = ConsoleKeys[ScanCode]; 
                } 
            }
        }
        /// <summary>
        /// Get mouse input from USB HID mouse
        /// 
        /// POTENTIALLY UEFI-COMPATIBLE: USB-based, no legacy ports.
        /// REUSABLE: HID boot protocol mouse report parsing.
        /// Returns signed deltas (sbyte) and button state.
        /// 
        /// For UEFI: This is the recommended mouse input method.
        /// </summary>
        public static void GetMouse(USBDevice device, out sbyte AxisX, out sbyte AxisY, out MouseButtons buttons) {
            AxisX = 0; AxisY = 0; buttons = MouseButtons.None; 
            byte* desc = stackalloc byte[4]; // Standard boot mouse report is 3 or 4 bytes
            bool res = GetHIDPacket(device, desc, 4);
            if (res) { 
                AxisX = (sbyte)desc[1]; 
                AxisY = (sbyte)desc[2]; 
                if ((desc[0] & 0x01) != 0) buttons |= MouseButtons.Left; 
                if ((desc[0] & 0x02) != 0) buttons |= MouseButtons.Right; 
                if ((desc[0] & 0x04) != 0) buttons |= MouseButtons.Middle; 
            }
        }
        public static void Initialize(USBDevice device) { 
            if (device.Protocol == 1) { // Keyboard
                USB.NumDevice++; 
                InitializeKeyboard(device); 
            } else if (device.Protocol == 2) { // Mouse
                USB.NumDevice++; 
                InitializeMouse(device); 
            } 
        }
        static void InitializeMouse(USBDevice device) { 
            Mouse = device; 
            // Set Idle rate to 0 (infinite) to get reports only when state changes
            (*_usbRequest).Clean();
            _usbRequest->Request = 0x0A; // SET_IDLE
            _usbRequest->RequestType = 0x21; // Device-to-host, Class, Interface
            _usbRequest->Value = 0; // Duration = 0 (infinite)
            _usbRequest->Index = device.Interface;
            _usbRequest->Length = 0;
            USB.SendAndReceive(device, _usbRequest, null, device.Parent);
        }
        static void InitializeKeyboard(USBDevice device) {
            Keyboard = device;
            // Set Idle rate to 0 (infinite)
            (*_usbRequest).Clean();
            _usbRequest->Request = 0x0A; // SET_IDLE
            _usbRequest->RequestType = 0x21;
            _usbRequest->Value = 0;
            _usbRequest->Index = device.Interface;
            _usbRequest->Length = 0;
            USB.SendAndReceive(device, _usbRequest, null, device.Parent);

            // Build a HID usage -> ConsoleKey table large enough for arrow keys, etc.
            // POTENTIALLY REUSABLE: This mapping can be used for any HID keyboard
            ConsoleKeys = new ConsoleKey[256];
            for (int i = 0; i < ConsoleKeys.Length; i++) ConsoleKeys[i] = None;
            // Letters: HID 0x04..0x1D => A..Z
            ConsoleKeys[0x04] = A; ConsoleKeys[0x05] = B; ConsoleKeys[0x06] = C; ConsoleKeys[0x07] = D; ConsoleKeys[0x08] = E; ConsoleKeys[0x09] = F; ConsoleKeys[0x0A] = G; ConsoleKeys[0x0B] = H; ConsoleKeys[0x0C] = I; ConsoleKeys[0x0D] = J; ConsoleKeys[0x0E] = K; ConsoleKeys[0x0F] = L; ConsoleKeys[0x10] = M; ConsoleKeys[0x11] = N; ConsoleKeys[0x12] = O; ConsoleKeys[0x13] = P; ConsoleKeys[0x14] = Q; ConsoleKeys[0x15] = R; ConsoleKeys[0x16] = S; ConsoleKeys[0x17] = T; ConsoleKeys[0x18] = U; ConsoleKeys[0x19] = V; ConsoleKeys[0x1A] = W; ConsoleKeys[0x1B] = X; ConsoleKeys[0x1C] = Y; ConsoleKeys[0x1D] = Z;
            // Numbers: HID 0x1E..0x27 => 1..0 (D1..D0)
            ConsoleKeys[0x1E] = D1; ConsoleKeys[0x1F] = D2; ConsoleKeys[0x20] = D3; ConsoleKeys[0x21] = D4; ConsoleKeys[0x22] = D5; ConsoleKeys[0x23] = D6; ConsoleKeys[0x24] = D7; ConsoleKeys[0x25] = D8; ConsoleKeys[0x26] = D9; ConsoleKeys[0x27] = D0;
            // Controls
            ConsoleKeys[0x28] = Enter; // Enter
            ConsoleKeys[0x29] = Escape; // Esc
            ConsoleKeys[0x2A] = Backspace; // Backspace
            ConsoleKeys[0x2B] = Tab; // Tab
            ConsoleKeys[0x2C] = Space; // Space
            // Arrows (HID usages)
            ConsoleKeys[0x4F] = Right; // Right Arrow
            ConsoleKeys[0x50] = Left;  // Left Arrow
            ConsoleKeys[0x51] = Down;  // Down Arrow
            ConsoleKeys[0x52] = Up;    // Up Arrow
        }
        
        // ============================================================================
        // UEFI MIGRATION NOTES
        // ============================================================================
        // 
        // USB HID is the RECOMMENDED input method for UEFI:
        // 
        // 1. TO ENABLE USB HID IN UEFI MODE:
        //    - Remove the "Legacy mode only" guard in Program.KMain()
        //    - Ensure EHCI.Initialize() works with UEFI memory mapping
        //    - EHCI needs PCI device detection (already has this)
        //    - May need to handle case where UEFI already claimed USB controller
        // 
        // 2. ALTERNATIVE: XHCI for USB 3.0
        //    - Modern UEFI systems often have XHCI instead of/alongside EHCI
        //    - Would need a new XHCI driver
        //    - XHCI is more complex but supports all USB speeds
        // 
        // 3. POLLING APPROACH (NO INTERRUPTS):
        //    - USB.LoopPoll() already does polling in a thread
        //    - Can be called from main loop without ThreadPool
        //    - Example:
        //      if (HID.Mouse != null) {
        //          HID.GetMouse(HID.Mouse, out sbyte x, out sbyte y, out var btns);
        //          // Apply filtering from PS2Mouse.cs
        //          Control.MousePosition.X = Math.Clamp(Control.MousePosition.X + x, ...);
        //      }
        // 
        // 4. COMBINING WITH PS2Mouse FILTERING:
        //    - Extract TouchpadSensitivity, NoiseThreshold, etc. from PS2Mouse
        //    - Apply to USB HID deltas for consistent feel
        // 
        // ============================================================================
    }
}