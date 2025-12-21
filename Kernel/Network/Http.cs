using guideXOS;
using guideXOS.Kernel.Drivers;
using guideXOS.Security;
using System.Collections.Generic;

namespace guideXOS.Network {
    // Minimal HTTP/1.1 client for simple WCF endpoints (http only).
    // Provides helpers for JSON + AES-CTR encryption.
    internal static unsafe class Http {
        private static ushort _nextLocalPort = 40000;
        private static ushort NextEphemeralPort() {
            if (_nextLocalPort < 40000 || _nextLocalPort > 55000) _nextLocalPort = 40000;
            return _nextLocalPort++;
        }

        public class Header { public string Key; public string Value; }

        public class Request {
            public string Method = "GET";
            public string Path = "/";
            public List<Header> Headers = new List<Header>();
            public byte[] Body = new byte[0];
        }

        public class Response {
            public int StatusCode; // -1 on transport error
            public string ReasonPhrase;
            public List<Header> Headers = new List<Header>();
            public byte[] Body = new byte[0];
        }

        // High level: send encrypted JSON payload using AES-128 CTR with 16-byte nonce, returns decrypted JSON string
        public static string PostEncryptedJson(string url, string json, byte[] key16, byte[] nonce16, int timeoutMs = 7000) {
            byte[] plain = ToAsciiBytes(json);
            byte[] cipher = AES128.CTR(key16, nonce16, plain);
            // You can wrap into a simple envelope {"nonce":"...","data":"..."} if needed; for now raw bytes as body
            var req = BuildPost(url, cipher, "application/octet-stream");
            if (req.request == null || string.IsNullOrEmpty(req.host)) return string.Empty;
            var resp = Send(req.host, req.port, req.request, timeoutMs);
            byte[] decrypted = AES128.CTR(key16, nonce16, resp.Body);
            return BytesToAscii(decrypted);
        }

        // High level: send JSON (cleartext)
        public static string PostJson(string url, string json, int timeoutMs = 7000) {
            byte[] body = ToAsciiBytes(json);
            var req = BuildPost(url, body, "application/json");
            // Guard against invalid/unsupported URLs to avoid null deref
            if (req.request == null || string.IsNullOrEmpty(req.host)) return string.Empty;
            var resp = Send(req.host, req.port, req.request, timeoutMs);
            return BytesToAscii(resp.Body);
        }

        private struct BuiltReq { public string host; public int port; public Request request; }
        private static BuiltReq BuildPost(string url, byte[] body, string contentType){
            if (!HasPrefix(url, "http://")) return default;
            string work = url.Substring(7);
            string hostPort; string path;
            int slash = work.IndexOf('/');
            if (slash >= 0) { hostPort = work.Substring(0, slash); path = work.Substring(slash); } else { hostPort = work; path = "/"; }
            // collapse duplicate leading slash in path (e.g., "//v1/auth")
            if (path.Length > 1 && path[0] == '/' && path[1] == '/') path = path.Substring(1);
            string host = hostPort; int port = 80; int colon = hostPort.IndexOf(':'); if (colon >= 0) { host = hostPort.Substring(0, colon); port = ParseInt(hostPort.Substring(colon + 1)); if (port == 0) port = 80; }
            var req = new Request(); req.Method = "POST"; req.Path = path; AddHeader(req.Headers, "Content-Type", contentType); req.Body = body; return new BuiltReq{host=host,port=port,request=req};
        }

