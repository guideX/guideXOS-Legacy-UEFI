using guideXOS.FS;
using guideXOS.GUI;
using System;

namespace guideXOS.OS {
    /// <summary>
    /// System Configuration - saves and loads user settings
    /// Only functional when NOT in LiveMode
    /// </summary>
    internal static class Configuration {
        private const string ConfigDir = "/etc/guidexos";
        private const string ConfigFile = "/etc/guidexos/config.ini";
        
        private static bool _initialized = false;
        
        /// <summary>
        /// Initialize configuration system
        /// </summary>
        public static void Initialize() {
            if (_initialized) return;
            
            Console.WriteLine("[CONFIG] Initializing configuration system...");
            
            if (SystemMode.IsLiveMode) {
                Console.WriteLine("[CONFIG] LiveMode detected - settings will not be saved");
                _initialized = true;
                return;
            }
            
            // Load saved configuration
            LoadConfiguration();
            
            _initialized = true;
            Console.WriteLine("[CONFIG] Configuration system initialized");
        }
        
        /// <summary>
        /// Load all configuration from disk
        /// </summary>
        public static void LoadConfiguration() {
            if (SystemMode.IsLiveMode) return;
            
            try {
                // Load UI settings
                LoadUISettings();
                Console.WriteLine("[CONFIG] Configuration loaded from disk");
            } catch {
                Console.WriteLine("[CONFIG] Error loading configuration");
            }
        }
        
        /// <summary>
        /// Save all configuration to disk
        /// </summary>
        public static void SaveConfiguration() {
            if (SystemMode.IsLiveMode) {
                Console.WriteLine("[CONFIG] Cannot save in LiveMode");
                return;
            }
            
            if (!SystemMode.CanWriteSettings()) {
                Console.WriteLine("[CONFIG] Storage not writable");
                return;
            }
            
            try {
                // Save UI settings
                SaveUISettings();
                Console.WriteLine("[CONFIG] Configuration saved to disk");
            } catch {
                Console.WriteLine("[CONFIG] Error saving configuration");
            }
        }
        
        /// <summary>
        /// Load UI settings from config file
        /// </summary>
        private static void LoadUISettings() {
            if (!File.Exists(ConfigFile)) return;
            
            try {
                byte[] data = File.ReadAllBytes(ConfigFile);
                string content = GetStringFromBytes(data);
                data.Dispose();
                
                // Parse simple key=value pairs
                // Look for icon_size
                string iconSizeStr = FindValue(content, "icon_size=");
                if (iconSizeStr != null) {
                    int iconSize = ParseInt(iconSizeStr);
                    if (iconSize > 0) {
                        Desktop.SetIconSize(iconSize);
                    }
                    iconSizeStr.Dispose();
                }
                
                // Look for show_widgets_on_startup
                string widgetsStr = FindValue(content, "show_widgets_on_startup=");
                if (widgetsStr != null) {
                    bool show = ParseBool(widgetsStr);
                    UISettings.ShowWidgetsOnStartup = show;
                    widgetsStr.Dispose();
                }
                
                content.Dispose();
                Console.WriteLine("[CONFIG] UI settings loaded");
            } catch {
                Console.WriteLine("[CONFIG] Error loading UI settings");
            }
        }
        
        /// <summary>
        /// Save UI settings to config file
        /// </summary>
        private static void SaveUISettings() {
            try {
                string content = "# guideXOS Configuration\n";
                content += "icon_size=" + Desktop.IconSize.ToString() + "\n";
                content += "show_widgets_on_startup=" + (UISettings.ShowWidgetsOnStartup ? "true" : "false") + "\n";
                content += "enable_fade_animations=" + (UISettings.EnableFadeAnimations ? "true" : "false") + "\n";
                content += "enable_window_slide_animations=" + (UISettings.EnableWindowSlideAnimations ? "true" : "false") + "\n";
                content += "enable_auto_background_rotation=" + (UISettings.EnableAutoBackgroundRotation ? "true" : "false") + "\n";
                content += "background_rotation_interval_minutes=" + UISettings.BackgroundRotationIntervalMinutes.ToString() + "\n";
                
                byte[] data = GetBytesFromString(content);
                File.WriteAllBytes(ConfigFile, data);
                data.Dispose();
                content.Dispose();
                
                Console.WriteLine("[CONFIG] UI settings saved");
            } catch {
                Console.WriteLine("[CONFIG] Error saving UI settings");
            }
        }
        
        /// <summary>
        /// Find value after key= in content
        /// </summary>
        private static string FindValue(string content, string key) {
            if (content == null || key == null) return null;
            
            // Manual string search since IndexOf only takes char
            int idx = -1;
            for (int i = 0; i <= content.Length - key.Length; i++) {
                bool match = true;
                for (int j = 0; j < key.Length; j++) {
                    if (content[i + j] != key[j]) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    idx = i;
                    break;
                }
            }
            
            if (idx < 0) return null;
            
            int startIdx = idx + key.Length;
            int endIdx = startIdx;
            
            // Find end of line
            while (endIdx < content.Length && content[endIdx] != '\n' && content[endIdx] != '\r') {
                endIdx++;
            }
            
            if (endIdx > startIdx) {
                return content.Substring(startIdx, endIdx - startIdx);
            }
            
            return null;
        }
        
        /// <summary>
        /// Parse integer from string
        /// </summary>
        private static int ParseInt(string str) {
            if (str == null || str.Length == 0) return 0;
            
            int result = 0;
            bool negative = false;
            int startIdx = 0;
            
            if (str[0] == '-') {
                negative = true;
                startIdx = 1;
            }
            
            for (int i = startIdx; i < str.Length; i++) {
                char c = str[i];
                if (c >= '0' && c <= '9') {
                    result = result * 10 + (c - '0');
                } else {
                    break; // Stop at first non-digit
                }
            }
            
            return negative ? -result : result;
        }
        
        /// <summary>
        /// Parse boolean from string
        /// </summary>
        private static bool ParseBool(string str) {
            if (str == null || str.Length == 0) return false;
            
            // Check for "true"
            if (str.Length >= 4) {
                char c0 = str[0];
                char c1 = str[1];
                char c2 = str[2];
                char c3 = str[3];
                
                // Convert to lowercase
                if (c0 >= 'A' && c0 <= 'Z') c0 = (char)(c0 + 32);
                if (c1 >= 'A' && c1 <= 'Z') c1 = (char)(c1 + 32);
                if (c2 >= 'A' && c2 <= 'Z') c2 = (char)(c2 + 32);
                if (c3 >= 'A' && c3 <= 'Z') c3 = (char)(c3 + 32);
                
                if (c0 == 't' && c1 == 'r' && c2 == 'u' && c3 == 'e') {
                    return true;
                }
            }
            
            // Check for "1"
            if (str.Length >= 1 && str[0] == '1') {
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Convert string to byte array (ASCII)
        /// </summary>
        private static byte[] GetBytesFromString(string str) {
            if (string.IsNullOrEmpty(str)) return new byte[0];
            
            byte[] data = new byte[str.Length];
            for (int i = 0; i < str.Length; i++) {
                data[i] = (byte)(str[i] & 0x7F);
            }
            return data;
        }
        
        /// <summary>
        /// Convert byte array to string (ASCII)
        /// </summary>
        private static string GetStringFromBytes(byte[] data) {
            if (data == null || data.Length == 0) return "";
            
            char[] chars = new char[data.Length];
            for (int i = 0; i < data.Length; i++) {
                chars[i] = (char)data[i];
            }
            return new string(chars);
        }
    }
}
