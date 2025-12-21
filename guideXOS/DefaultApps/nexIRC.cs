using guideXOS.GUI;
using guideXOS.Misc;
using guideXOS.Kernel.Drivers;
using System.Windows.Forms;
using guideXOS; // NETv4
using System;
using System.Collections.Generic;
using guideXOS.OS;

namespace guideXOS.DefaultApps {
    internal class nexIRC : Window {
        private int _pad = 10;
        private string _status = "Disconnected";
        private string _input = string.Empty;
        private bool _clickLock;
        private bool _keyDown;
        private byte _lastScan;
        private bool _connecting;
        private ulong _connectStart;
        private NETv4.TCPClient _tcp;
        private NETv4.IPAddress _serverIp;
        private
        const ushort IRC_PORT = 6667;
        private static ushort _nextEphemeral = 40000;
        private string _nick = "guideXOS";
        private string _user = "guidexos";
        private string _real = "guideXOS User";
        private string _currentChannel = "#guideXOS";
        private List<string> _log;
        private List<string> _channels;
        private string _buffer = string.Empty;
        private ulong _lastPingResponseTick;
        private bool _handshakeSent;

        public nexIRC(int x, int y) : base(x, y, 800, 520) {
            IsResizable = true;
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            Title = "nexIRC";
            ShowInTaskbar = true;
            _log = new List<string>(256);
            _channels = new List<string>(32);
            _channels.Add(_currentChannel);
            Keyboard.OnKeyChanged += Keyboard_OnKeyChanged;
            TryAutoConnect();
        }

        private static ushort NextEphemeralPort() {
            if (_nextEphemeral < 40000 || _nextEphemeral > 60000) _nextEphemeral = 40000;
            return _nextEphemeral++;
        }

        private void TryAutoConnect() {
            if (_connecting || _tcp != null) return;
            if (NETv4.IP.P1 == 0 && NETv4.IP.P2 == 0 && NETv4.IP.P3 == 0 && NETv4.IP.P4 == 0) {
                _status = "Waiting for network...";
                return;
            }
            if (!Firewall.Check("nexIRC", "tcp-connect")) {
                _status = "Blocked by firewall";
                return;
            }
            _status = "Resolving irc.libera.chat";
            _serverIp = NETv4.DNSQuery("irc.libera.chat");
            if (_serverIp.P1 == 0 && _serverIp.P2 == 0 && _serverIp.P3 == 0 && _serverIp.P4 == 0) {
                // fallback to oftc
                _status = "Resolving irc.oftc.net";
                _serverIp = NETv4.DNSQuery("irc.oftc.net");
            }
            if (_serverIp.P1 == 0 && _serverIp.P2 == 0 && _serverIp.P3 == 0 && _serverIp.P4 == 0) {
                _status = "DNS failed";
                return;
            }
            ushort local = NextEphemeralPort();
            _tcp = new NETv4.TCPClient(_serverIp, IRC_PORT, local);
            _tcp.Connect();
            _connecting = true;
            _connectStart = Timer.Ticks;
            _status = "Connecting...";
        }

        private void Keyboard_OnKeyChanged(object sender, System.ConsoleKeyInfo key) {
            if (!Visible) return;
            if (key.KeyState != System.ConsoleKeyState.Pressed) {
                _keyDown = false;
                _lastScan = 0;
                return;
            }
            if (_keyDown && Keyboard.KeyInfo.ScanCode == _lastScan) return;
            _keyDown = true;
            _lastScan = (byte)Keyboard.KeyInfo.ScanCode;
            if (key.Key == System.ConsoleKey.Escape) {
                this.Visible = false;
                return;
            }
            if (key.Key == System.ConsoleKey.Enter) {
                SubmitInput();
                return;
            }
            if (key.Key == System.ConsoleKey.Backspace) {
                if (_input.Length > 0) _input = _input.Substring(0, _input.Length - 1);
                return;
            }
            char c = MapChar(key);
            if (c != '\0') _input += c;
        }

