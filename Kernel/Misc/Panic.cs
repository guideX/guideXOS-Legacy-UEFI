using guideXOS.Kernel.Drivers;
using System;
using Graphics = guideXOS.Graph.Graphics;

namespace guideXOS.Misc {
    /// <summary>
    /// Enhanced Panic Screen with comprehensive crash information
    /// </summary>
    public static class Panic {
        private static bool _inPanic = false;
        
        /// <summary>
        /// Display basic error message (backward compatibility)
        /// </summary>
        public static void Error(string msg, bool skippable = false) {
            if (_inPanic) return; // Prevent recursive panics
            _inPanic = true;
            
            //Kill all CPUs
            LocalAPIC.SendAllInterrupt(0xFD);
            IDT.Disable();
            Framebuffer.TripleBuffered = false;

            ConsoleColor color = Console.ForegroundColor;

            //Console.ForegroundColor = System.ConsoleColor.Red;
            BootConsole.Write("PANIC: ");
            BootConsole.WriteLine(msg);
            BootConsole.WriteLine("All CPU Halted Now!");

            //BootConsole.ForegroundColor = color;

            if (!skippable) {
                Framebuffer.Update();
                for (; ; );
            }
        }
        
        /// <summary>
        /// Display enhanced panic screen with full diagnostic information
        /// </summary>
        public static unsafe void ShowEnhancedCrashScreen(
            int vector,
            ulong errorCode,
            bool hasErrorCode,
            IDT.RegistersStack* registers,
            IDT.InterruptReturnStack* interruptStack,
            string additionalInfo = null) {
            
            if (_inPanic) return;
            _inPanic = true;
            
            // Kill all CPUs
            LocalAPIC.SendAllInterrupt(0xFD);
            IDT.Disable();
            Framebuffer.TripleBuffered = false;
            
            // Clear screen with dark blue background (BSOD style)
            Framebuffer.Graphics.Clear(0xFF000080);
            
            int y = 10;
            int leftMargin = 20;
            int lineHeight = 18;
            
            // Draw header
            DrawPanicHeader(ref y, leftMargin, lineHeight, vector);
            
            // Draw exception details
            y += 10;
            DrawExceptionInfo(ref y, leftMargin, lineHeight, vector, errorCode, hasErrorCode, interruptStack);
            
            // Draw register dump
            y += 10;
            DrawRegisters(ref y, leftMargin, lineHeight, registers, interruptStack);
            
            // Draw stack trace
            y += 10;
            DrawStackTrace(ref y, leftMargin, lineHeight, interruptStack);
            
            // Draw system information
            y += 10;
            DrawSystemInfo(ref y, leftMargin, lineHeight);
            
            // Draw control registers
            y += 10;
            DrawControlRegisters(ref y, leftMargin, lineHeight);
            
            // Draw timing analysis
            y += 10;
            DrawTimingAnalysis(ref y, leftMargin, lineHeight, vector);
            
            // Draw footer
            DrawFooter();
            
            // Additional info if provided
            if (additionalInfo != null) {
                y = Framebuffer.Height - 40;
                DrawText(leftMargin, y, $"Additional: {additionalInfo}", 0xFFFFFF00);
            }
            
            Framebuffer.Update();
            
            // Halt forever
            for (; ; ) Native.Hlt();
        }
        
        private static void DrawPanicHeader(ref int y, int x, int lineHeight, int vector) {
            // Draw title bar
            Framebuffer.Graphics.FillRectangle(0, y, Framebuffer.Width, 40, 0xFF0000AA);
            
            string title = "guideXOS - KERNEL PANIC";
            DrawTextLarge(x, y + 8, title, 0xFFFFFFFF);
            
            y += 40;
            
            // Draw separator line
            Framebuffer.Graphics.FillRectangle(x, y, Framebuffer.Width - x * 2, 2, 0xFFFFFFFF);
            y += 10;
            
            // Exception type
            string exceptionName = GetExceptionName(vector);
            DrawText(x, y, $"EXCEPTION: {exceptionName} (Vector 0x{vector:X2})", 0xFFFF0000);
            y += lineHeight;
            
            DrawText(x, y, $"CPU: {SMP.ThisCPU}  |  Time: {Timer.Ticks} ticks  |  All CPUs Halted", 0xFFFFFFFF);
            y += lineHeight;
        }
        
