using guideXOS.GUI;
using guideXOS.Misc;
using guideXOS.FS;
using System.Drawing;

namespace guideXOS.Examples {
    /// <summary>
    /// Example demonstrating SVG icon usage in guideXOS
    /// </summary>
    public static class SVGIconExample {
        /// <summary>
        /// Enable SVG icons globally
        /// Call this during initialization to use SVG icons everywhere
        /// </summary>
        public static void EnableSVGIcons() {
            Icons.SetIconFormat(IconFormat.SVG);
            Console.WriteLine("SVG icons enabled! Desktop and file manager will now use SVG icons.");
        }

        /// <summary>
        /// Disable SVG icons and return to PNG
        /// </summary>
        public static void DisableSVGIcons() {
            Icons.SetIconFormat(IconFormat.PNG);
            Console.WriteLine("PNG icons enabled! Desktop and file manager will now use PNG icons.");
        }

        /// <summary>
        /// Toggle between SVG and PNG icons
        /// </summary>
        public static void ToggleIconFormat() {
            if (Icons.CurrentFormat == IconFormat.SVG) {
                DisableSVGIcons();
            } else {
                EnableSVGIcons();
            }
        }

        /// <summary>
        /// Example: Load and display a specific SVG file
        /// </summary>
        /// <param name="svgPath">Path to SVG file</param>
        /// <param name="x">X position to draw</param>
        /// <param name="y">Y position to draw</param>
        /// <param name="size">Icon size</param>
        public static void DrawCustomSVGIcon(string svgPath, int x, int y, int size = 48) {
            try {
                byte[] svgData = File.ReadAllBytes(svgPath);
                if (svgData != null && svgData.Length > 0) {
                    SVG svgIcon = new SVG(svgData, size, size);
                    Kernel.Drivers.Framebuffer.Graphics.DrawImage(x, y, svgIcon);
                    svgIcon.Dispose();
                    svgData.Dispose();
                    Console.WriteLine($"Drew SVG icon from {svgPath} at ({x},{y})");
                } else {
                    Console.WriteLine($"Failed to load SVG: {svgPath}");
                }
            } catch {
                Console.WriteLine($"Error loading SVG: {svgPath}");
            }
        }

        /// <summary>
        /// Example: Create a grid of SVG icons at different sizes
        /// </summary>
        public static void ShowSVGSizeComparison() {
            int[] sizes = { 16, 24, 32, 48, 64, 128 };
            int startX = 100;
            int startY = 100;
            int gap = 20;
            int currentX = startX;
            int currentY = startY;

            foreach (int size in sizes) {
                // Try to load a sample SVG
                string svgPath = "icons/system-file-manager.svg";
                try {
                    byte[] svgData = File.ReadAllBytes(svgPath);
                    if (svgData != null && svgData.Length > 0) {
                        SVG svgIcon = new SVG(svgData, size, size);
                        Kernel.Drivers.Framebuffer.Graphics.DrawImage(currentX, currentY, svgIcon);
                        
                        // Draw size label below icon
                        string label = size.ToString() + "px";
                        WindowManager.font.DrawString(currentX, currentY + size + 5, label);
                        
                        svgIcon.Dispose();
                        svgData.Dispose();
                        
                        currentX += size + gap;
                    }
                } catch {
                    Console.WriteLine($"Could not load {svgPath}");
                }
            }
        }

        /// <summary>
        /// Print current icon format status
        /// </summary>
        public static void PrintIconStatus() {
            string format = Icons.CurrentFormat == IconFormat.SVG ? "SVG" : "PNG";
            Console.WriteLine($"Current icon format: {format}");
            Console.WriteLine("Available commands:");
            Console.WriteLine("  - EnableSVGIcons()  : Switch to SVG icons");
            Console.WriteLine("  - DisableSVGIcons() : Switch to PNG icons");
            Console.WriteLine("  - ToggleIconFormat(): Toggle between formats");
        }
    }
}
