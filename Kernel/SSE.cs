using System.Runtime.InteropServices;
namespace guideXOS {
    /// <summary>
    /// SSE
    /// </summary>
    internal static class SSE {
        /// <summary>
        /// Enable SSE
        /// </summary>
        [DllImport("*")]
        public static extern void enable_sse();
    }
}