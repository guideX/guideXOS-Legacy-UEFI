using guideXOS.Graph;
using System.Windows.Forms;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// A framebuffer (frame buffer, or sometimes framestore) is a portion of random-access memory (RAM) containing a bitmap that drives a video display. It is a memory buffer containing data representing all the pixels in a complete video frame. Modern video cards contain framebuffer circuitry in their cores
    /// </summary>
    public static unsafe class Framebuffer {
        /// <summary>
        /// Width
        /// </summary>
        public static ushort Width;
        /// <summary>
        /// Height
        /// </summary>
        public static ushort Height;
        /// <summary>
        /// Video Memory - stored as a plain static field (NOT an auto-property)
        /// so the raw pointer value survives UEFI managed reference corruption.
        /// </summary>
        public static uint* VideoMemory;
        /// <summary>
        /// Original framebuffer physical address set during Initialize().
        /// This NEVER changes and is used by EnsureGraphics() to restore
        /// VideoMemory if it gets corrupted in UEFI mode.
        /// </summary>
        public static uint* OriginalVideoMemory;
        /// <summary>
        /// First Buffer
        /// </summary>
        public static uint* FirstBuffer;
        /// <summary>
        /// Second Buffer
        /// </summary>
        public static uint* SecondBuffer;
        /// <summary>
        /// Graphics
        /// </summary>
        public static Graphics Graphics;
        /// <summary>
        /// Triple Buffered
        /// </summary>
        static bool _TripleBuffered = false;
        /// <summary>
        /// Since you enabled TripleBuffered you have to call Framebuffer.Graphics.Update() in order to make it display
        /// </summary>
        public static bool TripleBuffered {
            get {
                return _TripleBuffered;
            }
            set {
                if (Graphics == null) return;
                if (VideoMemory == null) return;
                if (Width == 0 || Height == 0) return;
                if (_TripleBuffered == value) return;

                Graphics.VideoMemory = value ? FirstBuffer : VideoMemory;
                if (Graphics.VideoMemory == null) return;
                Graphics.Clear(0x0);
                _TripleBuffered = value;
                if (!_TripleBuffered) {
                    Console.Clear();
                }
            }
        }

        /// <summary>
        /// Ensures the Graphics object exists and points at the real framebuffer.
        /// In UEFI mode, static managed references (like Graphics) can be zeroed
        /// and VideoMemory can be corrupted to a heap address. This method uses
        /// OriginalVideoMemory (set once during Initialize) as the authoritative
        /// framebuffer address.
        /// </summary>
        public static void EnsureGraphics() {
            // STEP 1: If OriginalVideoMemory is set, always trust it over VideoMemory.
            // VideoMemory can get corrupted in UEFI mode; OriginalVideoMemory never changes.
            if ((ulong)OriginalVideoMemory != 0) {
                if ((ulong)VideoMemory != (ulong)OriginalVideoMemory) {
                    VideoMemory = OriginalVideoMemory;
                }
            }
            // STEP 2: If Graphics survived and already points at VideoMemory, we're done.
            if (Graphics != null && (ulong)Graphics.VideoMemory == (ulong)VideoMemory) return;
            // STEP 3: If Graphics exists but points elsewhere, fix it.
            if (Graphics != null && (ulong)VideoMemory != 0) {
                Graphics.VideoMemory = VideoMemory;
                return;
            }
            // STEP 4: Recreate Graphics from scratch.
            if ((ulong)VideoMemory == 0 || Width == 0 || Height == 0) return;
            Graphics = new Graphics(Width, Height, VideoMemory);
        }

        public static void Update() {
            if (BootConsole.CurrentMode == guideXOS.BootMode.UEFI) {
                return;
            }
            if (TripleBuffered) {
                for (int i = 0; i < Width * Height; i++) {
                    if (FirstBuffer[i] != SecondBuffer[i]) {
                        VideoMemory[i] = FirstBuffer[i];
                    }
                }
                Native.Movsd(SecondBuffer, FirstBuffer, (ulong)(Width * Height));
            }
            //if (Graphics != null) Graphics?.Update();
            Graphics?.Update();
        }

        public static void Initialize(ushort XRes, ushort YRes, uint* FB) {
            Width = XRes;
            Height = YRes;
            VideoMemory = FB; // Video memory must be set before any operation that might clear/draw
            OriginalVideoMemory = FB; // Permanent backup - NEVER overwritten
            FirstBuffer = (uint*)Allocator.Allocate((ulong)(XRes * YRes * 4));
            SecondBuffer = (uint*)Allocator.Allocate((ulong)(XRes * YRes * 4));
            Native.Stosd(FirstBuffer, 0, (ulong)(XRes * YRes));
            Native.Stosd(SecondBuffer, 0, (ulong)(XRes * YRes));
            Control.MousePosition.X = XRes / 2;
            Control.MousePosition.Y = YRes / 2;
            Graphics = new Graphics(Width, Height, FB); // Ensure Graphics is created ONLY here
            Console.Clear();
        }
    }
}
