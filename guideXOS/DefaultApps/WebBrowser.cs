using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using System.Windows.Forms;
using guideXOS.OS;
using System.Collections.Generic;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// Web Browser with HTML rendering and tabs
    /// </summary>
    internal class WebBrowser : Window {
        // Tab management
        private class BrowserTab {
            public string Url = "http://example.com/";
            public string Title = "New Tab";
            public string Status = "";
            public string RawHtml = string.Empty;
            public List<RenderElement> RenderedContent = new List<RenderElement>();
            public int ScrollOffset = 0;
            public NETv4.TCPClient Http;
            public byte[] RecvBuf;
            public int ContentStartIndex = -1;
            public bool IsLoading = false;
        }

        private class RenderElement {
            public string Type; // "text", "heading", "link", "paragraph"
            public string Content;
            public int X, Y, Width, Height;
            public uint Color;
            public bool IsBold;
            public string LinkUrl; // For links
        }

        private List<BrowserTab> _tabs = new List<BrowserTab>();
        private int _currentTab = 0;
        private bool _typingUrl = false;
        private string _urlEdit = string.Empty;
        private bool _tabClickLatch = false;
        private bool _newTabLatch = false;
        private bool _closeTabLatch = false;
        private int _hoverTab = -1;
        private bool _scrollDrag = false;
        private int _scrollStart = 0;
        private int _scrollStartY = 0;

        // Layout constants
        private const int TabBarHeight = 32;
        private const int UrlBarHeight = 32;
        private const int TabWidth = 160;
        private const int TabGap = 4;
        private const int NewTabBtnWidth = 28;
        private const int ContentPadding = 12;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public WebBrowser(int x, int y) : base(x, y, 900, 600) {
            Title = "Web Browser";
            ShowInTaskbar = true;
            ShowMaximize = true;
            ShowMinimize = true;
            ShowTombstone = true;
            ShowInStartMenu = true;
            IsResizable = true;
            Keyboard.OnKeyChanged += OnKey;

            // Create initial tab
            _tabs.Add(new BrowserTab());
        }

        /// <summary>
        /// On Key
        /// </summary>
        private void OnKey(object s, System.ConsoleKeyInfo key) {
            if (!Visible)
                return;
            if (key.KeyState != System.ConsoleKeyState.Pressed)
                return;

            // If not in typing mode, check for shortcuts only
            if (!_typingUrl) {
                // Ctrl+T for new tab
                if (key.Key == System.ConsoleKey.T && Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Control)) {
                    AddNewTab();
                    return;
                }

                // Ctrl+W to close tab
                if (key.Key == System.ConsoleKey.W && Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Control)) {
                    CloseCurrentTab();
                    return;
                }
                
                // Ctrl+L or F6 to focus URL bar
                if ((key.Key == System.ConsoleKey.L && Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Control)) ||
                    key.Key == System.ConsoleKey.F6) {
                    if (_currentTab >= 0 && _currentTab < _tabs.Count) {
                        _typingUrl = true;
                        _urlEdit = _tabs[_currentTab].Url;
                    }
                    return;
                }
                
                return; // Don't process other keys when not typing
            }

            // Typing mode active - handle text input
            if (key.Key == System.ConsoleKey.Enter) {
                if (_currentTab >= 0 && _currentTab < _tabs.Count) {
                    _tabs[_currentTab].Url = _urlEdit;
                    _typingUrl = false;
                    StartRequest(_tabs[_currentTab]);
                }
                return;
            }
            if (key.Key == System.ConsoleKey.Escape) {
                _typingUrl = false;
                return;
            }
            if (key.Key == System.ConsoleKey.Backspace) {
                if (_urlEdit.Length > 0)
                    _urlEdit = _urlEdit.Substring(0, _urlEdit.Length - 1);
                return;
            }

            // Ctrl+T for new tab (also works in typing mode)
            if (key.Key == System.ConsoleKey.T && Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Control)) {
                AddNewTab();
                return;
            }

            // Ctrl+W to close tab (also works in typing mode)
            if (key.Key == System.ConsoleKey.W && Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Control)) {
                CloseCurrentTab();
                return;
            }

            char c = MapChar(key);
            if (c != '\0')
                _urlEdit += c;
        }

        /// <summary>
        /// Map Char
        /// </summary>
        private char MapChar(System.ConsoleKeyInfo key) {
            if (key.KeyChar != '\0')
                return key.KeyChar;
            bool shift = Keyboard.KeyInfo.Modifiers.HasFlag(System.ConsoleModifiers.Shift);
            switch (key.Key) {
                case System.ConsoleKey.Space: return ' ';
                case System.ConsoleKey.OemPeriod: return shift ? '>' : '.';
                case System.ConsoleKey.OemComma: return shift ? '<' : ',';
                case System.ConsoleKey.OemMinus: return shift ? '_' : '-';
                case System.ConsoleKey.OemPlus: return shift ? '+' : '=';
                case System.ConsoleKey.Oem2: return shift ? '?' : '/';
                case System.ConsoleKey.Oem3: return shift ? '~' : '`';
                case System.ConsoleKey.Oem4: return shift ? '{' : '[';
                case System.ConsoleKey.Oem5: return shift ? '|' : '\\';
                case System.ConsoleKey.Oem6: return shift ? '}' : ']';
                case System.ConsoleKey.Oem7: return shift ? '"' : '\'';
            }
            return '\0';
        }

        /// <summary>
        /// On Input
        /// </summary>
        public override void OnInput() {
            base.OnInput();
            if (!Visible) return;

            int mx = Control.MousePosition.X;
            int my = Control.MousePosition.Y;
            bool left = Control.MouseButtons.HasFlag(MouseButtons.Left);

            // Tab bar interaction
            int tabY = Y + TabBarHeight;
            int tabX = X + 8;

            _hoverTab = -1;

            // Check tab clicks
            if (my >= Y && my <= tabY) {
                for (int i = 0; i < _tabs.Count; i++) {
                    int tx = tabX + i * (TabWidth + TabGap);
                    if (mx >= tx && mx <= tx + TabWidth) {
                        _hoverTab = i;
                        if (left && !_tabClickLatch) {
                            // Check if clicking close button
                            int closeBtnX = tx + TabWidth - 24;
                            if (mx >= closeBtnX && mx <= closeBtnX + 20) {
                                CloseTab(i);
                                _tabClickLatch = true;
                                _closeTabLatch = true;
                            } else {
                                _currentTab = i;
                                _tabClickLatch = true;
                            }
                        }
                        break;
                    }
                }

                // New tab button
                int newTabX = tabX + _tabs.Count * (TabWidth + TabGap);
                if (mx >= newTabX && mx <= newTabX + NewTabBtnWidth && my >= Y + 4 && my <= tabY - 4) {
                    if (left && !_newTabLatch) {
                        AddNewTab();
                        _newTabLatch = true;
                    }
                }
            }

            if (!left) {
                _tabClickLatch = false;
                _newTabLatch = false;
                _closeTabLatch = false;
            }

            if (_currentTab < 0 || _currentTab >= _tabs.Count) return;
            var tab = _tabs[_currentTab];

            // URL bar interaction
            int urlBarY = Y + TabBarHeight;
            int urlX = X + ContentPadding;
            int urlY = urlBarY + 8;
            int urlW = Width - ContentPadding * 2;
            int urlH = UrlBarHeight - 8;

            if (left && mx >= urlX && mx <= urlX + urlW && my >= urlY && my <= urlY + urlH) {
                _typingUrl = true;
                _urlEdit = tab.Url;
            }

            // Content scrolling
            int contentY = urlBarY + UrlBarHeight + 8;
            int contentH = Height - (contentY - Y) - ContentPadding;
            
            // Scroll wheel
            // (Note: Control.MouseScroll not available in this environment, would need custom implementation)

            // Check link clicks in rendered content
            if (left && !tab.IsLoading) {
                for (int i = 0; i < tab.RenderedContent.Count; i++) {
                    var elem = tab.RenderedContent[i];
                    if (elem.Type == "link" && elem.LinkUrl != null) {
                        int elemY = elem.Y - tab.ScrollOffset;
                        if (mx >= elem.X && mx <= elem.X + elem.Width && 
                            my >= elemY && my <= elemY + elem.Height) {
                            tab.Url = elem.LinkUrl;
                            StartRequest(tab);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// On Draw
        /// </summary>
        public override void OnDraw() {
            base.OnDraw();

            // Poll network on current tab
            if (_currentTab >= 0 && _currentTab < _tabs.Count) {
                PollReceive(_tabs[_currentTab]);
            }

            // Draw tab bar
            DrawTabBar();

            if (_currentTab < 0 || _currentTab >= _tabs.Count) return;
            var tab = _tabs[_currentTab];

            // Draw URL bar
            int urlBarY = Y + TabBarHeight;
            int urlX = X + ContentPadding;
            int urlY = urlBarY + 8;
            int urlW = Width - ContentPadding * 2;
            int urlH = UrlBarHeight - 8;

            Framebuffer.Graphics.FillRectangle(urlX, urlY, urlW, urlH, 0xFF2E2E2E);
            Framebuffer.Graphics.DrawRectangle(urlX, urlY, urlW, urlH, 0xFF444444, 1);

            string urlDisplay = _typingUrl ? _urlEdit : tab.Url;
            WindowManager.font.DrawString(urlX + 8, urlY + (urlH / 2 - WindowManager.font.FontSize / 2), 
                                         urlDisplay, urlW - 16, WindowManager.font.FontSize);

            // Draw content area
            int contentY = urlBarY + UrlBarHeight + 8;
            int contentH = Height - (contentY - Y) - ContentPadding;
            int contentW = urlW;

            Framebuffer.Graphics.FillRectangle(urlX, contentY, contentW, contentH, 0xFF1E1E1E);

            // Draw status if loading
            if (tab.IsLoading || !string.IsNullOrEmpty(tab.Status)) {
                WindowManager.font.DrawString(urlX + 8, contentY + 8, tab.Status);
            }

            // Render HTML content
            if (!tab.IsLoading && tab.RenderedContent.Count > 0) {
                DrawRenderedContent(tab, urlX + 8, contentY + 8, contentW - 16, contentH - 16);
            } else if (!tab.IsLoading && !string.IsNullOrEmpty(tab.RawHtml)) {
                // Fallback to raw HTML if rendering failed
                WindowManager.font.DrawString(urlX + 8, contentY + 32, tab.RawHtml, 
                                            contentW - 16, contentH - 40);
            }

            // Draw scrollbar if needed
            if (tab.RenderedContent.Count > 0) {
                DrawScrollbar(tab, urlX + contentW - 10, contentY, 10, contentH);
            }
        }

        /// <summary>
        /// Draw tab bar
        /// </summary>
        private void DrawTabBar() {
            int tabY = Y + TabBarHeight;
            Framebuffer.Graphics.FillRectangle(X, Y, Width, TabBarHeight, 0xFF2A2A2A);

            int tabX = X + 8;
            for (int i = 0; i < _tabs.Count && i < 8; i++) { // Max 8 visible tabs
                var tab = _tabs[i];
                int tx = tabX + i * (TabWidth + TabGap);
                int ty = Y + 4;
                int th = TabBarHeight - 4;

                bool isActive = (i == _currentTab);
                bool isHover = (i == _hoverTab);

                uint bg = isActive ? 0xFF1E1E1E : (isHover ? 0xFF353535 : 0xFF2E2E2E);
                Framebuffer.Graphics.FillRectangle(tx, ty, TabWidth, th, bg);
                Framebuffer.Graphics.DrawRectangle(tx, ty, TabWidth, th, 0xFF444444, 1);

                // Tab title
                string title = tab.Title;
                if (title.Length > 18) title = title.Substring(0, 15) + "...";
                WindowManager.font.DrawString(tx + 8, ty + (th / 2 - WindowManager.font.FontSize / 2), 
                                            title, TabWidth - 32, WindowManager.font.FontSize);

                // Close button
                int closeBtnX = tx + TabWidth - 24;
                int closeBtnY = ty + (th / 2) - 8;
                Framebuffer.Graphics.FillRectangle(closeBtnX, closeBtnY, 16, 16, isHover ? 0xFF4A4A4A : 0xFF3A3A3A);
                // Draw X
                WindowManager.font.DrawString(closeBtnX + 4, closeBtnY + 1, "×");
            }

            // New tab button
            int newTabX = tabX + _tabs.Count * (TabWidth + TabGap);
            int newTabY = Y + 4;
            int newTabH = TabBarHeight - 4;
            Framebuffer.Graphics.FillRectangle(newTabX, newTabY, NewTabBtnWidth, newTabH, 0xFF3A3A3A);
            Framebuffer.Graphics.DrawRectangle(newTabX, newTabY, NewTabBtnWidth, newTabH, 0xFF444444, 1);
            WindowManager.font.DrawString(newTabX + 8, newTabY + (newTabH / 2 - WindowManager.font.FontSize / 2), "+");
        }

        /// <summary>
        /// Draw rendered HTML content
        /// </summary>
        private void DrawRenderedContent(BrowserTab tab, int x, int y, int w, int h) {
            int clipY = y;
            int clipH = h;

            for (int i = 0; i < tab.RenderedContent.Count; i++) {
                var elem = tab.RenderedContent[i];
                int elemY = y + elem.Y - tab.ScrollOffset;

                // Clip to visible area
                if (elemY + elem.Height < clipY || elemY > clipY + clipH) continue;

                // Draw based on element type
                if (elem.Type == "link") {
                    // Draw link in blue with underline
                    WindowManager.font.DrawString(elem.X, elemY, elem.Content);
                    Framebuffer.Graphics.FillRectangle(elem.X, elemY + WindowManager.font.FontSize, 
                                                      elem.Width, 1, 0xFF5588FF);
                } else if (elem.Type == "heading") {
                    // Draw heading (bold effect by drawing twice)
                    WindowManager.font.DrawString(elem.X, elemY, elem.Content);
                    WindowManager.font.DrawString(elem.X + 1, elemY, elem.Content);
                } else {
                    // Regular text
                    WindowManager.font.DrawString(elem.X, elemY, elem.Content);
                }
            }
        }

        /// <summary>
        /// Draw scrollbar
        /// </summary>
        private void DrawScrollbar(BrowserTab tab, int x, int y, int w, int h) {
            Framebuffer.Graphics.FillRectangle(x, y, w, h, 0xFF1A1A1A);

            // Calculate total content height
            int maxY = 0;
            for (int i = 0; i < tab.RenderedContent.Count; i++) {
                var elem = tab.RenderedContent[i];
                int bottom = elem.Y + elem.Height;
                if (bottom > maxY) maxY = bottom;
            }

            if (maxY > h) {
                int thumbH = (h * h) / maxY;
                if (thumbH < 20) thumbH = 20;
                if (thumbH > h) thumbH = h;

                int maxScroll = maxY - h;
                int thumbY = y;
                if (maxScroll > 0) {
                    thumbY = y + (tab.ScrollOffset * (h - thumbH)) / maxScroll;
                }

                Framebuffer.Graphics.FillRectangle(x + 1, thumbY, w - 2, thumbH, 0xFF4A4A4A);
            }
        }

        /// <summary>
        /// Add new tab
        /// </summary>
        private void AddNewTab() {
            if (_tabs.Count >= 10) return; // Max 10 tabs
            _tabs.Add(new BrowserTab());
            _currentTab = _tabs.Count - 1;
        }

        /// <summary>
        /// Close tab
        /// </summary>
        private void CloseTab(int index) {
            if (_tabs.Count <= 1) return; // Keep at least one tab

            if (index >= 0 && index < _tabs.Count) {
                var tab = _tabs[index];
                if (tab.Http != null) {
                    tab.Http.Close();
                    tab.Http.Remove();
                }
                _tabs.RemoveAt(index);

                if (_currentTab >= _tabs.Count) {
                    _currentTab = _tabs.Count - 1;
                }
            }
        }

        /// <summary>
        /// Close current tab
        /// </summary>
        private void CloseCurrentTab() {
            CloseTab(_currentTab);
        }

        /// <summary>
        /// Poll Receive
        /// </summary>
        private void PollReceive(BrowserTab tab) {
            if (tab.Http == null) return;
            for (; ; ) {
                var data = tab.Http.Receive();
                if (data == null) break;
                AppendData(tab, data);
            }
        }

        /// <summary>
        /// Start Request
        /// </summary>
        private void StartRequest(BrowserTab tab) {
            tab.Status = "Requesting...";
            tab.RawHtml = string.Empty;
            tab.RenderedContent.Clear();
            tab.ScrollOffset = 0;
            tab.ContentStartIndex = -1;
            tab.IsLoading = true;

            if (tab.Http != null) {
                tab.Http.Close();
                tab.Http.Remove();
                tab.Http = null;
            }

            // Parse http://host/path
            if (!StartsWithFast(tab.Url, "http://")) {
                tab.Status = "Only http:// supported";
                tab.IsLoading = false;
                return;
            }

            string rest = tab.Url.Substring(7);
            int slash = IndexOf(rest, '/');
            string host = slash >= 0 ? rest.Substring(0, slash) : rest;
            string path = slash >= 0 ? rest.Substring(slash) : "/";

            var hostIp = NETv4.DNSQuery(host);
            if (hostIp.P1 == 0 && hostIp.P2 == 0 && hostIp.P3 == 0 && hostIp.P4 == 0) {
                tab.Status = "DNS failed";
                tab.IsLoading = false;
                return;
            }

            ushort local = NextEphemeral();
            if (!Firewall.Check("WebBrowser", "tcp-connect")) {
                tab.Status = "Blocked by firewall";
                tab.IsLoading = false;
                return;
            }

            tab.Http = new NETv4.TCPClient(hostIp, 80, local);
            tab.Http.Connect();
            tab.RecvBuf = new byte[0];

            string req = "GET " + path + " HTTP/1.0\r\nHost: " + host + 
                        "\r\nUser-Agent: guideXOS/0.2\r\nConnection: close\r\n\r\n";
            var b = ToAscii(req);
            unsafe {
                fixed (byte* p = b)
                    tab.Http.Send(p, b.Length);
            }
            tab.Status = "Waiting response...";
        }

        /// <summary>
        /// Append Data
        /// </summary>
        private void AppendData(BrowserTab tab, byte[] data) {
            int old = tab.RecvBuf == null ? 0 : tab.RecvBuf.Length;
            byte[] nb = new byte[old + data.Length];
            for (int i = 0; i < old; i++)
                nb[i] = tab.RecvBuf[i];
            for (int i = 0; i < data.Length; i++)
                nb[old + i] = data[i];
            tab.RecvBuf = nb;

            if (tab.ContentStartIndex < 0) {
                for (int i = 3; i < tab.RecvBuf.Length; i++) {
                    if (tab.RecvBuf[i - 3] == '\r' && tab.RecvBuf[i - 2] == '\n' &&
                        tab.RecvBuf[i - 1] == '\r' && tab.RecvBuf[i] == '\n') {
                        tab.ContentStartIndex = i + 1;
                        break;
                    }
                }
            }

            if (tab.ContentStartIndex >= 0) {
                int len = tab.RecvBuf.Length - tab.ContentStartIndex;
                char[] chars = new char[len];
                for (int i = 0; i < len; i++) {
                    byte c = tab.RecvBuf[tab.ContentStartIndex + i];
                    chars[i] = c < 128 ? (char)c : '?';
                }
                tab.RawHtml = new string(chars);
                tab.Status = "Rendering...";
                tab.IsLoading = false;

                // Parse and render HTML
                ParseAndRenderHtml(tab);
                tab.Status = "OK";

                // Extract title from HTML
                ExtractTitle(tab);
            }
        }

        /// <summary>
        /// Parse and render HTML
        /// </summary>
        private void ParseAndRenderHtml(BrowserTab tab) {
            tab.RenderedContent.Clear();

            string html = tab.RawHtml;
            int x = X + ContentPadding + 8;
            int y = 0;
            int maxWidth = Width - ContentPadding * 2 - 32;
            int lineHeight = WindowManager.font.FontSize + 4;

            // Very simple HTML parser
            int i = 0;
            string currentText = "";
            bool inTag = false;
            string currentTag = "";
            bool inParagraph = false;

            while (i < html.Length) {
                char c = html[i];

                if (c == '<') {
                    // Flush current text
                    if (currentText.Length > 0) {
                        AddTextElement(tab, ref x, ref y, currentText, maxWidth, lineHeight, currentTag);
                        currentText = "";
                    }

                    inTag = true;
                    currentTag = "";
                    i++;
                    continue;
                }

                if (c == '>') {
                    inTag = false;
                    string lowerTag = ToLowerCase(currentTag);

                    // Handle specific tags
                    if (lowerTag == "br" || lowerTag == "br/") {
                        y += lineHeight;
                        x = X + ContentPadding + 8;
                    } else if (lowerTag == "p" || lowerTag == "div") {
                        if (inParagraph) {
                            y += lineHeight * 2;
                        }
                        inParagraph = true;
                        x = X + ContentPadding + 8;
                    } else if (lowerTag == "/p" || lowerTag == "/div") {
                        y += lineHeight;
                        x = X + ContentPadding + 8;
                        inParagraph = false;
                    } else if (StartsWithFast(lowerTag, "h1") || StartsWithFast(lowerTag, "h2") || 
                              StartsWithFast(lowerTag, "h3")) {
                        y += lineHeight * 2;
                        x = X + ContentPadding + 8;
                    }

                    currentTag = "";
                    i++;
                    continue;
                }

                if (inTag) {
                    currentTag += c;
                } else {
                    // Accumulate text content
                    if (c != '\r' && c != '\n' && c != '\t') {
                        currentText += c;
                    } else if (currentText.Length > 0) {
                        currentText += ' ';
                    }
                }

                i++;
            }

            // Flush remaining text
            if (currentText.Length > 0) {
                AddTextElement(tab, ref x, ref y, currentText, maxWidth, lineHeight, "");
            }
        }

        /// <summary>
        /// Add text element to rendered content
        /// </summary>
        private void AddTextElement(BrowserTab tab, ref int x, ref int y, string text, 
                                    int maxWidth, int lineHeight, string tag) {
            // Trim whitespace manually
            int start = 0;
            int end = text.Length - 1;
            while (start < text.Length && (text[start] == ' ' || text[start] == '\t' || text[start] == '\r' || text[start] == '\n')) start++;
            while (end >= start && (text[end] == ' ' || text[end] == '\t' || text[end] == '\r' || text[end] == '\n')) end--;
            
            if (end < start) return;
            text = text.Substring(start, end - start + 1);
            if (text.Length == 0) return;

            bool isHeading = StartsWithFast(tag, "h");
            bool isLink = StartsWithFast(tag, "a ");

            // Word wrap
            string[] words = SplitWords(text);
            string currentLine = "";

            for (int i = 0; i < words.Length; i++) {
                string word = words[i];
                string testLine = currentLine.Length > 0 ? currentLine + " " + word : word;
                int testWidth = WindowManager.font.MeasureString(testLine);

                if (testWidth > maxWidth && currentLine.Length > 0) {
                    // Add current line
                    AddLine(tab, x, y, currentLine, isHeading, isLink);
                    y += lineHeight;
                    x = X + ContentPadding + 8;
                    currentLine = word;
                } else {
                    currentLine = testLine;
                }
            }

            // Add remaining line
            if (currentLine.Length > 0) {
                AddLine(tab, x, y, currentLine, isHeading, isLink);
                x += WindowManager.font.MeasureString(currentLine) + 8;
            }
        }

        /// <summary>
        /// Add line to rendered content
        /// </summary>
        private void AddLine(BrowserTab tab, int x, int y, string text, bool isHeading, bool isLink) {
            int width = WindowManager.font.MeasureString(text);
            int height = WindowManager.font.FontSize;

            var elem = new RenderElement {
                Type = isHeading ? "heading" : (isLink ? "link" : "text"),
                Content = text,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Color = isLink ? 0xFF5588FF : 0xFFFFFFFF,
                IsBold = isHeading,
                LinkUrl = null
            };

            tab.RenderedContent.Add(elem);
        }

        /// <summary>
        /// Split text into words
        /// </summary>
        private string[] SplitWords(string text) {
            var words = new List<string>();
            string current = "";

            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') {
                    if (current.Length > 0) {
                        words.Add(current);
                        current = "";
                    }
                } else {
                    current += c;
                }
            }

            if (current.Length > 0) {
                words.Add(current);
            }

            return words.ToArray();
        }

        /// <summary>
        /// Extract title from HTML
        /// </summary>
        private void ExtractTitle(BrowserTab tab) {
            string html = ToLowerCase(tab.RawHtml);
            
            // Find <title> tag manually
            string searchStart = "<title>";
            int titleStart = FindString(html, searchStart);
            if (titleStart >= 0) {
                titleStart += searchStart.Length;
                string searchEnd = "</title>";
                int titleEnd = FindString(html, searchEnd, titleStart);
                if (titleEnd > titleStart) {
                    string title = tab.RawHtml.Substring(titleStart, titleEnd - titleStart);
                    
                    // Trim manually
                    int start = 0;
                    int end = title.Length - 1;
                    while (start < title.Length && (title[start] == ' ' || title[start] == '\t' || title[start] == '\r' || title[start] == '\n')) start++;
                    while (end >= start && (title[end] == ' ' || title[end] == '\t' || title[end] == '\r' || title[end] == '\n')) end--;
                    
                    if (end >= start) {
                        title = title.Substring(start, end - start + 1);
                        if (title.Length > 0) {
                            tab.Title = title;
                            return;
                        }
                    }
                }
            }
            tab.Title = "Untitled";
        }

        /// <summary>
        /// Convert string to lowercase
        /// </summary>
        private string ToLowerCase(string s) {
            char[] chars = new char[s.Length];
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                if (c >= 'A' && c <= 'Z') {
                    chars[i] = (char)(c + 32);
                } else {
                    chars[i] = c;
                }
            }
            return new string(chars);
        }

        /// <summary>
        /// Find substring in string (helper for IndexOf)
        /// </summary>
        private int FindString(string haystack, string needle, int startIndex = 0) {
            if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
            
            for (int i = startIndex; i <= haystack.Length - needle.Length; i++) {
                bool match = true;
                for (int j = 0; j < needle.Length; j++) {
                    if (haystack[i + j] != needle[j]) {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        /// <summary>
        /// Ephemeral port counter
        /// </summary>
        private static ushort _ephem = 41000;

        /// <summary>
        /// Next Ephemeral
        /// </summary>
        private static ushort NextEphemeral() {
            if (_ephem < 41000 || _ephem > 60000)
                _ephem = 41000;
            return _ephem++;
        }

        /// <summary>
        /// To Ascii
        /// </summary>
        private static byte[] ToAscii(string s) {
            byte[] b = new byte[s.Length];
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                b[i] = c < 128 ? (byte)c : (byte)'?';
            }
            return b;
        }

        /// <summary>
        /// On Set Visible
        /// </summary>
        public override void OnSetVisible(bool value) {
            base.OnSetVisible(value);
            if (value && _currentTab >= 0 && _currentTab < _tabs.Count) {
                var tab = _tabs[_currentTab];
                if (string.IsNullOrEmpty(tab.RawHtml)) {
                    StartRequest(tab);
                }
            }
        }

        /// <summary>
        /// IndexOf helper
        /// </summary>
        private static int IndexOf(string s, char ch) {
            for (int i = 0; i < s.Length; i++) {
                if (s[i] == ch)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// StartsWith helper
        /// </summary>
        private static bool StartsWithFast(string s, string prefix) {
            int l = prefix.Length;
            if (s.Length < l)
                return false;
            for (int i = 0; i < l; i++) {
                if (s[i] != prefix[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Dispose override to clean up tabs
        /// </summary>
        public override void Dispose() {
            for (int i = 0; i < _tabs.Count; i++) {
                if (_tabs[i].Http != null) {
                    _tabs[i].Http.Close();
                    _tabs[i].Http.Remove();
                }
            }
            base.Dispose();
        }
    }
}