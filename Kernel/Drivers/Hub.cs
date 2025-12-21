using guideXOS.Misc;
namespace guideXOS.Kernel.Drivers {
    /// <summary>
    /// HUB
    /// </summary>
    public static unsafe class Hub {
        /// <summary>
        /// Desc
        /// </summary>
        private struct Desc {
            public byte Length;
            public byte Type;
            public byte PortCount;
            public ushort Characteristic;
            public byte PortPowerTime;
            public byte Current;
        }
        /// <summary>
        /// Request
        /// </summary>
        private static USBRequest* req;
        /// <summary>
        /// Init
        /// </summary>
        public static void Initialize() {
            req = (USBRequest*)Allocator.Allocate((ulong)sizeof(USBRequest));
        }
        /// <summary>
        /// Initialize
        /// </summary>
        /// <param name="device"></param>
        internal static void Initialize(USBDevice device) {
            Desc* desc = (Desc*)Allocator.Allocate((ulong)sizeof(Desc));
            (*req).Clean();
            req->RequestType = 0xA0;
            req->Request = 0x06;
            req->Value = 0x2900;
            req->Index = 0;
            req->Length = (ushort)sizeof(Desc);
            bool b = USB.SendAndReceive(device, req, desc, null);
            if (!b) {
                Console.WriteLine("[USB Hub] Can't get Hub descriptor");
            }
            Console.WriteLine($"[USB Hub] This hub has {desc->PortCount} ports");
            for (int i = 0; i < desc->PortCount; i++) {
                // Power on port
                (*req).Clean();
                req->RequestType = 0x23;
                req->Request = 0x03;
                req->Value = 8; // PORT_POWER
                req->Index = ((ushort)(i + 1));
                req->Length = 0;
                b = USB.SendAndReceive(device, req, null, null);
                ACPITimer.SleepMicroseconds(100000);

                // Check for device connection
                uint status = 0;
                (*req).Clean();
                req->RequestType = 0xA3;
                req->Request = 0; // GET_STATUS
                req->Value = 0;
                req->Index = ((ushort)(i + 1));
                req->Length = sizeof(uint);
                b = USB.SendAndReceive(device, req, &status, null);
                if (!b) {
                    Console.WriteLine($"[USB Hub] Can't get Hub port {i} status");
                    continue;
                }

                // If a device is connected (bit 0 is set for PORT_CONNECTION)
                if ((status & 1) != 0) {
                    // Reset port
                    (*req).Clean();
                    req->RequestType = 0x23;
                    req->Request = 0x03; // SET_FEATURE
                    req->Value = 4; // PORT_RESET
                    req->Index = ((ushort)(i + 1));
                    req->Length = 0;
                    b = USB.SendAndReceive(device, req, null, null);
                    ACPITimer.SleepMicroseconds(200000); // Wait for reset to complete

                    // Wait for reset completion and get status
                    for (int k = 0; k < 10; k++) {
                        (*req).Clean();
                        req->RequestType = 0xA3;
                        req->Request = 0; // GET_STATUS
                        req->Value = 0;
                        req->Index = ((ushort)(i + 1));
                        req->Length = sizeof(uint);
                        b = USB.SendAndReceive(device, req, &status, null);
                        if (!b) {
                            Console.WriteLine($"[USB Hub] Can't get Hub port {i} status after reset");
                            break;
                        }
                        // Check if reset change bit is set (bit 20)
                        if ((status & (1 << 20)) != 0) {
                            break;
                        }
                        ACPITimer.SleepMicroseconds(50000);
                    }

                    if (!b) continue;

                    // Clear reset change feature
                    (*req).Clean();
                    req->RequestType = 0x23;
                    req->Request = 0x01; // CLEAR_FEATURE
                    req->Value = 20; // C_PORT_RESET
                    req->Index = ((ushort)(i + 1));
                    req->Length = 0;
                    USB.SendAndReceive(device, req, null, null);

                    // If port is now enabled (bit 1)
                    if ((status & 2) != 0) {
                        int speed = (int)((status >> 9) & 3);
                        Console.WriteLine($"[USB Hub] Port {i} has a device, speed: {speed}");
                        USB.InitPort(i, device, 2, speed);
                    }
                }
            }
        }
    }
}