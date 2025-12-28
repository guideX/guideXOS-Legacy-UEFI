using guideXOS.Kernel.Drivers;
using System;
using System.Windows.Forms;
namespace guideXOS.GUI {
    /// <summary>
    /// Network configuration dialog similar to Windows TCP/IPv4 Properties
    /// </summary>
    internal class NetworkConfigurationDialog : Window {
        private const int Pad = 24;
        private const int LineHeight = 32;
        private const int InputWidth = 180;
        private const int InputHeight = 28;
        private const int LabelWidth = 220;

        // Radio button state
        private bool _useDHCP = true;
        private bool _useDHCPforDNS = true;

        // IP address inputs (4 octets each)
        private string[] _ipOctets = new string[] { "", "", "", "" };
        private string[] _maskOctets = new string[] { "", "", "", "" };
        private string[] _gatewayOctets = new string[] { "", "", "", "" };
        private string[] _dnsOctets = new string[] { "", "", "", "" };
        private string[] _altDnsOctets = new string[] { "", "", "", "" };

        // Focus tracking
        private int _focusedField = -1; // -1 = none, 0-3 = IP, 4-7 = mask, 8-11 = gateway, 12-15 = DNS, 16-19 = Alt DNS

        // Button hover states
        private bool _okHover = false;
        private bool _cancelHover = false;

        // Keyboard state
        private bool _keyDown = false;
        private byte _lastScan = 0;

        // Cursor blink for textboxes
        private int _cursorBlink = 0;

        public NetworkConfigurationDialog() : base(
            (Framebuffer.Width - 580) / 2,
            (Framebuffer.Height - 680) / 2,
            580,
            680
        ) {
            Title = "Internet Protocol Version 4 (TCP/IPv4) Properties";
            IsResizable = false;
            ShowInTaskbar = true;
            ShowMaximize = false;
            ShowMinimize = true;
            ShowTombstone = true;
            ShowInStartMenu = false;

            // Load current network configuration
            LoadCurrentConfig();

            // Subscribe to keyboard events
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
        }

        private void LoadCurrentConfig() {
            // Check if we have an IP configured
            if (NETv4.IP.P1 != 0 || NETv4.IP.P2 != 0 || NETv4.IP.P3 != 0 || NETv4.IP.P4 != 0) {
                _useDHCP = false;
                _ipOctets[0] = NETv4.IP.P1.ToString();
                _ipOctets[1] = NETv4.IP.P2.ToString();
                _ipOctets[2] = NETv4.IP.P3.ToString();
                _ipOctets[3] = NETv4.IP.P4.ToString();

                _maskOctets[0] = NETv4.Mask.P1.ToString();
                _maskOctets[1] = NETv4.Mask.P2.ToString();
                _maskOctets[2] = NETv4.Mask.P3.ToString();
                _maskOctets[3] = NETv4.Mask.P4.ToString();

                _gatewayOctets[0] = NETv4.GatewayIP.P1.ToString();
                _gatewayOctets[1] = NETv4.GatewayIP.P2.ToString();
                _gatewayOctets[2] = NETv4.GatewayIP.P3.ToString();
                _gatewayOctets[3] = NETv4.GatewayIP.P4.ToString();
            } else {
                _useDHCP = true;
            }
        }

        private void Keyboard_OnKeyChanged(object sender, ConsoleKeyInfo key) {
            if (!Visible) return;
            if (key.KeyState != ConsoleKeyState.Pressed) { _keyDown = false; _lastScan = 0; return; }
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan) return;
            _keyDown = true;
            _lastScan = (byte)Keyboard.KeyInfo.ScanCode;

            char ch = key.KeyChar;

            // Handle Enter key - always applies configuration (OK button is default)
            if (key.Key == ConsoleKey.Enter) {
                ApplyConfiguration();
                Visible = false;
                return;
            }

            // Handle Escape key
            if (key.Key == ConsoleKey.Escape) {
                Visible = false;
                return;
            }

