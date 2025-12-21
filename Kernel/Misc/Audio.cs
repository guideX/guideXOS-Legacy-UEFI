using System.Runtime;
namespace guideXOS.Misc {
    /// <summary>
    /// Audio
    /// </summary>
    public static unsafe class Audio {
        /// <summary>
        /// Sample Rate
        /// </summary>
        public const int SampleRate = 44100;
        /// <summary>
        /// Has Audio Device
        /// </summary>
        public static bool HasAudioDevice;
        /// <summary>
        /// Cache Size
        /// </summary>
        public const int CacheSize = 1024 * 86;
        /// <summary>
        /// Size Per Packet
        /// </summary>
        public const int SizePerPacket = SampleRate * 2;
        /// <summary>
        /// Can Take
        /// </summary>
        public static bool CanTake;
        /// <summary>
        /// Initialize
        /// </summary>
        public static void Initialize() {
            CanTake = true;
            HasAudioDevice = false;
            cache = (byte*)Allocator.Allocate(CacheSize);
            bytesWritten = 0;
        }
        /// <summary>
        /// Cache
        /// </summary>
        public static byte* cache;
        /// <summary>
        /// Bytes Written
        /// </summary>
        public static int bytesWritten;
        /// <summary>
        /// Snd Write
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        [RuntimeExport("snd_write")]
        public static int snd_write(byte* buffer, int len) {
            CanTake = false;
            if (bytesWritten + len > CacheSize) {
                Native.Movsb(cache + bytesWritten - len, cache + bytesWritten, len);
                bytesWritten -= len;
            }
            Native.Movsb(cache + bytesWritten, buffer, len);
            bytesWritten += len;
            CanTake = true;
            return len;
        }
        /// <summary>
        /// Snd Clear
        /// </summary>
        [RuntimeExport("snd_clear")]
        public static void snd_clear() {
            bytesWritten = 0;
        }
        /// <summary>
        /// Require
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static bool require(byte* buffer) {
            if (CanTake && bytesWritten > 0) {
                int size = SizePerPacket > bytesWritten ? bytesWritten : SizePerPacket;
                Native.Movsb(buffer, cache, size);
                bytesWritten -= size;
                if (bytesWritten > SizePerPacket) {
                    Native.Movsb(cache, cache + size, bytesWritten);
                }
                return true;
            } else {
                return false;
            }
        }
    }
}