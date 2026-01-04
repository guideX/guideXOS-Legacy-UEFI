using System.Runtime.CompilerServices;

namespace guideXOS.Kernel.Drivers.Input {
    /// <summary>
    /// Lock-free Single-Producer Single-Consumer (SPSC) ring buffer for mouse events
    /// 
    /// ============================================================================
    /// EARLY BOOT / INTERRUPT-SAFE DESIGN
    /// ============================================================================
    /// 
    /// This buffer is designed for use during early boot and across the
    /// ExitBootServices boundary. It has the following properties:
    /// 
    /// MEMORY:
    /// - Fixed-size array allocated at construction time
    /// - NO heap allocation after initialization
    /// - Buffer size must be power of 2 for efficient modulo via bitmask
    /// - ALLOCATED IN KERNEL MEMORY (persists after ExitBootServices)
    /// 
    /// CONCURRENCY:
    /// - Lock-free for single-producer, single-consumer scenarios
    /// - Producer (UEFI poller) only writes _writeIndex
    /// - Consumer (kernel main loop) only writes _readIndex
    /// - No atomic RMW operations needed - just volatile reads/writes
    /// - Safe to use with interrupts enabled or disabled
    /// 
    /// OVERFLOW POLICY: DROP-OLDEST
    /// - When buffer is full, oldest events are silently dropped
    /// - This ensures the producer never blocks
    /// - Consumer always sees most recent events
    /// - DroppedEvents counter tracks overflow for diagnostics
    /// 
    /// WHY DROP-OLDEST:
    /// - Mouse input is real-time; old positions are stale
    /// - Blocking producer could cause UEFI firmware issues
    /// - Consumer can aggregate multiple events into one update
    /// 
    /// ============================================================================
    /// EXITBOOTSERVICES COMPATIBILITY
    /// ============================================================================
    /// 
    /// This buffer is specifically designed to survive ExitBootServices:
    /// 
    /// BEFORE ExitBootServices:
    /// - Producer (UefiPointerProvider) enqueues events from UEFI protocol
    /// - Events are stored in kernel-allocated memory
    /// - Consumer can dequeue at any time
    /// 
    /// AT ExitBootServices:
    /// - Producer stops adding new events (protocol becomes invalid)
    /// - All buffered events remain valid
    /// - Buffer state (indices, counts) remains valid
    /// 
    /// AFTER ExitBootServices:
    /// - Consumer can still dequeue remaining buffered events
    /// - No new events will be added from UEFI source
    /// - USB HID or other sources may use different buffers
    /// 
    /// WHY THIS WORKS:
    /// - Buffer array is in kernel heap, not UEFI memory
    /// - MouseEvent structs are value types (copied, not referenced)
    /// - No pointers to UEFI memory inside events
    /// - Indices are simple integers
    /// 
    /// ============================================================================
    /// USAGE:
    /// - Producer calls TryEnqueue() from UEFI poller or interrupt handler
    /// - Consumer calls TryDequeue() from main render loop
    /// - Never call both from same context (not thread-safe for that)
    /// 
    /// ============================================================================
    /// </summary>
    public sealed class MouseEventRingBuffer {
        // Fixed-size buffer - allocated once at construction
        private readonly MouseEvent[] _buffer;
        
        // Capacity must be power of 2 for efficient masking
        private readonly int _capacity;
        private readonly int _mask;  // _capacity - 1, for fast modulo
        
        // Producer writes to _writeIndex, consumer reads it
        // Consumer writes to _readIndex, producer reads it
        // Using volatile to ensure visibility across CPU cores/contexts
        private volatile int _writeIndex;
        private volatile int _readIndex;
        
        // Statistics (written by producer only)
        private ulong _droppedEvents;
        private ulong _totalEnqueued;
        
        // Statistics (written by consumer only)
        private ulong _totalDequeued;

        /// <summary>
        /// Number of events currently in buffer (approximate, may be stale)
        /// </summary>
        public int Count {
            get {
                int write = _writeIndex;
                int read = _readIndex;
                return (write - read) & _mask;
            }
        }

        /// <summary>
        /// Maximum number of events buffer can hold
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// True if buffer appears empty (may be stale)
        /// </summary>
        public bool IsEmpty => _writeIndex == _readIndex;

        /// <summary>
        /// Number of events dropped due to buffer overflow
        /// </summary>
        public ulong DroppedEvents => _droppedEvents;

        /// <summary>
        /// Total events successfully enqueued
        /// </summary>
        public ulong TotalEnqueued => _totalEnqueued;

        /// <summary>
        /// Total events successfully dequeued
        /// </summary>
        public ulong TotalDequeued => _totalDequeued;

        /// <summary>
        /// Create a new lock-free ring buffer
        /// </summary>
        /// <param name="capacityPowerOf2">
        /// Capacity as power of 2 (e.g., 6 means 2^6 = 64 events).
        /// Valid range: 4-10 (16 to 1024 events).
        /// Default: 6 (64 events)
        /// </param>
        public MouseEventRingBuffer(int capacityPowerOf2 = 6) {
            // Clamp to valid range
            if (capacityPowerOf2 < 4) capacityPowerOf2 = 4;   // Min 16 events
            if (capacityPowerOf2 > 10) capacityPowerOf2 = 10; // Max 1024 events
            
            _capacity = 1 << capacityPowerOf2;  // 2^n
            _mask = _capacity - 1;              // For fast modulo
            
            // Allocate fixed buffer - this is the ONLY allocation
            _buffer = new MouseEvent[_capacity];
            
            // Initialize indices
            _writeIndex = 0;
            _readIndex = 0;
            
            // Initialize statistics
            _droppedEvents = 0;
            _totalEnqueued = 0;
            _totalDequeued = 0;
        }

