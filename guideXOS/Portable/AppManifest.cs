using System;
namespace guideXOS.Portable {
    namespace guideXOS.Portable {
        public class AppManifest {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Version { get; set; }
            public string EntryAssembly { get; set; }
            public string EntryType { get; set; }
            public string EntryMethod { get; set; }
            public string RunAs { get; set; }
            public string[] Permissions { get; set; } = Array.Empty<string>();
            public string Icon { get; set; }
            public string Description { get; set; }

            public static AppManifest FromIniFile(string pathToManifest) {
                var text = File.ReadAllText(pathToManifest);
                var ini = SimpleIni.Parse(text);
                var m = new AppManifest
                {
                    Id = ini.Get("App", "Id", Guid.NewGuid().ToString()),
                    Name = ini.Get("App", "Name", Path.GetFileName(Path.GetDirectoryName(pathToManifest)) ?? "Unknown"),
                    Version = ini.Get("App", "Version", "0.0.0"),
                    EntryAssembly = ini.Get("App", "EntryAssembly", "app.dll"),
                    EntryType = ini.Get("App", "EntryType", null),
                    EntryMethod = ini.Get("App", "EntryMethod", "Start"),
                    RunAs = ini.Get("App", "RunAs", "user"),
                    Icon = ini.Get("App", "Icon", "icon.png"),
                    Description = ini.Get("App", "Description", "")
                };
                var perms = ini.Get("App", "Permissions", "");
                if (!string.IsNullOrWhiteSpace(perms))
                    m.Permissions = perms.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                return m;
            }
        }
    }
}
