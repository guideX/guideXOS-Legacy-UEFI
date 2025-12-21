using System;

namespace guideXOS.OS {
    /// <summary>
    /// Manages user session state including authentication tokens and web service configuration.
    /// </summary>
    internal static class Session {
        /// <summary>
        /// Gets or sets the base URL of the guideXOS.Web service.
        /// Adjust if your web server runs on a different port.
        /// </summary>
        public static string ServiceBaseUrl = "http://localhost:5068/";
        
        /// <summary>
        /// Gets or sets the current authenticated login token (GUID string).
        /// Empty if not logged in.
        /// </summary>
        public static string LoginToken = string.Empty;
    }
}
