using guideXOS.GUI;
using System;

namespace guideXOS.Shell.Commands {
    /// <summary>
    /// Console command to toggle icon format between PNG and SVG
    /// Usage: iconformat [svg|png|toggle|status]
    /// </summary>
    public static class IconFormatCommand {
        /// <summary>
        /// Execute the icon format command
        /// </summary>
        /// <param name="args">Command arguments</param>
        public static void Execute(string[] args) {
            if (args == null || args.Length == 0) {
                ShowHelp();
                return;
            }

            string command = args[0];
            
            // Convert to lowercase for case-insensitive comparison
            command = command.ToLower();

            if (command == "svg") {
                Icons.SetIconFormat(IconFormat.SVG);
                Console.WriteLine("Icon format changed to SVG");
                Console.WriteLine("Desktop and file manager icons will now use SVG format");
                Console.WriteLine("Note: SVG files must be present in icons/ directory");
            }
            else if (command == "png") {
                Icons.SetIconFormat(IconFormat.PNG);
                Console.WriteLine("Icon format changed to PNG");
                Console.WriteLine("Desktop and file manager icons will now use PNG format");
            }
            else if (command == "toggle") {
                if (Icons.CurrentFormat == IconFormat.SVG) {
                    Icons.SetIconFormat(IconFormat.PNG);
                    Console.WriteLine("Switched to PNG icons");
                } else {
                    Icons.SetIconFormat(IconFormat.SVG);
                    Console.WriteLine("Switched to SVG icons");
                }
            }
            else if (command == "status" || command == "info") {
                ShowStatus();
            }
            else {
                Console.WriteLine("Unknown option: " + command);
                ShowHelp();
            }
        }

        private static void ShowHelp() {
            Console.WriteLine("iconformat - Change desktop icon format");
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("  iconformat svg     - Use SVG icons");
            Console.WriteLine("  iconformat png     - Use PNG icons");
            Console.WriteLine("  iconformat toggle  - Toggle between SVG and PNG");
            Console.WriteLine("  iconformat status  - Show current format");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  iconformat svg");
            Console.WriteLine("  iconformat toggle");
        }

        private static void ShowStatus() {
            string current = Icons.CurrentFormat == IconFormat.SVG ? "SVG" : "PNG";
            Console.WriteLine("Current icon format: " + current);
            Console.WriteLine("");
            Console.WriteLine("SVG Icons:");
            Console.WriteLine("  - Scalable to any size");
            Console.WriteLine("  - Requires SVG files in icons/ directory");
            Console.WriteLine("  - Supports basic shapes (rect, circle, ellipse)");
            Console.WriteLine("");
            Console.WriteLine("PNG Icons:");
            Console.WriteLine("  - Pre-rendered raster images");
            Console.WriteLine("  - Multiple sizes available (16, 24, 32, 48, 128)");
            Console.WriteLine("  - Default format with full compatibility");
        }
    }
}
