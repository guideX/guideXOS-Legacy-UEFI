using guideXOS.Misc;
using System;
using static System.ConsoleKey;
namespace guideXOS.Kernel.Drivers {
    public static unsafe class PS2Keyboard {
        private const byte DataPort = 0x60;
        private const byte CommandPort = 0x64;
        
        // Modifier key states
        private static bool _leftShift = false;
        private static bool _rightShift = false;
        private static bool _leftCtrl = false;
        private static bool _rightCtrl = false;
        private static bool _leftAlt = false;
        private static bool _rightAlt = false;
        private static bool _capsLock = false;
        private static bool _numLock = false;
        private static bool _scrollLock = false;
        
        // Extended scancode flag (0xE0 prefix)
        private static bool _extended = false;
        // PS/2 Set 2 handling
        private static bool _set2 = false;          // Seen 0xF0 break prefix => device uses Set 2
        private static bool _breakPending = false;   // Last byte was 0xF0 => next code is a release
        
        // Spurious scancode filter - ignore first few scancodes after initialization
        private static int _scancodeCount = 0;
        private const int MinScancodeBeforeAccept = 5; // Ignore first 5 scancodes
        
        /// <summary>
        /// Initialize PS/2 Keyboard
        /// </summary>
        public static void Initialize() {
            // Register IRQ1 (keyboard interrupt)
            Interrupts.EnableInterrupt(0x21, &OnInterrupt);
            
            // Enable keyboard
            Native.Hlt();
            Native.Out8(CommandPort, 0x60); // Send command byte
            Native.Hlt();
            Native.Out8(DataPort, 0x65); // Enable keyboard interrupt
            Native.Hlt();
            
            // Reset scancode counter
            _scancodeCount = 0;
            
            Console.WriteLine("[PS2KBD] PS/2 Keyboard initialized");
        }
        
        /// <summary>
        /// Keyboard interrupt handler
        /// </summary>
        public static void OnInterrupt() {
            byte scancode = Native.In8(DataPort);
            
            // QEMU workaround: Filter out spurious 0x00 scancodes
            if (scancode == 0x00) {
                return;
            }
            
            // Filter spurious scancodes during early boot
            // Sometimes QEMU/VMs send garbage scancodes during initialization
            _scancodeCount++;
            if (_scancodeCount <= MinScancodeBeforeAccept) {
                // Ignore first few scancodes after initialization
                // They're often garbage from keyboard controller reset
                return;
            }
            
            // Handle extended scancodes (0xE0 prefix)
            if (scancode == 0xE0) {
                _extended = true;
                return;
            }
            // Handle Set 2 break prefix (0xF0)
            if (scancode == 0xF0) {
                _breakPending = true;
                return;
            }
            
            bool released;
            byte rawCode;
            
            // If we saw 0xF0 prefix, this is a Set 2 release
            if (_breakPending) {
                _set2 = true; // Keyboard is using Set 2
                released = true;
                rawCode = scancode;
            } else if (_set2) {
                // We know keyboard uses Set 2, no 0xF0 means press
                released = false;
                rawCode = scancode;
            } else {
                // Set 1: releases have bit 7 set
                released = (scancode & 0x80) != 0;
                rawCode = (byte)(scancode & 0x7F);
            }
            
            // Translate Set 2 make codes into Set 1 equivalents so downstream logic can stay in Set 1
            byte makeCode = _set2 ? TranslateSet2ToSet1(rawCode, _extended) : rawCode;
            
            // QEMU workaround: Ignore modifier-only interrupts with invalid makeCodes
            // Sometimes QEMU sends spurious interrupts that corrupt modifier states
            if (!released && makeCode > 0x58 && makeCode != 0x5B && makeCode != 0x5C && makeCode != 0x5D) {
                return;
            }
            
            // Update modifier states FIRST
            UpdateModifiers(makeCode, released);
            
            // Get the ConsoleKey and character
            ConsoleKey key = ScancodeToKey(makeCode, _extended);
            // Only produce characters on key press, not release
            char keyChar = released ? '\0' : GetChar(makeCode, _extended);
            
            // Build modifiers flags
            ConsoleModifiers mods = ConsoleModifiers.None;
            if (_leftShift || _rightShift) mods |= ConsoleModifiers.Shift;
            if (_leftCtrl || _rightCtrl) mods |= ConsoleModifiers.Control;
            if (_leftAlt || _rightAlt) mods |= ConsoleModifiers.Alt;
            if (_capsLock) mods |= ConsoleModifiers.CapsLock;
            
            // Create ConsoleKeyInfo (keep original scancode for diagnostics/debouncing)
            Keyboard.KeyInfo = new ConsoleKeyInfo {
                Key = key,
                KeyChar = keyChar,
                Modifiers = mods,
                KeyState = released ? ConsoleKeyState.Released : ConsoleKeyState.Pressed,
                ScanCode = scancode
            };
            
            // Reset prefix flags
            _extended = false;
            _breakPending = false;
            
            // Invoke keyboard events
            Keyboard.InvokeOnKeyChanged(Keyboard.KeyInfo);
            Kbd2Mouse.OnKeyChanged(Keyboard.KeyInfo);
        }
        
