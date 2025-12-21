namespace guideXOS.Kernel.Helpers {
    /// <summary>
    /// String Helper
    /// </summary>
    internal unsafe static class StringHelper {
        /// <summary>
        /// String Length
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public static int StringLength(byte* c) {
            int i = 0;
            while (c[i] != 0) i++;
            return i;
        }
    }
}