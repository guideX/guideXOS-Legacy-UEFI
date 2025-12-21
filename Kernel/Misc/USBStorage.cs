using guideXOS.Misc;
using System.Collections.Generic;

namespace guideXOS.Kernel.Drivers {
    // Registry of connected USB mass-storage devices to surface icons in UI.
    public static class USBStorage {
        private static List<USBDevice> _devs = new List<USBDevice>(16);
        private static int _count;
        public static int Count => _count;

        // Called by driver when a mass storage device is enumerated
        public static void Register(USBDevice dev) { if (dev == null) return; _devs.Add(dev); _count = _devs.Count; }
        // Called when device removed (not implemented yet)
        public static void Unregister(USBDevice dev) { if (dev == null) return; int idx = _devs.IndexOf(dev); if (idx >= 0) _devs.RemoveAt(idx); _count = _devs.Count; }

        public static USBDevice GetFirst() { return _devs.Count > 0 ? _devs[0] : null; }
        public static USBDevice[] GetAll() { return _devs.ToArray(); }
    }
}