        private static unsafe void DrawExceptionInfo(ref int y, int x, int lineHeight, int vector, ulong errorCode, bool hasErrorCode, IDT.InterruptReturnStack* interruptStack) {
            DrawSectionHeader(x, ref y, "EXCEPTION DETAILS", lineHeight);
            
            // Exception-specific information
            switch (vector) {
                case 14: // Page Fault
                    ulong pageFaultAddr = Native.ReadCR2();
                    DrawText(x + 20, y, "Faulting Address: 0x" + pageFaultAddr.ToString("X16"), 0xFFFFFF00);
                    y += lineHeight;
                    
                    if (pageFaultAddr < 0x1000) {
                        DrawText(x + 20, y, "Type: NULL POINTER DEREFERENCE", 0xFFFF4444);
                    } else {
                        DrawText(x + 20, y, "Type: Invalid Memory Access", 0xFFFF4444);
                    }
                    y += lineHeight;
                    
                    if (hasErrorCode) {
                        ulong ec = errorCode;
                        bool present = (ec & 1) != 0;
                        bool write = ((ec >> 1) & 1) != 0;
                        bool user = ((ec >> 2) & 1) != 0;
                        bool reserved = ((ec >> 3) & 1) != 0;
                        bool instructionFetch = ((ec >> 4) & 1) != 0;
                        
                        DrawText(x + 20, y, "Access: " + (write ? "WRITE" : "READ") + "  |  Mode: " + (user ? "USER" : "KERNEL") + "  |  Present: " + present, 0xFFFFFFFF);
                        y += lineHeight;
                        
                        if (reserved) {
                            DrawText(x + 20, y, "Reason: Reserved bit violation", 0xFFFF8888);
                            y += lineHeight;
                        }
                        if (instructionFetch) {
                            DrawText(x + 20, y, "Reason: Instruction fetch from non-executable page", 0xFFFF8888);
                            y += lineHeight;
                        }
                    }
                    break;
                    
                case 13: // General Protection Fault
                    DrawText(x + 20, y, "Type: General Protection Fault", 0xFFFF4444);
                    y += lineHeight;
                    if (hasErrorCode) {
                        DrawText(x + 20, y, "Error Code: 0x" + errorCode.ToString("X16"), 0xFFFFFFFF);
                        y += lineHeight;
                        
                        bool external = (errorCode & 1) != 0;
                        ulong tableIndex = (errorCode >> 1) & 0x3;
                        ulong selectorIndex = errorCode >> 3;
                        
                        string table = tableIndex switch {
                            0 => "GDT",
                            1 => "IDT",
                            2 => "LDT",
                            3 => "IDT",
                            _ => "Unknown"
                        };
                        
                        DrawText(x + 20, y, "Segment: " + table + "[" + selectorIndex + "]  |  External: " + external, 0xFFFFFFFF);
                        y += lineHeight;
                        
                        // Additional GPF troubleshooting info
                        DrawText(x + 20, y, "Common Causes:", 0xFFFFFF00);
                        y += lineHeight;
                        DrawText(x + 40, y, "- Invalid segment selector in GDT/IDT/LDT", 0xFFCCCCCC);
                        y += lineHeight;
                        DrawText(x + 40, y, "- Access violation (privilege/limit check)", 0xFFCCCCCC);
                        y += lineHeight;
                        DrawText(x + 40, y, "- Invalid descriptor type for operation", 0xFFCCCCCC);
                        y += lineHeight;
                        DrawText(x + 40, y, "- Writing to read-only segment", 0xFFCCCCCC);
                        y += lineHeight;
                        
                        // Check if error code is 0 (often NULL selector)
                        if (errorCode == 0) {
                            DrawText(x + 20, y, "NOTE: Error code 0 may indicate NULL selector usage", 0xFFFF8888);
                            y += lineHeight;
                        }
                    } else {
                        DrawText(x + 20, y, "No error code - likely instruction-related GPF", 0xFFFF8888);
                        y += lineHeight;
                        DrawText(x + 40, y, "Possible: Invalid opcode, privilege violation, or bad operand", 0xFFCCCCCC);
                        y += lineHeight;
                    }
                    break;
                    
                case 0: // Divide by Zero
                    DrawText(x + 20, y, "Type: Division by zero or overflow", 0xFFFF4444);
                    y += lineHeight;
                    break;
                    
                case 6: // Invalid Opcode
                    DrawText(x + 20, y, "Type: Attempted to execute invalid instruction", 0xFFFF4444);
                    y += lineHeight;
                    break;
                    
                case 8: // Double Fault
                    DrawText(x + 20, y, "Type: Exception occurred while handling another exception", 0xFFFF4444);
                    y += lineHeight;
                    DrawText(x + 20, y, "This indicates a critical kernel error!", 0xFFFF0000);
                    y += lineHeight;
                    break;
            }
            
            // Instruction pointer
            DrawText(x + 20, y, "Instruction Pointer: 0x" + interruptStack->rip.ToString("X16") + "  (CS: 0x" + interruptStack->cs.ToString("X4") + ")", 0xFFFFFFFF);
            y += lineHeight;
            
            int cpl = (int)(interruptStack->cs & 3);
            DrawText(x + 20, y, "Privilege Level: Ring " + cpl + " " + (cpl == 0 ? "(Kernel)" : "(User)"), 0xFFFFFFFF);
            y += lineHeight;
            
            DrawText(x + 20, y, "Stack Pointer: 0x" + interruptStack->rsp.ToString("X16") + "  (SS: 0x" + interruptStack->ss.ToString("X4") + ")", 0xFFFFFFFF);
            y += lineHeight;
            
            DrawText(x + 20, y, "RFLAGS: 0x" + interruptStack->rflags.ToString("X16"), 0xFFFFFFFF);
            y += lineHeight;
            
            DrawRFlagsBreakdown(x + 40, ref y, lineHeight, interruptStack->rflags);
        }
        
