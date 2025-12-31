using guideXOS;
using System;
using System.Drawing;

namespace guideXOS.Misc {
    /// <summary>
    /// Pure managed PNG decoder - no native code required
    /// Supports: 8-bit RGBA, RGB, Grayscale, Grayscale+Alpha, Palette
    /// Uses custom DEFLATE implementation for zlib decompression
    /// </summary>
    public unsafe class ManagedPNG : Image {
        
        #region PNG Constants
        // Color types
        private const byte CT_GRAYSCALE = 0;
        private const byte CT_RGB = 2;
        private const byte CT_PALETTE = 3;
        private const byte CT_GRAYSCALE_ALPHA = 4;
        private const byte CT_RGBA = 6;
        
        // Filter types
        private const byte FILTER_NONE = 0;
        private const byte FILTER_SUB = 1;
        private const byte FILTER_UP = 2;
        private const byte FILTER_AVERAGE = 3;
        private const byte FILTER_PAETH = 4;
        
        // Maximum iterations for safety
        private const int MAX_DECODE_ITERATIONS = 10000000;
        #endregion
        
        public ManagedPNG(byte[] data) {
            BootConsole.WriteLine("[MPNG] Constructor start");
            if (data == null || data.Length < 8) {
                BootConsole.WriteLine("[MPNG] Invalid data");
                BuildFallback();
                return;
            }
            
            try {
                BootConsole.WriteLine("[MPNG] Calling Decode");
                if (!Decode(data)) {
                    BootConsole.WriteLine("[MPNG] Decode returned false");
                    BuildFallback();
                }
                BootConsole.WriteLine("[MPNG] Decode complete");
            } catch {
                BootConsole.WriteLine("[MPNG] Exception in Decode");
                BuildFallback();
            }
        }
        
        private void BuildFallback() {
            Width = 8;
            Height = 8;
            Bpp = 4;
            RawData = new int[64];
            for (int y = 0; y < 8; y++) {
                for (int x = 0; x < 8; x++) {
                    bool c = ((x ^ y) & 1) == 0;
                    RawData[y * 8 + x] = c ? unchecked((int)0xFFFF00FF) : unchecked((int)0xFF000000);
                }
            }
        }
        
        private bool Decode(byte[] data) {
            BootConsole.WriteLine("[MPNG] Decode start");
            
            // Check PNG signature
            if (data.Length < 8) return false;
            if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
                data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A) {
                BootConsole.WriteLine("[MPNG] Bad signature");
                return false;
            }
            
            BootConsole.WriteLine("[MPNG] Signature OK");
            int pos = 8;
            int width = 0, height = 0;
            byte bitDepth = 0, colorType = 0, interlaceMethod = 0;
            byte[] palette = null;
            byte[] transparency = null;
            int idatTotalSize = 0;
            
            // First pass: parse header and count IDAT size
            BootConsole.WriteLine("[MPNG] First pass");
            int firstPass = pos;
            while (firstPass + 12 <= data.Length) {
                uint chunkLen = ReadBE32(data, firstPass);
                uint chunkType = ReadBE32(data, firstPass + 4);
                
                if (chunkLen > 0x7FFFFFFF) break; // Invalid chunk
                
                if (chunkType == 0x49484452) { // IHDR
                    width = (int)ReadBE32(data, firstPass + 8);
                    height = (int)ReadBE32(data, firstPass + 12);
                    bitDepth = data[firstPass + 16];
                    colorType = data[firstPass + 17];
                    interlaceMethod = data[firstPass + 20];
                    BootConsole.WriteLine("[MPNG] IHDR: " + width.ToString() + "x" + height.ToString() + " bpp=" + bitDepth.ToString() + " ct=" + colorType.ToString());
                } else if (chunkType == 0x49444154) { // IDAT
                    idatTotalSize += (int)chunkLen;
                } else if (chunkType == 0x504C5445) { // PLTE
                    palette = new byte[chunkLen];
                    for (int i = 0; i < chunkLen; i++) {
                        palette[i] = data[firstPass + 8 + i];
                    }
                } else if (chunkType == 0x74524E53) { // tRNS
                    transparency = new byte[chunkLen];
                    for (int i = 0; i < chunkLen; i++) {
                        transparency[i] = data[firstPass + 8 + i];
                    }
                } else if (chunkType == 0x49454E44) { // IEND
                    break;
                }
                
                firstPass += 12 + (int)chunkLen;
            }
            