        private char MapChar(System.ConsoleKeyInfo key) {
            if (key.KeyChar != '\0') return key.KeyChar;
            var k = key.Key;
            bool shift = Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Shift);
            if (k >= System.ConsoleKey.A && k <= System.ConsoleKey.Z) {
                char ch = (char)('a' + (k - System.ConsoleKey.A));
                if (shift) ch = (char)('A' + (ch - 'a'));
                return ch;
            }
            if (k >= System.ConsoleKey.D0 && k <= System.ConsoleKey.D9) {
                int d = (int)(k - System.ConsoleKey.D0);
                if (!shift) return (char)('0' + d);
                switch (d) {
                    case 1:
                        return '!';
                    case 2:
                        return '@';
                    case 3:
                        return '#';
                    case 4:
                        return '$';
                    case 5:
                        return '%';
                    case 6:
                        return '^';
                    case 7:
                        return '&';
                    case 8:
                        return '*';
                    case 9:
                        return '(';
                    case 0:
                        return ')';
                }
            }
            switch (k) {
                case System.ConsoleKey.Space:
                    return ' ';
                case System.ConsoleKey.OemPeriod:
                    return shift ? '>' : '.';
                case System.ConsoleKey.OemComma:
                    return shift ? '<' : ',';
                case System.ConsoleKey.OemMinus:
                    return shift ? '_' : '-';
                case System.ConsoleKey.OemPlus:
                    return shift ? '+' : '=';
                case System.ConsoleKey.Oem1:
                    return shift ? ':' : ';';
                case System.ConsoleKey.Oem2:
                    return shift ? '?' : '/';
                case System.ConsoleKey.Oem3:
                    return shift ? '~' : '`';
                case System.ConsoleKey.Oem4:
                    return shift ? '{' : '[';
                case System.ConsoleKey.Oem5:
                    return shift ? '|' : '\\';
                case System.ConsoleKey.Oem6:
                    return shift ? '}' : ']';
                case System.ConsoleKey.Oem7:
                    return shift ? '"' : '\'';
            }
            return '\0';
        }

        private string TrimTrailing(string s) {
            int i = s.Length;
            while (i > 0 && (s[i - 1] == ' ' || s[i - 1] == '\r' || s[i - 1] == '\n' || s[i - 1] == '\t')) i--;
            if (i == s.Length) return s;
            return s.Substring(0, i);
        }
        private string ToLowerFast(string s) {
            char[] c = new char[s.Length];
            for (int i = 0; i < s.Length; i++) {
                char ch = s[i];
                if (ch >= 'A' && ch <= 'Z') ch = (char)(ch + 32);
                c[i] = ch;
            }
            return new string(c);
        }
        private bool StartsWithFast(string s, string prefix) {
            int l = prefix.Length;
            if (s.Length < l) return false;
            for (int i = 0; i < l; i++) {
                if (s[i] != prefix[i]) return false;
            }
            return true;
        }

        private void SubmitInput() {
            string line = TrimTrailing(_input);
            _input = string.Empty;
            if (line.Length == 0) return;
            if (line[0] == '/') {
                HandleCommand(line);
                return;
            }
            if (_tcp == null || _tcp.Status != NETv4.TCPStatus.Established) {
                AddLog("Not connected.");
                return;
            }
            SendIrc("PRIVMSG " + _currentChannel + " :" + line + "\r\n");
            AddLog("<" + _nick + "> " + line);
        }

        private void HandleCommand(string cmdLine) {
            var parts = Split(cmdLine, ' ');
            string cmd = ToLowerFast(parts[0]);
            if (cmd == "/connect") {
                TryAutoConnect();
                return;
            }
            if (cmd == "/join") {
                if (parts.Length >= 2) {
                    string chan = parts[1];
                    if (!StartsWithFast(chan, "#")) chan = "#" + chan;
                    SendIrc("JOIN " + chan + "\r\n");
                    _currentChannel = chan;
                    if (_channels.IndexOf(chan) == -1) _channels.Add(chan);
                    AddLog("Joining " + chan);
                } else AddLog("Usage: /join #channel");
                return;
            }
            if (cmd == "/nick") {
                if (parts.Length >= 2) {
                    _nick = parts[1];
                    if (_tcp != null && _tcp.Status == NETv4.TCPStatus.Established) SendIrc("NICK " + _nick + "\r\n");
                    AddLog("Nick set to " + _nick);
                } else AddLog("Usage: /nick <name>");
                return;
            }
            if (cmd == "/quit") {
                string msg = parts.Length > 1 ? Join(parts, ' ', 1) : "Bye";
                if (_tcp != null) SendIrc("QUIT :" + msg + "\r\n");
                _status = "Disconnected";
                if (_tcp != null) {
                    _tcp.Close();
                    _tcp.Remove();
                }
                _tcp = null;
                _connecting = false;
                AddLog("Disconnected.");
                return;
            }
            if (cmd == "/clear") {
                _log.Clear();
                AddLog("Log cleared");
                return;
            }
            AddLog("Unknown command: " + cmd);
        }