        private static unsafe void DrawRegisters(ref int y, int x, int lineHeight, IDT.RegistersStack* regs, IDT.InterruptReturnStack* interruptStack) {
            DrawSectionHeader(x, ref y, "REGISTER DUMP", lineHeight);
            
            int col1X = x + 20;
            int col2X = x + 400;
            int startY = y;
            
            // Validate pointer before dereferencing
            if (regs == null) {
                DrawText(col1X, y, "Register data unavailable (invalid pointer)", 0xFF888888);
                y += lineHeight;
                return;
            }
            
            // Column 1
            DrawText(col1X, y, FormatRegister("RAX", regs->rax), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col1X, y, FormatRegister("RBX", regs->rbx), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col1X, y, FormatRegister("RCX", regs->rcx), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col1X, y, FormatRegister("RDX", regs->rdx), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col1X, y, FormatRegister("RSI", regs->rsi), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col1X, y, FormatRegister("RDI", regs->rdi), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col1X, y, "RBP: <not saved>", 0xFF888888);
            y += lineHeight;
            DrawText(col1X, y, FormatRegister("RSP", interruptStack->rsp), 0xFFFFFF00);
            y += lineHeight;
            
            // Column 2
            y = startY;
            DrawText(col2X, y, FormatRegister("R8 ", regs->r8), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col2X, y, FormatRegister("R9 ", regs->r9), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col2X, y, FormatRegister("R10", regs->r10), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col2X, y, FormatRegister("R11", regs->r11), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col2X, y, FormatRegister("R12", regs->r12), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col2X, y, FormatRegister("R13", regs->r13), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col2X, y, FormatRegister("R14", regs->r14), 0xFFFFFFFF);
            y += lineHeight;
            DrawText(col2X, y, FormatRegister("R15", regs->r15), 0xFFFFFFFF);
            y += lineHeight;
        }
        
        private static string FormatRegister(string name, ulong value) {
            // Build hex string manually to avoid any interpolation issues
            return name + ": 0x" + value.ToString("X16");
        }
        
