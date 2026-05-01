namespace guideXOS.FS {
    /// <summary>
    /// Disk
    /// </summary>
    public abstract unsafe class Disk {
        /// <summary>
        /// Instance
        /// </summary>
        public static Disk Instance;
        /// <summary>
        /// Constructor
        /// </summary>
        public Disk() {
            // DO NOT set Instance here - let derived classes do it after they're fully initialized
            // This prevents TarFS from trying to read before Ramdisk.ptr is set
        }
        /// <summary>
        /// Read
        /// </summary>
        /// <param name="sector"></param>
        /// <param name="count"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public abstract bool Read(ulong sector, uint count, byte* data);
        /// <summary>
        /// Write
        /// </summary>
        /// <param name="sector"></param>
        /// <param name="count"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public abstract bool Write(ulong sector, uint count, byte* data);
    }
}