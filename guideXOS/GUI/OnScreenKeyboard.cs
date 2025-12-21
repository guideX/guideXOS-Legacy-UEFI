using guideXOS.Kernel.Drivers;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace guideXOS.GUI {
    /// <summary>
    /// On-Screen Keyboard
    /// </summary>
    internal class OnScreenKeyboard : Window {
        private bool _shift = false;
        private bool _caps = false;
        private bool _clickLatch = false;
        
        // Keyboard layout
        private static readonly string[] _rows = new string[] {
            "`1234567890-=",
            "qwertyuiop[]\\",
            "asdfghjkl;'",
            "zxcvbnm,./"
        };
        
        private static readonly string[] _rowsShift = new string[] {
            "~!@#$%^&*()_+",
            "QWERTYUIOP{}|",
            "ASDFGHJKL:\"",
            "ZXCVBNM<>?"
        };

        public OnScreenKeyboard(int x, int y) : base(x, y, 800, 280) {
            Title = "On-Screen Keyboard";
            ShowInTaskbar = true;
            ShowMaximize = false;
            ShowMinimize = true;
            ShowTombstone = true;
            IsResizable = false;
            ShowInStartMenu = true;
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;

            bool leftDown = Control.MouseButtons.HasFlag(MouseButtons.Left);
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;

            if (leftDown && !_clickLatch) {
                _clickLatch = true;
                
                int keyW = 48;
                int keyH = 48;
                int gap = 4;
                int startY = Y + 10;
                
                // Check main keys
                for (int row = 0; row < _rows.Length; row++) {
                    string keys = _shift || _caps ? _rowsShift[row] : _rows[row];
                    int startX = X + 10 + row * 12; // slight indent per row
                    
                    for (int col = 0; col < keys.Length; col++) {
                        int kx = startX + col * (keyW + gap);
                        int ky = startY + row * (keyH + gap);
                        
                        if (mx >= kx && mx <= kx + keyW && my >= ky && my <= ky + keyH) {
                            char ch = keys[col];
                            SendKey(ch);
                            if (_shift && !_caps) _shift = false; // toggle shift off after one key
                            return;
                        }
                    }
                }
                
                // Special keys row (bottom)
                int bottomY = startY + _rows.Length * (keyH + gap);
                int bottomX = X + 10;
                
                // Shift key
                int shiftW = 80;
                if (mx >= bottomX && mx <= bottomX + shiftW && my >= bottomY && my <= bottomY + keyH) {
                    _shift = !_shift;
                    return;
                }
                
                // Caps Lock
                int capsX = bottomX + shiftW + gap;
                int capsW = 100;
                if (mx >= capsX && mx <= capsX + capsW && my >= bottomY && my <= bottomY + keyH) {
                    _caps = !_caps;
                    return;
                }
                
                // Space bar
                int spaceX = capsX + capsW + gap;
                int spaceW = 300;
                if (mx >= spaceX && mx <= spaceX + spaceW && my >= bottomY && my <= bottomY + keyH) {
                    SendKey(' ');
                    return;
                }
                
                // Backspace
                int backX = spaceX + spaceW + gap;
                int backW = 100;
                if (mx >= backX && mx <= backX + backW && my >= bottomY && my <= bottomY + keyH) {
                    SendKey('\b');
                    return;
                }
                
                // Enter
                int enterX = backX + backW + gap;
                int enterW = 80;
                if (mx >= enterX && mx <= enterX + enterW && my >= bottomY && my <= bottomY + keyH) {
                    SendKey('\r');
                    return;
                }
            } else if (!leftDown) {
                _clickLatch = false;
            }
        }

        private void SendKey(char ch) {
            // Simulate keyboard input
            ConsoleKey key = CharToConsoleKey(ch);
            ConsoleModifiers mods = ConsoleModifiers.None;
            
            if (_shift || _caps) {
                if (ch >= 'A' && ch <= 'Z') {
                    mods |= ConsoleModifiers.Shift;
                } else if (_shift) {
                    mods |= ConsoleModifiers.Shift;
                }
            }
            
            if (_caps) {
                mods |= ConsoleModifiers.CapsLock;
            }
            
            // Create key info
            var keyInfo = new ConsoleKeyInfo {
                Key = key,
                KeyChar = ch,
                Modifiers = mods,
                KeyState = ConsoleKeyState.Pressed,
                ScanCode = 0
            };
            
            // Fire keyboard event
            Keyboard.SimulateKey(keyInfo);
        }

        private ConsoleKey CharToConsoleKey(char ch) {
            if (ch == ' ') return ConsoleKey.Space;
            if (ch == '\b') return ConsoleKey.Backspace;
            if (ch == '\r' || ch == '\n') return ConsoleKey.Enter;
            if (ch >= 'a' && ch <= 'z') return (ConsoleKey)((int)ConsoleKey.A + (ch - 'a'));
            if (ch >= 'A' && ch <= 'Z') return (ConsoleKey)((int)ConsoleKey.A + (ch - 'A'));
            if (ch >= '0' && ch <= '9') return (ConsoleKey)((int)ConsoleKey.D0 + (ch - '0'));
            
            // Special characters
            switch (ch) {
                case '`': case '~': return ConsoleKey.Oem3;
                case '-': case '_': return ConsoleKey.OemMinus;
                case '=': case '+': return ConsoleKey.OemPlus;
                case '[': case '{': return ConsoleKey.Oem4;
                case ']': case '}': return ConsoleKey.Oem6;
                case '\\': case '|': return ConsoleKey.Oem5;
                case ';': case ':': return ConsoleKey.Oem1;
                case '\'': case '"': return ConsoleKey.Oem7;
                case ',': case '<': return ConsoleKey.OemComma;
                case '.': case '>': return ConsoleKey.OemPeriod;
                case '/': case '?': return ConsoleKey.Oem2;
                default: return ConsoleKey.A;
            }
        }

        public override void OnDraw() {
            base.OnDraw();
            if (!Visible) return;

            int keyW = 48;
            int keyH = 48;
            int gap = 4;
            int startY = Y + 10;
            
            // Draw main keyboard rows
            for (int row = 0; row < _rows.Length; row++) {
                string keys = _shift || _caps ? _rowsShift[row] : _rows[row];
                int startX = X + 10 + row * 12; // slight indent per row
                
                for (int col = 0; col < keys.Length; col++) {
                    int kx = startX + col * (keyW + gap);
                    int ky = startY + row * (keyH + gap);
                    
                    DrawKey(kx, ky, keyW, keyH, keys[col].ToString());
                }
            }
            
            // Special keys row (bottom)
            int bottomY = startY + _rows.Length * (keyH + gap);
            int bottomX = X + 10;
            
            // Shift
            int shiftW = 80;
            uint shiftColor = _shift ? 0xFF4A90E2 : 0xFF3A3A3A;
            DrawSpecialKey(bottomX, bottomY, shiftW, keyH, "Shift", shiftColor);
            
            // Caps Lock
            int capsX = bottomX + shiftW + gap;
            int capsW = 100;
            uint capsColor = _caps ? 0xFF4A90E2 : 0xFF3A3A3A;
            DrawSpecialKey(capsX, bottomY, capsW, keyH, "Caps", capsColor);
            
            // Space
            int spaceX = capsX + capsW + gap;
            int spaceW = 300;
            DrawSpecialKey(spaceX, bottomY, spaceW, keyH, "Space", 0xFF3A3A3A);
            
            // Backspace
            int backX = spaceX + spaceW + gap;
            int backW = 100;
            DrawSpecialKey(backX, bottomY, backW, keyH, "Back", 0xFF3A3A3A);
            
            // Enter
            int enterX = backX + backW + gap;
            int enterW = 80;
            DrawSpecialKey(enterX, bottomY, enterW, keyH, "Enter", 0xFF3A3A3A);
        }

        private void DrawKey(int x, int y, int w, int h, string label) {
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool hover = mx >= x && mx <= x + w && my >= y && my <= y + h;
            
            uint bg = hover ? 0xFF505050 : 0xFF3A3A3A;
            Framebuffer.Graphics.FillRectangle(x, y, w, h, bg);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF555555, 1);
            
            int textW = WindowManager.font.MeasureString(label);
            int textX = x + (w - textW) / 2;
            int textY = y + (h - WindowManager.font.FontSize) / 2;
            WindowManager.font.DrawString(textX, textY, label);
        }

        private void DrawSpecialKey(int x, int y, int w, int h, string label, uint customBg) {
            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool hover = mx >= x && mx <= x + w && my >= y && my <= y + h;
            
            uint bg = hover ? 0xFF505050 : customBg;
            
            Framebuffer.Graphics.FillRectangle(x, y, w, h, bg);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, 0xFF555555, 1);
            
            int textW = WindowManager.font.MeasureString(label);
            int textX = x + (w - textW) / 2;
            int textY = y + (h - WindowManager.font.FontSize) / 2;
            WindowManager.font.DrawString(textX, textY, label);
        }
    }
}