        private static unsafe void DrawStackTrace(ref int y, int x, int lineHeight, IDT.InterruptReturnStack* interruptStack) {
            DrawSectionHeader(x, ref y, "STACK TRACE (Last 16 values)", lineHeight);
            
            ulong* stackPtr = (ulong*)interruptStack->rsp;
            int col1X = x + 20;
            int col2X = x + 400;
            int startY = y;
            
            for (int i = 0; i < 16; i++) {
                try {
                    ulong val = stackPtr[i];
                    int offset = i * 8;
                    string offsetStr = offset.ToString();
                    string line = "[RSP+0x" + (offset < 16 ? "0" : "") + offsetStr + "]: 0x" + val.ToString("X16");
                    
                    if (i < 8) {
                        DrawText(col1X, y, line, 0xFFCCCCCC);
                        y += lineHeight;
                    } else {
                        if (i == 8) y = startY;
                        DrawText(col2X, y, line, 0xFFCCCCCC);
                        y += lineHeight;
                    }
                } catch {
                    int offset = i * 8;
                    string offsetStr = offset.ToString();
                    string line = "[RSP+0x" + (offset < 16 ? "0" : "") + offsetStr + "]: <invalid memory>";
                    
                    if (i < 8) {
                        DrawText(col1X, y, line, 0xFF888888);
                        y += lineHeight;
                    } else {
                        if (i == 8) y = startY;
                        DrawText(col2X, y, line, 0xFF888888);
                        y += lineHeight;
                    }
                    break;
                }
            }
            
            y = startY + (8 * lineHeight);
        }
        
        private static void DrawSystemInfo(ref int y, int x, int lineHeight) {
            DrawSectionHeader(x, ref y, "SYSTEM INFORMATION", lineHeight);
            
            try {
                ulong ticks = Timer.Ticks;
                ulong uptimeSeconds = ticks / 1000;
                ulong minutes = uptimeSeconds / 60;
                ulong seconds = uptimeSeconds % 60;
                
                DrawText(x + 20, y, $"CPU: {SMP.ThisCPU} (SMP)  |  Ticks: {ticks}  |  Uptime: {minutes}m {seconds}s", 0xFFFFFFFF);
                y += lineHeight;
                
                // Timing analysis - check if crash happens at regular intervals
                if (uptimeSeconds > 300) {
                    DrawText(x + 20, y, "TIMING NOTE: Crash after 5+ minutes - check for:", 0xFFFFFF00);
                    y += lineHeight;
                    DrawText(x + 40, y, "- Timer overflow issues, memory leaks, or background tasks", 0xFFCCCCCC);
                    y += lineHeight;
                } else if (uptimeSeconds > 60 && uptimeSeconds < 120) {
                    DrawText(x + 20, y, "TIMING NOTE: Crash in 1-2 minute range - check initialization", 0xFFFFFF00);
                    y += lineHeight;
                }
                
                DrawText(x + 20, y, $"Framebuffer: {Framebuffer.Width}x{Framebuffer.Height} pixels", 0xFFFFFFFF);
                y += lineHeight;
                
                DrawText(x + 20, y, $"Interrupts: {(IDT.Initialized ? "Initialized" : "Not Initialized")}", 0xFFFFFFFF);
                y += lineHeight;
                
                // Try to get additional system info if available
                try {
                    DrawText(x + 20, y, $"Graphics: {Framebuffer.Graphics.Width}x{Framebuffer.Graphics.Height}", 0xFFFFFFFF);
                    y += lineHeight;
                } catch {
                    // Ignore if not available
                }
                
            } catch {
                DrawText(x + 20, y, "System information unavailable", 0xFF888888);
                y += lineHeight;
            }
        }
        
