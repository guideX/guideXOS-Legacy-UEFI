using System;
using System.Collections.Generic;
using System.Drawing;
using guideXOS.Graph;
using guideXOS.Kernel.Drivers;

namespace guideXOS.Misc {
    /// <summary>
    /// TrueType Font Renderer for guideXOS
    /// Supports .ttf file parsing and glyph rasterization
    /// </summary>
    public class TrueTypeFont {
        private byte[] _fontData;
        private int _fontSize;
        private float _scale;
        
        // Font tables
        private Dictionary<string, TableRecord> _tables;
        
        // Font metrics
        private int _unitsPerEm;
        private int _numGlyphs;
        private int _indexToLocFormat; // 0 = short, 1 = long
        private int _numHMetrics;
        
        // Glyph data
        private int[] _glyphOffsets;
        private int[] _advanceWidths;
        private short[] _leftSideBearings;
        
        // Character to glyph mapping (cmap)
        private Dictionary<char, int> _charToGlyph;
        
        // Glyph cache
        private Dictionary<char, Image> _glyphCache;
        
        private class TableRecord {
            public string Tag;
            public uint Checksum;
            public int Offset;
            public int Length;
        }
        
        private class GlyphOutline {
            public List<Point> Points = new List<Point>();
            public List<bool> OnCurve = new List<bool>();
            public List<int> EndPtsOfContours = new List<int>();
            public short XMin, YMin, XMax, YMax;
            public int NumberOfContours;
        }
        
        private struct Point {
            public float X, Y;
            public Point(float x, float y) { X = x; Y = y; }
        }
        
        /// <summary>
        /// Load a TrueType font from byte array
        /// </summary>
        public TrueTypeFont(byte[] fontData, int fontSize) {
            _fontData = fontData;
            _fontSize = fontSize;
            _tables = new Dictionary<string, TableRecord>();
            _charToGlyph = new Dictionary<char, int>();
            _glyphCache = new Dictionary<char, Image>();
            
            ParseFont();
            Console.WriteLine($"[TTF] Loaded font: {_numGlyphs} glyphs, {_unitsPerEm} units/em, size {fontSize}pt");
        }
        
        /// <summary>
        /// Parse the TTF file structure
        /// </summary>
        private void ParseFont() {
            // Read offset table (first 12 bytes)
            uint scalarType = ReadUInt32(0);
            int numTables = ReadUInt16(4);
            
            // Verify TTF signature
            if (scalarType != 0x00010000 && scalarType != 0x74727565) { // 'true' for Mac
                Console.WriteLine("[TTF] Not a valid TrueType font");
                return;
            }
            
            // Read table directory
            int offset = 12;
            for (int i = 0; i < numTables; i++) {
                var table = new TableRecord {
                    Tag = ReadString(offset, 4),
                    Checksum = ReadUInt32(offset + 4),
                    Offset = ReadInt32(offset + 8),
                    Length = ReadInt32(offset + 12)
                };
                _tables[table.Tag] = table;
                offset += 16;
            }
            
            // Parse required tables
            ParseHeadTable();
            ParseMaxpTable();
            ParseLocaTable();
            ParseHheaTable();
            ParseHmtxTable();
            ParseCmapTable();
            
            // Calculate scale factor
            _scale = (float)_fontSize / _unitsPerEm;
        }
        
        /// <summary>
        /// Parse 'head' table (font header)
        /// </summary>
        private void ParseHeadTable() {
            if (!_tables.ContainsKey("head")) {
                Console.WriteLine("[TTF] Missing 'head' table");
                return;
            }
            
            int offset = _tables["head"].Offset;
            _unitsPerEm = ReadUInt16(offset + 18);
            _indexToLocFormat = ReadInt16(offset + 50);
        }
        
        /// <summary>
        /// Parse 'maxp' table (maximum profile)
        /// </summary>
        private void ParseMaxpTable() {
            if (!_tables.ContainsKey("maxp")) {
                Console.WriteLine("[TTF] Missing 'maxp' table");
                return;
            }
            
            int offset = _tables["maxp"].Offset;
            _numGlyphs = ReadUInt16(offset + 4);
        }
        
