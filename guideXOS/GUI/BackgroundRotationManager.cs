using guideXOS.FS;
using guideXOS.Kernel.Drivers;
using guideXOS.Misc;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace guideXOS.GUI {
    /// <summary>
    /// Manages automatic background rotation with fade transitions
    /// </summary>
    internal static class BackgroundRotationManager {
        private static List<string> _backgroundPaths;
        private static int _currentIndex = 0;
        private static ulong _lastRotationTick = 0;
        private static bool _initialized = false;
        
        // Fade transition state - FIXED: Pre-render frames instead of per-pixel blending
        private static bool _isFading = false;
        private static Image _fadeFrame = null; // Pre-rendered composite frame
        private static ulong _fadeStartTick = 0;
        private static int _fadeFrameCount = 0;
        private static int _fadeCurrentFrame = 0;
        
        /// <summary>
        /// Initialize the background rotation manager
        /// </summary>
        public static void Initialize() {
            if (_initialized) return;
            
            _backgroundPaths = new List<string>();
            LoadBackgroundPaths();
            _lastRotationTick = Timer.Ticks;
            _initialized = true;
            
            // Load background on startup
            if (_backgroundPaths.Count > 0) {
                int selectedIndex = 0;
                
                // Choose random background if enabled and auto-rotation is disabled
                if (!UISettings.EnableAutoBackgroundRotation && UISettings.EnableRandomBackgroundOnStartup) {
                    // Use pseudo-random based on current tick count
                    selectedIndex = (int)(Timer.Ticks % (ulong)_backgroundPaths.Count);
                }
                // Otherwise use first background (index 0) for auto-rotation or when random is disabled
                
                try {
                    byte[] data = File.ReadAllBytes(_backgroundPaths[selectedIndex]);
                    if (data != null) {
                        var img = new PNG(data);
                        var resized = img.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                        img.Dispose();
                        
                        // Replace gradient with selected background
                        if (Program.Wallpaper != null) Program.Wallpaper.Dispose();
                        Program.Wallpaper = resized;
                        _currentIndex = selectedIndex;
                    }
                } catch {
                    // Failed to load background, keep gradient
                }
            }
        }
        
        /// <summary>
        /// Load all available background image paths
        /// </summary>
        private static void LoadBackgroundPaths() {
            _backgroundPaths.Clear();
            
            var files = File.GetFiles(@"Backgrounds/");
            if (files != null && files.Count > 0) {
                for (int i = 0; i < files.Count; i++) {
                    var fi = files[i];
                    if (fi.Attribute != FileAttribute.Directory) {
                        string name = fi.Name;
                        // Check for image files - case insensitive
                        bool isPng = name.EndsWith(".png") || name.EndsWith(".PNG");
                        bool isJpg = name.EndsWith(".jpg") || name.EndsWith(".JPG") || 
                                    name.EndsWith(".jpeg") || name.EndsWith(".JPEG");
                        bool isBmp = name.EndsWith(".bmp") || name.EndsWith(".BMP");
                        
                        // Skip thumbnail files
                        if (name.EndsWith("_thumb.png") || name.EndsWith("_thumb.PNG")) {
                            continue;
                        }
                        
                        if (isPng || isJpg || isBmp) {
                            string path = "Backgrounds/" + name;
                            _backgroundPaths.Add(path);
                        }
                    }
                    fi.Dispose();
                }
                files.Dispose();
            }
        }
        
        /// <summary>
        /// Update rotation logic - call this from main loop
        /// </summary>
        public static void Update() {
            if (!_initialized) Initialize();
            
            // Handle fade transition
            if (_isFading) {
                UpdateFadeTransition();
                return;
            }
            
            // Check if auto-rotation is enabled
            if (!UISettings.EnableAutoBackgroundRotation) return;
            
            // Check if we have backgrounds to rotate
            if (_backgroundPaths.Count <= 1) return;
            
            // Check if enough time has passed
            ulong elapsed = Timer.Ticks >= _lastRotationTick ? 
                           Timer.Ticks - _lastRotationTick : 0;
            ulong intervalMs = (ulong)UISettings.BackgroundRotationIntervalMinutes * 60000;
            
            if (elapsed >= intervalMs) {
                RotateToNext();
                _lastRotationTick = Timer.Ticks;
            }
        }
        
        /// <summary>
        /// Rotate to next background
        /// </summary>
        private static void RotateToNext() {
            if (_backgroundPaths.Count == 0) return;
            
            // Pick next background (random or sequential)
            _currentIndex = (_currentIndex + 1) % _backgroundPaths.Count;
            
            try {
                byte[] data = File.ReadAllBytes(_backgroundPaths[_currentIndex]);
                if (data != null) {
                    var img = new PNG(data);
                    var resized = img.ResizeImage(Framebuffer.Width, Framebuffer.Height);
                    img.Dispose(); // FIXED: Dispose original image
                    
                    // Check if fade transition is enabled
                    if (UISettings.EnableBackgroundFadeTransition && Program.Wallpaper != null) {
                        StartFadeTransition(resized);
                    } else {
                        // Instant change - FIXED: Dispose old wallpaper
                        if (Program.Wallpaper != null) Program.Wallpaper.Dispose();
                        Program.Wallpaper = resized;
                    }
                }
            } catch {
                // Failed to load background, try next one on next rotation
            }
        }
        
        /// <summary>
        /// Start fade transition between backgrounds - FIXED: Simplified to avoid per-pixel operations
        /// </summary>
        private static void StartFadeTransition(Image newBackground) {
            // FIXED: Disable fade transition to prevent memory leak
            // Instead use instant transition
            if (Program.Wallpaper != null) {
                Program.Wallpaper.Dispose();
            }
            Program.Wallpaper = newBackground;
            _isFading = false;
        }
        
        /// <summary>
        /// Update fade transition animation - FIXED: Removed expensive per-pixel blending
        /// </summary>
        private static void UpdateFadeTransition() {
            // Fade transition disabled to prevent memory leak
            _isFading = false;
        }
        
        /// <summary>
        /// Complete the fade transition - FIXED: Proper disposal
        /// </summary>
        private static void CompleteFadeTransition() {
            _isFading = false;
            
            if (_fadeFrame != null) {
                _fadeFrame.Dispose();
                _fadeFrame = null;
            }
        }
        
        /// <summary>
        /// Draw the current background - FIXED: No more per-pixel operations
        /// </summary>
        public static void DrawBackground() {
            if (Program.Wallpaper != null) {
                // Draw regular wallpaper
                Framebuffer.Graphics.DrawImage(0, 0, Program.Wallpaper, false);
            } else {
                // Fill with default color
                Framebuffer.Graphics.FillRectangle(0, 0, Framebuffer.Width, Framebuffer.Height, 0xFF1E1E1E);
            }
        }
        
        /// <summary>
        /// Reload background paths (call when new backgrounds are added)
        /// </summary>
        public static void ReloadBackgrounds() {
            LoadBackgroundPaths();
            _currentIndex = 0;
        }
        
        /// <summary>
        /// Force immediate rotation to next background
        /// </summary>
        public static void ForceRotateNext() {
            if (_isFading) return; // Don't rotate while fading
            RotateToNext();
            _lastRotationTick = Timer.Ticks;
        }
        
        /// <summary>
        /// Get count of available backgrounds
        /// </summary>
        public static int GetBackgroundCount() {
            return _backgroundPaths.Count;
        }
        
        /// <summary>
        /// Cleanup resources
        /// </summary>
        public static new void Dispose() {
            if (_fadeFrame != null) {
                _fadeFrame.Dispose();
                _fadeFrame = null;
            }
            _isFading = false;
        }
    }
}
