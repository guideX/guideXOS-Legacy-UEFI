using System.Drawing;
using guideXOS.Graph;
using guideXOS.Kernel.Drivers;

namespace guideXOS.Misc {
    /// <summary>
    /// TrueType/OpenType Font Loader and Rasterizer for guideXOS
    /// Supports basic TTF/OTF parsing with glyph rendering
    /// Implements subset of TrueType specification for embedded use
    /// </summary>
    public unsafe class TrueTypeFont {
        private byte[] fontData;
        private int numGlyphs;
        private int unitsPerEm;
        private short ascent;
        private short descent;
        private short lineGap;
        
        // Table offsets
        private int cmapOffset;
        private int glyfOffset;
        private int locaOffset;
        private int headOffset;
        private int hheaOffset;
        private int hmtxOffset;
        private int maxpOffset;
        
        // Glyph location format (0 = short, 1 = long)
        private int locaFormat;
        
        // Cached glyph metrics
        private int[] glyphOffsets;
        private int[] advanceWidths;
        
        public int FontSize { get; private set; }
        public string FontName { get; private set; }

        /// <summary>
        /// Load TrueType font from byte array
        /// </summary>
        public TrueTypeFont(byte[] data, int fontSize = 16) {
            fontData = data;
            FontSize = fontSize;
            FontName = "TrueType Font";
            
            if (!ParseFontTables()) {
                // Invalid font - use defaults
                numGlyphs = 256;
                unitsPerEm = 2048;
                return;
            }
            
            ParseFontMetrics();
            CacheGlyphData();
        }

        /// <summary>
        /// Parse TTF table directory
        /// </summary>
        private bool ParseFontTables() {
            if (fontData.Length < 12) return false;
            
            // Check for TTF signature (0x00010000) or OTF signature ('OTTO')
            uint signature = ReadUInt32BE(0);
            if (signature != 0x00010000 && signature != 0x4F54544F) {
                // Try TTC (TrueType Collection) signature
                if (signature != 0x74746366) return false;
            }
            
            int numTables = ReadUInt16BE(4);
            
            // Find required tables
            for (int i = 0; i < numTables; i++) {
                int entryOffset = 12 + i * 16;
                if (entryOffset + 16 > fontData.Length) break;
                
                uint tag = ReadUInt32BE(entryOffset);
                uint offset = ReadUInt32BE(entryOffset + 8);
                
                // Match table tags
                if (tag == 0x636D6170) cmapOffset = (int)offset;      // 'cmap'
                else if (tag == 0x676C7966) glyfOffset = (int)offset; // 'glyf'
                else if (tag == 0x6C6F6361) locaOffset = (int)offset; // 'loca'
                else if (tag == 0x68656164) headOffset = (int)offset; // 'head'
                else if (tag == 0x68686561) hheaOffset = (int)offset; // 'hhea'
                else if (tag == 0x686D7478) hmtxOffset = (int)offset; // 'hmtx'
                else if (tag == 0x6D617870) maxpOffset = (int)offset; // 'maxp'
            }
            
            return cmapOffset > 0 && glyfOffset > 0 && headOffset > 0;
        }

        /// <summary>
        /// Parse font metrics from head and hhea tables
        /// </summary>
        private void ParseFontMetrics() {
            // Parse 'head' table
            if (headOffset > 0) {
                unitsPerEm = ReadUInt16BE(headOffset + 18);
                locaFormat = ReadInt16BE(headOffset + 50);
            } else {
                unitsPerEm = 2048; // Default
            }
            
            // Parse 'hhea' table
            if (hheaOffset > 0) {
                ascent = ReadInt16BE(hheaOffset + 4);
                descent = ReadInt16BE(hheaOffset + 6);
                lineGap = ReadInt16BE(hheaOffset + 8);
            }
            
            // Parse 'maxp' table for numGlyphs
            if (maxpOffset > 0) {
                numGlyphs = ReadUInt16BE(maxpOffset + 4);
            } else {
                numGlyphs = 256; // Default
            }
        }

        /// <summary>
        /// Cache glyph offsets and metrics for faster access
        /// </summary>
        private void CacheGlyphData() {
            glyphOffsets = new int[numGlyphs + 1];
            advanceWidths = new int[numGlyphs];
            
            // Parse 'loca' table
            if (locaOffset > 0) {
                for (int i = 0; i <= numGlyphs; i++) {
                    if (locaFormat == 0) {
                        // Short format (offset / 2)
                        glyphOffsets[i] = ReadUInt16BE(locaOffset + i * 2) * 2;
                    } else {
                        // Long format
                        glyphOffsets[i] = (int)ReadUInt32BE(locaOffset + i * 4);
                    }
                }
            }
            
            // Parse 'hmtx' table for advance widths
            if (hmtxOffset > 0) {
                for (int i = 0; i < numGlyphs && i < 256; i++) {
                    advanceWidths[i] = ReadUInt16BE(hmtxOffset + i * 4);
                }
            }
        }

