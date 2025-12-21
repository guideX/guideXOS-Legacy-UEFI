using System;
using System.Collections.Generic;
namespace guideXOS.Portable {
    // very small parser (no sections merging, whitespace trimmed)
    public class SimpleIni {
        readonly Dictionary<string, Dictionary<string, string>> data = new();

        public static SimpleIni Parse(string text) {
            var ini = new SimpleIni();
            string current = "";
            using (StringReader sr = new StringReader(text)) {
                string line;
                while ((line = sr.ReadLine()) != null) {
                    line = String.Trim(line);
                    if (line.Length == 0) continue;
                    if (line.StartsWith(";") || line.StartsWith("#")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]")) {
                        current = line.Substring(1, line.Length - 2).Trim();
                        if (!ini.data.ContainsKey(current))
                            ini.data[current] = new();
                        continue;
                    }
                    var idx = line.IndexOf('=');
                    if (idx > 0) {
                        var key = String.Trim(line.Substring(0, idx));
                        var val = String.Trim(line.Substring(idx + 1));
                        if (!ini.data.ContainsKey(current))
                            ini.data[current] = new();
                        ini.data[current][key] = val;
                    }
                }
            }
            return ini;
        }

        public string Get(string section, string key, string defaultValue = null) {
            if (data.TryGetValue(section, out var sec) && sec.TryGetValue(key, out var v))
                return v;
            return defaultValue;
        }
    }
}