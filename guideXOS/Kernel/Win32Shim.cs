using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using guideXOS.GUI;

namespace guideXOS.Compat {
    // Minimal Win32 API shim for imported PE binaries.
    // Provides a tiny set of KERNEL32/USER32 exports and stubs for everything else.
    public static unsafe class Win32Shim {
        // Resolve returns a function pointer to be written into the IAT, or 0 if unsupported.
        public static ulong Resolve(string dllName, string procName) {
            if (string.IsNullOrEmpty(dllName) || string.IsNullOrEmpty(procName)) return 0UL;
            string d = Normalize(dllName);
            string p = Normalize(procName);

            // KERNEL32
            if (d == "KERNEL32.DLL" || d == "KERNEL32") {
                switch (p) {
                    case "SLEEP": return (ulong)(nint)(delegate*<uint, void>)&Sleep;
                    case "SLEEPEX": return (ulong)(nint)(delegate*<uint, int, int, void>)&SleepEx;
                    case "GETTICKCOUNT": return (ulong)(nint)(delegate*<uint>)&GetTickCount;
                    case "GETTICKCOUNT64": return (ulong)(nint)(delegate*<ulong>)&GetTickCount64;
                    case "EXITPROCESS": return (ulong)(nint)(delegate*<uint, void>)&ExitProcess;
                    case "GETLASTERROR": return (ulong)(nint)(delegate*<uint>)&GetLastError;
                    // Heap/Virtual stubs
                    case "LOCALALLOC": return (ulong)(nint)(delegate*<uint, nuint, void*>)&LocalAlloc;
                    case "LOCALFREE": return (ulong)(nint)(delegate*<void*, void*>)&LocalFree;
                    case "GLOBALALLOC": return (ulong)(nint)(delegate*<uint, nuint, void*>)&GlobalAlloc;
                    case "GLOBALFREE": return (ulong)(nint)(delegate*<void*, void*>)&GlobalFree;
                }
            }
            // USER32
            if (d == "USER32.DLL" || d == "USER32") {
                switch (p) {
                    case "MESSAGEBOXA": return (ulong)(nint)(delegate*<void*, sbyte*, sbyte*, uint, int>)&MessageBoxA;
                    case "CREATEWINDOWEXA": return (ulong)(nint)(delegate*<ulong, sbyte*, sbyte*, ulong, int, int, int, int, void*, void*, void*, void*, void*>)&CreateWindowExA;
                    case "SHOWWINDOW": return (ulong)(nint)(delegate*<void*, int, int>)&ShowWindow;
                    case "DESTROYWINDOW": return (ulong)(nint)(delegate*<void*, int>)&DestroyWindow;
                }
            }
            // GDI32 (stubs for now)
            if (d == "GDI32.DLL" || d == "GDI32") {
                switch (p) {
                    case "TEXTOUTA": return (ulong)(nint)(delegate*<void*, int, int, sbyte*, int, int>)&TextOutA;
                }
            }
            return 0UL; // unresolved -> caller will write a stub
        }

        private static string Normalize(string s) {
            // Normalize by trimming ASCII spaces and converting to upper ASCII only
            if (s == null) return string.Empty;
            int start = 0; int end = s.Length - 1;
            while (start <= end && s[start] == ' ') start++;
            while (end >= start && s[end] == ' ') end--;
            int len = end - start + 1; if (len <= 0) return string.Empty;
            char[] buf = new char[len];
            for (int i = 0; i < len; i++) {
                char c = s[start + i];
                if (c >= 'a' && c <= 'z') c = (char)(c - 32);
                buf[i] = c;
            }
            return new string(buf);
        }

        // ----- Helpers -----
        private static string AnsiZToString(sbyte* p) {
            if (p == null) return string.Empty;
            int len = 0; for (sbyte* q = p; *q != 0; q++) len++;
            char[] chars = new char[len];
            for (int i = 0; i < len; i++) chars[i] = (char)(byte)p[i];
            return new string(chars);
        }