        /// <summary>
        /// Translate PS/2 Set 2 make codes to Set 1 equivalents (partial but covers US layout alphanumerics and symbols)
        /// </summary>
        private static byte TranslateSet2ToSet1(byte code, bool extended) {
            // This is a simplified translation table. A full one is very large.
            // It covers a standard US QWERTY layout.
            
            // Extended codes are often consistent, so we pass them through
            // unless they are known right-side modifiers
            if (extended) {
                switch (code) {
                    case 0x14: return 0x1D; // RCtrl (E0,1D)
                    case 0x11: return 0x38; // RAlt (E0,38)
                    case 0x5A: return 0x1C; // Numpad Enter
                    case 0x4A: return 0x35; // Numpad /
                    // Arrow keys and other extended keys generally map 1:1
                    default: return code;   // Pass through others like arrow keys, etc.
                }
            }

            // Non-extended codes - comprehensive translation for US QWERTY
            switch (code) {
                // Modifiers
                case 0x12: return 0x2A; // LShift
                case 0x59: return 0x36; // RShift
                case 0x14: return 0x1D; // LCtrl
                case 0x11: return 0x38; // LAlt
                case 0x58: return 0x3A; // CapsLock
                case 0x77: return 0x45; // NumLock
                case 0x7E: return 0x46; // ScrollLock
                
                // Escape and Function keys
                case 0x76: return 0x01; // Escape
                case 0x05: return 0x3B; // F1
                case 0x06: return 0x3C; // F2
                case 0x04: return 0x3D; // F3
                case 0x0C: return 0x3E; // F4
                case 0x03: return 0x3F; // F5
                case 0x0B: return 0x40; // F6
                case 0x83: return 0x41; // F7
                case 0x0A: return 0x42; // F8
                case 0x01: return 0x43; // F9
                case 0x09: return 0x44; // F10
                case 0x78: return 0x57; // F11
                case 0x07: return 0x58; // F12
                
                // Top row (numbers and symbols)
                case 0x0E: return 0x29; // ` (backtick)
                case 0x16: return 0x03; // 1
                case 0x1E: return 0x03; // 2
                case 0x26: return 0x04; // 3
                case 0x25: return 0x05; // 4
                case 0x2E: return 0x06; // 5
                case 0x36: return 0x07; // 6
                case 0x3D: return 0x08; // 7
                case 0x3E: return 0x09; // 8
                case 0x46: return 0x0A; // 9
                case 0x45: return 0x0B; // 0
                case 0x4E: return 0x0C; // - (minus)
                case 0x55: return 0x0D; // = (equals)
                case 0x66: return 0x0E; // Backspace
                
                // Top letter row (QWERTY)
                case 0x0D: return 0x0F; // Tab
                case 0x15: return 0x10; // Q
                case 0x1D: return 0x11; // W
                case 0x24: return 0x12; // E
                case 0x2D: return 0x13; // R
                case 0x2C: return 0x14; // T
                case 0x35: return 0x15; // Y
                case 0x3C: return 0x16; // U
                case 0x43: return 0x17; // I
                case 0x44: return 0x18; // O
                case 0x4D: return 0x19; // P
                case 0x54: return 0x1A; // [ (left bracket)
                case 0x5B: return 0x1B; // ] (right bracket)
                case 0x5D: return 0x2B; // \ (backslash)
                
                // Middle letter row (ASDF)
                case 0x1C: return 0x1E; // A
                case 0x1B: return 0x1F; // S
                case 0x23: return 0x20; // D
                case 0x2B: return 0x21; // F
                case 0x34: return 0x22; // G
                case 0x33: return 0x23; // H
                case 0x3B: return 0x24; // J
                case 0x42: return 0x25; // K
                case 0x4B: return 0x26; // L
                case 0x4C: return 0x27; // ; (semicolon)
                case 0x52: return 0x28; // ' (apostrophe)
                case 0x5A: return 0x1C; // Enter
                
                // Bottom letter row (ZXCV)
                case 0x1A: return 0x2C; // Z
                case 0x22: return 0x2D; // X
                case 0x21: return 0x2E; // C
                case 0x2A: return 0x2F; // V
                case 0x32: return 0x30; // B
                case 0x31: return 0x31; // N
                case 0x3A: return 0x32; // M
                case 0x41: return 0x33; // , (comma)
                case 0x49: return 0x34; // . (period)
                case 0x4A: return 0x35; // / (slash)
                
                // Bottom row
                case 0x29: return 0x39; // Space
                
                // Numeric keypad
                case 0x70: return 0x52; // Numpad 0 (Ins)
                case 0x69: return 0x4F; // Numpad 1 (End)
                case 0x72: return 0x50; // Numpad 2 (Down)
                case 0x7A: return 0x51; // Numpad 3 (PgDn)
                case 0x6B: return 0x4B; // Numpad 4 (Left)
                case 0x73: return 0x4C; // Numpad 5
                case 0x74: return 0x4D; // Numpad 6 (Right)
                case 0x6C: return 0x47; // Numpad 7 (Home)
                case 0x75: return 0x48; // Numpad 8 (Up)
                case 0x7D: return 0x49; // Numpad 9 (PgUp)
                case 0x71: return 0x53; // Numpad . (Del)
                case 0x7C: return 0x37; // Numpad *
                case 0x7B: return 0x4A; // Numpad -
                case 0x79: return 0x4E; // Numpad +
                
                // If not in our table, return original code (might work for some keys)
                default: return code;
            }
        }
        
