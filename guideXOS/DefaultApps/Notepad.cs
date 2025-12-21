using guideXOS.FS;
using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using System;
using System.Windows.Forms;
namespace guideXOS.DefaultApps {
    /// <summary>
    /// Simple Notepad app: type text and save to a file to test filesystem writes.
    /// </summary>
    internal class Notepad : Window {
        private string _text;
        private bool _clickLock;
        private int _padding = 10;
        private int _btnH = 28;
        private int _btnWSaveAs = 88;
        private int _btnWSave = 72;
        private int _btnWWrap = 64;
        private string _fileName = "notes.txt";
        private string _savedPath;
        private bool _dirty;
        private bool _wrap = true;
        private SaveDialog _dlg;
        private SaveChangesDialog _confirmDlg;
        private byte _lastScan; private bool _keyDown;
        // Status bar
        private int _statusH = 26;
        // Track up to two currently pressed make scan codes (0 = none)
        private byte _k1; private byte _k2;

        public Notepad(int x, int y) : base(x, y, 700, 460) {
            IsResizable = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            ShowInStartMenu = true;
            Title = "Notepad";
            _text = string.Empty; _clickLock = false; _savedPath = null; _dirty = false; _dlg = null; _confirmDlg = null;
            _k1 = 0; _k2 = 0;
            // subscribe keyboard handler
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
        }

        public override void OnSetVisible(bool value) {
            // Intercept close when there are unsaved changes
            if (!value && _dirty) {
                if (_confirmDlg == null || !_confirmDlg.Visible) {
                    _confirmDlg = new SaveChangesDialog(this, () => {
                        // Save
                        if (!string.IsNullOrEmpty(_savedPath)) {
                            SaveTo(_savedPath);
                            this.Visible = false;
                        } else {
                            // open save as
                            OpenSaveAs(() => { this.Visible = false; });
                        }
                    }, () => {
                        // Don't Save
                        _dirty = false; this.Visible = false;
                    }, () => {
                        // Cancel close
                        this.Visible = true;
                    });
                    WindowManager.MoveToEnd(_confirmDlg);
                    _confirmDlg.Visible = true;
                }
                // keep notepad visible until decision
                this.Visible = true;
            }
        }

        private static char MapFromKey(ConsoleKeyInfo key) {
            // Prefer KeyChar when provided by the driver
            if (key.KeyChar != '\0') return key.KeyChar;
            // Fallback mapping from ConsoleKey with Shift/Caps handling
            var k = key.Key;
            bool shift = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool caps = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.CapsLock);
            if (k == ConsoleKey.Space) return ' ';
            if (k >= ConsoleKey.A && k <= ConsoleKey.Z) {
                char c = (char)('a' + (k - ConsoleKey.A));
                if (shift ^ caps) { if (c >= 'a' && c <= 'z') c = (char)('A' + (c - 'a')); }
                return c;
            }
            if (k >= ConsoleKey.D0 && k <= ConsoleKey.D9) {
                int d = (int)(k - ConsoleKey.D0);
                if (!shift) return (char)('0' + d);
                switch (d) {
                    case 0: return ')';
                    case 1: return '!';
                    case 2: return '@';
                    case 3: return '#';
                    case 4: return '$';
                    case 5: return '%';
                    case 6: return '^';
                    case 7: return '&';
                    case 8: return '*';
                    case 9: return '(';
                }
            }
            switch (k) {
                case ConsoleKey.OemPeriod: return shift ? '>' : '.';
                case ConsoleKey.OemComma: return shift ? '<' : ',';
                case ConsoleKey.OemMinus: return shift ? '_' : '-';
                case ConsoleKey.OemPlus: return shift ? '+' : '=';
                case ConsoleKey.Oem1: return shift ? ':' : ';';
                case ConsoleKey.Oem2: return shift ? '?' : '/';
                case ConsoleKey.Oem3: return shift ? '~' : '`';
                case ConsoleKey.Oem4: return shift ? '{' : '[';
                case ConsoleKey.Oem5: return shift ? '|' : '\\';
                case ConsoleKey.Oem6: return shift ? '}' : ']';
                case ConsoleKey.Oem7: return shift ? '"' : '\'';
            }
            return '\0';
        }

        private void UpdateStatusKeys(ConsoleKeyInfo key) {
            // Maintain up to two currently pressed make scan codes
            byte scan = (byte)Keyboard.KeyInfo.ScanCode;
            bool released = key.KeyState == ConsoleKeyState.Released;
            byte make = released ? (byte)(scan >= 0x80 ? scan - 0x80 : scan) : scan;

            if (released) {
                if (_k1 == make) _k1 = 0;
                else if (_k2 == make) _k2 = 0;
            } else { // pressed
                if (_k1 == 0 || _k1 == make) { _k1 = make; } else if (_k2 == 0 || _k2 == make) { _k2 = make; } else { _k2 = make; }
            }
        }