        private string[] Split(string s, char sep) {
            List<string> parts = new List<string>();
            string cur = "";
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                if (c == sep) {
                    if (cur.Length > 0) {
                        parts.Add(cur);
                        cur = "";
                    }
                } else {
                    cur += c;
                }
            }
            if (cur.Length > 0) parts.Add(cur);
            return parts.ToArray();
        }
        private string Join(string[] arr, char sep, int start) {
            if (start >= arr.Length) return string.Empty;
            string r = arr[start];
            for (int i = start + 1; i < arr.Length; i++) {
                r += sep;
                r += arr[i];
            }
            return r;
        }

        private void SendIrc(string text) {
            if (_tcp == null) return;
            var b = ToAscii(text);
            unsafe {
                fixed (byte* p = b) {
                    _tcp.Send(p, b.Length);
                }
            }
        }
        private static byte[] ToAscii(string s) {
            byte[] b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                b[i] = c < 128 ? (byte)c : (byte)
                '?';
            }
            return b;
        }

        private void AddLog(string line) {
            _log.Add(line);
            if (_log.Count > 600) {
                for (int i = 0; i < 200; i++) _log.RemoveAt(0);
            }
        }

        private void PollNetwork() {
            if (_tcp == null) return;
            if (_connecting) {
                if (_tcp.Status == NETv4.TCPStatus.Established) {
                    _connecting = false;
                    _status = "Connected";
                    SendHandshake();
                } else if ((long)(Timer.Ticks - _connectStart) > 10000) {
                    _status = "Timeout";
                    _connecting = false;
                    _tcp.Close();
                    _tcp.Remove();
                    _tcp = null;
                }
            }
            if (_tcp != null && _tcp.Status == NETv4.TCPStatus.Established) {
                for (; ; ) {
                    var data = _tcp.Receive();
                    if (data == null) break;
                    ProcessIncoming(data);
                }
                if (_lastPingResponseTick != 0 && (long)(Timer.Ticks - _lastPingResponseTick) > 60000) {
                    AddLog("Ping timeout");
                    _status = "Disconnected";
                    _tcp.Close();
                    _tcp.Remove();
                    _tcp = null;
                }
            }
        }

        private void SendHandshake() {
            if (_handshakeSent) return;
            _handshakeSent = true;
            SendIrc("NICK " + _nick + "\r\n");
            SendIrc("USER " + _user + " 0 * :" + _real + "\r\n");
            SendIrc("JOIN " + _currentChannel + "\r\n");
            AddLog("Handshake sent");
        }
        private void ProcessIncoming(byte[] data) {
            for (int i = 0; i < data.Length; i++) {
                char c = (char)data[i];
                if (c == '\r') continue;
                if (c == '\n') {
                    HandleLine(_buffer);
                    _buffer = string.Empty;
                } else _buffer += (c < 128 ? c : '?');
            }
        }
        private void HandleLine(string line) {
            if (line.Length == 0) return;
            if (StartsWithFast(line, "PING ")) {
                string token = line.Substring(5);
                SendIrc("PONG " + token + "\r\n");
                AddLog("-> PONG " + token);
                _lastPingResponseTick = Timer.Ticks;
                return;
            }
            AddLog(line);
        }

        public override void OnInput() {
            base.OnInput();
            if (Control.MouseButtons.HasFlag(MouseButtons.Left)) _clickLock = true;
            else _clickLock = false;
            PollNetwork();
            if (_status == "Waiting for network..." && NETv4.IP.P1 != 0) TryAutoConnect();
        }

        public override void OnDraw() {
            base.OnDraw();
            PollNetwork();
            int cx = X + _pad;
            int cy = Y + _pad;
            int cw = Width - _pad * 2;
            int ch = Height - _pad * 2;
            int chanW = 180;
            int inputH = 28;
            int statusH = 24;
            int chatH = ch - (inputH + 8 + statusH);
            Framebuffer.Graphics.AFillRectangle(cx, cy, chanW, chatH, 0x80262626);
            WindowManager.font.DrawString(cx + 8, cy + 8, BuildChannelsText(), chanW - 16, chatH - 16);
            int chatX = cx + chanW + 8;
            int chatW = cw - chanW - 8;
            Framebuffer.Graphics.AFillRectangle(chatX, cy, chatW, chatH, 0x80282828);
            DrawChatLines(chatX + 8, cy + 8, chatW - 16, chatH - 16);
            int iy = Y + Height - _pad - inputH;
            Framebuffer.Graphics.FillRectangle(chatX, iy, chatW, inputH, 0xFF2E2E2E);
            WindowManager.font.DrawString(chatX + 6, iy + (inputH / 2 - WindowManager.font.FontSize / 2), _input, chatW - 12, WindowManager.font.FontSize);
            int sy = iy - (statusH + 8);
            Framebuffer.Graphics.FillRectangle(cx, sy, cw, statusH, 0xFF252525);
            WindowManager.font.DrawString(cx + 8, sy + 4, _status);
        }
        private string BuildChannelsText() {
            string s = "Channels\n";
            var arr = _channels.ToArray();
            for (int i = 0; i < arr.Length; i++) {
                s += arr[i];
                if (arr[i] == _currentChannel) s += " *";
                s += "\n";
            }
            return s;
        }
        private void DrawChatLines(int x, int y, int w, int h) {
            int lineH = WindowManager.font.FontSize + 4;
            int maxLines = h / lineH;
            if (maxLines < 1) maxLines = 1;
            int total = _log.Count;
            int start = total > maxLines ? total - maxLines : 0;
            int cy = y;
            var arr = _log.ToArray();
            for (int i = start; i < total; i++) {
                WindowManager.font.DrawString(x, cy, arr[i], w, WindowManager.font.FontSize);
                cy += lineH;
            }
        }
    }
}