        /// <summary>
        /// Update modifier key states (Set 1 codes)
        /// </summary>
        private static void UpdateModifiers(byte makeCode, bool released) {
            if (_extended) {
                // Extended keys (E0 prefix) - right-side modifiers
                switch (makeCode) {
                    case 0x1D: // Right Ctrl
                        _rightCtrl = !released;
                        break;
                    case 0x38: // Right Alt
                        _rightAlt = !released;
                        break;
                }
            } else {
                // Normal keys (left-side modifiers and locks)
                switch (makeCode) {
                    case 0x2A: // Left Shift
                        _leftShift = !released;
                        break;
                    case 0x36: // Right Shift
                        _rightShift = !released;
                        break;
                    case 0x1D: // Left Ctrl
                        _leftCtrl = !released;
                        break;
                    case 0x38: // Left Alt
                        _leftAlt = !released;
                        break;
                    case 0x3A: // Caps Lock - toggle on press
                        if (!released) {
                            _capsLock = !_capsLock;
                        }
                        break;
                    case 0x45: // Num Lock - toggle on press
                        if (!released) {
                            _numLock = !_numLock;
                        }
                        break;
                    case 0x46: // Scroll Lock - toggle on press
                        if (!released) {
                            _scrollLock = !_scrollLock;
                        }
                        break;
                }
            }
        }
        
