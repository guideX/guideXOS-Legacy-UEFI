using System.Runtime.InteropServices;

namespace guideXOS.Kernel.Drivers.Input.Uefi {
    /// <summary>
    /// EFI_SIMPLE_POINTER_PROTOCOL GUID
    /// {31878C87-0B75-11D5-9A4F-0090273FC14D}
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EfiSimplePointerProtocolGuid {
        public static readonly EfiGuid Value = new EfiGuid(
            0x31878C87,
            0x0B75,
            0x11D5,
            0x9A, 0x4F, 0x00, 0x90, 0x27, 0x3F, 0xC1, 0x4D
        );
    }

    /// <summary>
    /// EFI GUID structure (128-bit identifier)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EfiGuid {
        public uint Data1;
        public ushort Data2;
        public ushort Data3;
        public byte Data4_0;
        public byte Data4_1;
        public byte Data4_2;
        public byte Data4_3;
        public byte Data4_4;
        public byte Data4_5;
        public byte Data4_6;
        public byte Data4_7;

        public EfiGuid(uint d1, ushort d2, ushort d3, 
                       byte d4_0, byte d4_1, byte d4_2, byte d4_3,
                       byte d4_4, byte d4_5, byte d4_6, byte d4_7) {
            Data1 = d1;
            Data2 = d2;
            Data3 = d3;
            Data4_0 = d4_0;
            Data4_1 = d4_1;
            Data4_2 = d4_2;
            Data4_3 = d4_3;
            Data4_4 = d4_4;
            Data4_5 = d4_5;
            Data4_6 = d4_6;
            Data4_7 = d4_7;
        }
    }

    /// <summary>
    /// EFI_SIMPLE_POINTER_STATE - Current pointer position and button state
    /// Returned by EFI_SIMPLE_POINTER_PROTOCOL.GetState()
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EfiSimplePointerState {
        /// <summary>
        /// Relative movement along X axis (signed)
        /// Units are defined by Mode.ResolutionX (counts per mm)
        /// </summary>
        public int RelativeMovementX;

        /// <summary>
        /// Relative movement along Y axis (signed)
        /// Units are defined by Mode.ResolutionY (counts per mm)
        /// </summary>
        public int RelativeMovementY;

        /// <summary>
        /// Relative movement along Z axis (scroll wheel, signed)
        /// Units are defined by Mode.ResolutionZ (counts per mm)
        /// </summary>
        public int RelativeMovementZ;

        /// <summary>
        /// Left button pressed (TRUE = pressed)
        /// </summary>
        public byte LeftButton;   // BOOLEAN in EFI

        /// <summary>
        /// Right button pressed (TRUE = pressed)
        /// </summary>
        public byte RightButton;  // BOOLEAN in EFI

        // Padding to align structure
        public ushort _padding;
    }

    /// <summary>
    /// EFI_SIMPLE_POINTER_MODE - Device capabilities
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EfiSimplePointerMode {
        /// <summary>
        /// Resolution of X axis (counts per mm)
        /// If 0, resolution is unknown
        /// </summary>
        public ulong ResolutionX;

        /// <summary>
        /// Resolution of Y axis (counts per mm)
        /// If 0, resolution is unknown
        /// </summary>
        public ulong ResolutionY;

        /// <summary>
        /// Resolution of Z axis (counts per mm)
        /// If 0, device has no Z axis
        /// </summary>
        public ulong ResolutionZ;

        /// <summary>
        /// TRUE if left button is supported
        /// </summary>
        public byte LeftButton;   // BOOLEAN

        /// <summary>
        /// TRUE if right button is supported
        /// </summary>
        public byte RightButton;  // BOOLEAN

        // Padding
        public ushort _padding;
        public uint _padding2;
    }

    /// <summary>
    /// EFI_SIMPLE_POINTER_PROTOCOL structure
    /// Used for relative pointer devices (mouse, touchpad)
    /// 
    /// ============================================================================
    /// UEFI LIFECYCLE NOTE
    /// ============================================================================
    /// 
    /// This protocol is ONLY available BEFORE ExitBootServices is called.
    /// After ExitBootServices:
    /// - The protocol pointer becomes invalid
    /// - Calling any function will cause undefined behavior
    /// - Kernel must switch to USB HID or other native drivers
    /// 
    /// ============================================================================
    /// FUNCTION POINTER NOTES
    /// ============================================================================
    /// 
    /// UEFI uses the Microsoft x64 calling convention (EFIAPI = __cdecl on x64).
    /// We store raw function pointers and cast them when calling.
    /// This avoids issues with .NET's unmanaged function pointer syntax.
    /// 
    /// ============================================================================
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EfiSimplePointerProtocol {
        /// <summary>
        /// Reset - Resets the pointer device
        /// EFI_STATUS (EFIAPI *Reset)(THIS, BOOLEAN ExtendedVerification)
        /// Stored as void* to avoid calling convention issues
        /// </summary>
        public void* Reset;

        /// <summary>
        /// GetState - Retrieves current pointer state
        /// EFI_STATUS (EFIAPI *GetState)(THIS, EFI_SIMPLE_POINTER_STATE *State)
        /// Returns EFI_SUCCESS if state changed since last call
        /// Returns EFI_NOT_READY if no state change
        /// Stored as void* to avoid calling convention issues
        /// </summary>
        public void* GetState;

        /// <summary>
        /// WaitForInput - Event to wait for input
        /// Not used in polling mode
        /// </summary>
        public void* WaitForInput;

        /// <summary>
        /// Mode - Pointer to device mode information
        /// </summary>
        public EfiSimplePointerMode* Mode;

        /// <summary>
        /// Call the Reset function
        /// </summary>
        /// <param name="extendedVerification">TRUE for extended verification</param>
        /// <returns>EFI status code</returns>
        public ulong CallReset(byte extendedVerification) {
            if (Reset == null) return EfiStatus.InvalidParameter;
            
            // Cast to function pointer and call
            // Using delegate* syntax compatible with this runtime
            fixed (EfiSimplePointerProtocol* self = &this) {
                var fn = (delegate*<EfiSimplePointerProtocol*, byte, ulong>)Reset;
                return fn(self, extendedVerification);
            }
        }

        /// <summary>
        /// Call the GetState function
        /// </summary>
        /// <param name="state">Output state structure</param>
        /// <returns>EFI status code</returns>
        public ulong CallGetState(EfiSimplePointerState* state) {
            if (GetState == null) return EfiStatus.InvalidParameter;
            
            // Cast to function pointer and call
            fixed (EfiSimplePointerProtocol* self = &this) {
                var fn = (delegate*<EfiSimplePointerProtocol*, EfiSimplePointerState*, ulong>)GetState;
                return fn(self, state);
            }
        }
    }

    /// <summary>
    /// EFI Status codes relevant to pointer protocol
    /// </summary>
    public static class EfiStatus {
        public const ulong Success = 0;
        public const ulong NotReady = 0x8000000000000006;  // No new state available
        public const ulong DeviceError = 0x8000000000000007;
        public const ulong InvalidParameter = 0x8000000000000002;

        public static bool IsError(ulong status) => (status & 0x8000000000000000) != 0;
        public static bool IsSuccess(ulong status) => status == Success;
        public static bool IsNotReady(ulong status) => status == NotReady;
    }
}
