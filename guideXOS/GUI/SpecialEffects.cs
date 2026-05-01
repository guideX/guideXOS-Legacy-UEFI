using guideXOS.Graph;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;

namespace guideXOS.GUI {
    /// <summary>
    /// Special visual effects for window animations (Tron-style digitize, burn, smoke, etc.)
    /// </summary>
    internal static class SpecialEffects {
        // Simple pseudo-random number generator state (avoid System.Random)
        private static uint _randState = 12345;
        
        /// <summary>
        /// Simple fast pseudo-random number generator
        /// </summary>
        private static int NextRandom(int max) {
            if (max <= 0) return 0; // Prevent division by zero
            _randState = _randState * 1103515245 + 12345;
            return (int)((_randState >> 16) % (uint)max);
        }
        
        // Lazy-loaded sine/cosine lookup tables to avoid static initialization issues in AOT
        private static int[] _sinTable = null;
        private static int[] _cosTable = null;
        
        /// <summary>
        /// Initialize trig tables on first use (lazy initialization to avoid AOT startup issues)
        /// </summary>
        private static void EnsureTablesInitialized() {
            if (_sinTable != null) return;
            
            // Pre-computed sine/cosine values (0-359 degrees, scaled by 1000)
            _sinTable = new int[] {
                0,17,35,52,70,87,105,122,139,156,174,191,208,225,242,259,276,292,309,326,342,358,375,391,407,423,438,454,469,485,500,515,530,545,559,574,588,602,616,629,643,656,669,682,695,707,719,731,743,755,766,777,788,799,809,819,829,839,848,857,866,875,883,891,899,906,914,921,927,934,940,946,951,956,961,966,970,974,978,982,985,988,990,993,995,996,998,999,999,1000,1000,999,999,998,996,995,993,990,988,985,982,978,974,970,966,961,956,951,946,940,934,927,921,914,906,899,891,883,875,866,857,848,839,829,819,809,799,788,777,766,755,743,731,719,707,695,682,669,656,643,629,616,602,588,574,559,545,530,515,500,485,469,454,438,423,407,391,375,358,342,326,309,292,276,259,242,225,208,191,174,156,139,122,105,87,70,52,35,17,0,-17,-35,-52,-70,-87,-105,-122,-139,-156,-174,-191,-208,-225,-242,-259,-276,-292,-309,-326,-342,-358,-375,-391,-407,-423,-438,-454,-469,-485,-500,-515,-530,-545,-559,-574,-588,-602,-616,-629,-643,-656,-669,-682,-695,-707,-719,-731,-743,-755,-766,-777,-788,-799,-809,-819,-829,-839,-848,-857,-866,-875,-883,-891,-899,-906,-914,-921,-927,-934,-940,-946,-951,-956,-961,-966,-970,-974,-978,-982,-985,-988,-990,-993,-995,-996,-998,-999,-999,-1000,-1000,-999,-999,-998,-996,-995,-993,-990,-988,-985,-982,-978,-974,-970,-966,-961,-956,-951,-946,-940,-934,-927,-921,-914,-906,-899,-891,-883,-875,-866,-857,-848,-839,-829,-819,-809,-799,-788,-777,-766,-755,-743,-731,-719,-707,-695,-682,-669,-656,-643,-629,-616,-602,-588,-574,-559,-545,-530,-515,-500,-485,-469,-454,-438,-423,-407,-391,-375,-358,-342,-326,-309,-292,-276,-259,-242,-225,-208,-191,-174,-156,-139,-122,-105,-87,-70,-52,-35,-17
            };
            
            _cosTable = new int[] {
                1000,1000,999,999,998,996,995,993,990,988,985,982,978,974,970,966,961,956,951,946,940,934,927,921,914,906,899,891,883,875,866,857,848,839,829,819,809,799,788,777,766,755,743,731,719,707,695,682,669,656,643,629,616,602,588,574,559,545,530,515,500,485,469,454,438,423,407,391,375,358,342,326,309,292,276,259,242,225,208,191,174,156,139,122,105,87,70,52,35,17,0,-17,-35,-52,-70,-87,-105,-122,-139,-156,-174,-191,-208,-225,-242,-259,-276,-292,-309,-326,-342,-358,-375,-391,-407,-423,-438,-454,-469,-485,-500,-515,-530,-545,-559,-574,-588,-602,-616,-629,-643,-656,-669,-682,-695,-707,-719,-731,-743,-755,-766,-777,-788,-799,-809,-819,-829,-839,-848,-857,-866,-875,-883,-891,-899,-906,-914,-921,-927,-934,-940,-946,-951,-956,-961,-966,-970,-974,-978,-982,-985,-988,-990,-993,-995,-996,-998,-999,-999,-1000,-1000,-999,-999,-998,-996,-995,-993,-990,-988,-985,-982,-978,-974,-970,-966,-961,-956,-951,-946,-940,-934,-927,-921,-914,-906,-899,-891,-883,-875,-866,-857,-848,-839,-829,-819,-809,-799,-788,-777,-766,-755,-743,-731,-719,-707,-695,-682,-669,-656,-643,-629,-616,-602,-588,-574,-559,-545,-530,-515,-500,-485,-469,-454,-438,-423,-407,-391,-375,-358,-342,-326,-309,-292,-276,-259,-242,-225,-208,-191,-174,-156,-139,-122,-105,-87,-70,-52,-35,-17,0,17,35,52,70,87,105,122,139,156,174,191,208,225,242,259,276,292,309,326,342,358,375,391,407,423,438,454,469,485,500,515,530,545,559,574,588,602,616,629,643,656,669,682,695,707,719,731,743,755,766,777,788,799,809,819,829,839,848,857,866,875,883,891,899,906,914,921,927,934,940,946,951,956,961,966,970,974,978,982,985,988,990,993,995,996,998,999,999
            };
        }
        