        /// <summary>
        /// Convert scancode to ConsoleKey (Set 1 codes)
        /// </summary>
        private static ConsoleKey ScancodeToKey(byte makeCode, bool extended) {
            if (extended) {
                // Extended keys (E0 prefix)
                switch (makeCode) {
                    case 0x1C: return Enter; // Keypad Enter
                    case 0x1D: return ConsoleKey.None; // Right Ctrl (modifier only, no separate key)
                    case 0x35: return Divide; // Keypad /
                    case 0x38: return ConsoleKey.None; // Right Alt (modifier only, no separate key)
                    case 0x47: return Home;
                    case 0x48: return Up;
                    case 0x49: return PageUp;
                    case 0x4B: return Left;
                    case 0x4D: return Right;
                    case 0x4F: return End;
                    case 0x50: return Down;
                    case 0x51: return PageDown;
                    case 0x52: return Insert;
                    case 0x53: return Delete;
                    case 0x5B: return LeftWindows;
                    case 0x5C: return RightWindows;
                    case 0x5D: return Applications;
                    default: return ConsoleKey.None;
                }
            }
            
            // Normal scancodes (Set 1)
            switch (makeCode) {
                case 0x01: return Escape;
                case 0x02: return D1;
                case 0x03: return D2;
                case 0x04: return D3;
                case 0x05: return D4;
                case 0x06: return D5;
                case 0x07: return D6;
                case 0x08: return D7;
                case 0x09: return D8;
                case 0x0A: return D9;
                case 0x0B: return D0;
                case 0x0C: return OemMinus;
                case 0x0D: return OemPlus;
                case 0x0E: return Backspace;
                case 0x0F: return Tab;
                case 0x10: return Q;
                case 0x11: return W;
                case 0x12: return E;
                case 0x13: return R;
                case 0x14: return T;
                case 0x15: return Y;
                case 0x16: return U;
                case 0x17: return I;
                case 0x18: return O;
                case 0x19: return P;
                case 0x1A: return Oem4; // [
                case 0x1B: return Oem6; // ]
                case 0x1C: return Enter;
                case 0x1D: return ConsoleKey.None; // Left Ctrl (modifier only)
                case 0x1E: return A;
                case 0x1F: return S;
                case 0x20: return D;
                case 0x21: return F;
                case 0x22: return G;
                case 0x23: return H;
                case 0x24: return J;
                case 0x25: return K;
                case 0x26: return L;
                case 0x27: return Oem1; // ;
                case 0x28: return Oem7; // '
                case 0x29: return Oem3; // `
                case 0x2A: return ConsoleKey.None; // Left Shift (modifier only)
                case 0x2B: return Oem5; // backslash
                case 0x2C: return Z;
                case 0x2D: return X;
                case 0x2E: return C;
                case 0x2F: return V;
                case 0x30: return B;
                case 0x31: return N;
                case 0x32: return M;
                case 0x33: return OemComma;
                case 0x34: return OemPeriod;
                case 0x35: return Oem2; // /
                case 0x36: return ConsoleKey.None; // Right Shift (modifier only)
                case 0x37: return Multiply; // Keypad *
                case 0x38: return ConsoleKey.None; // Left Alt (modifier only)
                case 0x39: return Space;
                case 0x3A: return CapsLock;
                case 0x3B: return F1;
                case 0x3C: return F2;
                case 0x3D: return F3;
                case 0x3E: return F4;
                case 0x3F: return F5;
                case 0x40: return F6;
                case 0x41: return F7;
                case 0x42: return F8;
                case 0x43: return F9;
                case 0x44: return F10;
                case 0x45: return NumLock;
                case 0x46: return Pause; // Scroll Lock (using Pause as closest match)
                case 0x47: return NumPad7; // Home
                case 0x48: return NumPad8; // Up
                case 0x49: return NumPad9; // PgUp
                case 0x4A: return Subtract; // Keypad -
                case 0x4B: return NumPad4; // Left
                case 0x4C: return NumPad5;
                case 0x4D: return NumPad6; // Right
                case 0x4E: return Add; // Keypad +
                case 0x4F: return NumPad1; // End
                case 0x50: return NumPad2; // Down
                case 0x51: return NumPad3; // PgDn
                case 0x52: return NumPad0; // Ins
                case 0x53: return Decimal; // Del
                case 0x57: return F11;
                case 0x58: return F12;
                default: return ConsoleKey.None;
            }
        }
        