        /// <summary>
        /// Enqueue an event (PRODUCER ONLY - called from UEFI poller)
        /// 
        /// Lock-free, never blocks. If buffer is full, drops oldest event.
        /// </summary>
        /// <param name="evt">Event to enqueue</param>
        /// <returns>True if enqueued, false if event was invalid</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueue(in MouseEvent evt) {
            if (!evt.IsValid) return false;
            
            int write = _writeIndex;
            int read = _readIndex;
            int nextWrite = (write + 1) & _mask;
            
            // Check if buffer is full (next write would hit read position)
            // Note: We lose one slot to distinguish full from empty
            if (nextWrite == read) {
                // Buffer full - drop oldest by advancing read index
                // This is the DROP-OLDEST policy
                _readIndex = (read + 1) & _mask;
                _droppedEvents++;
                
                // Debug: Log buffer overrun
                MouseDebug.LogBufferOverrun(_capacity, _droppedEvents);
            }
            
            // Store event at current write position
            _buffer[write] = evt;
            
            // Memory barrier to ensure event is written before index update
            // In C# volatile write provides release semantics
            _writeIndex = nextWrite;
            
            _totalEnqueued++;
            
            // Debug: Log enqueue (periodic)
            MouseDebug.LogEventEnqueue(Count, _capacity, evt.Source);
            
            return true;
        }

        /// <summary>
        /// Dequeue an event (CONSUMER ONLY - called from kernel main loop)
        /// 
        /// Lock-free, never blocks. Returns false if buffer empty.
        /// </summary>
        /// <param name="evt">Output: the dequeued event</param>
        /// <returns>True if event was available, false if buffer empty</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out MouseEvent evt) {
            int read = _readIndex;
            int write = _writeIndex;
            
            // Check if buffer is empty
            if (read == write) {
                evt = MouseEvent.Empty;
                return false;
            }
            
            // Read event from current read position
            evt = _buffer[read];
            
            // Memory barrier to ensure event is read before index update
            // In C# volatile write provides release semantics
            _readIndex = (read + 1) & _mask;
            
            _totalDequeued++;
            return true;
        }

        /// <summary>
        /// Peek at the oldest event without removing it (CONSUMER ONLY)
        /// </summary>
        /// <param name="evt">Output: the oldest event</param>
        /// <returns>True if event available, false if buffer empty</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPeek(out MouseEvent evt) {
            int read = _readIndex;
            int write = _writeIndex;
            
            if (read == write) {
                evt = MouseEvent.Empty;
                return false;
            }
            
            evt = _buffer[read];
            return true;
        }

        /// <summary>
        /// Dequeue all pending events and aggregate into single event (CONSUMER ONLY)
        /// 
        /// This is useful for the main loop to combine multiple small movements
        /// into one position update, reducing jitter.
        /// </summary>
        /// <returns>Aggregated event, or Empty if no events</returns>
        public MouseEvent DequeueAllAggregated() {
            int read = _readIndex;
            int write = _writeIndex;
            
            if (read == write) return MouseEvent.Empty;
            
            int totalDeltaX = 0;
            int totalDeltaY = 0;
            int totalDeltaZ = 0;
            System.Windows.Forms.MouseButtons latestButtons = System.Windows.Forms.MouseButtons.None;
            ulong latestTimestamp = 0;
            bool hasAbsolute = false;
            int absoluteX = 0;
            int absoluteY = 0;
            int eventCount = 0;
            
            // Process all available events
            while (read != write) {
                MouseEvent evt = _buffer[read];
                read = (read + 1) & _mask;
                eventCount++;
                
                if (evt.IsAbsolute) {
                    // For absolute positioning, use the latest coordinates
                    hasAbsolute = true;
                    absoluteX = evt.AbsoluteX;
                    absoluteY = evt.AbsoluteY;
                } else {
                    // For relative, accumulate deltas
                    totalDeltaX += evt.DeltaX;
                    totalDeltaY += evt.DeltaY;
                }
                totalDeltaZ += evt.DeltaZ;
                latestButtons = evt.Buttons;
                latestTimestamp = evt.Timestamp;
            }
            
            // Update read index atomically
            _readIndex = read;
            _totalDequeued += (ulong)eventCount;
            
            // Debug: Log aggregation
            MouseDebug.LogBufferAggregation(eventCount, totalDeltaX, totalDeltaY, hasAbsolute);
            
            // Use the aggregated factory method
            return MouseEvent.CreateAggregated(
                totalDeltaX, totalDeltaY, totalDeltaZ,
                absoluteX, absoluteY, hasAbsolute,
                latestButtons, latestTimestamp);
        }

        /// <summary>
        /// Clear all events from buffer (CONSUMER ONLY - use during initialization)
        /// </summary>
        public void Clear() {
            _readIndex = _writeIndex;
        }

        /// <summary>
        /// Reset statistics counters
        /// </summary>
        public void ResetStatistics() {
            _droppedEvents = 0;
            _totalEnqueued = 0;
            _totalDequeued = 0;
        }

        /// <summary>
        /// Get diagnostic string
        /// </summary>
        public string GetDiagnostics() {
            return "[MouseEventRingBuffer]\n" +
                   "  Capacity: " + _capacity + "\n" +
                   "  Count: " + Count + "\n" +
                   "  WriteIndex: " + _writeIndex + "\n" +
                   "  ReadIndex: " + _readIndex + "\n" +
                   "  TotalEnqueued: " + _totalEnqueued + "\n" +
                   "  TotalDequeued: " + _totalDequeued + "\n" +
                   "  DroppedEvents: " + _droppedEvents + "\n";
        }
    }
}