        /// <summary>
        /// Fast integer sine (returns value * 1000)
        /// </summary>
        private static int FastSin(int degrees) {
            EnsureTablesInitialized();
            degrees = degrees % 360;
            if (degrees < 0) degrees += 360;
            return _sinTable[degrees];
        }
        
        /// <summary>
        /// Fast integer cosine (returns value * 1000)
        /// </summary>
        private static int FastCos(int degrees) {
            EnsureTablesInitialized();
            degrees = degrees % 360;
            if (degrees < 0) degrees += 360;
            return _cosTable[degrees];
        }
        
        /// <summary>
        /// Integer square root approximation
        /// </summary>
        private static int IntSqrt(int n) {
            if (n <= 0) return 0;
            int x = n;
            int y = (x + 1) / 2;
            while (y < x) {
                x = y;
                y = (x + n / x) / 2;
            }
            return x;
        }
        
        /// <summary>
        /// Integer absolute value
        /// </summary>
        private static int IntAbs(int x) {
            return x < 0 ? -x : x;
        }
        
        /// <summary>
        /// Get a random effect (excluding None, Fade, and Random)
        /// </summary>
        public static WindowEffectType GetRandomEffect() {
            // Ensure tables are initialized before using them
            EnsureTablesInitialized();
            
            int choice = NextRandom(10);
            switch (choice) {
                case 0: return WindowEffectType.Digitize;
                case 1: return WindowEffectType.Derezz;
                case 2: return WindowEffectType.BurnIn;
                case 3: return WindowEffectType.BurnOut;
                case 4: return WindowEffectType.SmokeIn;
                case 5: return WindowEffectType.SmokeOut;
                case 6: return WindowEffectType.Glitch;
                case 7: return WindowEffectType.Ripple;
                case 8: return WindowEffectType.Explode;
                default: return WindowEffectType.Implode;
            }
        }
        