            // Handle Tab key - navigate between fields
            if (key.Key == ConsoleKey.Tab) {
                // Determine max field based on DNS mode
                int maxField = _useDHCPforDNS ? 11 : 19;

                if (_focusedField == -1) {
                    // Start from first IP field if manual mode
                    _focusedField = _useDHCP ? -1 : 0;
                } else {
                    // Move to next field
                    _focusedField++;

                    // Skip DNS fields if auto DNS
                    if (_useDHCPforDNS && _focusedField == 12) {
                        _focusedField = -1; // Wrap around
                    } else if (_focusedField > maxField) {
                        _focusedField = 0; // Wrap to first field
                    }

                    // If DHCP is enabled, can't focus IP fields
                    if (_useDHCP && _focusedField >= 0 && _focusedField <= 11) {
                        _focusedField = _useDHCPforDNS ? -1 : 12;
                    }
                }
                return;
            }

            // Only process input if a field is focused
            if (_focusedField == -1) return;

            // Only allow digits and backspace
            if (ch >= '0' && ch <= '9') {
                string[] targetArray = GetOctetArray(_focusedField, out int index);
                if (targetArray != null && targetArray[index].Length < 3) {
                    targetArray[index] = targetArray[index] + ch;

                    // Auto-advance if we have 3 digits or value would exceed 255
                    int val = ParseOctet(targetArray[index]);
                    if (targetArray[index].Length == 3 || val > 25) {
                        // Move to next field
                        int maxField = _useDHCPforDNS ? 11 : 19;
                        if (_focusedField < maxField) {
                            _focusedField++;
                            // Skip DNS fields if auto DNS
                            if (_useDHCPforDNS && _focusedField == 12) {
                                _focusedField = 0; // Wrap to first field
                            }
                        }
                    }
                }
            } else if (key.Key == ConsoleKey.Backspace) {
                string[] targetArray = GetOctetArray(_focusedField, out int index);
                if (targetArray != null && targetArray[index].Length > 0) {
                    targetArray[index] = targetArray[index].Substring(0, targetArray[index].Length - 1);
                }
            }
        }

        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;

            // Update cursor blink
            _cursorBlink++;

            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;

            // Calculate positions
            int contentX = X + Pad;
            int contentY = Y + BarHeight + Pad;
            int instructionY = contentY + 12;
            int dhcpRadioY = instructionY + LineHeight * 3 + 24;
            int manualRadioY = dhcpRadioY + LineHeight + 6;

            int ipLabelY = manualRadioY + LineHeight + 20;
            int maskLabelY = ipLabelY + LineHeight + 8;
            int gatewayLabelY = maskLabelY + LineHeight + 8;

            int dnsAutoRadioY = gatewayLabelY + LineHeight + 28;
            int dnsManualRadioY = dnsAutoRadioY + LineHeight + 6;
            int dnsLabelY = dnsManualRadioY + LineHeight + 20;
            int altDnsLabelY = dnsLabelY + LineHeight + 8;

            // Button positions
            int btnY = Y + Height - 70;
            int okBtnX = X + Width - 220;
            int cancelBtnX = X + Width - 110;

