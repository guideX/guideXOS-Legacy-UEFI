using guideXOS.FS;
using guideXOS.Misc;
using System.Drawing;
namespace guideXOS.GUI {
    /// <summary>
    /// Icon Format
    /// </summary>
    public enum IconFormat {
        PNG,
        SVG
    }
    
    /// <summary>
    /// Icons Privateq
    /// </summary>
    public class IconsPrivate {
        /// <summary>
        /// Document Icon
        /// </summary>
        public Image DocumentIcon;
        /// <summary>
        /// Image Icon
        /// </summary>
        public Image ImageIcon;
        /// <summary>
        /// Audio Icon
        /// </summary>
        public Image AudioIcon;
        /// <summary>
        /// Folder Icon
        /// </summary>
        public Image FolderIcon;
        /// <summary>
        /// Taskbar Icon
        /// </summary>
        public Image TaskbarIcon;
        /// <summary>
        /// Taskbar Icon
        /// </summary>
        public Image TaskbarIconDown;
        /// <summary>
        /// Taskbar Icon
        /// </summary>
        public Image TaskbarIconOver;
        /// <summary>
        /// Start Icon
        /// </summary>
        public Image StartIcon;
        /// <summary>
        /// Audio Pause Icon
        /// </summary>
        public Image AudioPauseIcon;
        /// <summary>
        /// Audio Play Icon
        /// </summary>
        public Image AudioPlayIcon;
        /// <summary>
        /// Calculator Icon
        /// </summary>
        public Image CalculatorIcon;
        /// <summary>
        /// Calendar Icon
        /// </summary>
        public Image CalendarIcon;
        /// <summary>
        /// Edit Icon
        /// </summary>
        public Image EditIcon;
        /// <summary>
        /// Lock Icon
        /// </summary>
        public Image LockIcon;
        /// <summary>
        /// Notepad Icon
        /// </summary>
        public Image NotepadIcon;
        /// <summary>
        /// Applications Icon
        /// </summary>
        public Image ApplicationsIcon;
        /// <summary>
        /// Configure Icon
        /// </summary>
        public Image ConfigureIcon;
        /// <summary>
        /// Chat Icon
        /// </summary>
        public Image ChatIcon;
        /// <summary>
        /// Network Icon
        /// </summary>
        public Image NetworkIcon;
        /// <summary>
        /// Icons Private
        /// </summary>
        /// <param name="size"></param>
        /// <param name="format">Icon format to use (PNG or SVG)</param>
        public IconsPrivate(int size, IconFormat format = IconFormat.PNG) {
            string ext = format == IconFormat.SVG ? "svg" : "png";
            string basePath = format == IconFormat.SVG ? "icons" : $"Images/BlueVelvet/{size}";
            
            if (format == IconFormat.SVG) {
                // Load SVG icons with target size
                ConfigureIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format);
                NotepadIcon = LoadIcon($"{basePath}/utilities-text-editor.svg", size, format);
                EditIcon = LoadIcon($"{basePath}/utilities-text-editor.svg", size, format);
                CalendarIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format); // placeholder
                CalculatorIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format); // placeholder
                DocumentIcon = LoadIcon($"{basePath}/utilities-text-editor.svg", size, format);
                AudioIcon = LoadIcon($"{basePath}/vlc.svg", size, format);
                ImageIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format); // placeholder
                FolderIcon = LoadIcon($"{basePath}/system-file-manager.svg", size, format);
                TaskbarIcon = LoadIcon($"Images/startmenubutton.png", size, IconFormat.PNG); // Keep PNG
                TaskbarIconOver = LoadIcon($"Images/startmenubutton_over.png", size, IconFormat.PNG);
                TaskbarIconDown = LoadIcon($"Images/startmenubutton_over.png", size, IconFormat.PNG);
                StartIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format);
                AudioPauseIcon = LoadIcon($"{basePath}/vlc.svg", size, format);
                AudioPlayIcon = LoadIcon($"{basePath}/vlc.svg", size, format);
                LockIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format);
                ApplicationsIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format);
                ChatIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format);
                NetworkIcon = LoadIcon($"{basePath}/preferences-system.svg", size, format);
            } else {
                // Original PNG loading
                ConfigureIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/configure.png"));
                NotepadIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/notepad.png"));
                EditIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/edit.png"));
                CalendarIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/calendar.png"));
                CalculatorIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/calculator.png"));
                DocumentIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/documents.png"));
                AudioIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/music.png"));
                ImageIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/image.png"));
                FolderIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/folder.png"));
                TaskbarIcon = new PNG(File.ReadAllBytes($"Images/startmenubutton.png"));
                TaskbarIconOver = new PNG(File.ReadAllBytes($"Images/startmenubutton_over.png"));
                TaskbarIconDown = new PNG(File.ReadAllBytes($"Images/startmenubutton_over.png"));
                StartIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/play.png"));
                AudioPauseIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/pause.png"));
                AudioPlayIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/play.png"));
                LockIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/lock.png"));
                ApplicationsIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/applications.png"));
                ChatIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/chat.png"));
                NetworkIcon = new PNG(File.ReadAllBytes($"Images/BlueVelvet/{size}/network.png"));
            }
        }
        
        /// <summary>
        /// Load icon from file with specified format
        /// </summary>
        private Image LoadIcon(string path, int size, IconFormat format) {
            try {
                byte[] data = File.ReadAllBytes(path);
                if (data == null || data.Length == 0) {
                    // Fall back to PNG if SVG not found
                    return new PNG(new byte[0]);
                }
                
                if (format == IconFormat.SVG) {
                    return new SVG(data, size, size);
                } else {
                    return new PNG(data);
                }
            } catch {
                // Return fallback icon on error
                return new PNG(new byte[0]);
            }
        }
    }
    /// <summary>
    /// Icons
    /// </summary>
    public static class Icons {
        /// <summary>
        /// Current icon format
        /// </summary>
        public static IconFormat CurrentFormat = IconFormat.PNG;
        
        /// <summary>
        /// Set icon format for all icon sizes
        /// </summary>
        public static void SetIconFormat(IconFormat format) {
            CurrentFormat = format;
            // Reinitialize all icon sets with new format
            _iconsPrivate16 = new IconsPrivate(16, format);
            _iconsPrivate24 = new IconsPrivate(24, format);
            _iconsPrivate32 = new IconsPrivate(32, format);
            _iconsPrivate48 = new IconsPrivate(48, format);
            _iconsPrivate128 = new IconsPrivate(128, format);
        }
        
        /// <summary>
        /// Icons Private
        /// </summary>
        private static IconsPrivate _iconsPrivate16;
        /// <summary>
        /// Icons Private
        /// </summary>
        private static IconsPrivate _iconsPrivate24;
        /// <summary>
        /// Icons Private
        /// </summary>
        private static IconsPrivate _iconsPrivate32;
        /// <summary>
        /// Icons Private 48
        /// </summary>
        private static IconsPrivate _iconsPrivate48;
        /// <summary>
        /// Icons Private 128
        /// </summary>
        private static IconsPrivate _iconsPrivate128;
        
        /// <summary>
        /// Static constructor to initialize icons (deferred until first use)
        /// </summary>
        static Icons() {
            // CRITICAL: In UEFI mode, skip PNG decoding entirely
            // Icons will use fallback placeholders instead
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                _iconsPrivate16 = null;
                _iconsPrivate24 = null;
                _iconsPrivate32 = null;
                _iconsPrivate48 = null;
                _iconsPrivate128 = null;
            } else {
                _iconsPrivate16 = new IconsPrivate(16);
                _iconsPrivate24 = new IconsPrivate(24);
                _iconsPrivate32 = new IconsPrivate(32);
                _iconsPrivate48 = new IconsPrivate(48);
                _iconsPrivate128 = new IconsPrivate(128);
            }
        }
        /// <summary>
        /// Network Icon
        /// </summary>
        public static Image NetworkIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.NetworkIcon;
                case 24:
                    return _iconsPrivate24.NetworkIcon;
                case 32:
                    return _iconsPrivate32.NetworkIcon;
                case 48:
                    return _iconsPrivate48.NetworkIcon;
                case 128:
                    return _iconsPrivate128.NetworkIcon;
                default:
                    return _iconsPrivate32.NetworkIcon;
            }
        }
        /// <summary>
        /// Chat Icon
        /// </summary>
        public static Image ChatIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.ChatIcon;
                case 24:
                    return _iconsPrivate24.ChatIcon;
                case 32:
                    return _iconsPrivate32.ChatIcon;
                case 48:
                    return _iconsPrivate48.ChatIcon;
                case 128:
                    return _iconsPrivate128.ChatIcon;
                default:
                    return _iconsPrivate32.ChatIcon;
            }
        }
        /// <summary>
        /// Configure Icon
        /// </summary>
        public static Image ConfigureIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.ConfigureIcon;
                case 24:
                    return _iconsPrivate24.ConfigureIcon;
                case 32:
                    return _iconsPrivate32.ConfigureIcon;
                case 48:
                    return _iconsPrivate48.ConfigureIcon;
                case 128:
                    return _iconsPrivate128.ConfigureIcon;
                default:
                    return _iconsPrivate32.ConfigureIcon;
            }
        }
        /// <summary>
        /// Notepad Icon
        /// </summary>
        public static Image NotepadIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.NotepadIcon;
                case 24:
                    return _iconsPrivate24.NotepadIcon;
                case 32:
                    return _iconsPrivate32.NotepadIcon;
                case 48:
                    return _iconsPrivate48.NotepadIcon;
                case 128:
                    return _iconsPrivate128.NotepadIcon;
                default:
                    return _iconsPrivate32.NotepadIcon;
            }
        }
        /// <summary>
        /// Applications Icon
        /// </summary>
        public static Image ApplicationsIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.ApplicationsIcon;
                case 24:
                    return _iconsPrivate24.ApplicationsIcon;
                case 32:
                    return _iconsPrivate32.ApplicationsIcon;
                case 48:
                    return _iconsPrivate48.ApplicationsIcon;
                case 128:
                    return _iconsPrivate128.ApplicationsIcon;
                default:
                    return _iconsPrivate32.ApplicationsIcon;
            }
        }


        /// <summary>
        /// Edit Icon
        /// </summary>
        public static Image EditIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.EditIcon;
                case 24:
                    return _iconsPrivate24.EditIcon;
                case 32:
                    return _iconsPrivate32.EditIcon;
                case 48:
                    return _iconsPrivate48.EditIcon;
                case 128:
                    return _iconsPrivate128.EditIcon;
                default:
                    return _iconsPrivate32.EditIcon;
            }
        }
        /// <summary>
        /// Edit Icon
        /// </summary>
        public static Image LockIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.LockIcon;
                case 24:
                    return _iconsPrivate24.LockIcon;
                case 32:
                    return _iconsPrivate32.LockIcon;
                case 48:
                    return _iconsPrivate48.LockIcon;
                case 128:
                    return _iconsPrivate128.LockIcon;
                default:
                    return _iconsPrivate32.LockIcon;
            }
        }
        /// <summary>
        /// Audio Pause Icon
        /// </summary>
        public static Image AudioPauseIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.AudioPauseIcon;
                case 24:
                    return _iconsPrivate24.AudioPauseIcon;
                case 32:
                    return _iconsPrivate32.AudioPauseIcon;
                case 48:
                    return _iconsPrivate48.AudioPauseIcon;
                case 128:
                    return _iconsPrivate128.AudioPauseIcon;
                default:
                    return _iconsPrivate32.AudioPauseIcon;
            }
        }
        /// <summary>
        /// Calculator Icon
        /// </summary>
        public static Image CalculatorIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.CalculatorIcon;
                case 24:
                    return _iconsPrivate24.CalculatorIcon;
                case 32:
                    return _iconsPrivate32.CalculatorIcon;
                case 48:
                    return _iconsPrivate48.CalculatorIcon;
                case 128:
                    return _iconsPrivate128.CalculatorIcon;
                default:
                    return _iconsPrivate32.CalculatorIcon;
            }
        }
        /// <summary>
        /// Calendar Icon
        /// </summary>
        public static Image CalendarIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.CalendarIcon;
                case 24:
                    return _iconsPrivate24.CalendarIcon;
                case 32:
                    return _iconsPrivate32.CalendarIcon;
                case 48:
                    return _iconsPrivate48.CalendarIcon;
                case 128:
                    return _iconsPrivate128.CalendarIcon;
                default:
                    return _iconsPrivate32.CalendarIcon;
            }
        }
        /// <summary>
        /// Audio Play Icon
        /// </summary>
        public static Image AudioPlayIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.AudioPlayIcon;
                case 24:
                    return _iconsPrivate24.AudioPlayIcon;
                case 32:
                    return _iconsPrivate32.AudioPlayIcon;
                case 48:
                    return _iconsPrivate48.AudioPlayIcon;
                case 128:
                    return _iconsPrivate128.AudioPlayIcon;
                default:
                    return _iconsPrivate32.AudioPlayIcon;
            }
        }
        /// <summary>
        /// Document Icon
        /// </summary>
        public static Image DocumentIcon(int size) {
            switch(size) {
                case 16:
                    return _iconsPrivate16?.DocumentIcon ?? new Image(size, size);
                case 24:
                    return _iconsPrivate24?.DocumentIcon ?? new Image(size, size);
                case 32:
                    return _iconsPrivate32?.DocumentIcon ?? new Image(size, size);
                case 48:
                    return _iconsPrivate48?.DocumentIcon ?? new Image(size, size);
                case 128:
                    return _iconsPrivate128?.DocumentIcon ?? new Image(size, size);
                default:
                    return _iconsPrivate32?.DocumentIcon ?? new Image(32, 32);
            }
        }
        /// <summary>
        /// Image Icon
        /// </summary>
        public static Image ImageIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16?.ImageIcon ?? new Image(size, size);
                case 24:
                    return _iconsPrivate24?.ImageIcon ?? new Image(size, size);
                case 32:
                    return _iconsPrivate32?.ImageIcon ?? new Image(size, size);
                case 48:
                    return _iconsPrivate48?.ImageIcon ?? new Image(size, size);
                case 128:
                    return _iconsPrivate128?.ImageIcon ?? new Image(size, size);
                default:
                    return _iconsPrivate32?.ImageIcon ?? new Image(32, 32);
            }
        }
        /// <summary>
        /// Audio Icon
        /// </summary>
        public static Image AudioIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16?.AudioIcon ?? new Image(size, size);
                case 24:
                    return _iconsPrivate24?.AudioIcon ?? new Image(size, size);
                case 32:
                    return _iconsPrivate32?.AudioIcon ?? new Image(size, size);
                case 48:
                    return _iconsPrivate48?.AudioIcon ?? new Image(size, size);
                case 128:
                    return _iconsPrivate128?.AudioIcon ?? new Image(size, size);
                default:
                    return _iconsPrivate32?.AudioIcon ?? new Image(32, 32);
            }
        }
        /// <summary>
        /// Folder Icon
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Image FolderIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.FolderIcon;
                case 24:
                    return _iconsPrivate24.FolderIcon;
                case 32:
                    return _iconsPrivate32.FolderIcon;
                case 48:
                    return _iconsPrivate48.FolderIcon;
                case 128:
                    return _iconsPrivate128.FolderIcon;
                default:
                    return _iconsPrivate32.FolderIcon;
            }
        }
        /// <summary>
        /// Taskbar Icon
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Image TaskbarIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.TaskbarIcon;
                case 24:
                    return _iconsPrivate24.TaskbarIcon;
                case 32:
                    return _iconsPrivate32.TaskbarIcon;
                case 48:
                    return _iconsPrivate48.TaskbarIcon;
                case 128:
                    return _iconsPrivate128.TaskbarIcon;
                default:
                    return _iconsPrivate32.TaskbarIcon;
            }
        }
        /// <summary>
        /// Taskbar Icon
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Image TaskbarIconOver(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.TaskbarIconOver;
                case 24:
                    return _iconsPrivate24.TaskbarIconOver;
                case 32:
                    return _iconsPrivate32.TaskbarIconOver;
                case 48:
                    return _iconsPrivate48.TaskbarIconOver;
                case 128:
                    return _iconsPrivate128.TaskbarIconOver;
                default:
                    return _iconsPrivate32.TaskbarIconOver;
            }
        }
        /// <summary>
        /// Taskbar Icon
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Image TaskbarIconDown(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.TaskbarIconDown;
                case 24:
                    return _iconsPrivate24.TaskbarIconDown;
                case 32:
                    return _iconsPrivate32.TaskbarIconDown;
                case 48:
                    return _iconsPrivate48.TaskbarIconDown;
                case 128:
                    return _iconsPrivate128.TaskbarIconDown;
                default:
                    return _iconsPrivate32.TaskbarIconDown;
            }
        }
        /// <summary>
        /// Start Icon
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public static Image StartIcon(int size) {
            switch (size) {
                case 16:
                    return _iconsPrivate16.StartIcon;
                case 24:
                    return _iconsPrivate24.StartIcon;
                case 32:
                    return _iconsPrivate32.StartIcon;
                case 48:
                    return _iconsPrivate48.StartIcon;
                case 128:
                    return _iconsPrivate128.StartIcon;
                default:
                    return _iconsPrivate32.StartIcon;
            }
        }
    }
}