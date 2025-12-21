using guideXOS.Network;
using System.Collections.Generic;

namespace guideXOS.FS {
    /// <summary>
    /// Virtual Cloud File System backed by HTTP endpoints exposed by the guideXOS.Web project.
    /// Required endpoints (implemented in CloudFsController):
    ///  - GET  {base}/v1/fs/entries?path={urlenc}&recursive=false&pageSize=1000 => { items:[{ name:string, type:"File"|"Directory" }] }
    ///  - GET  {base}/v1/fs/files/content?path={urlenc} => raw bytes
    ///  - PUT  {base}/v1/fs/files/content?path={urlenc}&createParents=true => writes raw bytes (body)
    ///  - DELETE {base}/v1/fs/entries?path={urlenc}
    /// Auth: Authorization: Bearer {LoginGuid}
    /// </summary>
    public class CloudFS : FileSystem {
        private string _host;
        private int _port;
        private string _basePath;
        private string _bearer;

        /// <summary>
        /// Create a CloudFS pointing to a baseUrl like "http://127.0.0.1:5073" or "http://host:port".
        /// bearerToken should be the LoginGuid returned by the Web service.
        /// </summary>
        public CloudFS(string baseUrl = "http://127.0.0.1:5073", string bearerToken = null) {
            ParseBase(baseUrl, out _host, out _port, out _basePath);
            _bearer = bearerToken; // GUID string
        }

        public override List<FileInfo> GetFiles(string Directory) {
            string qp = "path=" + UrlEncode(Directory ?? string.Empty) + "&recursive=false&pageSize=1000";
            var req = NewRequest("GET", Combine(_basePath, "/v1/fs/entries?" + qp));
            AddCommonHeaders(req.Headers, true);
            var resp = Http.Send(_host, _port, req, 8000);
            var list = new List<FileInfo>();
            if (resp == null || resp.StatusCode < 200 || resp.StatusCode >= 300 || resp.Body == null) return list;
            string json = BytesToAscii(resp.Body);
            var root = JsonLite.Parse(json) as JsonLite.JObject; if (root == null) return list;
            var itemsVal = JsonLite.Get(root, "items");
            var arr = itemsVal as JsonLite.JArray; if (arr == null) return list;
            for (int i = 0; i < arr.Items.Count; i++) {
                var obj = arr.Items[i] as JsonLite.JObject; if (obj == null) continue;
                string name = JsonLite.GetString(obj, "name");
                string type = JsonLite.GetString(obj, "type");
                if (name == null) continue;
                var fi = new FileInfo(); fi.Name = name; fi.Attribute = 0;
                if (type != null && EqualsIgnoreCase(type, "Directory")) fi.Attribute |= FileAttribute.Directory;
                list.Add(fi);
            }
            return list;
        }

        public override byte[] ReadAllBytes(string Name) {
            string qp = "path=" + UrlEncode(Name ?? string.Empty);
            var req = NewRequest("GET", Combine(_basePath, "/v1/fs/files/content?" + qp));
            AddCommonHeaders(req.Headers, false);
            var resp = Http.Send(_host, _port, req, 15000);
            if (resp == null || resp.StatusCode < 200 || resp.StatusCode >= 300) return new byte[0];
            return resp.Body ?? new byte[0];
        }

        public override void WriteAllBytes(string Name, byte[] Content) {
            if (Content == null) Content = new byte[0];
            string qp = "path=" + UrlEncode(Name ?? string.Empty) + "&createParents=true";
            var req = NewRequest("PUT", Combine(_basePath, "/v1/fs/files/content?" + qp));
            AddCommonHeaders(req.Headers, false);
            AddHeader(req.Headers, "Content-Type", "application/octet-stream");
            req.Body = Content;
            var resp = Http.Send(_host, _port, req, 20000);
            // ignore non-2xx like other FS drivers do
        }

