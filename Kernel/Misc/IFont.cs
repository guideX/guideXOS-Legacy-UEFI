using guideXOS.Graph;
using guideXOS.Kernel.Drivers;
using System.Drawing;
namespace guideXOS.Misc {
    /// <summary>
    /// Font style enumeration
    /// </summary>
    public enum FontStyle {
        Normal = 0,
        Bold = 1,
        Italic = 2,
        BoldItalic = 3
    }
    
    internal class IFont {
        private readonly Image image;
        private readonly string charset;
        private readonly bool useFixedWidth;
        
        // Font variants (bold, italic, bold+italic)
        private IFont _boldVariant;
        private IFont _italicVariant;
        private IFont _boldItalicVariant;

        public int FontSize;
        public int CharWidth;
        public int Padding;

        public int NumRow => image.Width / FontSize;

        public IFont(Image _img, string _charset, int size, bool fixedWidth = false, int fixedCharWidth = 0, int padding = 0) {
            image = _img;
            charset = _charset;
            FontSize = size;
            useFixedWidth = fixedWidth;
            CharWidth = fixedCharWidth > 0 ? fixedCharWidth : size;
            Padding = padding;
        }
        
        /// <summary>
        /// Set font variants for bold/italic support
        /// </summary>
        public void SetVariants(IFont bold, IFont italic, IFont boldItalic) {
            _boldVariant = bold;
            _italicVariant = italic;
            _boldItalicVariant = boldItalic;
        }
        
        /// <summary>
        /// Get a font variant
        /// </summary>
        public IFont GetVariant(FontStyle style) {
            switch (style) {
                case FontStyle.Bold:
                    return _boldVariant ?? this;
                case FontStyle.Italic:
                    return _italicVariant ?? this;
                case FontStyle.BoldItalic:
                    return _boldItalicVariant ?? this;
                default:
                    return this;
            }
        }

        public int DrawChar(Graphics g, int X, int Y, char Chr) {
            int index = charset.IndexOf(Chr);
            if (index == -1) {
                if (Chr == ' ') return useFixedWidth ? CharWidth / 2 : FontSize / 2;
                return 0;
            }

            int baseX = 0, baseY = 0;
            for (int i = 0; i < index; i++) {
                baseX += FontSize;
                if ((baseX / FontSize) >= NumRow) {
                    baseX = 0;
                    baseY += FontSize;
                }
            }

            int renderWidth;
            
            if (useFixedWidth) {
                renderWidth = CharWidth;
            } else {
                renderWidth = FontSize;
                int consecutiveEmptyCols = 0;
                bool hasContent = false;
                
                for (int w = 0; w < FontSize - 1; w++) {
                    int counter = 0;
                    for (int h = 0; h < FontSize; h++) {
                        if (baseX + w >= image.Width || baseY + h >= image.Height) {
                            counter++;
                            continue;
                        }
                        
                        uint color = image.GetPixel(baseX + w, baseY + h);
                        if ((color & 0xFF000000) == 0) {
                            counter++;
                        } else {
                            hasContent = true;
                        }
                    }
                    
                    if (counter == FontSize) {
                        if (hasContent) {
                            consecutiveEmptyCols++;
                            if (consecutiveEmptyCols >= 2) {
                                renderWidth = w - consecutiveEmptyCols + 1;
                                break;
                            }
                        }
                    } else {
                        consecutiveEmptyCols = 0;
                    }
                }
            }

            if (X != -1 && Y != -1) {
                for (int w = 0; w < renderWidth && w < FontSize; w++) {
                    for (int h = 0; h < FontSize; h++) {
                        if (baseX + w >= image.Width || baseY + h >= image.Height)
                            continue;
                            
                        uint color = image.GetPixel(baseX + w, baseY + h);
                        g.DrawPoint(X + w, Y + h, color, true);
                    }
                }
            }

            return renderWidth;
        }

        public void DrawString(int X, int Y, string Str, Graphics g) {
            int w = 0, h = 0;
            for (int i = 0; i < Str.Length; i++) {
                w += DrawChar(g, X + w, Y + h, Str[i]) + Padding;
            }
        }
        
