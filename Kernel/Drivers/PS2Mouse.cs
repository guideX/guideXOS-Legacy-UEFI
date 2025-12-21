using guideXOS.GUI;
using guideXOS.Misc;
using System;
using System.Windows.Forms;
using static Native;
namespace guideXOS.Kernel.Drivers {
    public static unsafe class PS2Mouse {
        private const byte Data = 0x60;
        private const byte Command = 0x64;

        private const byte SetDefaults = 0xF6;
        private const byte EnableDataReporting = 0xF4;

        private static int Phase = 0;
        public static byte[] MData;
        private static int aX;
        private static int aY;
        public static int DeltaZ;

        public static int ScreenWidth = 0;
        public static int ScreenHeight = 0;
        
        // Debug counters
        public static int InterruptCount = 0;
        public static int PacketCount = 0;
        
        // Touchpad sensitivity and filtering
        public static float TouchpadSensitivity = 0.25f; // Reduce touchpad sensitivity (was 1.0)
        public static int NoiseThreshold = 2; // Ignore tiny movements under this value
        public static int MaxDeltaPerPacket = 50; // Prevent huge jumps (touchpad noise)
        public static bool EnableTouchpadFiltering = true; // Master toggle for touchpad fixes
        
        // Button debouncing to prevent phantom clicks
        private static byte lastButtonState = 0;
        private static int buttonStableCount = 0;
        private static int ButtonDebounceThreshold = 2; // Require 2 consistent packets before accepting button change

        public static void Initialise() {
            MData = new byte[4];
            Interrupts.EnableInterrupt(0x2c, &OnInterrupt);
            
            // Load touchpad settings from UISettings
            EnableTouchpadFiltering = UISettings.EnableTouchpadFiltering;
            TouchpadSensitivity = UISettings.TouchpadSensitivity;
            NoiseThreshold = UISettings.TouchpadNoiseThreshold;
            MaxDeltaPerPacket = UISettings.TouchpadMaxDelta;
            ButtonDebounceThreshold = UISettings.TouchpadButtonDebounce;

            byte _status;

            // Wait for controller to be ready
            for (int i = 0; i < 1000; i++) Hlt();
            Out8(Command, 0xA8); // Enable auxiliary device

            // Wait for command to complete
            for (int i = 0; i < 1000; i++) Hlt();
            Out8(Command, 0x20); // Read controller config
            for (int i = 0; i < 1000; i++) Hlt();
            _status = ((byte)(In8(0x60) | 3)); // Enable IRQ12 and IRQ1
            for (int i = 0; i < 1000; i++) Hlt();
            Out8(Command, 0x60); // Write controller config
            for (int i = 0; i < 1000; i++) Hlt();
            Out8(Data, _status);

            // Wait before sending mouse commands
            for (int i = 0; i < 1000; i++) Hlt();
            WriteRegister(SetDefaults);
            for (int i = 0; i < 1000; i++) Hlt();
            WriteRegister(EnableDataReporting);

            // IntelliMouse detection sequence
            for (int i = 0; i < 1000; i++) Hlt();
            WriteRegister(0xF3); // Set Sample Rate
            WriteRegister(200);
            for (int i = 0; i < 1000; i++) Hlt();
            WriteRegister(0xF3); // Set Sample Rate
            WriteRegister(100);
            for (int i = 0; i < 1000; i++) Hlt();
            WriteRegister(0xF3); // Set Sample Rate
            WriteRegister(80);
            for (int i = 0; i < 1000; i++) Hlt();
            WriteRegister(0xF2); // Get Device ID
            ReadRegister(); // Discard ACK
            byte deviceId = ReadRegister(); // Should be 0x03 for wheel mouse

            Control.MouseButtons = MouseButtons.None;
            DeltaZ = 0;
            
            Console.WriteLine("[PS2Mouse] Initialized (DeviceID: 0x" + deviceId.ToString("X2") + ")");
        }

        public static void WriteRegister(byte value) {
            // Wait for controller ready
            for (int i = 0; i < 100; i++) Hlt();
            Out8(Command, 0xD4); // Tell controller to send to mouse
            for (int i = 0; i < 100; i++) Hlt();
            Out8(Data, value);
            for (int i = 0; i < 100; i++) Hlt();
            ReadRegister(); // Wait for ACK
        }

        public static byte ReadRegister() {
            Hlt();
            return In8(Data);
        }

        public static void OnInterrupt() {
            byte D = In8(Data);
            InterruptCount++; // Count all interrupts for debugging
            
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
                Phase = 4; // Move to phase 4 for the Z-axis byte
            }
            else if (Phase == 4)
            {
                MData[3] = D;
                Phase = 1;
                PacketCount++; // Count completed packets

                // Validate packet - bit 3 should always be set in byte 0
                if ((MData[0] & 0x08) == 0) {
                    // Invalid packet, ignore it
                    return;
                }

                // Extract sign bits BEFORE masking them out
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

                // The 4th byte is the scroll wheel movement.
                sbyte wheel = (sbyte)MData[3];
                DeltaZ = wheel;

                // Additional sanity check: if both deltas are at their maximum (likely corrupted packet), ignore movement
                if (EnableTouchpadFiltering && Math.Abs(aX) >= MaxDeltaPerPacket && Math.Abs(aY) >= MaxDeltaPerPacket) {
                    // This is likely a corrupted packet causing corner jumping, ignore it
                    return;
                }

                // Update position with filtered deltas
                Control.MousePosition.X = Math.Clamp(Control.MousePosition.X + aX, 0, Framebuffer.Width);
                Control.MousePosition.Y = Math.Clamp(Control.MousePosition.Y + aY, 0, Framebuffer.Height);
            }
        }
    }
}