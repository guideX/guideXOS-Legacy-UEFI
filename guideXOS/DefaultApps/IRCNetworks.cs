using guideXOS.GUI;
using guideXOS.Misc;
using guideXOS.Kernel.Drivers;
using System.Windows.Forms;
using System; using System.Collections.Generic;

namespace guideXOS.DefaultApps {
    // Simple IRC network profile manager window
    internal class IRCNetworks : Window {
        private class Entry { public string Name; public string Host; public ushort Port; public string Nick; public string User; public string Real; }
        private List<Entry> _entries = new List<Entry>(32);
        private int _selected = -1;
        private bool _clickLatch;
        private string _status = "Select or add a network";
        private int _pad = 10;
        private int _rowH = 28;
        public IRCNetworks(int x, int y) : base(x, y, 560, 420) { 
            Title = "IRC Networks"; 
            ShowInTaskbar = true; 
            LoadDefaults(); 
        }

        private void LoadDefaults() {
            if (_entries.Count == 0) {
                _entries.Add(new Entry { Name = "Libera", Host = "irc.libera.chat", Port = 6667, Nick = "guideXOS", User = "guidexos", Real = "guideXOS User" });
                _entries.Add(new Entry { Name = "OFTC", Host = "irc.oftc.net", Port = 6667, Nick = "guideXOS", User = "guidexos", Real = "guideXOS User" });
            }
        }

        public override void OnInput() {
            base.OnInput(); if (!Visible) return;
            int mx = Control.MousePosition.X; int my = Control.MousePosition.Y; bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);
            int listX = X + _pad; int listY = Y + _pad + 32; int listW = 220; int listH = Height - _pad*2 - 120;
            if (left) {
                if (!_clickLatch) {
                    if (mx >= listX && mx <= listX + listW && my >= listY && my <= listY + listH) {
                        int rel = (my - listY) / _rowH; if (rel >=0 && rel < _entries.Count) _selected = rel;
                    } else {
                        // buttons
                        int bxAdd = X + _pad; int byAdd = Y + Height - _pad - 32;
                        if (mx>=bxAdd && mx<=bxAdd+100 && my>=byAdd && my<=byAdd+28) { AddEntry(); }
                        int bxDel = bxAdd + 110;
                        if (_selected>=0 && mx>=bxDel && mx<=bxDel+100 && my>=byAdd && my<=byAdd+28) { _entries.RemoveAt(_selected); _selected=-1; }
                        int bxConnect = bxDel + 110;
                        if (_selected>=0 && mx>=bxConnect && mx<=bxConnect+120 && my>=byAdd && my<=byAdd+28) { AttemptConnect(); }
                    }
                    _clickLatch = true;
                }
            } else _clickLatch = false;
        }

        private void AddEntry() { _entries.Add(new Entry { Name = "NewNet", Host = "irc.example.net", Port = 6667, Nick = "user", User = "user", Real = "Real Name" }); _status = "Added network"; }
        private void AttemptConnect() { if (_selected<0) return; var e = _entries[_selected]; _status = "Launching nexIRC..."; // spawn client with profile
            var irc = new nexIRC(X + Width + 20, Y + 20); irc.Visible = true; WindowManager.MoveToEnd(irc); }

        public override void OnDraw() {
            base.OnDraw(); if (!Visible) return;
            int cx = X + _pad; int cy = Y + _pad; int cw = Width - _pad*2; int ch = Height - _pad*2;
            // Left list
            int listW = 220; int listH = ch - 120; int listY = cy + 32; Framebuffer.Graphics.FillRectangle(cx, listY, listW, listH, 0xFF262626);
            WindowManager.font.DrawString(cx, cy, "Networks");
            for (int i=0;i<_entries.Count;i++) {
                int yRow = listY + i * _rowH; if (yRow + _rowH > listY + listH) break;
                uint bg = (i==_selected)?0xFF303A42u:0xFF2A2A2Au; Framebuffer.Graphics.FillRectangle(cx+1, yRow, listW-2, _rowH-1, bg);
                WindowManager.font.DrawString(cx+8, yRow + (_rowH/2 - WindowManager.font.FontSize/2), _entries[i].Name, listW-16, WindowManager.font.FontSize);
            }
            // Right detail
            int dx = cx + listW + 16; int dw = cw - listW - 16; int dy = cy; int dh = ch - 60; Framebuffer.Graphics.FillRectangle(dx, dy, dw, dh, 0xFF272727);
            if (_selected >= 0) {
                var e = _entries[_selected];
                int y = dy + 8;
                DrawField(dx + 8, ref y, "Name", e.Name);
                DrawField(dx + 8, ref y, "Host", e.Host);
                DrawField(dx + 8, ref y, "Port", e.Port.ToString());
                DrawField(dx + 8, ref y, "Nick", e.Nick);
                DrawField(dx + 8, ref y, "User", e.User);
                DrawField(dx + 8, ref y, "Real", e.Real);
            } else {
                WindowManager.font.DrawString(dx + 8, dy + 8, "Select a network");
            }
            // Buttons
            int by = Y + Height - _pad - 32;
            DrawButton(cx, by, 100, 28, "Add");
            DrawButton(cx + 110, by, 100, 28, "Remove");
            DrawButton(cx + 220, by, 120, 28, "Connect");
            // Status
            WindowManager.font.DrawString(cx, by - 24, _status);
        }

        private void DrawField(int x, ref int y, string label, string value) { WindowManager.font.DrawString(x, y, label + ": " + value); y += WindowManager.font.FontSize + 6; }
        private void DrawButton(int x,int y,int w,int h,string text){ Framebuffer.Graphics.FillRectangle(x,y,w,h,0xFF2E2E2E); WindowManager.font.DrawString(x+8,y+(h/2-WindowManager.font.FontSize/2),text); }
    }
}