        public override void Delete(string Name) {
            string qp = "path=" + UrlEncode(Name ?? string.Empty);
            var req = NewRequest("DELETE", Combine(_basePath, "/v1/fs/entries?" + qp));
            AddCommonHeaders(req.Headers, false);
            var resp = Http.Send(_host, _port, req, 8000);
            // ignore result
        }

        public override void Format() { /* no-op for cloud */ }

        // ----------------- helpers -----------------
        private static Http.Request NewRequest(string method, string path) {
            var r = new Http.Request(); r.Method = method; r.Path = path; return r;
        }

        private void AddCommonHeaders(System.Collections.Generic.List<Http.Header> headers, bool acceptJson) {
            if (acceptJson) AddHeader(headers, "Accept", "application/json");
            if (!string.IsNullOrEmpty(_bearer)) AddHeader(headers, "Authorization", "Bearer " + _bearer);
        }

        private static void AddHeader(System.Collections.Generic.List<Http.Header> list, string k, string v) {
            var h = new Http.Header(); h.Key = k; h.Value = v; list.Add(h);
        }

        private static string Combine(string a, string b) {
            if (a == null || a.Length == 0) return b;
            if (a[a.Length - 1] == '/' && b.Length > 0 && b[0] == '/') return a + b.Substring(1);
            if (a[a.Length - 1] != '/' && b.Length > 0 && b[0] != '/') return a + "/" + b;
            return a + b;
        }

        private static void CombineParseHost(string hostPort, out string host, out int port) {
            int colon = hostPort.IndexOf(':');
            if (colon >= 0) { host = hostPort.Substring(0, colon); port = ParseInt(hostPort.Substring(colon + 1)); if (port == 0) port = 80; }
            else { host = hostPort; port = 80; }
        }

        private static void ParseBase(string url, out string host, out int port, out string basePath) {
            host = "127.0.0.1"; port = 80; basePath = "/";
            if (url == null) return;
            // only http:// supported
            string work = url;
            if (HasPrefix(work, "http://")) work = work.Substring(7);
            int slash = work.IndexOf('/');
            string hostPort = slash >= 0 ? work.Substring(0, slash) : work;
            basePath = slash >= 0 ? work.Substring(slash) : "/";
            CombineParseHost(hostPort, out host, out port);
            if (basePath.Length == 0) basePath = "/";
        }

        private static string UrlEncode(string s) {
            if (s == null) return string.Empty;
            // Encode everything except unreserved [A-Za-z0-9-_.~]
            char[] buf = new char[s.Length * 3 + 8]; int n = 0;
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.' || c == '~') {
                    buf[n++] = c;
                } else {
                    byte b = (byte)(c & 0xFF);
                    buf[n++] = '%'; buf[n++] = HexNibble((b >> 4) & 0xF); buf[n++] = HexNibble(b & 0xF);
                }
            }
            return new string(buf, 0, n);
        }
        private static char HexNibble(int v) { return (char)(v < 10 ? ('0' + v) : ('A' + (v - 10))); }

        private static bool EqualsIgnoreCase(string a, string b) {
            if (a == null || b == null) return false; if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) { char ca = a[i]; if (ca >= 'A' && ca <= 'Z') ca = (char)(ca + 32); char cb = b[i]; if (cb >= 'A' && cb <= 'Z') cb = (char)(cb + 32); if (ca != cb) return false; }
            return true;
        }
        private static bool HasPrefix(string s, string prefix) { if (prefix.Length > s.Length) return false; for (int i = 0; i < prefix.Length; i++) if (s[i] != prefix[i]) return false; return true; }

        private static int ParseInt(string s) { int n = 0; for (int i = 0; i < s.Length; i++) { char c = s[i]; if (c < '0' || c > '9') break; n = n * 10 + (c - '0'); } return n; }
        private static string BytesToAscii(byte[] b) { char[] c = new char[b.Length]; for (int i = 0; i < b.Length; i++) c[i] = (char)b[i]; return new string(c); }
    }
}