        /// <summary>
        /// Render a special effect overlay on a window
        /// </summary>
        /// <param name="effect">The effect type to render</param>
        /// <param name="x">Window X position</param>
        /// <param name="y">Window Y position (content area, not including title bar)</param>
        /// <param name="width">Window width</param>
        /// <param name="height">Window height</param>
        /// <param name="barHeight">Title bar height</param>
        /// <param name="progress">Effect progress from 0.0 (start) to 1.0 (complete)</param>
        public static void RenderEffect(WindowEffectType effect, int x, int y, int width, int height, int barHeight, float progress) {
            try {
                if (Framebuffer.Graphics == null) return;
                
                // CRITICAL: Validate dimensions to prevent division/modulus by zero
                // which causes CPU #DE fault (page fault) that halts all CPUs on bare metal
                if (width <= 0 || height <= 0 || barHeight < 0) return;
                if ((height + barHeight) <= 0) return;
                
                // Ensure tables are initialized before rendering any effects
                EnsureTablesInitialized();

                // Convert float progress (0.0-1.0) to integer (0-1000) to avoid all floating point math
                // CRITICAL: This conversion must happen FIRST before any other operations
                int progressInt = (int)(progress * 1000);
                if (progressInt < 0) progressInt = 0;
                if (progressInt > 1000) progressInt = 1000;
                
                switch (effect) {
                    case WindowEffectType.Digitize:
                        RenderDigitize(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.Derezz:
                        RenderDerezz(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.BurnIn:
                        RenderBurnIn(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.BurnOut:
                        RenderBurnOut(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.SmokeIn:
                        RenderSmokeIn(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.SmokeOut:
                        RenderSmokeOut(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.Glitch:
                        RenderGlitch(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.Ripple:
                        RenderRipple(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.Explode:
                        RenderExplode(x, y, width, height, barHeight, progressInt);
                        break;
                    case WindowEffectType.Implode:
                        RenderImplode(x, y, width, height, barHeight, progressInt);
                        break;
                }
            } catch {
                // Silently fail if any effect rendering causes an exception
                // This prevents special effects from crashing the OS
            }
        }
        
        /// <summary>
        /// Tron-style digitize effect - horizontal scanlines materializing from top to bottom
        /// </summary>
        private static void RenderDigitize(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int totalHeight = height + barHeight;
            if (totalHeight <= 0 || width <= 0) return;
            int scanlineCount = UISettings.DigitizeScanlineCount;
            if (scanlineCount <= 0) return;
            uint color = UISettings.DigitizeColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            // Scanlines appear from top to bottom
            int visibleScanlines = (scanlineCount * progressInt) / 1000;
            
            for (int i = 0; i < visibleScanlines; i++) {
                int scanY = y - barHeight + (i * totalHeight / scanlineCount);
                
                // Skip if scanline is off-screen
                if (scanY < 0 || scanY >= screenH) continue;
                
                // Main scanline
                g.DrawLine(x, scanY, x + width, scanY, color);
                
                // Glow effect around scanline
                if (visibleScanlines > 0) {
                    byte alpha = (byte)(100 * (1000 - (i * 1000 / visibleScanlines)) / 1000);
                    uint glowColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                    if (scanY > 0) g.DrawLine(x, scanY - 1, x + width, scanY - 1, glowColor);
                    if (scanY < screenH - 1) g.DrawLine(x, scanY + 1, x + width, scanY + 1, glowColor);
                }
                
                // Random pixel sparkles along the scanline
                for (int j = 0; j < width / 20; j++) {
                    int sparkX = x + NextRandom(width);
                    int sparkSize = 2;
                    if (sparkX >= 0 && sparkX < screenW && scanY - 1 >= 0)
                        g.FillRectangle(sparkX, scanY - 1, sparkSize, sparkSize + 1, color);
                }
            }
            
            // Progressive reveal - draw darker overlay on areas not yet digitized
            if (progressInt < 1000) {
                int revealHeight = (totalHeight * progressInt) / 1000;
                int darkY = y - barHeight + revealHeight;
                int darkHeight = totalHeight - revealHeight;
                if (darkHeight > 0) {
                    uint darkOverlay = 0xDD000000;
                    g.FillRectangle(x, darkY, width, darkHeight, darkOverlay);
                }
            }
        }
        
        /// <summary>
        /// Tron-style de-rezz effect - window breaks into pixels and fades
        /// </summary>
        private static void RenderDerezz(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int totalHeight = height + barHeight;
            if (totalHeight <= 0 || width <= 0) return;
            uint color = UISettings.DerezzColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            // Grid of breaking pixels
            int gridSize = 8;
            int gridW = width / gridSize;
            int gridH = totalHeight / gridSize;
            
            for (int gy = 0; gy < gridH; gy++) {
                for (int gx = 0; gx < gridW; gx++) {
                    // Stagger the breakdown based on position
                    int cellProgressInt = progressInt + (NextRandom(100) * 1000 / 500);
                    if (cellProgressInt > 1000) cellProgressInt = 1000;
                    
                    int cellX = x + (gx * gridSize);
                    int cellY = (y - barHeight) + (gy * gridSize);
                    
                    if (cellProgressInt > 300) {
                        // Draw breaking cell outline - skip if off-screen
                        if (cellX >= -gridSize && cellX < screenW && cellY >= -gridSize && cellY < screenH) {
                            byte alpha = (byte)(((1000 - cellProgressInt) * 255) / 1000);
                            uint cellColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                            g.DrawRectangle(cellX, cellY, gridSize, gridSize, cellColor, 1);
                        
                            // Add falling effect
                            if (cellProgressInt > 600) {
                                int fallDist = ((cellProgressInt - 600) * 20) / 1000;
                                if (cellY + fallDist < screenH)
                                    g.DrawRectangle(cellX, cellY + fallDist, gridSize, gridSize, cellColor, 1);
                            }
                        }
                    }
                }
            }
            
            // Fade overlay
            byte fadeAlpha = (byte)((progressInt * 200) / 1000);
            uint fadeColor = ((uint)fadeAlpha << 24);
            g.FillRectangle(x, y - barHeight, width, totalHeight, fadeColor);
        }
        
        /// <summary>
        /// Burn in effect - fire particles coalesce into window
        /// </summary>
        private static void RenderBurnIn(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int particleCount = UISettings.BurnParticleCount;
            if (particleCount <= 0) return;
            uint color = UISettings.BurnColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            int centerX = x + width / 2;
            int centerY = y - barHeight + (height + barHeight) / 2;
            
            for (int i = 0; i < particleCount; i++) {
                // Pseudo-random but deterministic based on particle index (degrees)
                int angleDeg = (i * 137) % 360; // Use golden angle approximation
                int radius = ((i * 17) % 200) + 50;
                
                // Particles move inward as progress increases
                int currentRadius = (radius * (1000 - progressInt)) / 1000;
                
                // Use integer trig (scaled by 1000)
                int px = centerX + (currentRadius * FastCos(angleDeg)) / 1000;
                int py = centerY + (currentRadius * FastSin(angleDeg)) / 1000;
                
                // Skip particles that are off-screen
                if (px < 0 || px >= screenW || py < 0 || py >= screenH) continue;
                
                // Color intensity based on progress
                byte alpha = (byte)((progressInt * 255) / 1000);
                uint pColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                
                // Draw particle
                int pSize = 3;
                g.FillRectangle(px, py, pSize, pSize, pColor);
                
                // Trailing glow
                if (currentRadius > 5) {
                    int trailRadius = currentRadius + 10;
                    int trailX = centerX + (trailRadius * FastCos(angleDeg)) / 1000;
                    int trailY = centerY + (trailRadius * FastSin(angleDeg)) / 1000;
                    if (trailX >= 0 && trailX < screenW && trailY >= 0 && trailY < screenH) {
                        uint trailColor = ((uint)(alpha / 2) << 24) | (color & 0x00FFFFFF);
                        g.FillRectangle(trailX, trailY, 2, 2, trailColor);
                    }
                }
            }
            
            // Fade in overlay
            byte fadeAlpha = (byte)(((1000 - progressInt) * 200) / 1000);
            uint fadeColor = ((uint)fadeAlpha << 24);
            g.FillRectangle(x, y - barHeight, width, height + barHeight, fadeColor);
        }
        
        /// <summary>
        /// Burn out effect - window explodes into fire particles
        /// </summary>
        private static void RenderBurnOut(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int particleCount = UISettings.BurnParticleCount;
            if (particleCount <= 0) return;
            uint color = UISettings.BurnColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            int centerX = x + width / 2;
            int centerY = y - barHeight + (height + barHeight) / 2;
            
            for (int i = 0; i < particleCount; i++) {
                int angleDeg = (i * 137) % 360;
                int radius = ((i * 17) % 150) + 30;
                
                // Particles move outward as progress increases
                int currentRadius = (radius * progressInt) / 1000;
                
                int px = centerX + (currentRadius * FastCos(angleDeg)) / 1000;
                int py = centerY + (currentRadius * FastSin(angleDeg)) / 1000;
                
                // Skip particles that are off-screen to prevent out-of-bounds access
                if (px < 0 || px >= screenW || py < 0 || py >= screenH) continue;
                
                // Fade out as they travel
                byte alpha = (byte)(((1000 - progressInt) * 255) / 1000);
                uint pColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                
                int pSize = 3;
                g.FillRectangle(px, py, pSize, pSize, pColor);
            }
        }
        
        /// <summary>
        /// Smoke in effect - smoke particles form into window
        /// </summary>
        private static void RenderSmokeIn(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int particleCount = UISettings.SmokeParticleCount;
            if (particleCount <= 0) return;
            uint color = UISettings.SmokeColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            int totalHeight = height + barHeight;
            if (width <= 0 || totalHeight <= 0) return; // Prevent division by zero
            
            for (int i = 0; i < particleCount; i++) {
                // Start positions around window
                int posX = (i * 37) % width;
                int posY = (i * 53) % totalHeight;
                
                // Drift motion
                int driftX = ((i * 11) % 40) - 20;
                int driftY = -((i * 7) % 30);
                
                // Move toward final position
                int px = x + posX + (driftX * (1000 - progressInt) / 1000);
                int py = (y - barHeight) + posY + (driftY * (1000 - progressInt) / 1000);
                
                // Skip particles that are off-screen
                if (px < 0 || px >= screenW || py < 0 || py >= screenH) continue;
                
                byte alpha = (byte)((progressInt * 100) / 1000);
                uint pColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                
                int pSize = 4;
                g.FillRectangle(px, py, pSize, pSize, pColor);
            }
            
            // Fade in
            byte fadeAlpha = (byte)(((1000 - progressInt) * 150) / 1000);
            uint fadeColor = ((uint)fadeAlpha << 24);
            g.FillRectangle(x, y - barHeight, width, height + barHeight, fadeColor);
        }
        
        /// <summary>
        /// Smoke out effect - window dissipates into smoke
        /// </summary>
        private static void RenderSmokeOut(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int particleCount = UISettings.SmokeParticleCount;
            if (particleCount <= 0) return;
            uint color = UISettings.SmokeColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            int totalHeight = height + barHeight;
            if (width <= 0 || totalHeight <= 0) return; // Prevent modulus by zero (CPU #DE fault)
            
            for (int i = 0; i < particleCount; i++) {
                int posX = (i * 37) % width;
                int posY = (i * 53) % totalHeight;
                
                int driftX = ((i * 11) % 40) - 20;
                int driftY = -((i * 7) % 30);
                
                int px = x + posX + (driftX * progressInt / 1000);
                int py = (y - barHeight) + posY + (driftY * progressInt / 1000);
                
                // Skip particles that are off-screen
                if (px < 0 || px >= screenW || py < 0 || py >= screenH) continue;
                
                byte alpha = (byte)(((1000 - progressInt) * 100) / 1000);
                uint pColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                
                int pSize = 4 + (progressInt * 2 / 1000);
                g.FillRectangle(px, py, pSize, pSize, pColor);
            }
            
            // Fade out
            byte fadeAlpha = (byte)((progressInt * 200) / 1000);
            uint fadeColor = ((uint)fadeAlpha << 24);
            g.FillRectangle(x, y - barHeight, width, height + barHeight, fadeColor);
        }
        
        /// <summary>
        /// Glitch effect - digital corruption with scanlines and pixel displacement
        /// </summary>
        private static void RenderGlitch(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            uint color = UISettings.GlitchColor;
            int intensity = UISettings.GlitchIntensity;
            if (intensity <= 0) intensity = 1; // Prevent modulus by zero
            int screenH = Framebuffer.Height;
            
            int totalHeight = height + barHeight;
            if (totalHeight <= 0 || width <= 0) return; // Prevent modulus by zero
            
            // More glitches at start and end
            int glitchAmount = 1000 - (IntAbs(progressInt - 500) * 2);
            int glitchCount = (glitchAmount * 15) / 1000;
            
            for (int i = 0; i < glitchCount; i++) {
                int glitchY = (y - barHeight) + ((i * 71) % totalHeight);
                int glitchH = 2 + ((i * 13) % 8);
                int offset = ((i % 2) == 0 ? 1 : -1) * ((i * 7) % intensity);
                
                // Skip if off-screen
                if (glitchY < 0 || glitchY >= screenH) continue;
                
                // Draw displaced scanline
                byte alpha = (byte)((glitchAmount * 180) / 1000);
                uint glitchColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                g.FillRectangle(x + offset, glitchY, width, glitchH, glitchColor);
                
                // RGB separation effect
                uint redShift = ((uint)(alpha / 2) << 24) | 0x00FF0000;
                uint blueShift = ((uint)(alpha / 2) << 24) | 0x000000FF;
                g.DrawLine(x + offset - 2, glitchY, x + width + offset - 2, glitchY, redShift);
                g.DrawLine(x + offset + 2, glitchY, x + width + offset + 2, glitchY, blueShift);
            }
            
            // Fade based on effect direction
            byte fadeAlpha;
            if (progressInt < 500) {
                fadeAlpha = (byte)(((1000 - progressInt * 2) * 200) / 1000);
            } else {
                fadeAlpha = (byte)(((progressInt - 500) * 2 * 200) / 1000);
            }
            uint fadeColor = ((uint)fadeAlpha << 24);
            g.FillRectangle(x, y - barHeight, width, height + barHeight, fadeColor);
        }
        
        /// <summary>
        /// Ripple effect - concentric waves emanating from center
        /// </summary>
        private static void RenderRipple(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int waveCount = UISettings.RippleWaveCount;
            if (waveCount <= 0) return;
            uint color = UISettings.RippleColor;
            
            int centerX = x + width / 2;
            int centerY = y - barHeight + (height + barHeight) / 2;
            
            // Calculate max radius using integer square root
            int totalH = height + barHeight;
            if (totalH <= 0 || width <= 0) return;
            int maxRadius = IntSqrt(width * width + totalH * totalH) / 2;
            
            for (int i = 0; i < waveCount; i++) {
                int waveProgressInt = progressInt - (i * 100);
                if (waveProgressInt < 0) continue;
                if (waveProgressInt > 1000) waveProgressInt = 1000;
                
                int radius = (maxRadius * waveProgressInt) / 1000;
                byte alpha = (byte)(((1000 - waveProgressInt) * 180) / 1000);
                uint waveColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                
                // Draw circle (approximate with line segments)
                int segments = 36;
                int degStep = 360 / segments;
                for (int s = 0; s < segments; s++) {
                    int deg1 = s * degStep;
                    int deg2 = (s + 1) * degStep;
                    
                    int x1 = centerX + (radius * FastCos(deg1)) / 1000;
                    int y1 = centerY + (radius * FastSin(deg1)) / 1000;
                    int x2 = centerX + (radius * FastCos(deg2)) / 1000;
                    int y2 = centerY + (radius * FastSin(deg2)) / 1000;
                    
                    g.DrawLine(x1, y1, x2, y2, waveColor);
                }
            }
            
            // Fade in/out based on direction
            byte fadeAlpha;
            if (progressInt < 500) {
                fadeAlpha = (byte)(((1000 - progressInt * 2) * 150) / 1000);
            } else {
                fadeAlpha = (byte)(((progressInt - 500) * 2 * 150) / 1000);
            }
            uint fadeColor = ((uint)fadeAlpha << 24);
            g.FillRectangle(x, y - barHeight, width, height + barHeight, fadeColor);
        }
        
        /// <summary>
        /// Explode effect - window explodes into particles
        /// </summary>
        private static void RenderExplode(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int particleCount = UISettings.ExplodeParticleCount;
            if (particleCount <= 0) return;
            uint color = UISettings.ExplodeColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            int centerX = x + width / 2;
            int centerY = y - barHeight + (height + barHeight) / 2;
            
            for (int i = 0; i < particleCount; i++) {
                int angleDeg = (i * 137) % 360;
                int speed = ((i * 23) % 100) + 50;
                
                int dist = (speed * progressInt) / 1000;
                int px = centerX + (dist * FastCos(angleDeg)) / 1000;
                int py = centerY + (dist * FastSin(angleDeg)) / 1000;
                
                // Skip particles that are off-screen
                if (px < 0 || px >= screenW || py < 0 || py >= screenH) continue;
                
                byte alpha = (byte)(((1000 - progressInt) * 255) / 1000);
                uint pColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                
                int pSize = 2 + (progressInt * 2 / 1000);
                g.FillRectangle(px, py, pSize, pSize, pColor);
                
                // Trail
                if (dist > 10) {
                    int trailDist = dist - 10;
                    int trailX = centerX + (trailDist * FastCos(angleDeg)) / 1000;
                    int trailY = centerY + (trailDist * FastSin(angleDeg)) / 1000;
                    if (trailX >= 0 && trailX < screenW && trailY >= 0 && trailY < screenH) {
                        uint trailColor = ((uint)(alpha / 2) << 24) | (color & 0x00FFFFFF);
                        g.FillRectangle(trailX, trailY, 2, 2, trailColor);
                    }
                }
            }
        }
        
        /// <summary>
        /// Implode effect - particles converge into window
        /// </summary>
        private static void RenderImplode(int x, int y, int width, int height, int barHeight, int progressInt) {
            var g = Framebuffer.Graphics;
            int particleCount = UISettings.ExplodeParticleCount;
            if (particleCount <= 0) return;
            uint color = UISettings.ExplodeColor;
            int screenW = Framebuffer.Width;
            int screenH = Framebuffer.Height;
            
            int centerX = x + width / 2;
            int centerY = y - barHeight + (height + barHeight) / 2;
            
            for (int i = 0; i < particleCount; i++) {
                int angleDeg = (i * 137) % 360;
                int speed = ((i * 23) % 100) + 50;
                
                int dist = (speed * (1000 - progressInt)) / 1000;
                int px = centerX + (dist * FastCos(angleDeg)) / 1000;
                int py = centerY + (dist * FastSin(angleDeg)) / 1000;
                
                // Skip particles that are off-screen
                if (px < 0 || px >= screenW || py < 0 || py >= screenH) continue;
                
                byte alpha = (byte)((progressInt * 255) / 1000);
                uint pColor = ((uint)alpha << 24) | (color & 0x00FFFFFF);
                
                int pSize = 2 + ((1000 - progressInt) * 2 / 1000);
                g.FillRectangle(px, py, pSize, pSize, pColor);
            }
            
            // Fade in
            byte fadeAlpha = (byte)(((1000 - progressInt) * 150) / 1000);
            uint fadeColor = ((uint)fadeAlpha << 24);
            g.FillRectangle(x, y - barHeight, width, height + barHeight, fadeColor);
        }
    }
}
