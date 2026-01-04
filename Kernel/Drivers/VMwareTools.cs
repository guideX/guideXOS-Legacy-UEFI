using guideXOS.Misc;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// VMware Backdoor Interface for absolute mouse positioning
    /// 
    /// ============================================================================
    /// UEFI COMPATIBILITY AUDIT
    /// ============================================================================
    /// 
    /// CLASSIFICATION SUMMARY:
    /// -----------------------
    /// 1. BIOS-ERA ONLY:
    ///    - Uses VMware-specific I/O port backdoor (port 0x5658/0x5659)
    ///    - Only works inside VMware virtual machines
    ///    - Detection can crash on non-VMware hypervisors
    /// 
    /// 2. UEFI-INCOMPATIBLE:
    ///    - is_vmware_backdoor(): Port I/O can hang or crash on QEMU/KVM
    ///    - vmware_send(): Native assembly that uses IN/OUT on magic port
    ///    - Currently DISABLED in Initialize() due to QEMU crashes
    ///    
    ///    WHY: The VMware backdoor is hypervisor-specific:
    ///    - QEMU doesn't implement it (crashes when accessed)
    ///    - Hyper-V doesn't implement it
    ///    - Only VMware products support this interface
    ///    - UEFI boot mode doesn't change this limitation
    /// 
    /// 3. POTENTIALLY REUSABLE LOGIC:
    ///    - Absolute pointer concept: Mouse position as 0-65535 range
    ///    - Coordinate scaling: x/65536f * Framebuffer.Width
    ///    - Button bit extraction: BitHelpers.IsBitSet pattern
    ///    - Control.MousePosition/MouseButtons update pattern
    /// 
    /// RECOMMENDED UEFI APPROACH:
    /// --------------------------
    /// 1. For VMware UEFI: Could still work, but detection needs guard
    /// 2. For QEMU: Skip entirely - use USB HID or virtio-input
    /// 3. For real hardware: Not applicable (no VMware backdoor)
    /// 
    /// Consider:
    /// - VirtIO input device for QEMU (cross-hypervisor compatible)
    /// - EFI_ABSOLUTE_POINTER_PROTOCOL from UEFI spec
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class VMwareTools {
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct vmware_cmd {
            [FieldOffset(0)]
            public uint ax;
            [FieldOffset(0)]
            public uint magic;

            [FieldOffset(4)]
            public uint bx;
            [FieldOffset(4)]
            public uint size;

            [FieldOffset(8)]
            public uint cx;
            [FieldOffset(8)]
            public ushort command;

            [FieldOffset(12)]
            public uint dx;
            [FieldOffset(12)]
            public ushort port;

            [FieldOffset(16)]
            public uint si;
            [FieldOffset(20)]
            public uint di;
        }

        const uint VMWARE_MAGIC = 0x564D5868;
        const ushort CMD_GETVERSION = 10;

        const ushort CMD_ABSPOINTER_DATA = 39;
        const ushort CMD_ABSPOINTER_STATUS = 40;
        const ushort CMD_ABSPOINTER_COMMAND = 41;

        const uint ABSPOINTER_ENABLE = 0x45414552;/* Q E A E */
        const ushort ABSPOINTER_RELATIVE = 0xF5;
        const uint ABSPOINTER_ABSOLUTE = 0x53424152; /* R A B S */

        public static bool Available = false;

        /// <summary>
        /// Initialize VMware backdoor interface
        /// 
        /// UEFI-INCOMPATIBLE: Currently DISABLED because:
        /// 1. vmware_send() crashes on non-VMware hypervisors (QEMU, KVM)
        /// 2. No safe way to detect VMware before calling backdoor
        /// 3. UEFI mode doesn't change this fundamental limitation
        /// 
        /// The backdoor uses magic I/O port 0x5658 which only VMware implements.
        /// On other hypervisors, this triggers #GP or undefined behavior.
        /// </summary>
        public static void Initialize() {
            // Debug: entering VMwareTools.Initialize
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'v');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'m');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'1');
            
            // SKIP VMware detection - it causes crashes in QEMU
            // The vmware_send() uses special port I/O that can crash on non-VMware platforms
            // TODO: Re-enable when running on actual VMware
            Available = false;
            
            // Debug: skipping VMware detection
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'v');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'m');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'S');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'K');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'I');
            while ((Native.In8(0x3FD) & 0x20) == 0) { }
            Native.Out8(0x3F8, (byte)'P');
        }

        /// <summary>
        /// Handle VMware absolute mouse pointer
        /// 
        /// UEFI-INCOMPATIBLE: Requires VMware backdoor to be active.
        /// 
        /// POTENTIALLY REUSABLE: The absolute pointer handling pattern:
        /// - Read status first to check for errors
        /// - Read data packet with button state and position
        /// - Scale 0-65535 range to screen resolution
        /// - Update Control.MousePosition/MouseButtons
        /// 
        /// This pattern could be adapted for EFI_ABSOLUTE_POINTER_PROTOCOL.
        /// </summary>
        public static void vmware_handle_mouse() {
            vmware_cmd cmd;
            /* Read the mouse status */
            cmd.bx = 0;
            cmd.command = CMD_ABSPOINTER_STATUS;
            vmware_send(&cmd);

            /* Mouse status is in EAX */
            if (cmd.ax == 0xFFFF0000) {
                /* An error has occured, let's turn the device off and back on */
                //mouse_off();
                mouse_absolute();
                return;
            }

            /* The status command returns a size we need to read, should be at least 4. */
            if ((cmd.ax & 0xFFFF) < 4) return;

            /* Read 4 bytes of mouse data */
            cmd.bx = 4;
            cmd.command = CMD_ABSPOINTER_DATA;
            vmware_send(&cmd);

            /* Mouse data is now stored in AX, BX, CX, and DX */
            //uint flags = (cmd.ax & 0xFFFF0000) >> 16; /* Not important */
            _ = (cmd.ax & 0xFFFF0000) >> 16; /* Not important */
            uint buttons = cmd.ax & 0xFFFF; /* 0x10 = Right, 0x20 = Left, 0x08 = Middle */
            uint x = cmd.bx; /* Both X and Y are scaled from 0 to 0xFFFF */
            uint y = cmd.cx; /* You should map these somewhere to the actual resolution. */
            //byte z = (byte)cmd.dx; /* Z is a single signed byte indicating scroll direction. */
            _ = (byte)cmd.dx; /* Z is a single signed byte indicating scroll direction. */

            Control.MouseButtons = MouseButtons.None;
            if (BitHelpers.IsBitSet(buttons, 3)) Control.MouseButtons |= MouseButtons.Middle;
            if (BitHelpers.IsBitSet(buttons, 4)) Control.MouseButtons |= MouseButtons.Right;
            if (BitHelpers.IsBitSet(buttons, 5)) Control.MouseButtons |= MouseButtons.Left;
            //Control.MousePosition.X = (int)(((x * 100) / 65536) * (Framebuffer.Width / 100));
            //Control.MousePosition.Y = (int)(((y * 100) / 65536) * (Framebuffer.Height / 100));
            Control.MousePosition.X = (int)(x / 65536f * Framebuffer.Width);
            Control.MousePosition.Y = (int)(y / 65536f * Framebuffer.Height);

            /* TODO: Do something useful here with these values, such as providing them to userspace! */
        }

        public static void mouse_relative() {
            vmware_cmd cmd;
            cmd.bx = ABSPOINTER_RELATIVE;
            cmd.command = CMD_ABSPOINTER_COMMAND;
            vmware_send(&cmd);
        }

        public static void mouse_absolute() {
            vmware_cmd cmd;

            /* Enable */
            cmd.bx = ABSPOINTER_ENABLE;
            cmd.command = CMD_ABSPOINTER_COMMAND;
            vmware_send(&cmd);

            /* Status */
            cmd.bx = 0;
            cmd.command = CMD_ABSPOINTER_STATUS;
            vmware_send(&cmd);

            /* Read data (1) */
            cmd.bx = 1;
            cmd.command = CMD_ABSPOINTER_DATA;
            vmware_send(&cmd);

            /* Enable absolute */
            cmd.bx = ABSPOINTER_ABSOLUTE;
            cmd.command = CMD_ABSPOINTER_COMMAND;
            vmware_send(&cmd);
        }

        /// <summary>
        /// Check if VMware backdoor is available
        /// 
        /// UEFI-INCOMPATIBLE: This function itself can crash on non-VMware!
        /// The IN instruction to port 0x5658 with magic value can cause:
        /// - #GP exception on QEMU/KVM without VMware compat
        /// - Undefined behavior on real hardware
        /// - System hang on some hypervisors
        /// 
        /// CRITICAL: Do NOT call this on non-VMware systems.
        /// Currently, Initialize() skips this entirely.
        /// </summary>
        public static bool is_vmware_backdoor() {
            vmware_cmd cmd;
            cmd.bx = ~VMWARE_MAGIC;
            cmd.command = CMD_GETVERSION;
            vmware_send(&cmd);

            if (cmd.bx != VMWARE_MAGIC || cmd.ax == 0xFFFFFFFF) {
                /* Not a backdoor! */
                return false;
            }

            return true;
        }
        // UEFI-INCOMPATIBLE: Native assembly functions that use VMware-specific ports
        // These will crash or hang on non-VMware hypervisors
        [DllImport("*")]
        public static extern void vmware_send(vmware_cmd* cmd);  // Port 0x5658 - BIOS-ERA ONLY

        [DllImport("*")]
        public static extern void vmware_send_hb(vmware_cmd* cmd);  // High-bandwidth backdoor

        [DllImport("*")]
        public static extern void vmware_get_hb(vmware_cmd* cmd);  // High-bandwidth read
        
        // ============================================================================
        // UEFI MIGRATION NOTES
        // ============================================================================
        // 
        // For UEFI mouse support, consider these alternatives:
        // 
        // 1. USB HID Mouse (RECOMMENDED):
        //    - Already implemented in HID.cs
        //    - Works on all platforms with USB support
        //    - No hypervisor-specific code needed
        // 
        // 2. EFI_SIMPLE_POINTER_PROTOCOL:
        //    - Provides relative X/Y deltas
        //    - Poll from bootloader or via runtime service
        // 
        // 3. EFI_ABSOLUTE_POINTER_PROTOCOL:
        //    - Similar to VMware absolute pointer concept
        //    - Position as 0 to Resolution range
        //    - Could reuse coordinate scaling logic from vmware_handle_mouse()
        // 
        // 4. VirtIO Input (QEMU-specific):
        //    - Modern alternative to PS/2 emulation
        //    - Requires PCI device driver
        // 
        // ============================================================================
    }
}