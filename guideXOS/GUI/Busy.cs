namespace guideXOS.GUI {
    /// <summary>
    /// Busy
    /// </summary>
    public static class Busy {
        /// <summary>
        /// Count
        /// </summary>
        private static int _count;
        /// <summary>
        /// Is Busy
        /// </summary>
        public static bool IsBusy { get { return _count > 0; } }
        /// <summary>
        /// Push
        /// </summary>
        public static void Push() { _count++; }
        /// <summary>
        /// Pop
        /// </summary>
        public static void Pop() { if (_count > 0) _count--; }
    }
}