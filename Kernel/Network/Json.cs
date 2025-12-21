using System.Collections.Generic;

namespace guideXOS.Network {
    // Minimal JSON serializer/deserializer for simple objects: dictionaries, arrays, strings, numbers, booleans, null.
    // No float parsing; numbers parsed as long (or kept string) to avoid BCL dependencies.
    internal static class JsonLite {
        public abstract class JVal { }
        public class JObject : JVal { public List<string> Keys = new List<string>(); public List<JVal> Values = new List<JVal>(); }
        public class JArray : JVal { public List<JVal> Items = new List<JVal>(); }
        public class JString : JVal { public string Value; }
        public class JNumber : JVal { public string Raw; } // keep raw to avoid float ops
        public class JBool : JVal { public bool Value; }
        public class JNull : JVal { }

        public static JVal Parse(string s) { int i=0; SkipWS(s, ref i); return ParseValue(s, ref i); }

        private static void SkipWS(string s, ref int i){ while (i<s.Length){ char c=s[i]; if (c==' '||c=='\n'||c=='\r'||c=='\t') i++; else break; } }

        private static JVal ParseValue(string s, ref int i){ SkipWS(s, ref i); if (i>=s.Length) return null; char c=s[i];
            if (c=='{') return ParseObject(s, ref i); if (c=='[') return ParseArray(s, ref i); if (c=='"') return ParseString(s, ref i); if (c=='t'||c=='f') return ParseBool(s, ref i); if (c=='n') return ParseNull(s, ref i); return ParseNumber(s, ref i);
        }
        private static JObject ParseObject(string s, ref int i){ var o=new JObject(); i++; SkipWS(s, ref i); if (i<s.Length && s[i]=='}'){ i++; return o; }
            while (i<s.Length){ var key = ParseString(s, ref i).Value; SkipWS(s, ref i); i++; // skip :
                var val = ParseValue(s, ref i); o.Keys.Add(key); o.Values.Add(val); SkipWS(s, ref i);
                if (i<s.Length && s[i]==','){ i++; SkipWS(s, ref i); continue; }
                if (i<s.Length && s[i]=='}'){ i++; break; }
            } return o; }
        private static JArray ParseArray(string s, ref int i){ var a=new JArray(); i++; SkipWS(s, ref i); if (i<s.Length && s[i]==']'){ i++; return a; }
            while (i<s.Length){ var v = ParseValue(s, ref i); a.Items.Add(v); SkipWS(s, ref i); if (i<s.Length && s[i]==','){ i++; SkipWS(s, ref i); continue; } if (i<s.Length && s[i]==']'){ i++; break; } } return a; }
        private static JString ParseString(string s, ref int i){ i++; var chars = new char[s.Length]; int n=0; while (i<s.Length){ char c=s[i++]; if (c=='"') break; if (c=='\\'){ char e=s[i++]; switch(e){ case '"': c='"'; break; case '\\': c='\\'; break; case '/': c='/'; break; case 'b': c='\b'; break; case 'f': c='\f'; break; case 'n': c='\n'; break; case 'r': c='\r'; break; case 't': c='\t'; break; default: c=e; break; } } chars[n++]=c; } return new JString{ Value=new string(chars,0,n)}; }
        private static JBool ParseBool(string s, ref int i){ if (s[i]=='t'){ i+=4; return new JBool{Value=true}; } else { i+=5; return new JBool{Value=false}; } }
        private static JNull ParseNull(string s, ref int i){ i+=4; return new JNull(); }
        private static JNumber ParseNumber(string s, ref int i){ int start=i; while (i<s.Length){ char c=s[i]; if ((c>='0'&&c<='9')||c=='-'||c=='+'||c=='.'||c=='e'||c=='E'){ i++; } else break; } return new JNumber{ Raw=s.Substring(start,i-start) }; }

        // Serialize simple values back to JSON
        public static string Stringify(JVal v){ if (v==null) return "null"; if (v is JString) return Quote(((JString)v).Value); if (v is JBool) return ((JBool)v).Value?"true":"false"; if (v is JNumber) return ((JNumber)v).Raw; if (v is JNull) return "null"; if (v is JObject){ var o=(JObject)v; string s="{"; for(int i=0;i<o.Keys.Count;i++){ if(i>0) s+=","; s+=Quote(o.Keys[i])+":"+Stringify(o.Values[i]); } s+="}"; return s;} if (v is JArray){ var a=(JArray)v; string s="["; for(int i=0;i<a.Items.Count;i++){ if(i>0) s+=","; s+=Stringify(a.Items[i]); } s+="]"; return s;} return "null"; }
        private static string Quote(string s){ char[] buf=new char[s.Length*2+2]; int n=0; buf[n++]='"'; for(int i=0;i<s.Length;i++){ char c=s[i]; if (c=='"'||c=='\\'){ buf[n++]='\\'; buf[n++]=c; } else if (c=='\n'){ buf[n++]='\\'; buf[n++]='n'; } else if (c=='\r'){ buf[n++]='\\'; buf[n++]='r'; } else if (c=='\t'){ buf[n++]='\\'; buf[n++]='t'; } else { buf[n++]=c; } } buf[n++]='"'; return new string(buf,0,n); }

        // Convenience helpers for objects
        public static string GetString(JObject o, string key){ for(int i=0;i<o.Keys.Count;i++){ if (EqualsIgnoreCase(o.Keys[i],key) && o.Values[i] is JString) return ((JString)o.Values[i]).Value; } return null; }
        public static JVal Get(JObject o, string key){ for(int i=0;i<o.Keys.Count;i++){ if (EqualsIgnoreCase(o.Keys[i],key)) return o.Values[i]; } return null; }
        private static bool EqualsIgnoreCase(string a, string b){ if (a.Length != b.Length) return false; for (int i=0;i<a.Length;i++){ char ca=a[i]; if (ca>='A' && ca<='Z') ca=(char)(ca+32); char cb=b[i]; if (cb>='A' && cb<='Z') cb=(char)(cb+32); if (ca!=cb) return false; } return true; }
    }
}