            BootConsole.WriteLine("[MPNG] IDAT size: " + idatTotalSize.ToString());
            
            // Validate
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096) {
                BootConsole.WriteLine("[MPNG] Invalid dimensions");
                return false;
            }
            if (bitDepth != 8) {
                BootConsole.WriteLine("[MPNG] Unsupported bit depth");
                return false;
            }
            if (interlaceMethod != 0) {
                BootConsole.WriteLine("[MPNG] Interlaced not supported");
                return false;
            }
            if (idatTotalSize <= 0) {
                BootConsole.WriteLine("[MPNG] No IDAT data");
                return false;
            }
            
            // Collect IDAT data
            BootConsole.WriteLine("[MPNG] Collecting IDAT");
            byte[] compressedData = new byte[idatTotalSize];
            int compressedPos = 0;
            pos = 8;
            
            while (pos + 12 <= data.Length && compressedPos < idatTotalSize) {
                uint chunkLen = ReadBE32(data, pos);
                uint chunkType = ReadBE32(data, pos + 4);
                
                if (chunkType == 0x49444154) { // IDAT
                    for (int i = 0; i < chunkLen && compressedPos < idatTotalSize; i++) {
                        compressedData[compressedPos++] = data[pos + 8 + i];
                    }
                } else if (chunkType == 0x49454E44) { // IEND
                    break;
                }
                
                pos += 12 + (int)chunkLen;
            }
            
            // Decompress
            BootConsole.WriteLine("[MPNG] Decompressing");
            int bpp = GetBytesPerPixel(colorType);
            int expectedSize = height * (width * bpp + 1);
            BootConsole.WriteLine("[MPNG] Expected size: " + expectedSize.ToString());
            
            byte[] rawPixels = DecompressZlib(compressedData, expectedSize);
            compressedData.Dispose();
            
            if (rawPixels == null) {
                BootConsole.WriteLine("[MPNG] Decompress failed");
                return false;
            }
            
            BootConsole.WriteLine("[MPNG] Decompress OK");
            
            // Set dimensions
            Width = width;
            Height = height;
            Bpp = 4;
            RawData = new int[width * height];
            
            // Process scanlines
            BootConsole.WriteLine("[MPNG] Processing scanlines");
            byte[] prevScanline = new byte[width * bpp];
            byte[] currScanline = new byte[width * bpp];
            
            int rawPos = 0;
            for (int y = 0; y < height; y++) {
                if (rawPos >= rawPixels.Length) break;
                
                byte filterType = rawPixels[rawPos++];
                
                // Read scanline
                int scanlineBytes = width * bpp;
                for (int i = 0; i < scanlineBytes && rawPos < rawPixels.Length; i++) {
                    currScanline[i] = rawPixels[rawPos++];
                }
                
                // Apply filter
                ApplyFilter(filterType, currScanline, prevScanline, bpp);
                
                // Convert to ARGB
                for (int x = 0; x < width; x++) {
                    RawData[y * width + x] = ConvertToARGB(currScanline, x, colorType, bpp, palette, transparency);
                }
                
                // Swap scanlines
                var temp = prevScanline;
                prevScanline = currScanline;
                currScanline = temp;
            }
            
            BootConsole.WriteLine("[MPNG] Done");
            rawPixels.Dispose();
            prevScanline.Dispose();
            currScanline.Dispose();
            
