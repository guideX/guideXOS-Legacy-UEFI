// trampoline_msvc.cpp
// Trampoline using embedded machine code
// Works with MSVC x64 without inline assembly

#include <Uefi.h>
#include <stdint.h>

// MSVC intrinsics
extern "C" {
    void __halt(void);
}
#pragma intrinsic(__halt)

// Declare the function type for the trampoline
// MS x64 ABI: RCX=1st, RDX=2nd, R8=3rd, R9=4th
typedef void (*TrampolineFunc)(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);

// Raw machine code for the final handoff sequence
// This is position-independent code that:
// 1. cli (disable interrupts)
// 2. Save kernel entry to r12, bootInfo to r13
// 3. Load CR3 from r9
// 4. Switch stack to r8
// 5. Set up MS x64 calling convention (RCX = bootInfo)
// 6. Jump to kernel
//
// Input:
//   RCX = kernelEntry
//   RDX = bootInfo
//   R8  = stackTop
//   R9  = pml4Phys

// The trampoline code bytes
// We allocate this at runtime in executable memory
static const uint8_t g_TrampolineCodeBytes[] = {
    // cli - disable interrupts
    0xFA,
    
    // mov r12, rcx  ; r12 = kernelEntry (save)
    0x49, 0x89, 0xCC,
    
    // mov r13, rdx  ; r13 = bootInfo (save)
    0x49, 0x89, 0xD5,
    
    // mov rax, r9   ; rax = pml4Phys
    0x4C, 0x89, 0xC8,
    
    // mov cr3, rax  ; load new page tables
    0x0F, 0x22, 0xD8,
    
    // mov rsp, r8   ; switch to new stack
    0x4C, 0x89, 0xC4,
    
    // and rsp, 0xFFFFFFFFFFFFFFF0 ; ensure 16-byte alignment
    0x48, 0x83, 0xE4, 0xF0,
    
    // sub rsp, 40   ; allocate shadow space (32) + 8 for alignment
    0x48, 0x83, 0xEC, 0x28,
    
    // mov rcx, r13  ; RCX = bootInfo (first parameter for kernel)
    0x4C, 0x89, 0xE9,
    
    // xor rdx, rdx  ; clear second param
    0x48, 0x31, 0xD2,
    
    // xor r8, r8    ; clear third param
    0x4D, 0x31, 0xC0,
    
    // xor r9, r9    ; clear fourth param  
    0x4D, 0x31, 0xC9,
    
    // jmp r12       ; jump to kernel (indirect through r12)
    0x41, 0xFF, 0xE4,
    
    // --- Should never reach here ---
    // hlt
    0xF4,
    
    // jmp $-1 (infinite loop back to hlt)
    0xEB, 0xFD
};

// Global pointer to executable copy of trampoline
// This must be set up before calling BootHandoffTrampoline
static TrampolineFunc g_ExecutableTrampoline = nullptr;

// Call this during bootloader init to set up the trampoline
// Pass a pointer to executable memory that will persist after ExitBootServices
extern "C" void SetupTrampoline(void* executableMemory)
{
    // Copy the trampoline code to executable memory
    for (UINTN i = 0; i < sizeof(g_TrampolineCodeBytes); ++i) {
        ((uint8_t*)executableMemory)[i] = g_TrampolineCodeBytes[i];
    }
    
    // Save the executable pointer
    g_ExecutableTrampoline = (TrampolineFunc)executableMemory;
}

// This function transfers control to the kernel
// It will NOT return
// IMPORTANT: SetupTrampoline() must be called first!
extern "C" void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys)
{
    if (g_ExecutableTrampoline == nullptr) {
        // Fallback: try to execute from read-only memory
        // This might work on some UEFI implementations
        g_ExecutableTrampoline = (TrampolineFunc)(void*)g_TrampolineCodeBytes;
    }
    
    // Call the trampoline code
    // Parameters are already in the right registers per MS x64 ABI
    // This call WILL NOT RETURN
    g_ExecutableTrampoline(kernelEntry, bootInfo, stackTop, pml4Phys);
    
    // Should never reach here - but just in case
    for (;;) {
        __halt();
    }
}

// Return the size of the trampoline code for allocation purposes
extern "C" UINTN GetTrampolineCodeSize(void)
{
    return sizeof(g_TrampolineCodeBytes);
}
