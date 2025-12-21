using guideXOS.GUI;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using guideXOS.FS;
using System;

namespace guideXOS.DefaultApps {
    /// <summary>
    /// TTF Font Demo - Demonstrates TrueType font rendering
    /// </summary>
    internal class TTFFontDemo : Window {
        private TrueTypeFont _ttfFont12;
        private TrueTypeFont _ttfFont16;
        private TrueTypeFont _ttfFont24;
        private bool _fontsLoaded = false;
        private string _errorMessage = "";
        
        public TTFFontDemo(int x, int y, int w, int h) : base(x, y, w, h) {
            this.Title = "TTF Font Demo";
            this.ShowInStartMenu = true;
            
            // Try to load Roboto TTF fonts
            LoadTTFFonts();
        }
        
        private void LoadTTFFonts() {
            try {
                //Console.WriteLine("[TTF Demo] Loading Roboto fonts...");
                
                // Try to load Roboto-Regular.ttf at different sizes
                byte[] robotoData = File.ReadAllBytes("Fonts/roboto/Roboto-Regular.ttf");
                
                if (robotoData != null && robotoData.Length > 0) {
                    _ttfFont12 = new TrueTypeFont(robotoData, 12);
                    _ttfFont16 = new TrueTypeFont(robotoData, 16);
                    _ttfFont24 = new TrueTypeFont(robotoData, 24);
                    _fontsLoaded = true;
                    //Console.WriteLine("[TTF Demo] Successfully loaded Roboto fonts!");
                } else {
                    _errorMessage = "Could not read Roboto-Regular.ttf";
                    //Console.WriteLine("[TTF Demo] Error: " + _errorMessage);
                }
            } catch {
                _errorMessage = "Error loading TTF fonts";
                //Console.WriteLine("[TTF Demo] Error: " + _errorMessage);
            }
        }
        
        public override void OnDraw() {
            base.OnDraw();
            
            int cx = this.X + 1;
            int cy = this.Y + 1;
            int cw = this.Width - 2;
            int ch = this.Height - 2;
            
            // Clear content area
            Framebuffer.Graphics.FillRectangle(cx, cy, cw, ch, 0xFF1E1E1E);
            
            int yOffset = cy + 20;
            
            if (!_fontsLoaded) {
                // Show error message using bitmap font
                WindowManager.font.DrawString(cx + 20, yOffset, "Failed to load TTF fonts!");
                WindowManager.font.DrawString(cx + 20, yOffset + 30, _errorMessage);
                WindowManager.font.DrawString(cx + 20, yOffset + 60, "Make sure Roboto-Regular.ttf");
                WindowManager.font.DrawString(cx + 20, yOffset + 90, "is in Fonts/roboto/ directory");
                return;
            }
            
            // Title using bitmap font
            WindowManager.font.DrawString(cx + 20, yOffset, "TrueType Font Rendering Test");
            yOffset += 40;
            
            try {
                // Draw samples at different sizes
                WindowManager.font.DrawString(cx + 20, yOffset, "12pt Roboto:");
                yOffset += 25;
                _ttfFont12.DrawString(Framebuffer.Graphics, cx + 40, yOffset, "The quick brown fox jumps over the lazy dog", 0xFFFFFFFF);
                yOffset += 35;
                
                WindowManager.font.DrawString(cx + 20, yOffset, "16pt Roboto:");
                yOffset += 25;
                _ttfFont16.DrawString(Framebuffer.Graphics, cx + 40, yOffset, "The quick brown fox jumps over the lazy dog", 0xFFFFFFFF);
                yOffset += 45;
                
                WindowManager.font.DrawString(cx + 20, yOffset, "24pt Roboto:");
                yOffset += 30;
                _ttfFont24.DrawString(Framebuffer.Graphics, cx + 40, yOffset, "The quick brown fox", 0xFFFFFFFF);
                yOffset += 60;
                
                // Show character samples
                WindowManager.font.DrawString(cx + 20, yOffset, "Character samples:");
                yOffset += 25;
                
                string samples = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
                _ttfFont16.DrawString(Framebuffer.Graphics, cx + 40, yOffset, samples, 0xFFFFFFFF);
                yOffset += 35;
                
                string numbers = "0123456789 !@#$%^&*()_+-=[]{}|;:',.<>?/";
                _ttfFont16.DrawString(Framebuffer.Graphics, cx + 40, yOffset, numbers, 0xFFFFFFFF);
                
            } catch {
                WindowManager.font.DrawString(cx + 20, yOffset, "Rendering error occurred");
            }
        }
        
        public override void OnInput() {
            base.OnInput();
        }
    }
}
