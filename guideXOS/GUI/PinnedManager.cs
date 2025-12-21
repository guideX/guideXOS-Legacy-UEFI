using System.Collections.Generic;
using System.Drawing;
namespace guideXOS.GUI {
    internal static class PinnedManager {
        // Kind: 0=App, 1=File, 2=OpenComputerFiles
        private struct Pinned { public string Name; public Image Icon; public string Path; public byte Kind; }
        private static List<Pinned> _items = new List<Pinned>(16);
        
        public static bool IsPinned(string name){ 
            if(string.IsNullOrEmpty(name)) return false; 
            for(int i=0;i<_items.Count;i++){ 
                if(_items[i].Name==name) return true; 
            } 
            return false; 
        }
        
        public static void PinApp(string name, Image icon){ 
            if(string.IsNullOrEmpty(name)) return; 
            if(IsPinned(name)) return; 
            _items.Add(new Pinned{ Name=name, Icon= icon ?? Icons.DocumentIcon(32), Path=null, Kind=0}); 
        }
        
        public static void PinFile(string displayName, string absolutePath, Image icon){ 
            if(string.IsNullOrEmpty(displayName)||string.IsNullOrEmpty(absolutePath)) return; 
            if(IsPinned(displayName)) return; 
            _items.Add(new Pinned{ Name=displayName, Icon= icon ?? Icons.DocumentIcon(32), Path=absolutePath, Kind=1}); 
        }
        
        public static void PinComputerFiles(){ 
            if(!IsPinned("Computer Files")) 
                _items.Add(new Pinned{ Name="Computer Files", Icon=Icons.FolderIcon(32), Path=null, Kind=2}); 
        }
        
        // FIXED: Dispose icon when unpinning
        public static void Unpin(string name){ 
            for(int i=0;i<_items.Count;i++){ 
                if(_items[i].Name==name){ 
                    // Dispose the icon before removing
                    var item = _items[i];
                    if (item.Icon != null) {
                        item.Icon.Dispose();
                    }
                    _items.RemoveAt(i); 
                    return; 
                } 
            } 
        }
        
        public static int Count => _items.Count;
        public static string Name(int i)=> _items[i].Name;
        public static Image Icon(int i)=> _items[i].Icon ?? Icons.DocumentIcon(32);
        public static byte Kind(int i)=> _items[i].Kind;
        public static string Path(int i)=> _items[i].Path;
        
        /// <summary>
        /// FIXED: Added cleanup method to dispose all cached icons
        /// </summary>
        public static new void Dispose() {
            for (int i = 0; i < _items.Count; i++) {
                var item = _items[i];
                if (item.Icon != null) {
                    item.Icon.Dispose();
                }
            }
            _items.Clear();
        }
    }
}
