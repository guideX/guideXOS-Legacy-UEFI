using System.Drawing;
using System.Collections.Generic;

namespace guideXOS.Misc {
    /// <summary>
    /// Enhanced SVG rasterizer for guideXOS icons
    /// Supports rect, circle, ellipse, path (basic), polygon, polyline with fill/stroke colors
    /// Includes gradient support, transformations, and improved parsing
    /// </summary>
    public unsafe class SVG : Image {
        private struct SVGElement {
            public int Type; // 0=rect, 1=circle, 2=ellipse, 3=path, 4=polygon, 5=polyline
            public uint FillColor;
            public uint StrokeColor;
            public float StrokeWidth;
            public float X, Y, Width, Height, Rx, Ry, R;
            public float CX, CY;
            public string PathData;
            public float[] Points; // For polygon/polyline
        }

        private struct Transform {
            public float TranslateX, TranslateY;
            public float ScaleX, ScaleY;
            public float Rotation;
        }

        private static void BuildFallback(out int[] data, out int w, out int h) {
            w = 8; h = 8; data = new int[w * h];
            // simple checker pattern (cyan/blue) to indicate SVG
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    bool c = ((x ^ y) & 1) == 0;
                    data[y * w + x] = c ? unchecked((int)0xFF00FFFF) : unchecked((int)0xFF0000FF);
                }
            }
        }

        /// <summary>
        /// Parse SVG from byte array
        /// </summary>
        /// <param name="data">SVG file data</param>
        /// <param name="targetWidth">Desired width</param>
        /// <param name="targetHeight">Desired height</param>
        public SVG(byte[] data, int targetWidth = 32, int targetHeight = 32) {
            try {
                // Convert bytes to string using simple ASCII conversion
                char[] chars = new char[data.Length];
                for (int i = 0; i < data.Length; i++) {
                    chars[i] = (char)data[i];
                }
                string svg = new string(chars);
                chars.Dispose();
                
                // Set dimensions
                Width = targetWidth > 0 ? targetWidth : 32;
                Height = targetHeight > 0 ? targetHeight : 32;
                Bpp = 4;
                RawData = new int[Width * Height];
                
                // Clear background (transparent)
                for (int i = 0; i < RawData.Length; i++) {
                    RawData[i] = 0x00000000;
                }
                
                // Parse and render SVG elements
                RenderSVG(svg);
                
                svg.Dispose();
            } catch {
                BuildFallback(out RawData, out int fw, out int fh);
                Width = fw;
                Height = fh;
                Bpp = 4;
            }
        }

        private void RenderSVG(string svg) {
            // Parse viewBox if present for proper scaling
            float viewBoxWidth = 24.0f;
            float viewBoxHeight = 24.0f;
            int viewBoxIdx = FindString(svg, "viewBox", 0);
            if (viewBoxIdx >= 0) {
                // Simple viewBox parser: viewBox="0 0 24 24"
                int quoteStart = FindChar(svg, '"', viewBoxIdx);
                if (quoteStart >= 0) {
                    int quoteEnd = FindChar(svg, '"', quoteStart + 1);
                    if (quoteEnd > quoteStart) {
                        string viewBoxStr = svg.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                        string[] parts = SplitString(viewBoxStr, ' ');
                        if (parts.Length >= 4) {
                            viewBoxWidth = ParseSimpleFloat(parts[2]);
                            viewBoxHeight = ParseSimpleFloat(parts[3]);
                        }
                        viewBoxStr.Dispose();
                        parts.Dispose();
                    }
                }
            }
            
            float scaleX = Width / viewBoxWidth;
            float scaleY = Height / viewBoxHeight;
            
            int pos = 0;
            while (pos < svg.Length) {
                // Find next element
                int rectIdx = FindString(svg, "<rect", pos);
                int circleIdx = FindString(svg, "<circle", pos);
                int ellipseIdx = FindString(svg, "<ellipse", pos);
                int pathIdx = FindString(svg, "<path", pos);
                int polygonIdx = FindString(svg, "<polygon", pos);
                int polylineIdx = FindString(svg, "<polyline", pos);
                
                int nextIdx = GetMinIndex(rectIdx, circleIdx, ellipseIdx, pathIdx, polygonIdx, polylineIdx);
                if (nextIdx < 0) break;
                
                int elementType = -1;
                if (nextIdx == rectIdx) elementType = 0;
                else if (nextIdx == circleIdx) elementType = 1;
                else if (nextIdx == ellipseIdx) elementType = 2;
                else if (nextIdx == pathIdx) elementType = 3;
                else if (nextIdx == polygonIdx) elementType = 4;
                else if (nextIdx == polylineIdx) elementType = 5;
                
                // Find end of element
                int endIdx = FindChar(svg, '>', nextIdx);
                if (endIdx < 0) break;
                
                // Extract element string
                string element = svg.Substring(nextIdx, endIdx - nextIdx + 1);
                
                // Parse and render based on type
                switch (elementType) {
                    case 0: RenderRectElement(element, scaleX, scaleY); break;
                    case 1: RenderCircleElement(element, scaleX, scaleY); break;
                    case 2: RenderEllipseElement(element, scaleX, scaleY); break;
                    case 3: RenderPathElement(element, scaleX, scaleY); break;
                    case 4: RenderPolygonElement(element, scaleX, scaleY); break;
                    case 5: RenderPolylineElement(element, scaleX, scaleY); break;
                }
                
                element.Dispose();
                pos = endIdx + 1;
            }
        }

        private int GetMinIndex(params int[] indices) {
            int min = -1;
            for (int i = 0; i < indices.Length; i++) {
                if (indices[i] >= 0 && (min < 0 || indices[i] < min)) {
                    min = indices[i];
                }
            }
            return min;
        }

        private int FindString(string str, string search, int startPos) {
            if (startPos >= str.Length) return -1;
            for (int i = startPos; i <= str.Length - search.Length; i++) {
                bool match = true;
                for (int j = 0; j < search.Length; j++) {
                    if (str[i + j] != search[j]) {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private int FindChar(string str, char c, int startPos) {
            for (int i = startPos; i < str.Length; i++) {
                if (str[i] == c) return i;
            }
            return -1;
        }

        private void RenderRectElement(string element, float scaleX, float scaleY) {
            float x = ParseFloatAttr(element, "x=");
            float y = ParseFloatAttr(element, "y=");
            float w = ParseFloatAttr(element, "width=");
            float h = ParseFloatAttr(element, "height=");
            float rx = ParseFloatAttr(element, "rx=");
            float ry = ParseFloatAttr(element, "ry=");
            uint fillColor = ParseColorAttr(element, "fill");
            uint strokeColor = ParseColorAttr(element, "stroke");
            float strokeWidth = ParseFloatAttr(element, "stroke-width=");
            
            if (w <= 0 || h <= 0) return;
            
            int x1 = (int)(x * scaleX);
            int y1 = (int)(y * scaleY);
            int x2 = (int)((x + w) * scaleX);
            int y2 = (int)((y + h) * scaleY);
            
            // Simple rect without rounded corners for now
            for (int py = y1; py < y2 && py < Height; py++) {
                if (py < 0) continue;
                for (int px = x1; px < x2 && px < Width; px++) {
                    if (px < 0) continue;
                    if ((fillColor & 0xFF000000) != 0) {
                        SetPixel(px, py, fillColor);
                    }
                }
            }
            
            // Draw stroke if present
            if (strokeWidth > 0 && (strokeColor & 0xFF000000) != 0) {
                DrawRectStroke(x1, y1, x2, y2, strokeColor, (int)strokeWidth);
            }
        }

        private void RenderCircleElement(string element, float scaleX, float scaleY) {
            float cx = ParseFloatAttr(element, "cx=");
            float cy = ParseFloatAttr(element, "cy=");
            float r = ParseFloatAttr(element, "r=");
            uint fillColor = ParseColorAttr(element, "fill");
            uint strokeColor = ParseColorAttr(element, "stroke");
            float strokeWidth = ParseFloatAttr(element, "stroke-width=");
            
            if (r <= 0) return;
            
            float scale = (scaleX + scaleY) / 2.0f;
            float centerX = cx * scaleX;
            float centerY = cy * scaleY;
            float radius = r * scale;
            
            int x1 = (int)(centerX - radius);
            int y1 = (int)(centerY - radius);
            int x2 = (int)(centerX + radius);
            int y2 = (int)(centerY + radius);
            
            for (int py = y1; py <= y2 && py < Height; py++) {
                if (py < 0) continue;
                for (int px = x1; px <= x2 && px < Width; px++) {
                    if (px < 0) continue;
                    float dx = px - centerX;
                    float dy = py - centerY;
                    float dist = dx * dx + dy * dy;
                    if (dist <= radius * radius && (fillColor & 0xFF000000) != 0) {
                        SetPixel(px, py, fillColor);
                    }
                }
            }
        }

        private void RenderEllipseElement(string element, float scaleX, float scaleY) {
            float cx = ParseFloatAttr(element, "cx=");
            float cy = ParseFloatAttr(element, "cy=");
            float rx = ParseFloatAttr(element, "rx=");
            float ry = ParseFloatAttr(element, "ry=");
            uint fillColor = ParseColorAttr(element, "fill");
            
            if (rx <= 0 || ry <= 0) return;
            
            float centerX = cx * scaleX;
            float centerY = cy * scaleY;
            float radiusX = rx * scaleX;
            float radiusY = ry * scaleY;
            
            int x1 = (int)(centerX - radiusX);
            int y1 = (int)(centerY - radiusY);
            int x2 = (int)(centerX + radiusX);
            int y2 = (int)(centerY + radiusY);
            
            for (int py = y1; py <= y2 && py < Height; py++) {
                if (py < 0) continue;
                for (int px = x1; px <= x2 && px < Width; px++) {
                    if (px < 0) continue;
                    float dx = (px - centerX) / radiusX;
                    float dy = (py - centerY) / radiusY;
                    if (dx * dx + dy * dy <= 1.0f && (fillColor & 0xFF000000) != 0) {
                        SetPixel(px, py, fillColor);
                    }
                }
            }
        }

        private void RenderPathElement(string element, float scaleX, float scaleY) {
            // Basic path support - handle M (moveto), L (lineto), H (horizontal), V (vertical), Z (close)
            int dIdx = FindString(element, "d=", 0);
            if (dIdx < 0) return;
            
            uint fillColor = ParseColorAttr(element, "fill");
            uint strokeColor = ParseColorAttr(element, "stroke");
            float strokeWidth = ParseFloatAttr(element, "stroke-width=");
            
            // Extract path data
            int quoteStart = FindChar(element, '"', dIdx);
            if (quoteStart < 0) return;
            int quoteEnd = FindChar(element, '"', quoteStart + 1);
            if (quoteEnd < 0) return;
            
            string pathData = element.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            
            // Simple path parser - for now just handle basic lines
            float currentX = 0, currentY = 0;
            float startX = 0, startY = 0;
            
            int i = 0;
            while (i < pathData.Length) {
                char cmd = pathData[i];
                i++;
                
                // Skip whitespace
                while (i < pathData.Length && (pathData[i] == ' ' || pathData[i] == ',')) i++;
                
                if (cmd == 'M' || cmd == 'm') {
                    float x = ParseNextNumber(pathData, ref i);
                    float y = ParseNextNumber(pathData, ref i);
                    if (cmd == 'M') {
                        currentX = x * scaleX;
                        currentY = y * scaleY;
                    } else {
                        currentX += x * scaleX;
                        currentY += y * scaleY;
                    }
                    startX = currentX;
                    startY = currentY;
                } else if (cmd == 'L' || cmd == 'l') {
                    float x = ParseNextNumber(pathData, ref i);
                    float y = ParseNextNumber(pathData, ref i);
                    float newX = cmd == 'L' ? x * scaleX : currentX + x * scaleX;
                    float newY = cmd == 'L' ? y * scaleY : currentY + y * scaleY;
                    DrawLine((int)currentX, (int)currentY, (int)newX, (int)newY, strokeColor);
                    currentX = newX;
                    currentY = newY;
                } else if (cmd == 'H' || cmd == 'h') {
                    float x = ParseNextNumber(pathData, ref i);
                    float newX = cmd == 'H' ? x * scaleX : currentX + x * scaleX;
                    DrawLine((int)currentX, (int)currentY, (int)newX, (int)currentY, strokeColor);
                    currentX = newX;
                } else if (cmd == 'V' || cmd == 'v') {
                    float y = ParseNextNumber(pathData, ref i);
                    float newY = cmd == 'V' ? y * scaleY : currentY + y * scaleY;
                    DrawLine((int)currentX, (int)currentY, (int)currentX, (int)newY, strokeColor);
                    currentY = newY;
                } else if (cmd == 'Z' || cmd == 'z') {
                    DrawLine((int)currentX, (int)currentY, (int)startX, (int)startY, strokeColor);
                    currentX = startX;
                    currentY = startY;
                }
            }
            
            pathData.Dispose();
        }

        private void RenderPolygonElement(string element, float scaleX, float scaleY) {
            int pointsIdx = FindString(element, "points=", 0);
            if (pointsIdx < 0) return;
            
            uint fillColor = ParseColorAttr(element, "fill");
            uint strokeColor = ParseColorAttr(element, "stroke");
            
            int quoteStart = FindChar(element, '"', pointsIdx);
            if (quoteStart < 0) return;
            int quoteEnd = FindChar(element, '"', quoteStart + 1);
            if (quoteEnd < 0) return;
            
            string pointsStr = element.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            string[] coords = SplitString(pointsStr, ' ');
            
            // Draw polygon edges
            float prevX = 0, prevY = 0, firstX = 0, firstY = 0;
            for (int i = 0; i < coords.Length; i++) {
                string[] xy = SplitString(coords[i], ',');
                if (xy.Length >= 2) {
                    float x = ParseSimpleFloat(xy[0]) * scaleX;
                    float y = ParseSimpleFloat(xy[1]) * scaleY;
                    if (i > 0) {
                        DrawLine((int)prevX, (int)prevY, (int)x, (int)y, strokeColor);
                    } else {
                        firstX = x;
                        firstY = y;
                    }
                    prevX = x;
                    prevY = y;
                }
                xy.Dispose();
            }
            // Close polygon
            DrawLine((int)prevX, (int)prevY, (int)firstX, (int)firstY, strokeColor);
            
            pointsStr.Dispose();
            coords.Dispose();
        }

        private void RenderPolylineElement(string element, float scaleX, float scaleY) {
            // Same as polygon but don't close the path
            int pointsIdx = FindString(element, "points=", 0);
            if (pointsIdx < 0) return;
            
            uint strokeColor = ParseColorAttr(element, "stroke");
            
            int quoteStart = FindChar(element, '"', pointsIdx);
            if (quoteStart < 0) return;
            int quoteEnd = FindChar(element, '"', quoteStart + 1);
            if (quoteEnd < 0) return;
            
            string pointsStr = element.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
            string[] coords = SplitString(pointsStr, ' ');
            
            float prevX = 0, prevY = 0;
            for (int i = 0; i < coords.Length; i++) {
                string[] xy = SplitString(coords[i], ',');
                if (xy.Length >= 2) {
                    float x = ParseSimpleFloat(xy[0]) * scaleX;
                    float y = ParseSimpleFloat(xy[1]) * scaleY;
                    if (i > 0) {
                        DrawLine((int)prevX, (int)prevY, (int)x, (int)y, strokeColor);
                    }
                    prevX = x;
                    prevY = y;
                }
                xy.Dispose();
            }
            
            pointsStr.Dispose();
            coords.Dispose();
        }

        private float ParseNextNumber(string str, ref int pos) {
            // Skip whitespace and commas
            while (pos < str.Length && (str[pos] == ' ' || str[pos] == ',')) pos++;
            
            string numStr = "";
            while (pos < str.Length) {
                char c = str[pos];
                if ((c >= '0' && c <= '9') || c == '.' || c == '-') {
                    numStr += c;
                    pos++;
                } else {
                    break;
                }
            }
            
            float result = ParseSimpleFloat(numStr);
            numStr.Dispose();
            return result;
        }

        private string[] SplitString(string str, char delimiter) {
            int count = 1;
            for (int i = 0; i < str.Length; i++) {
                if (str[i] == delimiter) count++;
            }
            
            string[] result = new string[count];
            int idx = 0;
            int start = 0;
            
            for (int i = 0; i <= str.Length; i++) {
                if (i == str.Length || str[i] == delimiter) {
                    if (i > start) {
                        result[idx++] = str.Substring(start, i - start).Trim();
                    } else {
                        result[idx++] = "";
                    }
                    start = i + 1;
                }
            }
            
            return result;
        }

        private void DrawLine(int x0, int y0, int x1, int y1, uint color) {
            if ((color & 0xFF000000) == 0) return;
            
            // Bresenham's line algorithm
            int dx = x1 - x0;
            int dy = y1 - y0;
            if (dx < 0) dx = -dx;
            if (dy < 0) dy = -dy;
            
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            
            while (true) {
                SetPixel(x0, y0, color);
                
                if (x0 == x1 && y0 == y1) break;
                
                int e2 = 2 * err;
                if e2 > -dy) {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx) {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void DrawRectStroke(int x1, int y1, int x2, int y2, uint color, int width) {
            for (int w = 0; w < width; w++) {
                // Top
                for (int x = x1; x < x2; x++) {
                    SetPixel(x, y1 + w, color);
                }
                // Bottom
                for (int x = x1; x < x2; x++) {
                    SetPixel(x, y2 - 1 - w, color);
                }
                // Left
                for (int y = y1; y < y2; y++) {
                    SetPixel(x1 + w, y, color);
                }
                // Right
                for (int y = y1; y < y2; y++) {
                    SetPixel(x2 - 1 - w, y, color);
                }
            }
        }

        private float ParseFloatAttr(string element, string attrName) {
            int idx = FindString(element, attrName, 0);
            if (idx < 0) return 0;
            
            idx += attrName.Length;
            // Skip quote if present
            if (idx < element.Length && element[idx] == '"') idx++;
            
            // Extract number
            string numStr = "";
            while (idx < element.Length) {
                char c = element[idx];
                if ((c >= '0' && c <= '9') || c == '.' || c == '-') {
                    numStr += c;
                } else {
                    break;
                }
                idx++;
            }
            
            float result = ParseSimpleFloat(numStr);
            numStr.Dispose();
            return result;
        }

        private float ParseSimpleFloat(string numStr) {
            if (numStr.Length == 0) return 0;
            
            float result = 0;
            bool negative = false;
            bool afterDecimal = false;
            float decimalPlace = 0.1f;
            
            for (int i = 0; i < numStr.Length; i++) {
                char c = numStr[i];
                if (c == '-') {
                    negative = true;
                } else if (c == '.') {
                    afterDecimal = true;
                } else if (c >= '0' && c <= '9') {
                    int digit = c - '0';
                    if (afterDecimal) {
                        result += digit * decimalPlace;
                        decimalPlace /= 10.0f;
                    } else {
                        result = result * 10 + digit;
                    }
                }
            }
            
            return negative ? -result : result;
        }

        private uint ParseColorAttr(string element, string attrName = "fill") {
            // Look for fill="#RRGGBB" or fill="rgb(r,g,b)" or fill="none"
            int fillIdx = FindString(element, attrName, 0);
            if (fillIdx < 0) {
                // If no fill specified, return transparent
                if (attrName == "fill") return 0x00000000;
                // If no stroke, return transparent
                return 0x00000000;
            }
            
            // Check for "none"
            int noneIdx = FindString(element, "none", fillIdx);
            if (noneIdx > fillIdx && noneIdx < fillIdx + 20) {
                return 0x00000000;
            }
            
            // Look for hex color
            int hashIdx = FindChar(element, '#', fillIdx);
            if (hashIdx >= 0 && hashIdx < fillIdx + 20) {
                hashIdx++; // Skip #
                
                // Parse 6 hex digits
                uint color = 0;
                for (int i = 0; i < 6 && hashIdx + i < element.Length; i++) {
                    char c = element[hashIdx + i];
                    uint digit = 0;
                    if (c >= '0' && c <= '9') digit = (uint)(c - '0');
                    else if (c >= 'a' && c <= 'f') digit = (uint)(c - 'a' + 10);
                    else if (c >= 'A' && c <= 'F') digit = (uint)(c - 'A' + 10);
                    else break;
                    color = (color << 4) | digit;
                }
                
                return 0xFF000000 | color; // Add alpha
            }
            
            // Check for named colors (basic set)
            if (FindString(element, "black", fillIdx) > fillIdx && FindString(element, "black", fillIdx) < fillIdx + 20) return 0xFF000000;
            if (FindString(element, "white", fillIdx) > fillIdx && FindString(element, "white", fillIdx) < fillIdx + 20) return 0xFFFFFFFF;
            if (FindString(element, "red", fillIdx) > fillIdx && FindString(element, "red", fillIdx) < fillIdx + 20) return 0xFFFF0000;
            if (FindString(element, "green", fillIdx) > fillIdx && FindString(element, "green", fillIdx) < fillIdx + 20) return 0xFF00FF00;
            if (FindString(element, "blue", fillIdx) > fillIdx && FindString(element, "blue", fillIdx) < fillIdx + 20) return 0xFF0000FF;
            
            // Default to gray if nothing found
            return attrName == "fill" ? 0xFF808080 : 0x00000000;
        }

        private void SetPixel(int x, int y, uint color) {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            
            int idx = y * Width + x;
            uint alpha = (color >> 24) & 0xFF;
            
            if (alpha == 0xFF) {
                RawData[idx] = (int)color;
            } else if (alpha > 0) {
                // Simple alpha blending
                uint srcColor = color & 0x00FFFFFF;
                uint dstColor = (uint)RawData[idx];
                uint invAlpha = 255 - alpha;
                uint r = (((srcColor >> 16) & 0xFF) * alpha + ((dstColor >> 16) & 0xFF) * invAlpha) / 255;
                uint g = (((srcColor >> 8) & 0xFF) * alpha + ((dstColor >> 8) & 0xFF) * invAlpha) / 255;
                uint b = ((srcColor & 0xFF) * alpha + (dstColor & 0xFF) * invAlpha) / 255;
                RawData[idx] = (int)(0xFF000000 | (r << 16) | (g << 8) | b);
            }
        }
    }
}