        /// <summary>
        /// Get glyph index for character using cmap
        /// </summary>
        private int GetGlyphIndex(char c) {
            if (cmapOffset == 0) return 0;
            
            int numTables = ReadUInt16BE(cmapOffset + 2);
            
            // Find Unicode cmap (platform 3, encoding 1 or 10)
            int subtableOffset = 0;
            for (int i = 0; i < numTables; i++) {
                int recordOffset = cmapOffset + 4 + i * 8;
                int platformId = ReadUInt16BE(recordOffset);
                int encodingId = ReadUInt16BE(recordOffset + 2);
                
                if (platformId == 3 && (encodingId == 1 || encodingId == 10)) {
                    subtableOffset = cmapOffset + (int)ReadUInt32BE(recordOffset + 4);
                    break;
                }
            }
            
            if (subtableOffset == 0) return 0;
            
            int format = ReadUInt16BE(subtableOffset);
            
            // Format 4: Segment mapping to delta values (most common)
            if (format == 4) {
                int segCount = ReadUInt16BE(subtableOffset + 6) / 2;
                int endCodesOffset = subtableOffset + 14;
                int startCodesOffset = endCodesOffset + segCount * 2 + 2;
                int deltaOffset = startCodesOffset + segCount * 2;
                int rangeOffset = deltaOffset + segCount * 2;
                
                for (int i = 0; i < segCount; i++) {
                    int endCode = ReadUInt16BE(endCodesOffset + i * 2);
                    int startCode = ReadUInt16BE(startCodesOffset + i * 2);
                    
                    if (c >= startCode && c <= endCode) {
                        int delta = ReadInt16BE(deltaOffset + i * 2);
                        int range = ReadUInt16BE(rangeOffset + i * 2);
                        
                        if (range == 0) {
                            return (c + delta) & 0xFFFF;
                        } else {
                            int glyphOffset = rangeOffset + i * 2 + range + (c - startCode) * 2;
                            int glyphIndex = ReadUInt16BE(glyphOffset);
                            if (glyphIndex != 0) {
                                return (glyphIndex + delta) & 0xFFFF;
                            }
                        }
                    }
                }
            }
            
            return 0; // Return .notdef glyph
        }

        /// <summary>
        /// Render character to bitmap - simplified version
        /// </summary>
        public Image RenderChar(char c) {
            int glyphIndex = GetGlyphIndex(c);
            if (glyphIndex < 0 || glyphIndex >= numGlyphs) glyphIndex = 0;
            
            // For now, return a simple placeholder glyph
            // Full glyph rendering with contours would be more complex
            return CreatePlaceholderGlyph(c);
        }

        private Image CreatePlaceholderGlyph(char c) {
            // Create simple bitmap representation
            int width = FontSize * 2 / 3;
            int height = FontSize;
            
            Image img = new Image();
            img.Width = width;
            img.Height = height;
            img.Bpp = 4;
            img.RawData = new int[width * height];
            
            // Clear to transparent
            for (int i = 0; i < img.RawData.Length; i++) {
                img.RawData[i] = 0x00000000;
            }
            
            // Draw simple representation (white box outline)
            uint white = 0xFFFFFFFF;
            for (int x = 0; x < width; x++) {
                img.RawData[x] = (int)white; // Top
                img.RawData[(height - 1) * width + x] = (int)white; // Bottom
            }
            for (int y = 0; y < height; y++) {
                img.RawData[y * width] = (int)white; // Left
                img.RawData[y * width + width - 1] = (int)white; // Right
            }
            
            return img;
        }

        /// <summary>
        /// Get advance width for character
        /// </summary>
        public int GetAdvanceWidth(char c) {
            int glyphIndex = GetGlyphIndex(c);
            if (glyphIndex < 0 || glyphIndex >= advanceWidths.Length) return FontSize / 2;
            
            float scale = (float)FontSize / unitsPerEm;
            return (int)(advanceWidths[glyphIndex] * scale);
        }

        /// <summary>
        /// Measure string width
        /// </summary>
        public int MeasureString(string text) {
            int width = 0;
            for (int i = 0; i < text.Length; i++) {
                width += GetAdvanceWidth(text[i]);
            }
            return width;
        }

        /// <summary>
        /// Draw string using TrueType font
        /// </summary>
        public void DrawString(Graphics g, int x, int y, string text, uint color = 0xFFFFFFFF) {
            int currentX = x;
            for (int i = 0; i < text.Length; i++) {
                Image glyph = RenderChar(text[i]);
                
                // Draw glyph with color
                for (int py = 0; py < glyph.Height; py++) {
                    for (int px = 0; px < glyph.Width; px++) {
                        uint glyphPixel = (uint)glyph.RawData[py * glyph.Width + px];
                        if ((glyphPixel & 0xFF000000) != 0) {
                            g.DrawPoint(currentX + px, y + py, color, true);
                        }
                    }
                }
                
                currentX += GetAdvanceWidth(text[i]);
                glyph.Dispose();
            }
        }

        // Helper methods for reading big-endian values
        private ushort ReadUInt16BE(int offset) {
            if (offset + 1 >= fontData.Length) return 0;
            return (ushort)((fontData[offset] << 8) | fontData[offset + 1]);
        }

        private short ReadInt16BE(int offset) {
            return (short)ReadUInt16BE(offset);
        }

        private uint ReadUInt32BE(int offset) {
            if (offset + 3 >= fontData.Length) return 0;
            return (uint)((fontData[offset] << 24) | (fontData[offset + 1] << 16) | 
                          (fontData[offset + 2] << 8) | fontData[offset + 3]);
        }
    }
}