        /// <summary>
        /// Parse 'loca' table (glyph locations)
        /// </summary>
        private void ParseLocaTable() {
            if (!_tables.ContainsKey("loca")) {
                Console.WriteLine("[TTF] Missing 'loca' table");
                return;
            }
            
            int offset = _tables["loca"].Offset;
            _glyphOffsets = new int[_numGlyphs + 1];
            
            if (_indexToLocFormat == 0) {
                // Short format (offsets / 2)
                for (int i = 0; i <= _numGlyphs; i++) {
                    _glyphOffsets[i] = ReadUInt16(offset + i * 2) * 2;
                }
            } else {
                // Long format
                for (int i = 0; i <= _numGlyphs; i++) {
                    _glyphOffsets[i] = ReadInt32(offset + i * 4);
                }
            }
        }
        
        /// <summary>
        /// Parse 'hhea' table (horizontal header)
        /// </summary>
        private void ParseHheaTable() {
            if (!_tables.ContainsKey("hhea")) {
                Console.WriteLine("[TTF] Missing 'hhea' table");
                return;
            }
            
            int offset = _tables["hhea"].Offset;
            _numHMetrics = ReadUInt16(offset + 34);
        }
        
        /// <summary>
        /// Parse 'hmtx' table (horizontal metrics)
        /// </summary>
        private void ParseHmtxTable() {
            if (!_tables.ContainsKey("hmtx")) {
                Console.WriteLine("[TTF] Missing 'hmtx' table");
                return;
            }
            
            int offset = _tables["hmtx"].Offset;
            _advanceWidths = new int[_numGlyphs];
            _leftSideBearings = new short[_numGlyphs];
            
            // Read metrics for glyphs with unique advance widths
            for (int i = 0; i < _numHMetrics; i++) {
                _advanceWidths[i] = ReadUInt16(offset + i * 4);
                _leftSideBearings[i] = ReadInt16(offset + i * 4 + 2);
            }
            
            // Remaining glyphs use the last advance width
            int lastAdvance = _advanceWidths[_numHMetrics - 1];
            for (int i = _numHMetrics; i < _numGlyphs; i++) {
                _advanceWidths[i] = lastAdvance;
                _leftSideBearings[i] = ReadInt16(offset + _numHMetrics * 4 + (i - _numHMetrics) * 2);
            }
        }
        
        /// <summary>
        /// Parse 'cmap' table (character to glyph mapping)
        /// </summary>
        private void ParseCmapTable() {
            if (!_tables.ContainsKey("cmap")) {
                Console.WriteLine("[TTF] Missing 'cmap' table");
                return;
            }
            
            int tableOffset = _tables["cmap"].Offset;
            int numTables = ReadUInt16(tableOffset + 2);
            
            // Find Unicode cmap (platform 3, encoding 1 or 10)
            int cmapOffset = -1;
            for (int i = 0; i < numTables; i++) {
                int recordOffset = tableOffset + 4 + i * 8;
                int platformId = ReadUInt16(recordOffset);
                int encodingId = ReadUInt16(recordOffset + 2);
                int offset = ReadInt32(recordOffset + 4);
                
                if (platformId == 3 && (encodingId == 1 || encodingId == 10)) {
                    cmapOffset = tableOffset + offset;
                    break;
                }
            }
            
            if (cmapOffset == -1) {
                Console.WriteLine("[TTF] No Unicode cmap found");
                return;
            }
            
            // Parse cmap format
            int format = ReadUInt16(cmapOffset);
            
            if (format == 4) {
                ParseCmapFormat4(cmapOffset);
            } else {
                Console.WriteLine($"[TTF] Unsupported cmap format: {format}");
            }
        }
        
