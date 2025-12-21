using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.FS;
using System.Drawing;

namespace guideXOS.Misc {
    internal static class BootSplash {
        static string sTeam;
        static string sOS;
        static string sVer;
        static int phase;
        static ulong lastTick;
        static bool inited;
        // Faster blink control
        const int BlinkPhases = 3; // number of blocks
        const ulong BlinkIntervalMs = 80; // advance phase every 80 ms for faster blinking
        static ulong nextBlinkTick;
        // Fallback frame-based animation when Timer.Ticks does not advance during boot
        static int frameAdvanceCounter;
        const int FramesPerAdvance = 2; // advance phase every N frames when timer is stuck
        
        // Logo image
        static Image logoImage;
        static bool logoLoadAttempted; // Track if we've tried to load the logo
        static bool backgroundDrawn; // Track if background has been drawn
        
        // Dots position cache to avoid recalculation
        static int dotsY;
        static int dotsStartX;
        static int dotsSize;
        static int dotsGap;

        public static void Initialize(string team = "Team Nexgen", string os = "guideXOS", string ver = "Version: 0.2") {
            sTeam = team;
            sOS = os;
            sVer = ver;
            phase = 0;
            lastTick = 0;
            inited = true;
            nextBlinkTick = Timer.Ticks + BlinkIntervalMs;
            logoImage = null;
            logoLoadAttempted = false;
            backgroundDrawn = false;
            frameAdvanceCounter = 0;
            
            // DON'T load logo here - filesystem isn't initialized yet!
            // Logo will be loaded lazily on first Tick() call
        }

        public static void Cleanup() {
            // Dispose logo image to free memory before transitioning to desktop
            if (logoImage != null) {
                logoImage.Dispose();
                logoImage = null;
            }
            inited = false;
            backgroundDrawn = false;
        }

        public static void Tick() {
            if (!inited) return;
            if (Framebuffer.Graphics == null) return; // Safety check
            
            // Lazy load logo on first tick (filesystem should be ready by then)
            if (!logoLoadAttempted) {
                logoLoadAttempted = true; // Set this FIRST to prevent re-entry
                try {
                    byte[] logoData = File.ReadAllBytes("Images/tnlogo.png");
                    if (logoData != null && logoData.Length > 0) {
                        logoImage = new PNG(logoData);
                        logoData.Dispose();
                    }
                } catch {
                    // If logo fails to load, continue without it
                    logoImage = null;
                }
            }
            
            int w = Framebuffer.Width;
            int h = Framebuffer.Height;

            // Draw background and logo ONCE on first frame
            if (!backgroundDrawn) {
                // Clear screen once
                Framebuffer.Graphics.Clear(0x00000000);

                // Draw logo image if available (centered)
                int logoBottomY = h / 2; // Default if no logo
                if (logoImage != null) {
                    int logoX = (w / 2) - (logoImage.Width / 2);
                    int logoY = (h / 2) - (logoImage.Height / 2) - 30; // Offset up a bit to make room for dots
                    Framebuffer.Graphics.DrawImage(logoX, logoY, logoImage);
                    logoBottomY = logoY + logoImage.Height;
                }

                // Calculate dots position - place them below the logo with some spacing
                dotsSize = 10;
                dotsGap = 12;
                int totalW = dotsSize * 3 + dotsGap * 2;
                dotsStartX = (w / 2) - (totalW / 2);
                dotsY = logoBottomY + 40; // 40 pixels below the logo bottom

                // Draw initial dots on first frame
                for (int i = 0; i < 3; i++) {
                    uint col = (i == phase) ? 0xFF2E86C1u : 0xFF3A3A3Au;
                    Framebuffer.Graphics.FillRectangle(dotsStartX + i * (dotsSize + dotsGap), dotsY, dotsSize, dotsSize, col);
                }

                backgroundDrawn = true;
            }

            // Check if phase changed
            ulong now = Timer.Ticks;
            bool phaseChanged = false;
            // Detect if timer appears stuck (no change between frames)
            bool timerStuck = (now == lastTick);
            if (!timerStuck) {
                // Timer is advancing: use time-based blinking
                if (now >= nextBlinkTick) {
                    phaseChanged = true;
                    phase = (phase + 1) % BlinkPhases;
                    nextBlinkTick = now + BlinkIntervalMs;
                }
            } else {
                // Timer is stuck: use frame-based fallback to animate
                frameAdvanceCounter++;
                if (frameAdvanceCounter >= FramesPerAdvance) {
                    frameAdvanceCounter = 0;
                    phaseChanged = true;
                    phase = (phase + 1) % BlinkPhases;
                }
            }
            // Update lastTick for next detection
            lastTick = now;

            // Only redraw dots if phase changed (eliminates flicker!)
            if (phaseChanged) {
                // Clear only the dots area with opaque black to ensure redraw is visible
                int dotsClearWidth = dotsSize * 3 + dotsGap * 2;
                Framebuffer.Graphics.AFillRectangle(dotsStartX - 2, dotsY - 2, dotsClearWidth + 4, dotsSize + 4, 0xFF000000);

                // Redraw all 3 dots
                for (int i = 0; i < 3; i++) {
                    uint col = (i == phase) ? 0xFF2E86C1u : 0xFF3A3A3Au;
                    Framebuffer.Graphics.FillRectangle(dotsStartX + i * (dotsSize + dotsGap), dotsY, dotsSize, dotsSize, col);
                }
            }

            Framebuffer.Update();
        }
    }
}
