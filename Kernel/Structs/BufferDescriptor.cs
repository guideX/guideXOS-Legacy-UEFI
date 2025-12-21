using System.Runtime.InteropServices;
namespace guideXOS.Kernel.Structs {
    /// <summary>
    /// Buffer Descriptor
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BufferDescriptor {
        /// <summary>
        /// Address
        /// </summary>
        public uint Address;
        /// <summary>
        /// Size
        /// </summary>
        public ushort Size;
        /// <summary>
        /// Attribute
        /// </summary>
        public ushort Arribute;
    }
}