        /// <summary>
        /// Parse cmap format 4 (BMP Unicode)
        /// </summary>
        private void ParseCmapFormat4(int offset) {
            int segCountX2 = ReadUInt16(offset + 6);
            int segCount = segCountX2 / 2;
            
            int endCountOffset = offset + 14;
            int startCountOffset = endCountOffset + segCountX2 + 2;
            int idDeltaOffset = startCountOffset + segCountX2;
            int idRangeOffsetOffset = idDeltaOffset + segCountX2;
            
            for (int i = 0; i < segCount; i++) {
                int endCode = ReadUInt16(endCountOffset + i * 2);
                int startCode = ReadUInt16(startCountOffset + i * 2);
                int idDelta = ReadInt16(idDeltaOffset + i * 2);
                int idRangeOffset = ReadUInt16(idRangeOffsetOffset + i * 2);
                
                for (int c = startCode; c <= endCode; c++) {
                    if (c == 0xFFFF) break;
                    
                    int glyphId;
                    if (idRangeOffset == 0) {
                        glyphId = (c + idDelta) & 0xFFFF;
                    } else {
                        int glyphIndexOffset = idRangeOffsetOffset + i * 2 + idRangeOffset + (c - startCode) * 2;
                        glyphId = ReadUInt16(glyphIndexOffset);
                        if (glyphId != 0) {
                            glyphId = (glyphId + idDelta) & 0xFFFF;
                        }
                    }
                    
                    if (glyphId > 0 && glyphId < _numGlyphs) {
                        _charToGlyph[(char)c] = glyphId;
                    }
                }
            }
        }
        
        /// <summary>
        /// Get glyph index for a character
        /// </summary>
        private int GetGlyphIndex(char c) {
            return _charToGlyph.ContainsKey(c) ? _charToGlyph[c] : 0;
        }
        
        /// <summary>
        /// Load glyph outline from 'glyf' table
        /// </summary>
        private GlyphOutline LoadGlyphOutline(int glyphIndex) {
            if (!_tables.ContainsKey("glyf")) {
                Console.WriteLine("[TTF] Missing 'glyf' table");
                return new GlyphOutline();
            }
            
            int glyfOffset = _tables["glyf"].Offset;
            int offset = glyfOffset + _glyphOffsets[glyphIndex];
            int nextOffset = glyfOffset + _glyphOffsets[glyphIndex + 1];
            
            var outline = new GlyphOutline();
            
            // Empty glyph (e.g., space)
            if (offset == nextOffset) {
                return outline;
            }
            
            outline.NumberOfContours = ReadInt16(offset);
            outline.XMin = ReadInt16(offset + 2);
            outline.YMin = ReadInt16(offset + 4);
            outline.XMax = ReadInt16(offset + 6);
            outline.YMax = ReadInt16(offset + 8);
            
            // Simple glyph (positive contours)
            if (outline.NumberOfContours >= 0) {
                int ptr = offset + 10;
                
                // Read end points
                for (int i = 0; i < outline.NumberOfContours; i++) {
                    outline.EndPtsOfContours.Add(ReadUInt16(ptr));
                    ptr += 2;
                }
                
                int numPoints = outline.EndPtsOfContours[outline.NumberOfContours - 1] + 1;
                
                // Skip instructions
                int instructionLength = ReadUInt16(ptr);
                ptr += 2 + instructionLength;
                
                // Read flags
                byte[] flags = new byte[numPoints];
                for (int i = 0; i < numPoints; i++) {
                    flags[i] = _fontData[ptr++];
                    outline.OnCurve.Add((flags[i] & 1) != 0);
                    
                    // Repeat flag
                    if ((flags[i] & 8) != 0) {
                        int repeatCount = _fontData[ptr++];
                        for (int j = 0; j < repeatCount; j++) {
                            i++;
                            flags[i] = flags[i - 1];
                            outline.OnCurve.Add((flags[i] & 1) != 0);
                        }
                    }
                }
                
                // Read X coordinates
                float[] xCoords = new float[numPoints];
                float x = 0;
                for (int i = 0; i < numPoints; i++) {
                    byte flag = flags[i];
                    if ((flag & 2) != 0) {
                        // Short format
                        byte val = _fontData[ptr++];
                        x += (flag & 16) != 0 ? val : -val;
                    } else if ((flag & 16) == 0) {
                        // Long format
                        x += ReadInt16(ptr);
                        ptr += 2;
                    }
                    xCoords[i] = x;
                }
                
                // Read Y coordinates
                float[] yCoords = new float[numPoints];
                float y = 0;
                for (int i = 0; i < numPoints; i++) {
                    byte flag = flags[i];
                    if ((flag & 4) != 0) {
                        // Short format
                        byte val = _fontData[ptr++];
                        y += (flag & 32) != 0 ? val : -val;
                    } else if ((flag & 32) == 0) {
                        // Long format
                        y += ReadInt16(ptr);
                        ptr += 2;
                    }
                    yCoords[i] = y;
                }
                
                // Store points
                for (int i = 0; i < numPoints; i++) {
                    outline.Points.Add(new Point(xCoords[i] * _scale, yCoords[i] * _scale));
                }
            }
            
            return outline;
        }
        