        private void Keyboard_OnKeyChanged(object sender, ConsoleKeyInfo key) {
            if (!Visible) return;
            // Always update key status pair for statusbar
            UpdateStatusKeys(key);

            // Debug: show key info including modifiers
            if (false) { // Set to true to enable debug
                //Desktop.msgbox.SetText($"Scan: 0x{Keyboard.KeyInfo.ScanCode:X2} Char: '{key.KeyChar}' Key: {key.Key} Mods: {key.Modifiers}");
                //Desktop.msgbox.Visible = true;
            }

            if ((_dlg != null && _dlg.Visible) || (_confirmDlg != null && _confirmDlg.Visible)) return; // let dialog handle keys when visible
            if (key.KeyState != ConsoleKeyState.Pressed) { _keyDown = false; _lastScan = 0; return; }
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan) return; // de-bounce to avoid repeats
            _keyDown = true; _lastScan = (byte)Keyboard.KeyInfo.ScanCode;

            // Controls
            if (key.Key == ConsoleKey.Escape) { return; }
            if (key.Key == ConsoleKey.Backspace) { if (_text.Length > 0) { _text = _text.Substring(0, _text.Length - 1); _dirty = true; } return; }
            if (key.Key == ConsoleKey.Enter) { _text += "\n"; _dirty = true; return; }
            if (key.Key == ConsoleKey.Tab) { _text += "    "; _dirty = true; return; }

