// debug_helpers.h
// Post-ExitBootServices debugging helpers for GuideXOS
// These only use CPU instructions and direct memory writes - no UEFI calls!

#pragma once

#include <Uefi.h>
#include <stdint.h>
#include "guidexOSBootInfo.h"

// MSVC intrinsics for port I/O
extern "C" {
    unsigned char __inbyte(unsigned short port);
    void __outbyte(unsigned short port, unsigned char value);
    unsigned __int64 __readcr0(void);
    unsigned __int64 __readcr3(void);
    unsigned __int64 __readcr4(void);
    unsigned __int64 __readmsr(unsigned long msr);
}
#pragma intrinsic(__inbyte, __outbyte, __readcr0, __readcr3, __readcr4, __readmsr)

namespace guideXOS {
namespace debug {

// Serial port I/O for debugging (COM1 = 0x3F8)
static inline void SerialInit() {
    // Disable interrupts
    __outbyte(0x3F9, 0x00);
    // Enable DLAB (set baud rate divisor)
    __outbyte(0x3FB, 0x80);
    // Set divisor to 1 (115200 baud)
    __outbyte(0x3F8, 0x01);
    __outbyte(0x3F9, 0x00);
    // 8 bits, no parity, one stop bit
    __outbyte(0x3FB, 0x03);
    // Enable FIFO
    __outbyte(0x3FA, 0xC7);
    // Enable IRQs, set RTS/DSR
    __outbyte(0x3FC, 0x0B);
}

static inline bool SerialReady() {
    return (__inbyte(0x3FD) & 0x20) != 0;
}

static inline void SerialPutChar(char c) {
    while (!SerialReady()) {
        // Spin wait
    }
    __outbyte(0x3F8, (unsigned char)c);
}

static inline void SerialPrint(const char* s) {
    if (!s) return;
    while (*s) {
        if (*s == '\n') {
            SerialPutChar('\r');
        }
        SerialPutChar(*s++);
    }
}

static inline void SerialPrintHex64(uint64_t v) {
    const char* hex = "0123456789ABCDEF";
    SerialPrint("0x");
    for (int i = 60; i >= 0; i -= 4) {
        SerialPutChar(hex[(v >> i) & 0xF]);
    }
}

static inline void SerialPrintHex32(uint32_t v) {
    const char* hex = "0123456789ABCDEF";
    SerialPrint("0x");
    for (int i = 28; i >= 0; i -= 4) {
        SerialPutChar(hex[(v >> i) & 0xF]);
    }
}

// Draw a colored pixel directly to framebuffer
static inline void DrawPixel(uint32_t* fb, uint32_t pitch, uint32_t x, uint32_t y, uint32_t color) {
    fb[y * pitch + x] = color;
}

// Fill a rectangle on the framebuffer
static inline void FillRect(uint32_t* fb, uint32_t pitch, uint32_t x, uint32_t y, 
                            uint32_t w, uint32_t h, uint32_t color) {
    for (uint32_t py = y; py < y + h; ++py) {
        for (uint32_t px = x; px < x + w; ++px) {
            fb[py * pitch + px] = color;
        }
    }
}

// Show progress bar on framebuffer
// stage: 0-9, each stage shows a different colored block
static inline void ShowProgress(const BootInfo* bi, int stage) {
    if (!bi || !(bi->Flags & (1u << 1))) return;
    
    uint32_t* fb = (uint32_t*)(uintptr_t)bi->FramebufferBase;
    uint32_t pitch = bi->FramebufferPitch / 4;
    
    // Colors for each stage
    uint32_t colors[] = {
        0x00FF0000, // Red
        0x00FF7F00, // Orange
        0x00FFFF00, // Yellow
        0x007FFF00, // Yellow-green
        0x0000FF00, // Green
        0x0000FF7F, // Cyan-green
        0x0000FFFF, // Cyan
        0x00007FFF, // Light blue
        0x000000FF, // Blue
        0x00FFFFFF  // White
    };
    
    if (stage >= 0 && stage < 10) {
        // Draw a 50x50 square at (stage * 60 + 10, 10)
        uint32_t x = stage * 60 + 10;
        FillRect(fb, pitch, x, 10, 50, 50, colors[stage]);
    }
}

// Verify CPU is in expected state after ExitBootServices
static inline void VerifyCpuState() {
    uint64_t cr0 = __readcr0();
    uint64_t cr3 = __readcr3();
    uint64_t cr4 = __readcr4();
    uint64_t efer = __readmsr(0xC0000080); // IA32_EFER
    
    SerialPrint("\n=== CPU State Check ===\n");
    SerialPrint("CR0 = "); SerialPrintHex64(cr0); SerialPrint("\n");
    SerialPrint("CR3 = "); SerialPrintHex64(cr3); SerialPrint("\n");
    SerialPrint("CR4 = "); SerialPrintHex64(cr4); SerialPrint("\n");
    SerialPrint("EFER = "); SerialPrintHex64(efer); SerialPrint("\n");
    
    // Check for expected bits
    if (cr0 & (1ULL << 31)) {
        SerialPrint("  [OK] CR0.PG (Paging) enabled\n");
    } else {
        SerialPrint("  [FAIL] CR0.PG (Paging) DISABLED!\n");
    }
    
    if (cr0 & 1) {
        SerialPrint("  [OK] CR0.PE (Protected Mode) enabled\n");
    } else {
        SerialPrint("  [FAIL] CR0.PE (Protected Mode) DISABLED!\n");
    }
    
    if (cr4 & (1ULL << 5)) {
        SerialPrint("  [OK] CR4.PAE enabled\n");
    } else {
        SerialPrint("  [FAIL] CR4.PAE DISABLED!\n");
    }
    
    if (efer & (1ULL << 8)) {
        SerialPrint("  [OK] EFER.LME (Long Mode Enable) set\n");
    } else {
        SerialPrint("  [FAIL] EFER.LME DISABLED!\n");
    }
    
    if (efer & (1ULL << 10)) {
        SerialPrint("  [OK] EFER.LMA (Long Mode Active) set\n");
    } else {
        SerialPrint("  [FAIL] EFER.LMA DISABLED - NOT IN 64-BIT MODE!\n");
    }
}

// Check if an address is readable by attempting to read from it
static inline bool IsAddressReadable(uint64_t addr) {
    // This is a simple heuristic - in a real system you'd check the page tables
    // For now, just check if it's in a reasonable range
    if (addr == 0) return false;
    if (addr < 0x1000) return false; // Low memory often unmapped
    return true;
}

// Validate kernel entry point looks sane
static inline void ValidateKernelEntry(uint64_t entryPhys, const BootInfo* bi) {
    SerialPrint("\n=== Kernel Entry Validation ===\n");
    SerialPrint("Entry point: "); SerialPrintHex64(entryPhys); SerialPrint("\n");
    
    // Try to read first few bytes at entry point
    uint8_t* entry = (uint8_t*)(uintptr_t)entryPhys;
    
    SerialPrint("First 16 bytes at entry:\n  ");
    for (int i = 0; i < 16; ++i) {
        uint8_t b = entry[i];
        const char* hex = "0123456789ABCDEF";
        SerialPutChar(hex[(b >> 4) & 0xF]);
        SerialPutChar(hex[b & 0xF]);
        SerialPutChar(' ');
    }
    SerialPrint("\n");
    
    // Check for common prologue patterns
    // x86-64 function prologue often starts with: push rbp (0x55) or sub rsp (0x48 0x83 0xEC)
    if (entry[0] == 0x55 || (entry[0] == 0x48 && entry[1] == 0x83)) {
        SerialPrint("  [OK] Looks like valid x64 function prologue\n");
    } else if (entry[0] == 0x00 && entry[1] == 0x00) {
        SerialPrint("  [WARN] Entry point appears to be zeros - kernel not loaded?\n");
    } else {
        SerialPrint("  [INFO] Unknown prologue pattern\n");
    }
}

} // namespace debug
} // namespace guideXOS