        /// <summary>
        /// Rasterize a glyph to a bitmap
        /// </summary>
        public Image RenderChar(char c) {
            // Check cache
            if (_glyphCache.ContainsKey(c)) {
                return _glyphCache[c];
            }
            
            int glyphIndex = GetGlyphIndex(c);
            if (glyphIndex == 0) {
                // Return empty image for missing glyph
                var emptyImg = new Image(_fontSize / 2, _fontSize);
                _glyphCache[c] = emptyImg;
                return emptyImg;
            }
            
            var outline = LoadGlyphOutline(glyphIndex);
            
            // Calculate bitmap size
            int width = (int)Math.Ceiling((outline.XMax - outline.XMin) * _scale) + 4;
            int height = _fontSize + 4;
            
            if (width <= 0) width = _fontSize / 2;
            if (height <= 0) height = _fontSize;
            
            var bitmap = new Image(width, height);
            
            // Simple scanline rasterization
            if (outline.NumberOfContours > 0) {
                RasterizeOutline(bitmap, outline);
            }
            
            _glyphCache[c] = bitmap;
            return bitmap;
        }
        
        /// <summary>
        /// Rasterize glyph outline using scanline algorithm
        /// </summary>
        private void RasterizeOutline(Image bitmap, GlyphOutline outline) {
            int startPt = 0;
            
            for (int contour = 0; contour < outline.NumberOfContours; contour++) {
                int endPt = outline.EndPtsOfContours[contour];
                
                // Rasterize each contour
                for (int i = startPt; i <= endPt; i++) {
                    int next = i == endPt ? startPt : i + 1;
                    
                    var p1 = outline.Points[i];
                    var p2 = outline.Points[next];
                    bool on1 = outline.OnCurve[i];
                    bool on2 = outline.OnCurve[next];
                    
                    // Draw line or curve
                    if (on1 && on2) {
                        DrawLine(bitmap, p1, p2);
                    } else if (on1 && !on2) {
                        // Start of curve - need next on-curve point
                        int nextOn = FindNextOnCurve(outline, next, endPt, startPt);
                        if (nextOn != -1) {
                            var pControl = p2;
                            var pEnd = outline.Points[nextOn];
                            DrawQuadraticBezier(bitmap, p1, pControl, pEnd);
                            i = nextOn - 1; // Skip to end point
                        }
                    }
                }
                
                startPt = endPt + 1;
            }
            
            // Fill the shape
            FloodFill(bitmap);
        }
        
        /// <summary>
        /// Find next on-curve point
        /// </summary>
        private int FindNextOnCurve(GlyphOutline outline, int start, int end, int wrapTo) {
            for (int i = start; i <= end; i++) {
                if (outline.OnCurve[i]) return i;
            }
            return wrapTo; // Wrap around
        }
        
        /// <summary>
        /// Draw a line between two points
        /// </summary>
        private void DrawLine(Image bitmap, Point p1, Point p2) {
            int x1 = (int)p1.X;
            int y1 = (int)p1.Y;
            int x2 = (int)p2.X;
            int y2 = (int)p2.Y;
            
            // Bresenham's line algorithm
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;
            
            while (true) {
                if (x1 >= 0 && x1 < bitmap.Width && y1 >= 0 && y1 < bitmap.Height) {
                    bitmap.RawData[y1 * bitmap.Width + x1] = unchecked((int)0xFFFFFFFF);
                }
                
                if (x1 == x2 && y1 == y2) break;
                
                int e2 = 2 * err;
                if (e2 > -dy) {
                    err -= dy;
                    x1 += sx;
                }
                if (e2 < dx) {
                    err += dx;
                    y1 += sy;
                }
            }
        }
        
