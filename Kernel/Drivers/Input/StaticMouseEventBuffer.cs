using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Static, pre-allocated lock-free ring buffer for very early boot
    /// 
    /// ============================================================================
    /// ZERO-ALLOCATION DESIGN
    /// ============================================================================
    /// 
    /// This buffer uses a fixed-size static array that is allocated at compile time.
    /// It requires NO heap allocation at runtime, making it safe to use even before
    /// the memory allocator is initialized.
    /// 
    /// USE CASES:
    /// - Very early boot before heap is available
    /// - Interrupt handlers where allocation is forbidden
    /// - ExitBootServices transition period
    /// 
    /// LIMITATIONS:
    /// - Fixed capacity (64 events) cannot be changed at runtime
    /// - Only one instance (static)
    /// - Must call Initialize() before use
    /// 
    /// For most scenarios, use MouseEventRingBuffer instead which allows
    /// configurable capacity.
    /// 
    /// ============================================================================
    /// OVERFLOW POLICY: DROP-OLDEST
    /// ============================================================================
    /// 
    /// When the buffer is full, the oldest event is dropped to make room for
    /// the new event. This ensures:
    /// - Producer never blocks (critical for interrupt handlers)
    /// - Consumer always sees most recent mouse state
    /// - Old/stale position data is discarded
    /// 
    /// ============================================================================
    /// </summary>
    public static unsafe class StaticMouseEventBuffer {
        // Fixed capacity - power of 2 for efficient masking
        private const int CAPACITY = 64;
        private const int MASK = CAPACITY - 1;
        
        // Static buffer - no allocation needed
        // We use a fixed-size array embedded in a struct to avoid heap allocation
        private static FixedBuffer _buffer;
        
        // Indices for lock-free SPSC operation
        private static volatile int _writeIndex;
        private static volatile int _readIndex;
        
        // Statistics
        private static ulong _droppedEvents;
        private static ulong _totalEnqueued;
        private static ulong _totalDequeued;
        
        // Initialization flag
        private static bool _initialized;

        /// <summary>
        /// Fixed-size buffer structure to avoid heap allocation
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct FixedBuffer {
            // We store events as raw bytes and manually marshal
            // Each MouseEvent is approximately 48 bytes
            public fixed byte Data[CAPACITY * 48];
        }

        /// <summary>
        /// Number of events currently in buffer
        /// </summary>
        public static int Count {
            get {
                if (!_initialized) return 0;
                return (_writeIndex - _readIndex) & MASK;
            }
        }

        /// <summary>
        /// Maximum capacity
        /// </summary>
        public static int Capacity => CAPACITY;

        /// <summary>
        /// True if buffer is empty
        /// </summary>
        public static bool IsEmpty => !_initialized || _writeIndex == _readIndex;

        /// <summary>
        /// Number of dropped events
        /// </summary>
        public static ulong DroppedEvents => _droppedEvents;

        /// <summary>
        /// Initialize the static buffer (must be called once before use)
        /// </summary>
        public static void Initialize() {
            _writeIndex = 0;
            _readIndex = 0;
            _droppedEvents = 0;
            _totalEnqueued = 0;
            _totalDequeued = 0;
            
            // Zero out the buffer
            fixed (byte* ptr = _buffer.Data) {
                for (int i = 0; i < CAPACITY * 48; i++) {
                    ptr[i] = 0;
                }
            }
            
            _initialized = true;
        }

        /// <summary>
        /// Enqueue an event (PRODUCER ONLY)
        /// Lock-free, never blocks. Drops oldest if full.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryEnqueue(in MouseEvent evt) {
            if (!_initialized || !evt.IsValid) return false;
            
            int write = _writeIndex;
            int read = _readIndex;
            int nextWrite = (write + 1) & MASK;
            
            // Check if full - drop oldest
            if (nextWrite == read) {
                _readIndex = (read + 1) & MASK;
                _droppedEvents++;
            }
            
            // Store event
            StoreEvent(write, in evt);
            
            // Update write index
            _writeIndex = nextWrite;
            _totalEnqueued++;
            
            return true;
        }

        /// <summary>
        /// Dequeue an event (CONSUMER ONLY)
        /// Lock-free, never blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryDequeue(out MouseEvent evt) {
            if (!_initialized) {
                evt = MouseEvent.Empty;
                return false;
            }
            
            int read = _readIndex;
            int write = _writeIndex;
            
            if (read == write) {
                evt = MouseEvent.Empty;
                return false;
            }
            
            // Load event
            evt = LoadEvent(read);
            
            // Update read index
            _readIndex = (read + 1) & MASK;
            _totalDequeued++;
            
            return true;
        }

        /// <summary>
        /// Clear the buffer
        /// </summary>
        public static void Clear() {
            if (!_initialized) return;
            _readIndex = _writeIndex;
        }

        /// <summary>
        /// Store event at index (manual marshaling)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void StoreEvent(int index, in MouseEvent evt) {
            fixed (byte* ptr = _buffer.Data) {
                byte* dest = ptr + (index * 48);
                
                // Store fields manually
                *(int*)(dest + 0) = evt.DeltaX;
                *(int*)(dest + 4) = evt.DeltaY;
                *(int*)(dest + 8) = evt.DeltaZ;
                *(int*)(dest + 12) = (int)evt.Buttons;
                *(byte*)(dest + 16) = evt.IsAbsolute ? (byte)1 : (byte)0;
                *(int*)(dest + 20) = evt.AbsoluteX;
                *(int*)(dest + 24) = evt.AbsoluteY;
                *(ulong*)(dest + 32) = evt.Timestamp;
                *(byte*)(dest + 40) = evt.IsValid ? (byte)1 : (byte)0;
            }
        }

        /// <summary>
        /// Load event from index (manual marshaling)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static MouseEvent LoadEvent(int index) {
            fixed (byte* ptr = _buffer.Data) {
                byte* src = ptr + (index * 48);
                
                return new MouseEvent {
                    DeltaX = *(int*)(src + 0),
                    DeltaY = *(int*)(src + 4),
                    DeltaZ = *(int*)(src + 8),
                    Buttons = (System.Windows.Forms.MouseButtons)(*(int*)(src + 12)),
                    IsAbsolute = *(byte*)(src + 16) != 0,
                    AbsoluteX = *(int*)(src + 20),
                    AbsoluteY = *(int*)(src + 24),
                    Timestamp = *(ulong*)(src + 32),
                    IsValid = *(byte*)(src + 40) != 0
                };
            }
        }

        /// <summary>
        /// Get diagnostics string
        /// </summary>
        public static string GetDiagnostics() {
            return "[StaticMouseEventBuffer]\n" +
                   "  Initialized: " + _initialized + "\n" +
                   "  Capacity: " + CAPACITY + "\n" +
                   "  Count: " + Count + "\n" +
                   "  WriteIndex: " + _writeIndex + "\n" +
                   "  ReadIndex: " + _readIndex + "\n" +
                   "  TotalEnqueued: " + _totalEnqueued + "\n" +
                   "  TotalDequeued: " + _totalDequeued + "\n" +
                   "  DroppedEvents: " + _droppedEvents + "\n";
        }
    }
}