        private static void DrawTimingAnalysis(ref int y, int x, int lineHeight, int vector) {
            DrawSectionHeader(x, ref y, "TIMING & PATTERN ANALYSIS", lineHeight);
            
            try {
                ulong ticks = Timer.Ticks;
                ulong uptimeSeconds = ticks / 1000;
                
                // Analyze crash timing patterns
                if (uptimeSeconds >= 290 && uptimeSeconds <= 350) {
                    DrawText(x + 20, y, "PATTERN DETECTED: Crash at ~5-6min mark (" + uptimeSeconds + "s)", 0xFFFF0000);
                    y += lineHeight;
                    DrawText(x + 20, y, "This timing suggests (pre-existing issue):", 0xFFFFFF00);
                    y += lineHeight;
                    DrawText(x + 40, y, "1. BackgroundRotationManager interval (~5 min default)", 0xFFCCCCCC);
                    y += lineHeight;
                    DrawText(x + 40, y, "2. Cached icon refresh (Program.cs, 5 min interval)", 0xFFCCCCCC);
                    y += lineHeight;
                    DrawText(x + 40, y, "3. Timer tick overflow or counter rollover", 0xFFCCCCCC);
                    y += lineHeight;
                    DrawText(x + 40, y, "4. Memory leak from Image disposal issue", 0xFFCCCCCC);
                    y += lineHeight;
                    DrawText(x + 40, y, "5. ThreadPool stack corruption after 330k interrupts", 0xFFCCCCCC);
                    y += lineHeight;
                    
                    if (vector == 13) {
                        DrawText(x + 20, y, "GPF at this timing: Likely memory/pointer corruption", 0xFFFF8888);
                        y += lineHeight;
                        DrawText(x + 40, y, "- Image.Dispose not freeing memory correctly", 0xFFCCCCCC);
                        y += lineHeight;
                        DrawText(x + 40, y, "- Background rotation PNG loading/disposal issue", 0xFFCCCCCC);
                        y += lineHeight;
                        DrawText(x + 40, y, "- Desktop icon cache refresh corrupting heap", 0xFFCCCCCC);
                        y += lineHeight;
                    }
                    
                    DrawText(x + 20, y, "IMMEDIATE CHECKS (PRE-EXISTING BUG):", 0xFFFFFF00);
                    y += lineHeight;
                    DrawText(x + 40, y, "- Program.cs: Icon cache refresh at 5min", 0xFFCCCCCC);
                    y += lineHeight;
                    DrawText(x + 40, y, "- BackgroundRotationManager: PNG load/dispose cycle", 0xFFCCCCCC);
                    y += lineHeight;
                    DrawText(x + 40, y, "- Image.cs: Dispose() implementation", 0xFFCCCCCC);
                    y += lineHeight;
                    DrawText(x + 40, y, "- Allocator: Heap corruption from leak", 0xFFCCCCCC);
                    y += lineHeight;
                } else if (uptimeSeconds < 60) {
                    DrawText(x + 20, y, "Early crash (< 1 min) - likely initialization issue", 0xFFFFFF00);
                    y += lineHeight;
                } else {
                    DrawText(x + 20, y, "Crash at " + uptimeSeconds + "s - not the typical 5min pattern", 0xFFFFFFFF);
                    y += lineHeight;
                }
                
            } catch {
                DrawText(x + 20, y, "Timing analysis unavailable", 0xFF888888);
                y += lineHeight;
            }
        }
        
        private static void DrawControlRegisters(ref int y, int x, int lineHeight) {
            DrawSectionHeader(x, ref y, "CONTROL REGISTERS & MEMORY STATE", lineHeight);
            
            try {
                ulong cr2 = Native.ReadCR2();
                DrawText(x + 20, y, "CR2 (Page Fault Address): 0x" + cr2.ToString("X16"), 0xFFFFFFFF);
                y += lineHeight;
                
                // IDT and GDT info
                DrawText(x + 20, y, "IDTR: Base=0x" + IDT.idtr.Base.ToString("X16") + "  Limit=0x" + IDT.idtr.Limit.ToString("X4"), 0xFFFFFFFF);
                y += lineHeight;
                
                DrawText(x + 20, y, "GDTR: Base=0x" + GDT.gdtr.Base.ToString("X16") + "  Limit=0x" + GDT.gdtr.Limit.ToString("X4"), 0xFFFFFFFF);
                y += lineHeight;
                
                // Memory allocator info
                try {
                    DrawText(x + 20, y, "Heap Base: 0x20000000 (512MB region)", 0xFFFFFFFF);
                    y += lineHeight;
                } catch {
                    // Ignore if unavailable
                }
                
                // ThreadPool info if available
                try {
                    DrawText(x + 20, y, "ThreadPool: Check for corrupted thread state or stack overflow", 0xFFFFFF00);
                    y += lineHeight;
                } catch {
                    // Ignore if unavailable
                }
                
            } catch {
                DrawText(x + 20, y, "Control register info unavailable", 0xFF888888);
                y += lineHeight;
            }
        }
        