        /// <summary>
        /// Get the character for a scancode with current modifiers (Set 1 codes)
        /// </summary>
        private static char GetChar(byte makeCode, bool extended) {
            bool shift = _leftShift || _rightShift;
            bool caps = _capsLock;
            
            // Extended keys don't produce characters (except numpad enter)
            if (extended) {
                if (makeCode == 0x1C) return '\n'; // Numpad Enter
                return '\0';
            }
            
            // Special keys that produce characters
            switch (makeCode) {
                case 0x39: return ' '; // Space
                case 0x1C: return '\n'; // Enter
                case 0x0F: return '\t'; // Tab
                case 0x0E: return '\b'; // Backspace
            }
            
            // Letters (A-Z) - proper scancode mapping
            // Row 1: Q W E R T Y U I O P (scannodes 0x10-0x19)
            if (makeCode >= 0x10 && makeCode <= 0x19) {
                char[] letters = new char[] { 'q', 'w', 'e', 'r', 't', 'y', 'u', 'i', 'o', 'p' };
                char c = letters[makeCode - 0x10];
                if (shift ^ caps) c = (char)(c - 32); // Convert to uppercase
                return c;
            }
            // Row 2: A S D F G H J K L (scannodes 0x1E-0x26)
            if (makeCode >= 0x1E && makeCode <= 0x26) {
                char[] letters = new char[] { 'a', 's', 'd', 'f', 'g', 'h', 'j', 'k', 'l' };
                char c = letters[makeCode - 0x1E];
                if (shift ^ caps) c = (char)(c - 32); // Convert to uppercase
                return c;
            }
            // Row 3: Z X C V B N M (scannodes 0x2C-0x32)
            if (makeCode >= 0x2C && makeCode <= 0x32) {
                char[] letters = new char[] { 'z', 'x', 'c', 'v', 'b', 'n', 'm' };
                char c = letters[makeCode - 0x2C];
                if (shift ^ caps) c = (char)(c - 32); // Convert to uppercase
                return c;
            }
            
            // Numbers and symbols on number row (scannodes 0x02-0x0B)
            if (makeCode >= 0x02 && makeCode <= 0x0B) {
                // Number keys 1-0
                if (!shift) {
                    // Unshifted: 1234567890
                    char[] unshiftedNumbers = new char[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' };
                    return unshiftedNumbers[makeCode - 0x02];
                } else {
                    // Shifted: !@#$%^&*()
                    char[] shiftedNumbers = new char[] { '!', '@', '#', '$', '%', '^', '&', '*', '(', ')' };
                    return shiftedNumbers[makeCode - 0x02];
                }
            }
            
            // Remaining special keys with shift variants
            if (!shift) {
                switch (makeCode) {
                    case 0x0C: return '-';  // -
                    case 0x0D: return '=';  // =
                    case 0x1A: return '[';  // [
                    case 0x1B: return ']';  // ]
                    case 0x2B: return '\\'; // \
                    case 0x27: return ';';  // ;
                    case 0x28: return '\''; // '
                    case 0x29: return '`';  // `
                    case 0x33: return ',';  // ,
                    case 0x34: return '.';  // .
                    case 0x35: return '/';  // /
                }
            } else {
                // Shifted symbols
                switch (makeCode) {
                    case 0x0C: return '_';  // _
                    case 0x0D: return '+';  // +
                    case 0x1A: return '{';  // {
                    case 0x1B: return '}';  // }
                    case 0x2B: return '|';  // |
                    case 0x27: return ':';  // :
                    case 0x28: return '"';  // "
                    case 0x29: return '~';  // ~
                    case 0x33: return '<';  // <
                    case 0x34: return '>';  // >
                    case 0x35: return '?';  // ?
                }
            }
            
            // Numeric keypad (when NumLock is on)
            if (_numLock) {
                switch (makeCode) {
                    case 0x47: return '7';
                    case 0x48: return '8';
                    case 0x49: return '9';
                    case 0x4B: return '4';
                    case 0x4C: return '5';
                    case 0x4D: return '6';
                    case 0x4F: return '1';
                    case 0x50: return '2';
                    case 0x51: return '3';
                    case 0x52: return '0';
                    case 0x53: return '.';
                    case 0x37: return '*';
                    case 0x4A: return '-';
                    case 0x4E: return '+';
                }
            }
            
            return '\0';
        }
    }
}