        public static Response Send(string host, int port, Request req, int timeoutMs = 5000) {
            // Network not initialized guard to avoid ARP table null deref
            if (NETv4.ARPTable == null) { var r = new Response(); r.StatusCode = -1; r.ReasonPhrase = "Network not initialized"; return r; }

            // Loopback is not supported by our stack; avoid trying to route it through gateway (can destabilize)
            if (EqualsIgnoreCase(host, "localhost") || EqualsIgnoreCase(host, "127.0.0.1")) {
                var rr = new Response(); rr.StatusCode = -1; rr.ReasonPhrase = "Loopback not supported"; return rr;
            }

            var dest = Resolve(host);
            // Also avoid connecting to ourselves directly (no local TCP acceptance for HTTP client)
            if (!IsZero(dest) && EqualsIp(dest, NETv4.IP)) {
                var rx = new Response(); rx.StatusCode = -1; rx.ReasonPhrase = "Self-connect not supported"; return rx;
            }
            if (IsZero(dest)) { var r = new Response(); r.StatusCode = -1; r.ReasonPhrase = "DNS failed"; return r; }
            ushort localPort = NextEphemeralPort();
            var tcp = new NETv4.TCPClient(dest, (ushort)port, localPort);
            tcp.Connect();
            // wait for established
            ulong start = Timer.Ticks;
            while (tcp.Status != NETv4.TCPStatus.Established) {
                if ((long)(Timer.Ticks - start) > timeoutMs) { var rr = new Response(); rr.StatusCode = -1; rr.ReasonPhrase = "Connect timeout"; return rr; }
                ACPITimer.Sleep(10);
            }

            // Ensure required headers
            if (GetHeader(req.Headers, "Host") == null) AddHeader(req.Headers, "Host", host);
            if (GetHeader(req.Headers, "Connection") == null) AddHeader(req.Headers, "Connection", "close");
            if (GetHeader(req.Headers, "User-Agent") == null) AddHeader(req.Headers, "User-Agent", "guideXOS/0.2");
            if (req.Body != null && req.Body.Length > 0) SetHeader(req.Headers, "Content-Length", ParseIntToString(req.Body.Length));

            // Build request bytes
            string head = req.Method + " " + req.Path + " HTTP/1.1\r\n";
            for (int i = 0; i < req.Headers.Count; i++) head += req.Headers[i].Key + ": " + req.Headers[i].Value + "\r\n";
            head += "\r\n";
            byte[] headBytes = ToAsciiBytes(head);
            int totalLen = headBytes.Length + (req.Body != null ? req.Body.Length : 0);
            byte[] sendBuf = new byte[totalLen];
            for (int i = 0; i < headBytes.Length; i++) sendBuf[i] = headBytes[i];
            if (req.Body != null && req.Body.Length > 0) for (int i = 0; i < req.Body.Length; i++) sendBuf[headBytes.Length + i] = req.Body[i];
            fixed (byte* p = sendBuf) { tcp.Send(p, sendBuf.Length); }

            // Receive and parse
            var raw = ReceiveAll(tcp, timeoutMs);
            ParseHttp(raw, out Response resp);
            tcp.Close();
            tcp.Remove();
            return resp;
        }

        private static NETv4.IPAddress Resolve(string host) {
            // If network not initialized, skip DNS to avoid UDP path usage
            if (NETv4.ARPTable == null) return default;
            // Special-case loopback names to avoid accidental external DNS lookup
            if (EqualsIgnoreCase(host, "localhost")) return new NETv4.IPAddress(127,0,0,1);
            // dotted-quad?
            byte b1=0,b2=0,b3=0,b4=0; int dots=0; int acc=0; bool ok=true;
            for (int i=0;i<host.Length;i++){
                char c=host[i];
                if (c>='0'&& c<='9'){ acc=acc*10 + (c-'0'); if (acc>255){ ok=false; break; } }
                else if (c=='.'){ if(dots==0) b1=(byte)acc; else if(dots==1) b2=(byte)acc; else if(dots==2) b3=(byte)acc; acc=0; dots++; }
                else { ok=false; break; }
            }
            if (ok && dots==3){ b4=(byte)acc; return new NETv4.IPAddress(b1,b2,b3,b4); }
            return NETv4.DNSQuery(host);
        }

        private static bool IsZero(NETv4.IPAddress ip){ return ip.P1==0 && ip.P2==0 && ip.P3==0 && ip.P4==0; }
        private static bool EqualsIp(NETv4.IPAddress a, NETv4.IPAddress b){ return a.P1==b.P1 && a.P2==b.P2 && a.P3==b.P3 && a.P4==b.P4; }

        private static byte[] ReceiveAll(NETv4.TCPClient tcp, int timeoutMs){
            ulong start = Timer.Ticks;
            List<byte[]> parts = new List<byte[]>();
            int total = 0;
            while (true){
                var chunk = tcp.Receive();
                if (chunk != null){
                    parts.Add(chunk); total += chunk.Length;
                    if (IsHttpComplete(parts, total)) break;
                    start = Timer.Ticks; // extend timeout on data
                } else {
                    if (tcp.Status == NETv4.TCPStatus.Closed) break;
                    if ((long)(Timer.Ticks - start) > timeoutMs) break;
                    ACPITimer.Sleep(10);
                }
            }
            byte[] all = new byte[total]; int o=0; for(int i=0;i<parts.Count;i++){ var p = parts[i]; for(int k=0;k<p.Length;k++) all[o++]=p[k]; }
            return all;
        }

