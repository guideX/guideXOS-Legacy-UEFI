using System.Drawing;

namespace guideXOS.GUI.Base {
    /// <summary>
    /// Window Base
    /// </summary>
    public abstract class WindowBase {
        /// <summary>
        /// Is Resizable
        /// </summary>
        public bool IsResizable = true;
        /// <summary>
        /// Visible
        /// </summary>
        public bool _visible;

        /// <summary>
        /// Controls whether this window shows in the taskbar
        /// </summary>
        public bool ShowInTaskbar = true;
        /// <summary>
        /// Controls visibility of Minimize/Maximize buttons
        /// </summary>
        public bool ShowMinimize = true;
        /// <summary>
        /// Show Maximize/Restore button (will automatically switch based on state)
        /// </summary>
        public bool ShowMaximize = true;
        /// <summary>
        /// Show Tombstone Button
        /// </summary>
        public bool ShowTombstone = true;
        /// <summary>
        /// Bar Height
        /// </summary>
        public int BarHeight = 40;
        /// <summary>
        /// Title
        /// </summary>
        public string Title;
        /// <summary>
        /// Show In Start Menu
        /// </summary>
        public bool ShowInStartMenu = true;
        /// <summary>
        /// Taskbar Icon
        /// </summary>
        public Image TaskbarIcon;
        /// <summary>
        /// Variables
        /// </summary>
        public int X, Y, Width, Height;
    }
}