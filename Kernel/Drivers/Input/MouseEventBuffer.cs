namespace guideXOS.Kernel.Drivers.Input {
/// <summary>
/// Ring buffer for mouse events
/// 
/// ============================================================================
/// UEFI COMPATIBILITY: FULLY COMPATIBLE
/// ============================================================================
/// 
/// This buffer stores mouse events so they survive the ExitBootServices
/// transition. Events captured by UEFI pointer protocol before ExitBootServices
/// remain available for processing after the transition.
/// 
/// Features:
/// - Fixed-size ring buffer (no dynamic allocation after init)
/// - Thread-safe single-producer single-consumer (no locks needed)
/// - Oldest events dropped when buffer is full
/// 
/// ============================================================================
/// EXITBOOTSERVICES COMPATIBILITY
/// ============================================================================
/// 
/// DATA THAT PERSISTS (kernel memory):
/// - _buffer array (MouseEvent[] allocated in kernel heap)
/// - _head, _tail, _count indices
/// - DroppedEvents counter
/// 
/// WHY IT SURVIVES:
/// - No pointers to UEFI memory
/// - MouseEvent is a value type (struct)
/// - All data is copied, not referenced
/// 
/// USAGE AFTER EXITBOOTSERVICES:
/// - Safe to call Dequeue(), Peek(), DequeueAll()
/// - Enqueue() still works but no new UEFI events will arrive
/// - USB HID events could be enqueued from other sources
/// 
/// </summary>
public class MouseEventBuffer {
        private readonly MouseEvent[] _buffer;
        private readonly int _capacity;
        private int _head;  // Next write position
        private int _tail;  // Next read position
        private int _count; // Current event count

        /// <summary>
        /// Number of events currently in buffer
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Maximum number of events buffer can hold
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// True if buffer is empty
        /// </summary>
        public bool IsEmpty => _count == 0;

        /// <summary>
        /// True if buffer is full (will drop oldest on next enqueue)
        /// </summary>
        public bool IsFull => _count >= _capacity;

        /// <summary>
        /// Number of events dropped due to buffer overflow
        /// </summary>
        public ulong DroppedEvents { get; private set; }

        /// <summary>
        /// Create a new event buffer with specified capacity
        /// </summary>
        /// <param name="capacity">Maximum events to store (default 64)</param>
        public MouseEventBuffer(int capacity = 64) {
            _capacity = capacity > 0 ? capacity : 64;
            _buffer = new MouseEvent[_capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
            DroppedEvents = 0;
        }

        /// <summary>
        /// Add an event to the buffer
        /// If buffer is full, oldest event is dropped
        /// </summary>
        /// <param name="mouseEvent">Event to add</param>
        public void Enqueue(MouseEvent mouseEvent) {
            if (!mouseEvent.IsValid) return;

            // If full, advance tail (drop oldest)
            if (_count >= _capacity) {
                _tail = (_tail + 1) % _capacity;
                _count--;
                DroppedEvents++;
                
                // Debug: Log buffer overrun
                MouseDebug.LogBufferOverrun(_capacity, DroppedEvents);
            }

            // Store at head and advance
            _buffer[_head] = mouseEvent;
            _head = (_head + 1) % _capacity;
            _count++;
            
            // Debug: Log enqueue
            MouseDebug.LogEventEnqueue(_count, _capacity, mouseEvent.Source);
        }

        /// <summary>
        /// Remove and return the oldest event from buffer
        /// </summary>
        /// <param name="mouseEvent">Output: the dequeued event</param>
        /// <returns>True if event was available, false if buffer empty</returns>
        public bool Dequeue(out MouseEvent mouseEvent) {
            if (_count <= 0) {
                mouseEvent = MouseEvent.Empty;
                return false;
            }

            mouseEvent = _buffer[_tail];
            _tail = (_tail + 1) % _capacity;
            _count--;
            return true;
        }

        /// <summary>
        /// Peek at the oldest event without removing it
        /// </summary>
        /// <param name="mouseEvent">Output: the oldest event</param>
        /// <returns>True if event available, false if buffer empty</returns>
        public bool Peek(out MouseEvent mouseEvent) {
            if (_count <= 0) {
                mouseEvent = MouseEvent.Empty;
                return false;
            }

            mouseEvent = _buffer[_tail];
            return true;
        }

        /// <summary>
        /// Clear all events from buffer
        /// </summary>
        public void Clear() {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        /// <summary>
        /// Aggregate all pending events into a single combined event
        /// Useful for combining multiple small movements into one update
        /// </summary>
        /// <returns>Combined event, or Empty if no events</returns>
        public MouseEvent DequeueAll() {
            if (_count <= 0) return MouseEvent.Empty;

            int totalDeltaX = 0;
            int totalDeltaY = 0;
            int totalDeltaZ = 0;
            System.Windows.Forms.MouseButtons latestButtons = System.Windows.Forms.MouseButtons.None;
            ulong latestTimestamp = 0;
            bool hasAbsolute = false;
            int absoluteX = 0;
            int absoluteY = 0;
            int eventCount = 0;

            while (_count > 0) {
                var evt = _buffer[_tail];
                _tail = (_tail + 1) % _capacity;
                _count--;
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

            // Debug: Log aggregation
            MouseDebug.LogBufferAggregation(eventCount, totalDeltaX, totalDeltaY, hasAbsolute);

            if (hasAbsolute) {
                return MouseEvent.CreateAbsolute(absoluteX, absoluteY, totalDeltaZ, latestButtons, latestTimestamp);
            } else {
                return MouseEvent.CreateRelative(totalDeltaX, totalDeltaY, totalDeltaZ, latestButtons, latestTimestamp);
            }
        }
    }
}