            char ch = MapFromKey(key);
            if (ch != '\0') { _text += ch; _dirty = true; }
        }

        private void SaveTo(string path) {
            // Save to Desktop.Dir + notes.txt
            byte[] data = new byte[_text.Length]; for (int i = 0; i < _text.Length; i++) data[i] = (byte)_text[i];
            File.WriteAllBytes(path, data); data.Dispose();
            _savedPath = path; _fileName = path.Substring(path.LastIndexOf('/') + 1); _dirty = false;
            Desktop.InvalidateDirCache();
            // Feedback
            Desktop.msgbox.X = X + 40; Desktop.msgbox.Y = Y + 80;
            Desktop.msgbox.SetText($"Saved: {path}");
            WindowManager.MoveToEnd(Desktop.msgbox); Desktop.msgbox.Visible = true;
            RecentManager.AddDocument(path, Icons.DocumentIcon(32));
        }

        private void OpenSaveAs(Action afterSaveClose = null) {
            _dlg = new SaveDialog(X + 40, Y + 40, 520, 360, Desktop.Dir, _fileName, (p) => { SaveTo(p); afterSaveClose?.Invoke(); });
            WindowManager.MoveToEnd(_dlg); _dlg.Visible = true;
        }

        private static bool StartsWithFast(string s, string pref) { int l = pref.Length; if (s == null || s.Length < l) return false; for (int i = 0; i < l; i++) if (s[i] != pref[i]) return false; return true; }
        public void OpenFile(string path) {
            if (string.IsNullOrEmpty(path)) return; string p = path; if (!StartsWithFast(p, "/") && !string.IsNullOrEmpty(Desktop.Dir)) p = Desktop.Dir + p; byte[] data = File.ReadAllBytes(p);
            if (data != null) { char[] chars = new char[data.Length]; for (int i = 0; i < data.Length; i++) { byte b = data[i]; chars[i] = b >= 32 && b < 127 ? (char)b : (b == 10 ? '\n' : '.'); } _text = new string(chars); data.Dispose(); _savedPath = p; _fileName = p.Substring(p.LastIndexOf('/') + 1); _dirty = false; Title = "Notepad - " + _fileName; RecentManager.AddDocument(p, Icons.DocumentIcon(32)); } else { _text = string.Empty; _savedPath = p; _fileName = p.Substring(p.LastIndexOf('/') + 1); _dirty = false; Title = "Notepad - " + _fileName; }
        }

        public override void OnInput() {
            base.OnInput(); if ((_dlg != null && _dlg.Visible) || (_confirmDlg != null && _confirmDlg.Visible)) return;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y;
            int bxSaveAs = X + _padding; int by = Y + _padding;
            int bxSave = bxSaveAs + _btnWSaveAs + 8; int bxWrap = bxSave + _btnWSave + 8;
            bool canSave = !string.IsNullOrEmpty(_savedPath) && _dirty;
            if (left) {
                if (!_clickLock) {
                    if (mx >= bxSaveAs && mx <= bxSaveAs + _btnWSaveAs && my >= by && my <= by + _btnH) { OpenSaveAs(); _clickLock = true; return; }
                    if (canSave && mx >= bxSave && mx <= bxSave + _btnWSave && my >= by && my <= by + _btnH) { SaveTo(_savedPath); _clickLock = true; return; }
                    if (mx >= bxWrap && mx <= bxWrap + _btnWWrap && my >= by && my <= by + _btnH) { _wrap = !_wrap; _clickLock = true; return; }
                }
            } else { _clickLock = false; }
        }

        public override void OnDraw() {
            base.OnDraw(); int cx = X + _padding; int cy = Y + _padding; int cw = Width - _padding * 2; int ch = Height - _padding * 2;
            // Buttons
            int bxSaveAs = cx; int by = cy; int bxSave = bxSaveAs + _btnWSaveAs + 8; int bxWrap = bxSave + _btnWSave + 8;
            // Visual feedback using UI helper
            uint cSaveAs = UI.ButtonFillColor(bxSaveAs, by, _btnWSaveAs, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(bxSaveAs, by, _btnWSaveAs, _btnH, cSaveAs); WindowManager.font.DrawString(bxSaveAs + 6, by + (_btnH / 2 - WindowManager.font.FontSize / 2), "Save As");
            bool canSave = !string.IsNullOrEmpty(_savedPath) && _dirty;
            uint baseSave = canSave ? 0xFF3A3A3Au : 0xFF2A2A2Au;
            uint cSave = UI.ButtonFillColor(bxSave, by, _btnWSave, _btnH, baseSave, 0xFF444444, 0xFF4A4A4A, canSave);
            Framebuffer.Graphics.FillRectangle(bxSave, by, _btnWSave, _btnH, cSave);
            WindowManager.font.DrawString(bxSave + 12, by + (_btnH / 2 - WindowManager.font.FontSize / 2), "Save");
            uint cWrap = UI.ButtonFillColor(bxWrap, by, _btnWWrap, _btnH, 0xFF3A3A3A, 0xFF444444, 0xFF4A4A4A);
            Framebuffer.Graphics.FillRectangle(bxWrap, by, _btnWWrap, _btnH, cWrap); WindowManager.font.DrawString(bxWrap + 10, by + (_btnH / 2 - WindowManager.font.FontSize / 2), _wrap ? "Wrap" : "NoWrap");

            int tx = cx; int ty = cy + _btnH + 8; int tw = cw; int th = ch - (_btnH + 8) - (_statusH + 6);
            if (th < 20) th = 20; // guard
            Framebuffer.Graphics.AFillRectangle(tx, ty, tw, th, 0x80282828);
            // Word wrap toggle
            if (_wrap) WindowManager.font.DrawString(tx + 6, ty + 6, _text, tw - 12, WindowManager.font.FontSize * 3);
            else WindowManager.font.DrawString(tx + 6, ty + 6, _text);

            // Status bar (bottom)
            DrawStatusBar(cx, cy, cw, ch);
        }

        private void DrawStatusBar(int cx, int cy, int cw, int ch) {
            int sx = cx; int sy = Y + Height - _padding - _statusH; int sw = cw; int sh = _statusH;
            // background and border
            Framebuffer.Graphics.FillRectangle(sx, sy, sw, sh, 0xFF252525);
            Framebuffer.Graphics.DrawRectangle(sx, sy, sw, sh, 0xFF3A3A3A, 1);

            // Compose hex keys text (up to two) in format: 0xNN, 0xNN
            string hex = string.Empty;
            if (_k1 != 0 && _k2 != 0) hex = "0x" + _k1.ToString("X2") + ", " + "0x" + _k2.ToString("X2");
            else if (_k1 != 0) hex = "0x" + _k1.ToString("X2");
            else if (_k2 != 0) hex = "0x" + _k2.ToString("X2");

            int textW = WindowManager.font.MeasureString(hex);
            int rightPadding = 8;
            int tx = sx + sw - rightPadding - textW;
            int ty = sy + (sh / 2) - (WindowManager.font.FontSize / 2);
            if (textW > 0) WindowManager.font.DrawString(tx, ty, hex);

            // Badges for modifiers to the left of hex text
            bool shift = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool caps = Keyboard.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.CapsLock);
            int gap = 8;
            int curX = tx - gap;
            int badgeH = WindowManager.font.FontSize + 6;
            int badgeY = sy + (sh / 2) - (badgeH / 2);

            if (caps) { curX = DrawBadge(curX, badgeY, "CAPS"); curX -= 6; }
            if (shift) { curX = DrawBadge(curX, badgeY, "SHIFT"); curX -= 6; }
        }

        private int DrawBadge(int rightX, int y, string text) {
            int padX = 8; int h = WindowManager.font.FontSize + 6; int w = WindowManager.font.MeasureString(text) + padX * 2;
            int x = rightX - w;
            // subtle translucent fill and border (blue-ish like selection)
            UIPrimitives.AFillRoundedRect(x, y, w, h, 0x332A5B9A, 6);
            UIPrimitives.DrawRoundedRect(x, y, w, h, 0xFF3F7FBF, 1, 6);
            WindowManager.font.DrawString(x + padX, y + (h / 2 - WindowManager.font.FontSize / 2), text);
            return x; // return new left edge to continue placing leftwards
        }

        public override void Dispose() {
            // CRITICAL FIX: Unsubscribe from keyboard events to prevent memory leak
            Keyboard.OnKeyChanged -= Keyboard_OnKeyChanged;
            base.Dispose();
        }
    }
}