            // Handle mouse clicks
            if (Control.MouseButtons.HasFlag(MouseButtons.Left) && !WindowManager.MouseHandled) {
                // IP Configuration Radio buttons
                int radioX = contentX + 4;
                if (mx >= radioX && mx <= radioX + 20) {
                    if (my >= dhcpRadioY && my <= dhcpRadioY + 20) {
                        _useDHCP = true;
                        _focusedField = -1;
                        WindowManager.MouseHandled = true;
                    } else if (my >= manualRadioY && my <= manualRadioY + 20) {
                        _useDHCP = false;
                        WindowManager.MouseHandled = true;
                    }
                    // DNS Radio buttons
                    else if (my >= dnsAutoRadioY && my <= dnsAutoRadioY + 20) {
                        _useDHCPforDNS = true;
                        if (_focusedField >= 12) _focusedField = -1;
                        WindowManager.MouseHandled = true;
                    } else if (my >= dnsManualRadioY && my <= dnsManualRadioY + 20) {
                        _useDHCPforDNS = false;
                        WindowManager.MouseHandled = true;
                    }
                }

                // Input fields (only if manual mode)
                if (!_useDHCP) {
                    int inputX = contentX + LabelWidth + 12;

                    // IP address fields
                    for (int i = 0; i < 4; i++) {
                        int fieldX = inputX + i * 52;
                        if (mx >= fieldX && mx <= fieldX + 48 && my >= ipLabelY && my <= ipLabelY + InputHeight) {
                            _focusedField = i;
                            WindowManager.MouseHandled = true;
                        }
                    }

                    // Subnet mask fields
                    for (int i = 0; i < 4; i++) {
                        int fieldX = inputX + i * 52;
                        if (mx >= fieldX && mx <= fieldX + 48 && my >= maskLabelY && my <= maskLabelY + InputHeight) {
                            _focusedField = 4 + i;
                            WindowManager.MouseHandled = true;
                        }
                    }

                    // Gateway fields
                    for (int i = 0; i < 4; i++) {
                        int fieldX = inputX + i * 52;
                        if (mx >= fieldX && mx <= fieldX + 48 && my >= gatewayLabelY && my <= gatewayLabelY + InputHeight) {
                            _focusedField = 8 + i;
                            WindowManager.MouseHandled = true;
                        }
                    }
                }

                // DNS fields (only if manual DNS mode)
                if (!_useDHCPforDNS) {
                    int inputX = contentX + LabelWidth + 12;

                    // Preferred DNS fields
                    for (int i = 0; i < 4; i++) {
                        int fieldX = inputX + i * 52;
                        if (mx >= fieldX && mx <= fieldX + 48 && my >= dnsLabelY && my <= dnsLabelY + InputHeight) {
                            _focusedField = 12 + i;
                            WindowManager.MouseHandled = true;
                        }
                    }

                    // Alternate DNS fields
                    for (int i = 0; i < 4; i++) {
                        int fieldX = inputX + i * 52;
                        if (mx >= fieldX && mx <= fieldX + 48 && my >= altDnsLabelY && my <= altDnsLabelY + InputHeight) {
                            _focusedField = 16 + i;
                            WindowManager.MouseHandled = true;
                        }
                    }
                }

                // OK button
                if (mx >= okBtnX && mx <= okBtnX + 90 && my >= btnY && my <= btnY + 36) {
                    ApplyConfiguration();
                    Visible = false;
                    WindowManager.MouseHandled = true;
                }

                // Cancel button
                if (mx >= cancelBtnX && mx <= cancelBtnX + 90 && my >= btnY && my <= btnY + 36) {
                    Visible = false;
                    WindowManager.MouseHandled = true;
                }
            }