        private static void DrawRFlagsBreakdown(int x, ref int y, int lineHeight, ulong rflags) {
            string flags = $"CF={GetBit(rflags, 0)} PF={GetBit(rflags, 2)} AF={GetBit(rflags, 4)} ZF={GetBit(rflags, 6)} " +
                          $"SF={GetBit(rflags, 7)} TF={GetBit(rflags, 8)} IF={GetBit(rflags, 9)} DF={GetBit(rflags, 10)}";
            DrawText(x, y, flags, 0xFFCCCCCC);
            y += lineHeight;
            
            string flags2 = $"OF={GetBit(rflags, 11)} IOPL={((rflags >> 12) & 3)} NT={GetBit(rflags, 14)} RF={GetBit(rflags, 16)} " +
                           $"VM={GetBit(rflags, 17)} AC={GetBit(rflags, 18)} VIF={GetBit(rflags, 19)} VIP={GetBit(rflags, 20)} ID={GetBit(rflags, 21)}";
            DrawText(x, y, flags2, 0xFFCCCCCC);
            y += lineHeight;
        }
        
        private static void DrawSectionHeader(int x, ref int y, string title, int lineHeight) {
            int width = Framebuffer.Width - (x * 2);
            int height = lineHeight + 4;
            Framebuffer.Graphics.FillRectangle(x, y, width, height, 0xFF0000CC);
            DrawText(x + 10, y + 2, title, 0xFFFFFFFF);
            y += lineHeight + 8;
        }
        
        private static void DrawFooter() {
            int footerY = Framebuffer.Height - 120;
            int width = Framebuffer.Width;
            int height = 120;
            Framebuffer.Graphics.FillRectangle(0, footerY, width, height, 0xFF0000AA);
            
            DrawText(20, footerY + 5, "A critical error has occurred and the system has been halted.", 0xFFFFFFFF);
            DrawText(20, footerY + 23, "Please save this information and report it to the developers.", 0xFFFFFFFF);
            DrawText(20, footerY + 41, "Press RESET button or power cycle to restart the system.", 0xFFFFFFFF);
            
            // Extended troubleshooting details
            DrawText(20, footerY + 65, "TROUBLESHOOTING:", 0xFFFFFF00);
            DrawText(20, footerY + 83, "- Check for memory corruption or invalid pointer access", 0xFFCCCCCC);
            DrawText(20, footerY + 101, "- Review recent code changes and driver initialization", 0xFFCCCCCC);
        }
        
        private static void DrawText(int x, int y, string text, uint color) {
            try {
                if (guideXOS.GUI.WindowManager.font != null) {
                    // IFont doesn't support custom colors, it uses the colors from the font image
                    guideXOS.GUI.WindowManager.font.DrawString(x, y, text);
                }
            } catch {
                // Silently ignore drawing errors in panic handler
            }
        }
        
        private static void DrawTextLarge(int x, int y, string text, uint color) {
            // Draw larger text by drawing multiple times with offsets
            try {
                if (guideXOS.GUI.WindowManager.font != null) {
                    for (int dy = 0; dy < 2; dy++) {
                        for (int dx = 0; dx < 2; dx++) {
                            guideXOS.GUI.WindowManager.font.DrawString(x + dx, y + dy, text);
                        }
                    }
                }
            } catch {
                // Silently ignore drawing errors in panic handler
            }
        }
        
        private static string GetExceptionName(int vector) {
            return vector switch {
                0 => "DIVIDE BY ZERO",
                1 => "DEBUG / SINGLE STEP",
                2 => "NON-MASKABLE INTERRUPT",
                3 => "BREAKPOINT",
                4 => "OVERFLOW",
                5 => "BOUND RANGE EXCEEDED",
                6 => "INVALID OPCODE",
                7 => "DEVICE NOT AVAILABLE",
                8 => "DOUBLE FAULT",
                9 => "COPROCESSOR SEGMENT OVERRUN",
                10 => "INVALID TSS",
                11 => "SEGMENT NOT PRESENT",
                12 => "STACK SEGMENT FAULT",
                13 => "GENERAL PROTECTION FAULT",
                14 => "PAGE FAULT",
                16 => "x87 FLOATING POINT EXCEPTION",
                17 => "ALIGNMENT CHECK",
                18 => "MACHINE CHECK",
                19 => "SIMD FLOATING POINT EXCEPTION",
                20 => "VIRTUALIZATION EXCEPTION",
                21 => "CONTROL PROTECTION EXCEPTION",
                _ => $"UNKNOWN EXCEPTION (0x{vector:X2})"
            };
        }
        
        private static int GetBit(ulong value, int bit) {
            return (int)((value >> bit) & 1);
        }
    }
}