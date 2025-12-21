; Minimal UEFI -> kernel handoff trampoline (x86_64, MS x64 ABI)
; Assembled with NASM: nasm -f win64 handoff_trampoline.asm -o handoff_trampoline.obj
;
; Extern C signature:
;   void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
;
; Behavior:
;   - cli
;   - rsp = stackTop (16-byte aligned)
;   - load cr3 = pml4Phys
;   - call kernelEntry(bootInfo) using MS x64 ABI (RCX=bootInfo, 32-byte shadow space)

BITS 64
DEFAULT REL

global BootHandoffTrampoline

section .text

BootHandoffTrampoline:
    ; Windows x64 calling convention on entry:
    ;   RCX = kernelEntry
    ;   RDX = bootInfo
    ;   R8  = stackTop
    ;   R9  = pml4Phys

    cli

    ; --- Breadcrumb: reached trampoline ---
    mov dx, 03F8h
    mov al, 'T'
    out dx, al

    ; Switch stack (R8 expected 16-byte aligned by caller)
    mov rsp, r8

    ; --- Breadcrumb: stack switched ---
    mov dx, 03F8h
    mov al, 'S'
    out dx, al

    ; Load CR3 with new PML4 physical address
    mov rax, r9
    mov cr3, rax

    ; --- Breadcrumb: CR3 installed ---
    mov dx, 03F8h
    mov al, '3'
    out dx, al

    ; Call kernelEntry(bootInfo)
    mov rax, rcx            ; rax = kernelEntry
    mov rcx, rdx            ; rcx = bootInfo (1st arg)

    sub rsp, 20h            ; 32-byte shadow space required by MS x64 ABI
    call rax
    add rsp, 20h

.hang:
    hlt
    jmp .hang