        /// <summary>
        /// Draw a quadratic Bézier curve
        /// </summary>
        private void DrawQuadraticBezier(Image bitmap, Point p0, Point p1, Point p2) {
            int steps = 20;
            for (int i = 0; i <= steps; i++) {
                float t = (float)i / steps;
                float t1 = 1 - t;
                
                float x = t1 * t1 * p0.X + 2 * t1 * t * p1.X + t * t * p2.X;
                float y = t1 * t1 * p0.Y + 2 * t1 * t * p1.Y + t * t * p2.Y;
                
                int px = (int)x;
                int py = (int)y;
                
                if (px >= 0 && px < bitmap.Width && py >= 0 && py < bitmap.Height) {
                    bitmap.RawData[py * bitmap.Width + px] = unchecked((int)0xFFFFFFFF);
                }
            }
        }
        
        /// <summary>
        /// Simple flood fill to fill the glyph shape
        /// </summary>
        private void FloodFill(Image bitmap) {
            // Simple scanline fill - fill between edges
            for (int y = 0; y < bitmap.Height; y++) {
                bool inside = false;
                for (int x = 0; x < bitmap.Width; x++) {
                    int idx = y * bitmap.Width + x;
                    if (bitmap.RawData[idx] != 0) {
                        inside = !inside;
                    } else if (inside) {
                        bitmap.RawData[idx] = unchecked((int)0xFFFFFFFF);
                    }
                }
            }
        }
        
        /// <summary>
        /// Draw a string at the specified position
        /// </summary>
        public void DrawString(Graphics g, int x, int y, string text, uint color = 0xFFFFFFFF) {
            int cursorX = x;
            
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                
                if (c == ' ') {
                    cursorX += _fontSize / 3;
                    continue;
                }
                
                var glyphBitmap = RenderChar(c);
                int glyphIndex = GetGlyphIndex(c);
                int advanceWidth = (int)(_advanceWidths[glyphIndex] * _scale);
                
                // Draw glyph
                for (int gy = 0; gy < glyphBitmap.Height; gy++) {
                    for (int gx = 0; gx < glyphBitmap.Width; gx++) {
                        uint pixel = (uint)glyphBitmap.RawData[gy * glyphBitmap.Width + gx];
                        if (pixel != 0) {
                            g.DrawPoint(cursorX + gx, y + gy, color, true);
                        }
                    }
                }
                
                cursorX += advanceWidth;
            }
        }
        
        /// <summary>
        /// Measure string width in pixels
        /// </summary>
        public int MeasureString(string text) {
            int width = 0;
            
            for (int i = 0; i < text.Length; i++) {
                char c = text[i];
                
                if (c == ' ') {
                    width += _fontSize / 3;
                    continue;
                }
                
                int glyphIndex = GetGlyphIndex(c);
                width += (int)(_advanceWidths[glyphIndex] * _scale);
            }
            
            return width;
        }
        
        /// <summary>
        /// Get advance width for a character
        /// </summary>
        public int GetAdvanceWidth(char c) {
            if (c == ' ') return _fontSize / 3;
            
            int glyphIndex = GetGlyphIndex(c);
            return (int)(_advanceWidths[glyphIndex] * _scale);
        }
        
        // Utility methods for reading binary data
        private byte ReadByte(int offset) => _fontData[offset];
        private short ReadInt16(int offset) => (short)((ReadByte(offset) << 8) | ReadByte(offset + 1));
        private ushort ReadUInt16(int offset) => (ushort)((ReadByte(offset) << 8) | ReadByte(offset + 1));
        private int ReadInt32(int offset) => (ReadInt16(offset) << 16) | ReadUInt16(offset + 2);
        private uint ReadUInt32(int offset) => (uint)((ReadUInt16(offset) << 16) | ReadUInt16(offset + 2));
        
        private string ReadString(int offset, int length) {
            var chars = new char[length];
            for (int i = 0; i < length; i++) {
                chars[i] = (char)ReadByte(offset + i);
            }
            return new string(chars);
        }
    }
}
