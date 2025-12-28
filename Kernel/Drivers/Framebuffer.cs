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
        /// Video Memory
        /// </summary>
        public static uint* VideoMemory { get; private set; }
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

        public static void Update() {
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