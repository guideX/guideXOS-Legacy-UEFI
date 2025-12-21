; Minimal UEFI -> kernel handoff trampoline (x86_64, MS x64 ABI)
; Assembled with NASM: nasm -f win64 handoff_trampoline.asm -o handoff_trampoline.obj
;
; Extern C signature:
;   void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
;
; Behavior:
;   - cli
;   - Save parameters BEFORE any stack/CR3 changes
;   - Load cr3 = pml4Phys (BEFORE stack switch, since trampoline must be mapped)
;   - rsp = stackTop (16-byte aligned)
;   - jmp kernelEntry(bootInfo) using MS x64 ABI (RCX=bootInfo, 32-byte shadow space)
;
; CRITICAL: The trampoline code AND the new stack must both be identity-mapped
;           in the new page tables before calling this function!

BITS 64
DEFAULT REL

global BootHandoffTrampoline
global SetupTrampoline
global GetTrampolineCodeSize

section .text

; void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
BootHandoffTrampoline:
    ; Windows x64 calling convention on entry:
    ;   RCX = kernelEntry
    ;   RDX = bootInfo
    ;   R8  = stackTop
    ;   R9  = pml4Phys

    cli                         ; Disable interrupts - no going back

    ; === Stage 1: Save all parameters in non-volatile registers FIRST ===
    ; We must do this BEFORE touching stack or CR3!
    mov r12, rcx                ; r12 = kernelEntry
    mov r13, rdx                ; r13 = bootInfo
    mov r14, r8                 ; r14 = stackTop
    mov r15, r9                 ; r15 = pml4Phys

    ; --- Breadcrumb: 'T' = Trampoline entry ---
    mov dx, 03F8h
.wait_T:
    add dx, 5                   ; 0x3FD = line status
    in al, dx
    test al, 20h
    jz .wait_T
    sub dx, 5                   ; back to 0x3F8
    mov al, 'T'
    out dx, al

    ; === Stage 2: Load CR3 with new page tables ===
    ; Do this BEFORE stack switch! The trampoline code is executing from
    ; memory that must be identity-mapped in both old and new page tables.
    test r15, r15               ; Check if pml4Phys is NULL
    jz .skip_cr3                ; If NULL, keep current page tables

    mov rax, r15
    mov cr3, rax                ; Load new page tables (TLB flush)

    ; --- Breadcrumb: '3' = CR3 loaded ---
    mov dx, 03F8h
.wait_3:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_3
    sub dx, 5
    mov al, '3'
    out dx, al

.skip_cr3:
    ; === Stage 3: Switch to new stack ===
    ; Now that we have new page tables, switch to the new stack
    ; (which must be mapped in the new page tables)
    mov rsp, r14                ; rsp = stackTop
    and rsp, ~0Fh               ; Ensure 16-byte alignment

    ; --- Breadcrumb: 'S' = Stack switched ---
    mov dx, 03F8h
.wait_S:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_S
    sub dx, 5
    mov al, 'S'
    out dx, al

    ; === Stage 4: Set up kernel call ===
    ; MS x64 ABI: RCX = first parameter (bootInfo)
    mov rcx, r13                ; rcx = bootInfo

    ; Allocate 32-byte shadow space (required by MS x64 ABI)
    ; Stack is 16-byte aligned, sub 32 keeps it aligned
    sub rsp, 20h

    ; Clear other parameter registers (not strictly necessary but clean)
    xor rdx, rdx
    xor r8, r8
    xor r9, r9

    ; --- Breadcrumb: 'J' = About to Jump ---
    mov dx, 03F8h
.wait_J:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_J
    sub dx, 5
    mov al, 'J'
    out dx, al
    mov al, 0Dh                 ; CR
    out dx, al
    mov al, 0Ah                 ; LF
    out dx, al

    ; === Stage 5: Jump to kernel ===
    ; Use JMP instead of CALL because the kernel should never return
    ; (and after CR3 switch, we might not be mapped for a return anyway)
    jmp r12

    ; === PANIC: Should never reach here ===
.hang:
    mov dx, 03F8h
.wait_X:
    add dx, 5
    in al, dx
    test al, 20h
    jz .wait_X
    sub dx, 5
    mov al, '!'
    out dx, al
    
    hlt
    jmp .hang

; Stub functions for compatibility with trampoline_msvc.cpp interface
; (These are no-ops since the code is already in executable memory)
SetupTrampoline:
    ret

GetTrampolineCodeSize:
    mov rax, 256                ; Return a reasonable size estimate
    ret
