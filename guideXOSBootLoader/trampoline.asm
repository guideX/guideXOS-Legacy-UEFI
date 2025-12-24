; trampoline.asm - Boot handoff trampoline for guideXOS
; Assembled with NASM: nasm -f win64 trampoline.asm -o trampoline.obj
;
; Microsoft x64 ABI (caller provides):
;   RCX = kernelEntry
;   RDX = bootInfo
;   R8  = stackTop
;   R9  = pml4Phys
;
; Kernel expects MS x64 ABI:
;   RCX = BootInfo*

BITS 64
DEFAULT REL

section .text

; Debug: Output character to COM1 serial port
%macro SERIAL_CHAR 1
    push    rax
    push    rdx
    mov     dx, 0x3FD           ; Line status register
%%wait_tx:
    in      al, dx
    test    al, 0x20            ; TX buffer empty?
    jz      %%wait_tx
    mov     dx, 0x3F8           ; Data register
    mov     al, %1
    out     dx, al
    pop     rdx
    pop     rax
%endmacro

global BootHandoffTrampoline
; void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
BootHandoffTrampoline:
    cli
    SERIAL_CHAR 'T'

    ; Preserve inputs across register setup
    mov     r12, rcx            ; kernelEntry
    mov     r13, rdx            ; bootInfo
    mov     r14, r8             ; stackTop
    mov     r15, r9             ; pml4Phys

    SERIAL_CHAR '1'

    ; Minimal validation
    test    r12, r12
    jz      .halt
    test    r13, r13
    jz      .halt
    test    r14, r14
    jz      .halt

    ; Load page tables (optional)
    test    r15, r15
    jz      .skip_cr3
    mov     rax, r15
    mov     cr3, rax
.skip_cr3:

    SERIAL_CHAR '3'

    ; Switch stack
    mov     rsp, r14
    and     rsp, ~0xF

    ; MS x64 ABI: at function entry after CALL, RSP%16==8.
    ; We JMP (no return address push), so subtract 40 to emulate that:
    ;  -32 shadow space
    ;  -8  fake return address
    sub     rsp, 40

    SERIAL_CHAR 'S'

    ; Set up kernel arguments
    mov     rcx, r13            ; RCX = BootInfo*

    ; Clear volatile regs (do NOT clobber r12)
    xor     rax, rax
    xor     rdx, rdx
    xor     r8,  r8
    xor     r9,  r9
    xor     r10, r10
    xor     r11, r11

    SERIAL_CHAR 'J'
    SERIAL_CHAR 10

    ; Enter kernel
    jmp     r12

.halt:
    SERIAL_CHAR '!'
    cli
    hlt
    jmp     .halt