            return true;
        }
        
        private int GetBytesPerPixel(byte colorType) {
            switch (colorType) {
                case CT_GRAYSCALE: return 1;
                case CT_RGB: return 3;
                case CT_PALETTE: return 1;
                case CT_GRAYSCALE_ALPHA: return 2;
                case CT_RGBA: return 4;
                default: return 4;
            }
        }
        
        private void ApplyFilter(byte filterType, byte[] curr, byte[] prev, int bpp) {
            int len = curr.Length;
            
            switch (filterType) {
                case FILTER_NONE:
                    break;
                case FILTER_SUB:
                    for (int i = bpp; i < len; i++)
                        curr[i] = (byte)(curr[i] + curr[i - bpp]);
                    break;
                case FILTER_UP:
                    for (int i = 0; i < len; i++)
                        curr[i] = (byte)(curr[i] + prev[i]);
                    break;
                case FILTER_AVERAGE:
                    for (int i = 0; i < len; i++) {
                        int a = i >= bpp ? curr[i - bpp] : 0;
                        curr[i] = (byte)(curr[i] + (a + prev[i]) / 2);
                    }
                    break;
                case FILTER_PAETH:
                    for (int i = 0; i < len; i++) {
                        int a = i >= bpp ? curr[i - bpp] : 0;
                        int b = prev[i];
                        int c = i >= bpp ? prev[i - bpp] : 0;
                        int p = a + b - c;
                        int pa = p > a ? p - a : a - p;
                        int pb = p > b ? p - b : b - p;
                        int pc = p > c ? p - c : c - p;
                        curr[i] = (byte)(curr[i] + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c));
                    }
                    break;
            }
        }
        
        private int ConvertToARGB(byte[] scanline, int x, byte colorType, int bpp, byte[] palette, byte[] transparency) {
            int pos = x * bpp;
            
            switch (colorType) {
                case CT_GRAYSCALE: {
                    byte g = scanline[pos];
                    return unchecked((int)(0xFF000000 | ((uint)g << 16) | ((uint)g << 8) | g));
                }
                case CT_RGB: {
                    return unchecked((int)(0xFF000000 | ((uint)scanline[pos] << 16) | ((uint)scanline[pos + 1] << 8) | scanline[pos + 2]));
                }
                case CT_PALETTE: {
                    byte idx = scanline[pos];
                    if (palette == null || idx * 3 + 2 >= palette.Length) return unchecked((int)0xFFFF00FF);
                    byte a = (transparency != null && idx < transparency.Length) ? transparency[idx] : (byte)255;
                    return unchecked((int)(((uint)a << 24) | ((uint)palette[idx * 3] << 16) | ((uint)palette[idx * 3 + 1] << 8) | palette[idx * 3 + 2]));
                }
                case CT_GRAYSCALE_ALPHA: {
                    byte g = scanline[pos];
                    return unchecked((int)(((uint)scanline[pos + 1] << 24) | ((uint)g << 16) | ((uint)g << 8) | g));
                }
                case CT_RGBA: {
                    return unchecked((int)(((uint)scanline[pos + 3] << 24) | ((uint)scanline[pos] << 16) | ((uint)scanline[pos + 1] << 8) | scanline[pos + 2]));
                }
                default:
                    return unchecked((int)0xFFFF00FF);
            }
        }
        
        private uint ReadBE32(byte[] data, int pos) {
            return ((uint)data[pos] << 24) | ((uint)data[pos + 1] << 16) | ((uint)data[pos + 2] << 8) | data[pos + 3];
        }
        
        #region DEFLATE Decompression
        
        private byte[] DecompressZlib(byte[] compressed, int expectedSize) {
            if (compressed == null || compressed.Length < 6) return null;
            
            // Skip zlib header (2 bytes)
            byte[] output = new byte[expectedSize];
            int outPos = 0;
            int inPos = 2;
            int bitBuf = 0;
            int bitCount = 0;
            int iterations = 0;
            
            bool lastBlock = false;
            while (!lastBlock && outPos < expectedSize && inPos < compressed.Length) {
                if (++iterations > MAX_DECODE_ITERATIONS) return null;
                
                // Read BFINAL
                if (bitCount < 1) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                lastBlock = (bitBuf & 1) == 1;
                bitBuf >>= 1; bitCount--;
                
                // Read BTYPE
                if (bitCount < 2) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                int blockType = bitBuf & 3;
                bitBuf >>= 2; bitCount -= 2;
                
                if (blockType == 0) {
                    // Stored block
                    bitBuf = 0; bitCount = 0; // Align to byte
                    if (inPos + 4 > compressed.Length) return null;
                    int len = compressed[inPos] | (compressed[inPos + 1] << 8);
                    inPos += 4; // Skip len and nlen
                    for (int i = 0; i < len && outPos < expectedSize && inPos < compressed.Length; i++) {
                        output[outPos++] = compressed[inPos++];
                    }
                } else if (blockType == 1 || blockType == 2) {
                    // Huffman compressed
                    int[] litLenLengths, distLengths;
                    
                    if (blockType == 1) {
                        // Fixed Huffman
                        litLenLengths = new int[288];
                        for (int i = 0; i < 144; i++) litLenLengths[i] = 8;
                        for (int i = 144; i < 256; i++) litLenLengths[i] = 9;
                        for (int i = 256; i < 280; i++) litLenLengths[i] = 7;
                        for (int i = 280; i < 288; i++) litLenLengths[i] = 8;
                        distLengths = new int[32];
                        for (int i = 0; i < 32; i++) distLengths[i] = 5;
                    } else {
                        // Dynamic Huffman - read code lengths
                        if (bitCount < 14) {
                            while (bitCount < 14 && inPos < compressed.Length) {
                                bitBuf |= compressed[inPos++] << bitCount;
                                bitCount += 8;
                            }
                        }
                        int hlit = (bitBuf & 0x1F) + 257; bitBuf >>= 5; bitCount -= 5;
                        int hdist = (bitBuf & 0x1F) + 1; bitBuf >>= 5; bitCount -= 5;
                        int hclen = (bitBuf & 0x0F) + 4; bitBuf >>= 4; bitCount -= 4;
                        
                        int[] clOrder = { 16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15 };
                        int[] clLengths = new int[19];
                        
                        for (int i = 0; i < hclen; i++) {
                            if (bitCount < 3) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                            clLengths[clOrder[i]] = bitBuf & 7;
                            bitBuf >>= 3; bitCount -= 3;
                        }
                        
                        // Build code length table
                        ushort[] clTable = BuildDecodeTable(clLengths, 19, 7);
                        
                        // Decode all lengths
                        int[] allLengths = new int[hlit + hdist];
                        int idx = 0;
                        while (idx < hlit + hdist) {
                            if (++iterations > MAX_DECODE_ITERATIONS) { clTable.Dispose(); allLengths.Dispose(); clLengths.Dispose(); return null; }
                            while (bitCount < 15 && inPos < compressed.Length) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                            int sym = DecodeSymbol(clTable, 7, ref bitBuf, ref bitCount);
                            
                            if (sym < 16) {
                                allLengths[idx++] = sym;
                            } else if (sym == 16) {
                                if (bitCount < 2) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                                int repeat = 3 + (bitBuf & 3); bitBuf >>= 2; bitCount -= 2;
                                int prev = idx > 0 ? allLengths[idx - 1] : 0;
                                for (int i = 0; i < repeat && idx < allLengths.Length; i++) allLengths[idx++] = prev;
                            } else if (sym == 17) {
                                if (bitCount < 3) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                                int repeat = 3 + (bitBuf & 7); bitBuf >>= 3; bitCount -= 3;
                                for (int i = 0; i < repeat && idx < allLengths.Length; i++) allLengths[idx++] = 0;
                            } else {
                                if (bitCount < 7) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                                int repeat = 11 + (bitBuf & 0x7F); bitBuf >>= 7; bitCount -= 7;
                                for (int i = 0; i < repeat && idx < allLengths.Length; i++) allLengths[idx++] = 0;
                            }
                        }
                        
                        litLenLengths = new int[hlit];
                        distLengths = new int[hdist];
                        for (int i = 0; i < hlit; i++) litLenLengths[i] = allLengths[i];
                        for (int i = 0; i < hdist; i++) distLengths[i] = allLengths[hlit + i];
                        
                        clTable.Dispose();
                        allLengths.Dispose();
                        clLengths.Dispose();
                    }
                    
                    // Build decode tables
                    ushort[] litTable = BuildDecodeTable(litLenLengths, litLenLengths.Length, 15);
                    ushort[] distTable = BuildDecodeTable(distLengths, distLengths.Length, 15);
                    
                    // Decode data
                    while (outPos < expectedSize) {
                        if (++iterations > MAX_DECODE_ITERATIONS) { litTable.Dispose(); distTable.Dispose(); litLenLengths.Dispose(); distLengths.Dispose(); return null; }
                        while (bitCount < 15 && inPos < compressed.Length) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                        
                        int sym = DecodeSymbol(litTable, 15, ref bitBuf, ref bitCount);
                        
                        if (sym < 256) {
                            output[outPos++] = (byte)sym;
                        } else if (sym == 256) {
                            break;
                        } else {
                            // Length code
                            int length = GetLengthValue(sym, ref bitBuf, ref bitCount, ref inPos, compressed);
                            
                            while (bitCount < 15 && inPos < compressed.Length) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                            int distCode = DecodeSymbol(distTable, 15, ref bitBuf, ref bitCount);
                            int distance = GetDistanceValue(distCode, ref bitBuf, ref bitCount, ref inPos, compressed);
                            
                            for (int i = 0; i < length && outPos < expectedSize; i++) {
                                output[outPos] = output[outPos - distance];
                                outPos++;
                            }
                        }
                    }
                    
                    litTable.Dispose();
                    distTable.Dispose();
                    litLenLengths.Dispose();
                    distLengths.Dispose();
                } else {
                    return null; // Invalid block type
                }
            }
            
            return output;
        }
        
        // Build a fast decode table using canonical Huffman codes
        private ushort[] BuildDecodeTable(int[] lengths, int count, int maxBits) {
            int tableSize = 1 << maxBits;
            ushort[] table = new ushort[tableSize];
            
            // Count codes of each length
            int[] blCount = new int[16];
            for (int i = 0; i < count; i++) {
                if (lengths[i] > 0 && lengths[i] <= 15) blCount[lengths[i]]++;
            }
            
            // Calculate starting code for each length
            int[] nextCode = new int[16];
            int code = 0;
            for (int bits = 1; bits <= 15; bits++) {
                code = (code + blCount[bits - 1]) << 1;
                nextCode[bits] = code;
            }
            
            // Assign codes and fill table
            for (int sym = 0; sym < count; sym++) {
                int len = lengths[sym];
                if (len == 0) continue;
                
                int c = nextCode[len]++;
                
                // Fill all table entries for this code
                int fillBits = maxBits - len;
                int fillCount = 1 << fillBits;
                int baseEntry = (ushort)((sym << 4) | len);
                
                // Reverse the code bits
                int reversed = 0;
                for (int i = 0; i < len; i++) {
                    reversed = (reversed << 1) | ((c >> i) & 1);
                }
                
                for (int fill = 0; fill < fillCount; fill++) {
                    int idx = reversed | (fill << len);
                    if (idx < tableSize) {
                        table[idx] = (ushort)baseEntry;
                    }
                }
            }
            
            blCount.Dispose();
            nextCode.Dispose();
            
            return table;
        }
        
        private int DecodeSymbol(ushort[] table, int maxBits, ref int bitBuf, ref int bitCount) {
            int entry = table[bitBuf & ((1 << maxBits) - 1)];
            int len = entry & 0xF;
            int sym = entry >> 4;
            if (len > 0) {
                bitBuf >>= len;
                bitCount -= len;
            }
            return sym;
        }
        
        // Length base values and extra bits (codes 257-285)
        private int GetLengthBase(int idx) {
            switch (idx) {
                case 0: return 3; case 1: return 4; case 2: return 5; case 3: return 6;
                case 4: return 7; case 5: return 8; case 6: return 9; case 7: return 10;
                case 8: return 11; case 9: return 13; case 10: return 15; case 11: return 17;
                case 12: return 19; case 13: return 23; case 14: return 27; case 15: return 31;
                case 16: return 35; case 17: return 43; case 18: return 51; case 19: return 59;
                case 20: return 67; case 21: return 83; case 22: return 99; case 23: return 115;
                case 24: return 131; case 25: return 163; case 26: return 195; case 27: return 227;
                case 28: return 258;
                default: return 3;
            }
        }
        
        private int GetLengthExtraBits(int idx) {
            switch (idx) {
                case 0: case 1: case 2: case 3: case 4: case 5: case 6: case 7: return 0;
                case 8: case 9: case 10: case 11: return 1;
                case 12: case 13: case 14: case 15: return 2;
                case 16: case 17: case 18: case 19: return 3;
                case 20: case 21: case 22: case 23: return 4;
                case 24: case 25: case 26: case 27: return 5;
                case 28: return 0;
                default: return 0;
            }
        }
        
        // Distance base values and extra bits (codes 0-29)
        private int GetDistBase(int code) {
            switch (code) {
                case 0: return 1; case 1: return 2; case 2: return 3; case 3: return 4;
                case 4: return 5; case 5: return 7; case 6: return 9; case 7: return 13;
                case 8: return 17; case 9: return 25; case 10: return 33; case 11: return 49;
                case 12: return 65; case 13: return 97; case 14: return 129; case 15: return 193;
                case 16: return 257; case 17: return 385; case 18: return 513; case 19: return 769;
                case 20: return 1025; case 21: return 1537; case 22: return 2049; case 23: return 3073;
                case 24: return 4097; case 25: return 6145; case 26: return 8193; case 27: return 12289;
                case 28: return 16385; case 29: return 24577;
                default: return 1;
            }
        }
        
        private int GetDistExtraBits(int code) {
            switch (code) {
                case 0: case 1: case 2: case 3: return 0;
                case 4: case 5: return 1;
                case 6: case 7: return 2;
                case 8: case 9: return 3;
                case 10: case 11: return 4;
                case 12: case 13: return 5;
                case 14: case 15: return 6;
                case 16: case 17: return 7;
                case 18: case 19: return 8;
                case 20: case 21: return 9;
                case 22: case 23: return 10;
                case 24: case 25: return 11;
                case 26: case 27: return 12;
                case 28: case 29: return 13;
                default: return 0;
            }
        }
        
        private int GetLengthValue(int code, ref int bitBuf, ref int bitCount, ref int inPos, byte[] compressed) {
            int idx = code - 257;
            if (idx < 0 || idx > 28) return 3;
            int extra = GetLengthExtraBits(idx);
            if (extra > 0) {
                while (bitCount < extra && inPos < compressed.Length) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                int extraVal = bitBuf & ((1 << extra) - 1);
                bitBuf >>= extra; bitCount -= extra;
                return GetLengthBase(idx) + extraVal;
            }
            return GetLengthBase(idx);
        }
        
        private int GetDistanceValue(int code, ref int bitBuf, ref int bitCount, ref int inPos, byte[] compressed) {
            if (code < 0 || code > 29) return 1;
            int extra = GetDistExtraBits(code);
            if (extra > 0) {
                while (bitCount < extra && inPos < compressed.Length) { bitBuf |= compressed[inPos++] << bitCount; bitCount += 8; }
                int extraVal = bitBuf & ((1 << extra) - 1);
                bitBuf >>= extra; bitCount -= extra;
                return GetDistBase(code) + extraVal;
            }
            return GetDistBase(code);
        }
        
        #endregion
    }
}
