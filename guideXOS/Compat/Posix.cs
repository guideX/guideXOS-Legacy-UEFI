using System;
using guideXOS.FS;
namespace guideXOS.Compat {
    /// <summary>
    /// Posix
    /// </summary>
    internal static class Posix {
        /// <summary>
        /// Starts With Fast
        /// </summary>
        /// <param name="s"></param>
        /// <param name="ch"></param>
        /// <returns></returns>
        private static bool StartsWithFast(string s, char ch) {
            return s != null && s.Length > 0 && s[0] == ch;
        }
        /// <summary>
        /// Ends With Slash
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static bool EndsWithSlash(string s) {
            return s != null && s.Length > 0 && s[s.Length - 1] == '/';
        }
        /// <summary>
        /// Last Slash Before
        /// </summary>
        /// <param name="s"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        private static int LastSlashBefore(string s, int limit) {
            if (s == null || limit <= 0) return -1;
            if (limit > s.Length) limit = s.Length;
            for (int i = limit - 1; i >= 0; i--) {
                if (s[i] == '/') return i;
            }
            return -1;
        }
        /// <summary>
        /// Starts With String
        /// </summary>
        /// <param name="s"></param>
        /// <param name="pref"></param>
        /// <returns></returns>
        private static bool StartsWithString(string s, string pref) {
            int l = pref.Length;
            if (s == null || s.Length < l) return false;
            for (int i = 0; i < l; i++) {
                if (s[i] != pref[i]) return false;
            }
            return true;
        }
        /// <summary>
        /// Normalize separators: collapse repeated '/', remove './', handle '../' segments.
        /// </summary>
        /// <param name="cwd"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string NormalizePath(string cwd, string input) {
            if (string.IsNullOrEmpty(input)) return cwd ?? string.Empty;
            bool absolute = StartsWithFast(input, '/');
            string path = absolute ? input : CombineRelative(cwd, input);
            // Split and process components
            string[] parts = Split(path);
            int w = 0; // write index
            for (int i = 0; i < parts.Length; i++) {
                string p = parts[i];
                if (p.Length == 0 || p == ".") continue;
                if (p == "..") {
                    if (w > 0) w--;
                    continue;
                }
                parts[w++] = p;
            }
            string result = "/";
            for (int i = 0; i < w; i++) {
                result += parts[i];
                if (i != w - 1) result += "/";
            }
            return result;
        }
        /// <summary>
        /// Ensure Dir
        /// </summary>
        /// <param name="absolute"></param>
        /// <returns></returns>
        public static string EnsureDir(string absolute) {
            if (absolute == "/") return absolute;
            if (!EndsWithSlash(absolute)) return absolute + "/";
            return absolute;
        }
        /// <summary>
        /// Combine Relative
        /// </summary>
        /// <param name="cwd"></param>
        /// <param name="rel"></param>
        /// <returns></returns>
        private static string CombineRelative(string cwd, string rel) {
            if (string.IsNullOrEmpty(cwd)) return rel;
            if (!cwd.EndsWith("/")) cwd += "/";
            return cwd + rel;
        }
        private static string[] Split(string s) {
            int n = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '/') n++;
            string[] arr = new string[n + 1];
            int idx = 0, start = 0;
            for (int i = 0; i <= s.Length; i++) {
                if (i == s.Length || s[i] == '/') {
                    int len = i - start;
                    arr[idx++] = len > 0 ? s.Substring(start, len) : "";
                    start = i + 1;
                }
            }
            return arr;
        }

        // Basic wrappers for listing and reading files (no permissions yet)
        public static string[] List(string path) {
            string p = path == "/" ? "" : EnsureDir(path);
            // Strip leading '/' for underlying FS which expects relative paths
            if (p.Length > 0 && p[0] == '/') p = p.Substring(1);
            var list = File.GetFiles(p);
            if (list == null) return Array.Empty<string>();
            string[] names = new string[list.Count];
            for (int i = 0; i < list.Count; i++) {
                var fi = list[i];
                bool isDir = fi.Attribute == FileAttribute.Directory;
                names[i] = isDir ? (fi.Name + "/") : fi.Name;
                fi.Dispose();
            }
            return names;
        }
        public static byte[] ReadFile(string path) {
            return File.ReadAllBytes(path);
        }
        public static bool WriteFile(string path, byte[] data) {
            File.WriteAllBytes(path, data);
            return true;
        }

        public static bool DirectoryExists(string absolute) {
            if (absolute == "/") return true;
            string p = EnsureDir(absolute);
            int last = LastSlashBefore(p, p.Length - 1);
            string parent = last >= 0 ? (last == 0 ? "/" : p.Substring(0, last)) : "/";
            string baseName = p.Substring(last + 1, p.Length - (last + 1) - 1);
            // Strip leading '/' for FS calls
            string parentRel = parent == "/" ? "" : (parent.Length > 0 && parent[0] == '/' ? parent.Substring(1) : parent);
            var list = File.GetFiles(parentRel);
            if (list != null) {
                for (int i = 0; i < list.Count; i++) {
                    var fi = list[i];
                    bool isDir = fi.Attribute == FileAttribute.Directory;
                    if (isDir && fi.Name == baseName) {
                        DisposeAll(list);
                        return true;
                    }
                }
                for (int i = 0; i < list.Count; i++) {
                    var fi = list[i];
                    if (fi.Name.Length > baseName.Length && StartsWithString(fi.Name, baseName)) {
                        DisposeAll(list);
                        return true;
                    }
                }
                DisposeAll(list);
            }
            return false;
        }
        private static void DisposeAll(System.Collections.Generic.List<FileInfo> list) {
            for (int j = 0; j < list.Count; j++) list[j].Dispose();
        }

        // Fuzzy resolve: token without extension -> unique file whose name starts with token+".". Returns true if resolved.
        public static bool TryFuzzyResolve(string cwdAbsolute, string token, out string resolved, out string error) {
            resolved = null;
            error = null;
            if (string.IsNullOrEmpty(token) || token.IndexOf('.') >= 0) return false;
            string dir = string.IsNullOrEmpty(cwdAbsolute) ? "/" : EnsureDir(cwdAbsolute);
            string rel = dir == "/" ? "" : (dir.Length > 0 && dir[0] == '/' ? dir.Substring(1) : dir);
            var list = File.GetFiles(rel);
            if (list == null) {
                return false;
            }
            int matches = 0;
            string matchName = null;
            for (int i = 0; i < list.Count; i++) {
                var fi = list[i];
                if (fi.Attribute == FileAttribute.Directory) {
                    fi.Dispose();
                    continue;
                }
                string nm = fi.Name;
                if (nm.Length > token.Length + 1 && StartsWithString(nm, token) && nm[token.Length] == '.') {
                    matches++;
                    matchName = nm;
                }
                fi.Dispose();
            }
            list.Dispose();
            if (matches == 1) {
                resolved = (dir == "/" ? "" : dir) + matchName;
                return true;
            }
            if (matches > 1) {
                error = "error: fuzzy match";
            }
            return false;
        }
    }
}