        private static bool IsHttpComplete(List<byte[]> parts, int total){
            // Find header end CRLFCRLF
            int scan=0; int headerEnd=-1;
            for (int i=0;i<parts.Count;i++){
                var p=parts[i];
                for (int j=0;j<p.Length;j++){
                    if (p[j]=='\r'){
                        if (Match(parts, i, j, "\r\n\r\n")) { headerEnd = scan + 4; goto found; }
                    }
                    scan++;
                }
            }
            found:;
            if (headerEnd<0) return false;
            // Build header substring into a temp array of length headerEnd
            byte[] all = new byte[headerEnd];
            int off=0; int remain = headerEnd; for (int i=0;i<parts.Count && remain>0;i++){ var p=parts[i]; for(int j=0;j<p.Length && remain>0;j++){ all[off++]=p[j]; remain--; } }
            int cl = HeaderContentLengthBytes(all);
            bool chunked = HeaderChunkedBytes(all);
            if (chunked){
                return false; // will complete on close
            }
            if (cl >= 0) return total >= headerEnd + cl;
            return false;
        }

        private static int HeaderContentLengthBytes(byte[] header){
            string h = BytesToAscii(header);
            int idx = IndexOfIgnoreCase(h, "Content-Length:");
            if (idx < 0) return -1;
            idx += 15;
            while (idx < h.Length && (h[idx]==' ' || h[idx]=='\t')) idx++;
            int n=0; bool any=false;
            while (idx < h.Length){ char c=h[idx++]; if (c<'0'||c>'9') break; n = n*10 + (c-'0'); any=true; }
            return any? n: -1;
        }
        private static bool HeaderChunkedBytes(byte[] header){ string h=BytesToAscii(header); return IndexOfIgnoreCase(h, "Transfer-Encoding:")>=0 && IndexOfIgnoreCase(h, "chunked")>=0; }

        private static void ParseHttp(byte[] raw, out Response resp){
            resp = new Response();
            int headerEnd = IndexOf(raw, ToAsciiBytes("\r\n\r\n"));
            if (headerEnd < 0) { resp.StatusCode = 0; resp.Body = raw; return; }
            int firstCR = IndexOf(raw, ToAsciiBytes("\r\n"));
            int code = 0; string reason = "";
            if (firstCR > 0){
                int p = 0; int spaces=0; int startCode=0; int endCode=0; for (; p<firstCR; p++){ if (raw[p]==' '){ spaces++; if (spaces==1) startCode=p+1; else if (spaces==2){ endCode=p; break; } } }
                for (int i=startCode;i<endCode;i++){ byte c=raw[i]; if (c>='0' && c<='9') code = code*10 + (c-'0'); }
                reason = BytesToAscii(raw, endCode+1, firstCR-(endCode+1));
            }
            resp.StatusCode = code; resp.ReasonPhrase = reason;
            int pos = firstCR + 2;
            while (pos < headerEnd){
                int lineEnd = IndexOf(raw, ToAsciiBytes("\r\n"), pos);
                if (lineEnd < 0 || lineEnd <= pos) break;
                int colon = -1; for (int i=pos;i<lineEnd;i++){ if (raw[i]==':'){ colon = i; break; } }
                if (colon > pos){
                    string key = TrimSpaces(BytesToAscii(raw, pos, colon - pos));
                    string val = TrimSpaces(BytesToAscii(raw, colon+1, lineEnd - (colon+1)));
                    var h = new Header(); h.Key = key; h.Value = val; resp.Headers.Add(h);
                }
                pos = lineEnd + 2;
            }
            int bodyStart = headerEnd + 4; int bodyLen = raw.Length - bodyStart;
            string te = GetHeader(resp.Headers, "Transfer-Encoding");
            if (te != null && IndexOfIgnoreCase(te, "chunked")>=0) {
                resp.Body = DecodeChunked(raw, bodyStart);
            } else {
                string cls = GetHeader(resp.Headers, "Content-Length");
                if (cls != null) {
                    int cl = ParseInt(cls);
                    byte[] body = new byte[cl]; for (int i=0;i<cl && i<bodyLen;i++) body[i] = raw[bodyStart+i]; resp.Body = body;
                } else {
                    byte[] body = new byte[bodyLen]; for (int i=0;i<bodyLen;i++) body[i] = raw[bodyStart+i]; resp.Body = body;
                }
            }
        }

        private static byte[] DecodeChunked(byte[] raw, int start){
            List<byte> data = new List<byte>();
            int i = start;
            while (i < raw.Length){
                int lineEnd = IndexOf(raw, ToAsciiBytes("\r\n"), i);
                if (lineEnd < 0) break;
                string hex = BytesToAscii(raw, i, lineEnd - i);
                int semi = IndexOf(hex, ';'); if (semi>=0) hex = hex.Substring(0, semi);
                int size = ParseHex(hex);
                i = lineEnd + 2;
                if (size == 0) break;
                int end = i + size; if (end > raw.Length) end = raw.Length;
                for (int k=i;k<end;k++) data.Add(raw[k]);
                i = end + 2;
            }
            return data.ToArray();
        }