            // Button hover states
            _okHover = mx >= okBtnX && mx <= okBtnX + 90 && my >= btnY && my <= btnY + 36;
            _cancelHover = mx >= cancelBtnX && mx <= cancelBtnX + 90 && my >= btnY && my <= btnY + 36;
        }

        private string[] GetOctetArray(int fieldIndex, out int arrayIndex) {
            if (fieldIndex >= 0 && fieldIndex <= 3) {
                arrayIndex = fieldIndex;
                return _ipOctets;
            } else if (fieldIndex >= 4 && fieldIndex <= 7) {
                arrayIndex = fieldIndex - 4;
                return _maskOctets;
            } else if (fieldIndex >= 8 && fieldIndex <= 11) {
                arrayIndex = fieldIndex - 8;
                return _gatewayOctets;
            } else if (fieldIndex >= 12 && fieldIndex <= 15) {
                arrayIndex = fieldIndex - 12;
                return _dnsOctets;
            } else if (fieldIndex >= 16 && fieldIndex <= 19) {
                arrayIndex = fieldIndex - 16;
                return _altDnsOctets;
            }
            arrayIndex = -1;
            return null;
        }

        private void ApplyConfiguration() {
            if (_useDHCP) {
                // Run DHCP
                try {
                    BootConsole.WriteLine("[NET] Attempting DHCP configuration...");
                    bool success = NETv4.DHCPDiscover();
                    if (success) {
                        BootConsole.WriteLine("[NET] DHCP successful");
                    } else {
                        BootConsole.WriteLine("[NET] DHCP failed");
                    }
                } catch {
                    BootConsole.WriteLine("[NET] DHCP error");
                }
            } else {
                // Parse and apply manual configuration
                try {
                    byte ip1 = ParseOctet(_ipOctets[0]);
                    byte ip2 = ParseOctet(_ipOctets[1]);
                    byte ip3 = ParseOctet(_ipOctets[2]);
                    byte ip4 = ParseOctet(_ipOctets[3]);

                    byte mask1 = ParseOctet(_maskOctets[0]);
                    byte mask2 = ParseOctet(_maskOctets[1]);
                    byte mask3 = ParseOctet(_maskOctets[2]);
                    byte mask4 = ParseOctet(_maskOctets[3]);

                    byte gw1 = ParseOctet(_gatewayOctets[0]);
                    byte gw2 = ParseOctet(_gatewayOctets[1]);
                    byte gw3 = ParseOctet(_gatewayOctets[2]);
                    byte gw4 = ParseOctet(_gatewayOctets[3]);

                    NETv4.IPAddress ip = new NETv4.IPAddress(ip1, ip2, ip3, ip4);
                    NETv4.IPAddress mask = new NETv4.IPAddress(mask1, mask2, mask3, mask4);
                    NETv4.IPAddress gateway = new NETv4.IPAddress(gw1, gw2, gw3, gw4);

                    NETv4.Configure(ip, gateway, mask);
                    BootConsole.WriteLine("[NET] Manual configuration applied");
                    BootConsole.WriteLine("[NET] IP: " + ip1.ToString() + "." + ip2.ToString() + "." + ip3.ToString() + "." + ip4.ToString());
                } catch {
                    BootConsole.WriteLine("[NET] Configuration error");
                }
            }
        }

        private byte ParseOctet(string s) {
            if (string.IsNullOrEmpty(s)) return 0;
            int val = 0;
            for (int i = 0; i < s.Length; i++) {
                if (s[i] >= '0' && s[i] <= '9') {
                    val = val * 10 + (s[i] - '0');
                }
            }
            if (val > 255) val = 255;
            return (byte)val;
        }

        public override void OnDraw() {
            base.OnDraw();
            if (!Visible || WindowManager.font == null) return;

            int contentX = X + Pad;
            int contentY = Y + BarHeight + Pad;
            int contentW = Width - Pad * 2;
            int contentH = Height - BarHeight - Pad * 2 - 70;

            // Draw dark background for content area
            Framebuffer.Graphics.FillRectangle(contentX, contentY, contentW, contentH, 0xFF1E1E1E);

            // Header text (bright color for readability)
            int textY = contentY + 12;
            string line1 = "You can get IP settings assigned automatically if your";
            WindowManager.font.DrawString(contentX + 10, textY, line1);
            textY += LineHeight;

            string line2 = "network supports this capability. Otherwise, you need";
            WindowManager.font.DrawString(contentX + 10, textY, line2);
            textY += LineHeight;

            string line3 = "to ask your network administrator for the appropriate";
            WindowManager.font.DrawString(contentX + 10, textY, line3);
            textY += LineHeight;

            string line4 = "IP settings.";
            WindowManager.font.DrawString(contentX + 10, textY, line4);

            int radioY = contentY + 12 + LineHeight * 3 + 24;

            // Radio button: Obtain an IP address automatically
            DrawRadioButton(contentX + 4, radioY, _useDHCP);
            WindowManager.font.DrawString(contentX + 32, radioY + 3, "Obtain an IP address automatically");

            // Radio button: Use the following IP address
            radioY += LineHeight + 6;
            DrawRadioButton(contentX + 4, radioY, !_useDHCP);
            WindowManager.font.DrawString(contentX + 32, radioY + 3, "Use the following IP address:");

            // IP configuration fields
            int labelX = contentX + 36;
            int inputX = contentX + LabelWidth + 12;
            int fieldY = radioY + LineHeight + 20;

            uint labelColor = _useDHCP ? 0xFF888888u : 0xFFCCCCCCu;

            // IP address
            WindowManager.font.DrawString(labelX, fieldY + 5, "IP address:");
            DrawIPFields(inputX, fieldY, _ipOctets, 0, !_useDHCP);

            fieldY += LineHeight + 8;

            // Subnet mask
            WindowManager.font.DrawString(labelX, fieldY + 5, "Subnet mask:");
            DrawIPFields(inputX, fieldY, _maskOctets, 4, !_useDHCP);

            fieldY += LineHeight + 8;

            // Default gateway
            WindowManager.font.DrawString(labelX, fieldY + 5, "Default gateway:");
            DrawIPFields(inputX, fieldY, _gatewayOctets, 8, !_useDHCP);

            fieldY += LineHeight + 28;

            // DNS section
            DrawRadioButton(contentX + 4, fieldY, _useDHCPforDNS);
            WindowManager.font.DrawString(contentX + 32, fieldY + 3, "Obtain DNS server address automatically");

            fieldY += LineHeight + 6;
            DrawRadioButton(contentX + 4, fieldY, !_useDHCPforDNS);
            WindowManager.font.DrawString(contentX + 32, fieldY + 3, "Use the following DNS server addresses:");

            fieldY += LineHeight + 20;

            uint dnsLabelColor = _useDHCPforDNS ? 0xFF888888u : 0xFFCCCCCCu;

            WindowManager.font.DrawString(labelX, fieldY + 5, "Preferred DNS server:");
            DrawIPFields(inputX, fieldY, _dnsOctets, 12, !_useDHCPforDNS);

            fieldY += LineHeight + 8;

            WindowManager.font.DrawString(labelX, fieldY + 5, "Alternate DNS server:");
            DrawIPFields(inputX, fieldY, _altDnsOctets, 16, !_useDHCPforDNS);

            // Buttons at bottom
            int btnY = Y + Height - 70;
            int okBtnX = X + Width - 220;
            int cancelBtnX = X + Width - 110;

            DrawButton(okBtnX, btnY, 90, 36, "OK", _okHover, true);
            DrawButton(cancelBtnX, btnY, 90, 36, "Cancel", _cancelHover, false);
        }

        private void DrawRadioButton(int x, int y, bool selected) {
            // Outer circle border
            int centerX = x + 10;
            int centerY = y + 10;
            int radius = 8;

            // Draw outer circle
            for (int dy = -radius; dy <= radius; dy++) {
                for (int dx = -radius; dx <= radius; dx++) {
                    int distSq = dx * dx + dy * dy;
                    if (distSq <= radius * radius && distSq >= (radius - 2) * (radius - 2)) {
                        Framebuffer.Graphics.DrawPoint(centerX + dx, centerY + dy, 0xFF999999);
                    }
                }
            }

            // Inner fill
            for (int dy = -(radius - 2); dy <= radius - 2; dy++) {
                for (int dx = -(radius - 2); dx <= radius - 2; dx++) {
                    int distSq = dx * dx + dy * dy;
                    if (distSq <= (radius - 2) * (radius - 2)) {
                        Framebuffer.Graphics.DrawPoint(centerX + dx, centerY + dy, 0xFF2A2A2A);
                    }
                }
            }

            // Inner dot if selected (bright blue)
            if (selected) {
                int dotRadius = 4;
                for (int dy = -dotRadius; dy <= dotRadius; dy++) {
                    for (int dx = -dotRadius; dx <= dotRadius; dx++) {
                        int distSq = dx * dx + dy * dy;
                        if (distSq <= dotRadius * dotRadius) {
                            Framebuffer.Graphics.DrawPoint(centerX + dx, centerY + dy, 0xFF4A9EFF);
                        }
                    }
                }
            }
        }

        private void DrawIPFields(int x, int y, string[] octets, int baseFieldIndex, bool enabled) {
            for (int i = 0; i < 4; i++) {
                int fieldX = x + i * 52;
                bool isFocused = enabled && _focusedField == baseFieldIndex + i;

                // Field background - darker when disabled
                uint bgColor = enabled ? 0xFF2A2A2Au : 0xFF1A1A1Au;
                uint borderColor = enabled ? isFocused ? 0xFF4A9EFFu : 0xFF555555u : 0xFF333333u;

                Framebuffer.Graphics.FillRectangle(fieldX, y, 48, InputHeight, bgColor);
                Framebuffer.Graphics.DrawRectangle(fieldX, y, 48, InputHeight, borderColor, 1);

                // Text - bright when enabled
                string text = octets[i];
                uint textColor = enabled ? 0xFFEEEEEEu : 0xFF666666u;

                if (!string.IsNullOrEmpty(text)) {
                    WindowManager.font.DrawString(fieldX + 7, y + 6, text);
                } else if (isFocused) {
                    // Show cursor when empty and focused
                    if (_cursorBlink / 30 % 2 == 0) {
                        Framebuffer.Graphics.FillRectangle(fieldX + 7, y + 6, 2, WindowManager.font.FontSize, 0xFFEEEEEE);
                    }
                }

                // Draw blinking cursor at end of text when focused
                if (isFocused && !string.IsNullOrEmpty(text)) {
                    if (_cursorBlink / 30 % 2 == 0) {
                        int cursorX = fieldX + 7 + text.Length * (WindowManager.font.FontSize / 2);
                        Framebuffer.Graphics.FillRectangle(cursorX, y + 6, 2, WindowManager.font.FontSize, 0xFFEEEEEE);
                    }
                }

                // Draw dots between octets
                if (i < 3) {
                    WindowManager.font.DrawString(fieldX + 50, y + 6, ".");
                }
            }
        }

        private void DrawButton(int x, int y, int w, int h, string text, bool hover, bool isDefault) {
            // Default button (OK) has a darker blue, brighter on hover
            uint bgColor;
            if (isDefault) {
                bgColor = hover ? 0xFF5A7A9Au : 0xFF4A6A8Au;
            } else {
                bgColor = hover ? 0xFF4A6A8Au : 0xFF3A5A7Au;
            }
            uint borderColor = isDefault ? 0xFF3A5A7Au : 0xFF2A4A6Au;

            Framebuffer.Graphics.FillRectangle(x, y, w, h, bgColor);
            Framebuffer.Graphics.DrawRectangle(x, y, w, h, borderColor, 1);

            // Draw a thicker border for default button
            if (isDefault) {
                Framebuffer.Graphics.DrawRectangle(x + 1, y + 1, w - 2, h - 2, 0xFF5A7A9Au, 1);
            }

            int textWidth = text.Length * (WindowManager.font.FontSize / 2);
            int textX = x + (w - textWidth) / 2;
            int textY = y + (h - WindowManager.font.FontSize) / 2;

            WindowManager.font.DrawString(textX, textY, text);
        }

        public override void Dispose() {
            // CRITICAL FIX: Unsubscribe from keyboard events to prevent memory leak
            Keyboard.OnKeyChanged -= Keyboard_OnKeyChanged;
            base.Dispose();
        }
    }
}