        private static volatile uint _lastError;

        private static uint GetLastError() => _lastError;

        private static void SetLastError(uint err) { _lastError = err; }

        // ----- KERNEL32 exports -----
        private static void Sleep(uint dwMilliseconds) {
            API.API_Sleep(dwMilliseconds);
        }

        private static void SleepEx(uint dwMilliseconds, int bAlertable, int unused = 0) {
            API.API_Sleep(dwMilliseconds);
        }

        private static uint GetTickCount() {
            // Constrain to 32-bit wrap
            return (uint)(API.API_GetTick() & 0xFFFFFFFF);
        }

        private static ulong GetTickCount64() {
            return API.API_GetTick();
        }

        private static void ExitProcess(uint uExitCode) {
            // No real process model yet; just log and return
            BootConsole.WriteLine($"[Win32Shim] ExitProcess({uExitCode}) called");
        }

        private static void* LocalAlloc(uint uFlags, nuint uBytes) {
            // Ignore flags; return zeroed memory. Clamp absurd sizes to avoid OOM crashes.
            if ((long)uBytes <= 0 || (ulong)uBytes > (64UL * 1024 * 1024)) { SetLastError(0x0000000E); return null; } // ERROR_OUTOFMEMORY
            return API.API_Calloc(1, (ulong)uBytes);
        }

        private static void* LocalFree(void* hMem) {
            if (hMem != null) API.API_Free((nint)hMem);
            return null;
        }

        private static void* GlobalAlloc(uint uFlags, nuint uBytes) {
            if ((long)uBytes <= 0 || (ulong)uBytes > (64UL * 1024 * 1024)) { SetLastError(0x0000000E); return null; }
            return API.API_Calloc(1, (ulong)uBytes);
        }

        private static void* GlobalFree(void* hMem) {
            if (hMem != null) API.API_Free((nint)hMem);
            return null;
        }

        // ----- USER32 exports -----
        private static int MessageBoxA(void* hWnd, sbyte* lpText, sbyte* lpCaption, uint uType) {
            // Avoid any dependency on System.Windows.Forms inside the OS kernel.
            // Just log to console and return IDOK (1).
            try {
                string text = AnsiZToString(lpText);
                string caption = AnsiZToString(lpCaption);
                if (!string.IsNullOrEmpty(caption)) BootConsole.WriteLine($"[MessageBoxA] {caption}: {text}");
                else BootConsole.WriteLine($"[MessageBoxA] {text}");
            } catch { }
            return 1;
        }

        private static void* CreateWindowExA(ulong dwExStyle, sbyte* lpClassName, sbyte* lpWindowName, ulong dwStyle,
            int X, int Y, int nWidth, int nHeight, void* hWndParent, void* hMenu, void* hInstance, void* lpParam) {
#if Kernel && HasGUI
            try {
                string title = AnsiZToString(lpWindowName);
                var handle = API.API_CreateWindow(X, Y, (nWidth <= 0 ? 320 : nWidth), (nHeight <= 0 ? 240 : nHeight), title);
                return (void*)handle;
            } catch {
                SetLastError(0x00000057); // ERROR_INVALID_PARAMETER
                return null;
            }
#else
            return null;
#endif
        }

        private static int ShowWindow(void* hWnd, int nCmdShow) {
            // No-op for now
            return 1;
        }

        private static int DestroyWindow(void* hWnd) {
            // Not tracked yet
            return 1;
        }

        // ----- GDI32 stubs -----
        private static int TextOutA(void* hdc, int x, int y, sbyte* lpString, int c) {
            // Not implemented; pretend success
            return 1;
        }

        // Fallback stub used when import cannot be resolved
        public static ulong NotImplementedStub() {
            BootConsole.WriteLine("[Win32Shim] Called unresolved import");
            return 0UL;
        }
    }
}
