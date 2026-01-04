using System.Drawing;

namespace System.Windows.Forms {
    /// <summary>
    /// Mouse State Container - Global mouse position and button state
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION: UEFI-COMPATIBLE (Pure data storage)
    /// 
    /// This class is a simple data container with NO hardware access.
    /// It stores the current mouse state that all input drivers update.
    /// 
    /// UPDATED BY:
    /// - PS2Mouse.OnInterrupt() (Legacy BIOS)
    /// - USB.OnInterrupt() via HID.GetMouse() (USB HID)
    /// - VMwareTools.vmware_handle_mouse() (VMware only)
    /// - Kbd2Mouse.OnKeyChanged() (Keyboard emulation)
    /// 
    /// READ BY:
    /// - WindowManager for hit testing
    /// - All GUI controls for input handling
    /// - Desktop for icon clicks, drag operations
    /// 
    /// UEFI USAGE:
    /// - Same as Legacy - just update MousePosition and MouseButtons
    /// - Any new input source can write to these fields directly
    /// - No changes needed for UEFI compatibility
    /// 
    /// ============================================================================
    /// </summary>
    public class Control {
        /// <summary>
        /// Current mouse cursor position (screen coordinates)
        /// </summary>
        public static Point MousePosition;
        
        /// <summary>
        /// Currently pressed mouse buttons
        /// </summary>
        public static MouseButtons MouseButtons;
    }
}