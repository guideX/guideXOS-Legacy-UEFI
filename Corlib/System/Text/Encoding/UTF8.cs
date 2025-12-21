namespace System.Text.Encoding {
    /// <summary>
    /// UTF8
    /// </summary>
    public static class UTF8 {
        /// <summary>
        /// Convert Bytes to String
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string GetString(this byte[] bytes) {
            char[] chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
                chars[i] = (char)bytes[i];
            return new string(chars);
        }
        /// <summary>
        /// Convert String to Bytes Array
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static byte[] GetBytes(string str) {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }
    }
}