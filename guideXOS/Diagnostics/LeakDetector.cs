using guideXOS.Kernel.Drivers;
using System;

namespace guideXOS.Diagnostics {
    /// <summary>
    /// Memory leak diagnostic tool - use to identify which operations are leaking
    /// </summary>
    public static class LeakDetector {
        private static ulong _lastMemory = 0;
        private static ulong _frameCount = 0;
        private static ulong _lastLogTick = 0;
        private static bool _enabled = false;
        
        // Accumulate leaks per operation
        private static long _backgroundMgrLeak = 0;
        private static long _inputAllLeak = 0;
        private static long _desktopUpdateLeak = 0;
        private static long _drawAllLeak = 0;
        private static long _cleanupLeak = 0;
        private static long _graphicsLeak = 0;
        
        public static void Enable() {
            _enabled = true;
            _lastMemory = Allocator.MemoryInUse;
            _frameCount = 0;
            _lastLogTick = Timer.Ticks;
            BootConsole.WriteLine("[LeakDetector] Enabled - monitoring memory usage");
        }
        
        public static void Disable() {
            _enabled = false;
            BootConsole.WriteLine("[LeakDetector] Disabled");
        }
        
        /// <summary>
        /// Call at start of frame
        /// </summary>
        public static void FrameStart() {
            if (!_enabled) return;
            _frameCount++;
            _lastMemory = Allocator.MemoryInUse;
        }
        
        /// <summary>
        /// Call after each major operation to track its memory impact
        /// </summary>
        public static void Checkpoint(string operationName, ref long accumulator) {
            if (!_enabled) return;
            
            ulong memNow = Allocator.MemoryInUse;
            long diff = (long)memNow - (long)_lastMemory;
            
            if (diff > 0) {
                accumulator += diff;
            }
            
            _lastMemory = memNow;
        }
        
        /// <summary>
        /// Call at end of frame to log results
        /// </summary>
        public static void FrameEnd() {
            if (!_enabled) return;
            
            // Log every second (60 frames)
            if (_frameCount % 60 == 0) {
                ulong elapsed = Timer.Ticks - _lastLogTick;
                if (elapsed >= 1000) {
                    LogResults();
                    _lastLogTick = Timer.Ticks;
                }
            }
        }
        
        private static void LogResults() {
            BootConsole.WriteLine("=== Memory Leak Report (per second) ===");
            
            if (_backgroundMgrLeak > 0)
                BootConsole.WriteLine($"BackgroundMgr:  +{_backgroundMgrLeak / 1024} KB");
            if (_inputAllLeak > 0)
                BootConsole.WriteLine($"InputAll:       +{_inputAllLeak / 1024} KB");
            if (_desktopUpdateLeak > 0)
                BootConsole.WriteLine($"Desktop.Update: +{_desktopUpdateLeak / 1024} KB");
            if (_drawAllLeak > 0)
                BootConsole.WriteLine($"DrawAll:        +{_drawAllLeak / 1024} KB");
            if (_cleanupLeak > 0)
                BootConsole.WriteLine($"Cleanup:        +{_cleanupLeak / 1024} KB");
            if (_graphicsLeak > 0)
                BootConsole.WriteLine($"Graphics:       +{_graphicsLeak / 1024} KB");
            
            long total = _backgroundMgrLeak + _inputAllLeak + _desktopUpdateLeak + 
                        _drawAllLeak + _cleanupLeak + _graphicsLeak;

            BootConsole.WriteLine($"TOTAL:          +{total / 1024} KB/sec");
            BootConsole.WriteLine($"Current Memory: {Allocator.MemoryInUse / (1024 * 1024)} MB");
            BootConsole.WriteLine("=======================================");
            
            // Reset accumulators
            _backgroundMgrLeak = 0;
            _inputAllLeak = 0;
            _desktopUpdateLeak = 0;
            _drawAllLeak = 0;
            _cleanupLeak = 0;
            _graphicsLeak = 0;
        }
        
        // Accessors for the accumulators
        public static ref long BackgroundMgrLeak => ref _backgroundMgrLeak;
        public static ref long InputAllLeak => ref _inputAllLeak;
        public static ref long DesktopUpdateLeak => ref _desktopUpdateLeak;
        public static ref long DrawAllLeak => ref _drawAllLeak;
        public static ref long CleanupLeak => ref _cleanupLeak;
        public static ref long GraphicsLeak => ref _graphicsLeak;
    }
}