        public void DrawString(int X, int Y, string Str) {
            int w = 0, h = 0;
            for (int i = 0; i < Str.Length; i++) {
                w += DrawChar(Framebuffer.Graphics, X + w, Y + h, Str[i]) + Padding;
            }
        }

        public int MeasureString(string Str) {
            int w = 0;
            for (int i = 0; i < Str.Length; i++) {
                w += DrawChar(Framebuffer.Graphics, -1, -1, Str[i]) + Padding;
            }
            return w;
        }

        public void DrawString(int X, int Y, string Str, int LineLimit = -1, int HeightLimit = -1) {
            int w = 0, h = 0;
            for (int i = 0; i < Str.Length; i++) {
                if (h != 0 && w == 0 && Str[i] == ' ') continue;
                w += DrawChar(Framebuffer.Graphics, X + w, Y + h, Str[i]) + Padding;
                if (w + FontSize > LineLimit && LineLimit != -1 || Str[i] == '\n') {
                    w = 0;
                    h += FontSize;

                    if (HeightLimit != -1 && h >= HeightLimit) {
                        return;
                    }
                }
            }
        }
        
        // STYLED VARIANTS - Use these for bold/italic support
        public void DrawStringStyled(int X, int Y, string Str, Graphics g, FontStyle style) {
            IFont targetFont = GetVariant(style);
            int w = 0, h = 0;
            for (int i = 0; i < Str.Length; i++) {
                w += targetFont.DrawChar(g, X + w, Y + h, Str[i]) + targetFont.Padding;
            }
        }
        
        public void DrawStringStyled(int X, int Y, string Str, FontStyle style) {
            IFont targetFont = GetVariant(style);
            int w = 0, h = 0;
            for (int i = 0; i < Str.Length; i++) {
                w += targetFont.DrawChar(Framebuffer.Graphics, X + w, Y + h, Str[i]) + targetFont.Padding;
            }
        }

        public int MeasureStringStyled(string Str, FontStyle style) {
            IFont targetFont = GetVariant(style);
            int w = 0;
            for (int i = 0; i < Str.Length; i++) {
                w += targetFont.DrawChar(Framebuffer.Graphics, -1, -1, Str[i]) + targetFont.Padding;
            }
            return w;
        }

        public void DrawStringStyled(int X, int Y, string Str, int LineLimit, int HeightLimit, FontStyle style) {
            IFont targetFont = GetVariant(style);
            int w = 0, h = 0;
            for (int i = 0; i < Str.Length; i++) {
                if (h != 0 && w == 0 && Str[i] == ' ') continue;
                w += targetFont.DrawChar(Framebuffer.Graphics, X + w, Y + h, Str[i]) + targetFont.Padding;
                if (w + FontSize > LineLimit && LineLimit != -1 || Str[i] == '\n') {
                    w = 0;
                    h += FontSize;

                    if (HeightLimit != -1 && h >= HeightLimit) {
                        return;
                    }
                }
            }
        }
        
        public void DiagnoseFont() {
            Console.WriteLine("Font Diagnosis:");
            Console.WriteLine($"FontSize: {FontSize}");
            Console.WriteLine($"Image Width: {image.Width}, Height: {image.Height}");
            Console.WriteLine($"NumRow: {NumRow}");
            Console.WriteLine($"Charset length: {charset.Length}");
            Console.WriteLine($"Charset: {charset}");
            Console.WriteLine($"Fixed Width Mode: {useFixedWidth}, CharWidth: {CharWidth}");
            Console.WriteLine($"Has Bold: {_boldVariant != null}");
            Console.WriteLine($"Has Italic: {_italicVariant != null}");
            Console.WriteLine($"Has BoldItalic: {_boldItalicVariant != null}");
            
            Console.WriteLine("\nTesting specific characters:");
            TestChar('/');
            TestChar('>');
            TestChar('Z');
            TestChar('g');
            TestChar('C');
            TestChar('P');
            TestChar('U');
            TestChar('R');
            TestChar('A');
            TestChar('M');
        }
        
        private void TestChar(char c) {
            int index = charset.IndexOf(c);
            Console.WriteLine($"  '{c}' -> index {index}" + (index == -1 ? " (NOT FOUND)" : ""));
        }
    }
}