        private static int IndexOf(byte[] buf, byte[] pat, int start=0){
            for (int i=start;i<=buf.Length - pat.Length;i++){
                bool ok=true; for (int j=0;j<pat.Length;j++){ if (buf[i+j]!=pat[j]){ ok=false; break; } }
                if (ok) return i;
            }
            return -1;
        }

        private static bool Match(List<byte[]> parts, int partIdx, int offset, string s){
            byte[] pat = ToAsciiBytes(s);
            int k=0; for (int i=partIdx;i<parts.Count;i++){
                var p=parts[i]; int j = (i==partIdx? offset:0);
                for (; j<p.Length && k<pat.Length; j++){
                    if (p[j]!=pat[k++]) return false;
                }
                if (k==pat.Length) return true;
            }
            return false;
        }

        private static byte[] ToAsciiBytes(string s){ if (s == null) return new byte[0]; byte[] b = new byte[s.Length]; for (int i=0;i<s.Length;i++) b[i] = (byte)(s[i] & 0x7F); return b; }
        private static string BytesToAscii(byte[] b){ return BytesToAscii(b,0,b.Length); }
        private static string BytesToAscii(byte[] b, int off, int len){ if (b==null || len<=0) return string.Empty; char[] c = new char[len]; for (int i=0;i<len;i++) c[i] = (char)b[off+i]; return new string(c); }

        private static void AddHeader(List<Header> list, string k, string v){ var h = new Header(); h.Key=k; h.Value=v; list.Add(h); }
        private static void SetHeader(List<Header> list, string k, string v){ for (int i=0;i<list.Count;i++){ if (EqualsIgnoreCase(list[i].Key,k)){ list[i].Value=v; return; } } AddHeader(list,k,v); }
        private static string GetHeader(List<Header> list, string k){ for (int i=0;i<list.Count;i++){ if (EqualsIgnoreCase(list[i].Key,k)) return list[i].Value; } return null; }

        private static bool EqualsIgnoreCase(string a, string b){
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i=0;i<a.Length;i++){ char ca=a[i]; if (ca>='A' && ca<='Z') ca=(char)(ca+32); char cb=b[i]; if (cb>='A' && cb<='Z') cb=(char)(cb+32); if (ca!=cb) return false; }
            return true;
        }
        private static int IndexOfIgnoreCase(string s, string needle){
            int n = needle.Length; if (n==0) return 0;
            for (int i=0;i+n<=s.Length;i++){
                bool ok=true; for (int j=0;j<n;j++){ char ca=s[i+j]; char cb=needle[j]; if (ca>='A'&&ca<='Z') ca=(char)(ca+32); if (cb>='A'&&cb<='Z') cb=(char)(cb+32); if (ca!=cb){ ok=false; break; } }
                if (ok) return i;
            }
            return -1;
        }
        private static int IndexOf(string s, char ch){ for (int i=0;i<s.Length;i++) if (s[i]==ch) return i; return -1; }
        private static bool HasPrefix(string s, string prefix){ if (prefix.Length> s.Length) return false; for(int i=0;i<prefix.Length;i++) if (s[i]!=prefix[i]) return false; return true; }
        private static string TrimSpaces(string s){ int a=0,b=s.Length-1; while (a<=b && (s[a]==' '||s[a]=='\t')) a++; while (b>=a && (s[b]==' '||s[b]=='\t')) b--; if (b<a) return string.Empty; return s.Substring(a, b-a+1); }
        private static int ParseInt(string s){ int n=0; for(int i=0;i<s.Length;i++){ char c=s[i]; if (c<'0'||c>'9') break; n=n*10+(c-'0'); } return n; }
        private static string ParseIntToString(int n){ if (n==0) return "0"; bool neg = n<0; if (neg) n = -n; char[] tmp = new char[12]; int i=0; while (n>0){ int d=n%10; tmp[i++]=(char)('0'+d); n/=10; } if (neg) tmp[i++]='-'; char[] outc = new char[i]; for(int k=0;k<i;k++) outc[k]=tmp[i-1-k]; return new string(outc); }
        private static int ParseHex(string s){ int n=0; for (int i=0;i<s.Length;i++){ char c=s[i]; int v; if (c>='0'&&c<='9') v=c-'0'; else if (c>='a'&&c<='f') v=10+(c-'a'); else if (c>='A'&&c<='F') v=10+(c-'A'); else break; n=(n<<4)|v; } return n; }
    }
}
