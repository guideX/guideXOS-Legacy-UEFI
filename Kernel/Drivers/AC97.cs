using guideXOS.Kernel.Structs;
using guideXOS.Misc;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// AC'97 (Audio Codec '97; also MC'97 for Modem Codec '97) is an audio codec standard developed by Intel Architecture Labs and various codec manufacturers in 1997. The standard was used in motherboards, modems, and sound cards.
    /// </summary>
    public static unsafe class AC97 {
        /// <summary>
        /// Device Located
        /// </summary>
        public static bool DeviceLocated {
            get {
                return _deviceLocated;
            }
        }
        /// <summary>
        /// Device Name
        /// </summary>
        public static string DeviceName {
            get {
                return _deviceName;
            }
        }
        /// <summary>
        /// Device Located
        /// </summary>
        private static bool _deviceLocated;
        /// <summary>
        /// Device Name
        /// </summary>
        private static string _deviceName;
        /// <summary>
        /// NAM, NABM
        /// </summary>
        private static uint NAM, NABM;
        /// <summary>
        /// Buffer Descriptors
        /// </summary>
        private static BufferDescriptor* _bufferDescriptors;
        /// <summary>
        /// Descriptor Count
        /// </summary>
        private static int _descriptorCount;
        /// <summary>
        /// Index
        /// </summary>
        private static byte _index = 0;
        /// <summary>
        /// Initialize
        /// </summary>
        public static unsafe void Initialize() {
            var device = PCI.GetDevice(0x8086, 0x2415);
            if (device == null) return;
            _deviceLocated = true;
            _deviceName = "[AC97] Intel 82801AA AC97 Audio Controller";
            device.WriteRegister(0x04, 0x04 | 0x02 | 0x01);
            _descriptorCount = 31;
            NAM = device.Bar0 & ~(0xFU);
            NABM = device.Bar1 & ~(0xFU);
            Native.Out8((ushort)(NABM + 0x2C), 0x2);
            Native.Out32((ushort)(NAM + 0x00), 0x6166696E);
            Native.Out16((ushort)(NAM + 0x02), 0x0F0F);
            Native.Out16((ushort)(NAM + 0x018), 0x0F0F);
            _bufferDescriptors = (BufferDescriptor*)Allocator.Allocate((ulong)(sizeof(BufferDescriptor) * 32));
            for (int i = 0; i < _descriptorCount; i++) {
                ulong ptr = (ulong)Allocator.Allocate(Audio.SizePerPacket);
                if (ptr > 0xFFFFFFFF) Panic.Error("Invalid buf");
                _bufferDescriptors[i].Address = (uint)ptr;
                _bufferDescriptors[i].Size = Audio.SizePerPacket / 2;
                _bufferDescriptors[i].Arribute = 1 << 15;
            }
            Interrupts.EnableInterrupt(device.IRQ, &OnInterrupt);
            _index = 0;
            Native.Out16((ushort)(NAM + 0x2C), Audio.SampleRate);
            Native.Out16((ushort)(NAM + 0x32), Audio.SampleRate);
            Native.Out8((ushort)(NABM + 0x1B), 0x02);
            Native.Out32((ushort)(NABM + 0x10), (uint)_bufferDescriptors);
            Native.Out8((ushort)(NABM + 0x15), _index);
            Native.Out8((ushort)(NABM + 0x1B), 0x19);
            Audio.HasAudioDevice = true;
            Console.WriteLine("[AC97] Audio Device Initialized");
        }
        /// <summary>
        /// On Interupt
        /// </summary>
        public static void OnInterrupt() {
            ushort Status = Native.In16((ushort)(NABM + 0x16));
            if ((Status & (1 << 3)) != 0) {
                int LastIndex = _index;
                Native.Stosb((void*)_bufferDescriptors[_index].Address, 0, Audio.SampleRate * 2);
                _index++;
                _index %= (byte)_descriptorCount;
                Audio.require((byte*)_bufferDescriptors[_index].Address);
                Native.Out8((ushort)(NABM + 0x15), _index);
            }
            Native.Out16((ushort)(NABM + 0x16), 0x1C);
        }
    }
}