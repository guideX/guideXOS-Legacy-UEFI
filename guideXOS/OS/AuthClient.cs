using System;
using guideXOS.Network;

namespace guideXOS.OS {
    /// <summary>
    /// Provides authentication services for user login and registration.
    /// Communicates with the guideXOS.Web authentication API.
    /// </summary>
    internal static class AuthClient {
        private static string JsonEscape(string s) { if (s == null) return "\"\""; char[] buf = new char[s.Length * 2 + 2]; int j = 0; buf[j++] = '"'; for (int i = 0; i < s.Length; i++) { char c = s[i]; switch (c) { case '"': buf[j++] = '\\'; buf[j++] = '"'; break; case '\\': buf[j++] = '\\'; buf[j++] = '\\'; break; case '\n': buf[j++] = '\\'; buf[j++] = 'n'; break; case '\r': buf[j++] = '\\'; buf[j++] = 'r'; break; case '\t': buf[j++] = '\\'; buf[j++] = 't'; break; default: buf[j++] = c; break; } } buf[j++] = '"'; return new string(buf, 0, j); }
        private static string ParseGuidFromJson(string json) {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            string key = "\"LoginGuid\"";
            int i = IndexOfIgnoreCase(json, key);
            if (i < 0) { key = "\"loginGuid\""; i = IndexOfIgnoreCase(json, key); }
            if (i < 0) return string.Empty;
            int colon = IndexOfFrom(json, ':', i);
            if (colon < 0) return string.Empty;
            int q1 = IndexOfFrom(json, '"', colon + 1); if (q1 < 0) return string.Empty;
            int q2 = IndexOfFrom(json, '"', q1 + 1); if (q2 < 0) return string.Empty;
            return json.Substring(q1 + 1, q2 - (q1 + 1));
        }
        private static int IndexOfIgnoreCase(string s, string needle) { int n = needle.Length; for (int i = 0; i + n <= s.Length; i++) { bool ok = true; for (int j = 0; j < n; j++) { char ca = s[i + j]; char cb = needle[j]; if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32); if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32); if (ca != cb) { ok = false; break; } } if (ok) return i; } return -1; }
        private static int IndexOfFrom(string s, char ch, int start) { for (int i = start; i < s.Length; i++) { if (s[i] == ch) return i; } return -1; }

        /// <summary>
        /// Attempts to authenticate a user with the provided credentials.
        /// </summary>
        /// <param name="username">The username for login.</param>
        /// <param name="password">The password for login.</param>
        /// <param name="loginToken">When successful, contains the authentication token (GUID).</param>
        /// <param name="message">When unsuccessful, contains the error message.</param>
        /// <returns>True if login was successful, otherwise false.</returns>
        public static bool TryLogin(string username, string password, out string loginToken, out string message) {
            loginToken = string.Empty; message = null;
            string body = "{" + "\"username\":" + JsonEscape(username) + ",\"password\":" + JsonEscape(password) + "}";
            string url = Session.ServiceBaseUrl + "/v1/auth/login";
            string resp = Http.PostJson(url, body, 8000);
            if (string.IsNullOrEmpty(resp)) { message = "No response"; return false; }
            loginToken = ParseGuidFromJson(resp);
            if (string.IsNullOrEmpty(loginToken)) { message = "Login unsuccessful"; return false; }
            return true;
        }

        /// <summary>
        /// Attempts to register a new user account with the provided credentials.
        /// </summary>
        /// <param name="username">The desired username.</param>
        /// <param name="password">The desired password.</param>
        /// <param name="message">When unsuccessful, contains the error message.</param>
        /// <returns>True if registration was successful, otherwise false.</returns>
        public static bool TryRegister(string username, string password, out string message) {
            message = null;
            string body = "{" + "\"username\":" + JsonEscape(username) + ",\"password\":" + JsonEscape(password) + "}";
            string url = Session.ServiceBaseUrl + "/v1/auth/register";
            string resp = Http.PostJson(url, body, 8000);
            if (string.IsNullOrEmpty(resp)) { message = "No response"; return false; }
            var g = ParseGuidFromJson(resp);
            bool ok = !string.IsNullOrEmpty(g) || IndexOfIgnoreCase(resp, "\"Success\":true") >= 0 || IndexOfIgnoreCase(resp, "\"success\":true") >= 0;
            if (!ok) { message = "Registration failed"; return false; }
            return true;
        }
    }
}