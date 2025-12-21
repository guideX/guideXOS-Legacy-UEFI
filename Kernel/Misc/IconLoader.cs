using System.Drawing;
using guideXOS.FS;
using guideXOS.GUI;

namespace guideXOS.Misc {
    /// <summary>
    /// Icon Loader - Helper class for loading icons from various formats
    /// Provides unified interface for PNG, SVG, and other icon formats
    /// </summary>
    public static class IconLoader {
        /// <summary>
        /// Load icon from file with automatic format detection
        /// </summary>
        /// <param name="path">Path to icon file</param>
        /// <param name="targetWidth">Desired width for SVG rendering</param>
        /// <param name="targetHeight">Desired height for SVG rendering</param>
        /// <returns>Image object or null if failed</returns>
        public static Image LoadIcon(string path, int targetWidth = 32, int targetHeight = 32) {
            try {
                byte[] data = File.ReadAllBytes(path);
                if (data == null || data.Length == 0) return null;
                
                // Detect format by file extension or magic bytes
                if (path.EndsWith(".svg")) {
                    return new SVG(data, targetWidth, targetHeight);
                } else if (path.EndsWith(".png")) {
                    return new PNG(data);
                } else {
                    // Try PNG by default
                    return new PNG(data);
                }
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Load icon with specific format
        /// </summary>
        public static Image LoadIcon(string path, IconFormat format, int size = 32) {
            try {
                byte[] data = File.ReadAllBytes(path);
                if (data == null || data.Length == 0) return null;
                
                if (format == IconFormat.SVG) {
                    return new SVG(data, size, size);
                } else {
                    return new PNG(data);
                }
            } catch {
                return null;
            }
        }

        /// <summary>
        /// Check if file is a supported icon format
        /// </summary>
        public static bool IsSupportedFormat(string path) {
            return path.EndsWith(".png") || path.EndsWith(".svg");
        }

        /// <summary>
        /// Get icon format from file extension
        /// </summary>
        public static IconFormat GetFormatFromPath(string path) {
            if (path.EndsWith(".svg")) {
                return IconFormat.SVG;
            }
            return IconFormat.PNG;
        }
    }
}
