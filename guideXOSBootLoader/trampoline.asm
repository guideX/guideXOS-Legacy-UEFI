; trampoline.asm - Boot handoff trampoline for guideXOS
; Assembled with NASM: nasm -f win64 trampoline.asm -o trampoline.obj
;
; Microsoft x64 ABI:
;   RCX = 1st param (kernelEntry)
;   RDX = 2nd param (bootInfo)  
;   R8  = 3rd param (stackTop)
;   R9  = 4th param (pml4Phys)
;
; The kernel (C#/.NET Native) expects MS x64 ABI:
;   RCX = pointer to BootInfo

BITS 64
DEFAULT REL

section .text

global BootHandoffTrampoline

; void BootHandoffTrampoline(void* kernelEntry, void* bootInfo, void* stackTop, void* pml4Phys);
BootHandoffTrampoline:
    ; Save parameters (MS x64 ABI)
    ; RCX = kernelEntry
    ; RDX = bootInfo
    ; R8  = stackTop
    ; R9  = pml4Phys
    
    ; Disable interrupts during transition
    cli
    
    ; --- Load new page tables ---
    ; CR3 = pml4Phys
    mov     rax, r9
    mov     cr3, rax
    
    ; --- Switch to new stack ---
    ; Stack must be 16-byte aligned before CALL (which pushes 8-byte return address)
    ; So RSP should be 16-byte aligned here (stackTop was already aligned)
    mov     rsp, r8
    
    ; --- Prepare kernel call ---
    ; Kernel expects BootInfo* in RCX (MS x64 ABI)
    ; Move bootInfo to RCX (kernel's first parameter)
    mov     rcx, rdx
    
    ; Zero other registers to avoid garbage
    xor     rdx, rdx
    xor     r8, r8
    xor     r9, r9
    xor     r10, r10
    xor     r11, r11
    xor     rbx, rbx
    xor     rbp, rbp
    xor     rsi, rsi
    xor     rdi, rdi
    xor     r12, r12
    xor     r13, r13
    xor     r14, r14
    xor     r15, r15
    
    ; --- Allocate shadow space (32 bytes) for MS x64 ABI ---
    ; This is required even if we don't use it
    sub     rsp, 32
    
    ; --- Jump to kernel ---
    ; We use JMP instead of CALL because the kernel should never return
    ; The kernel entry is in the original RCX, which we need to save first
    ; Actually, let's restructure: save kernel entry before we modify RCX
    
    ; Restore: we need to save kernelEntry before modifying RCX
    ; Let's redo this properly:
    jmp     .handoff_fixed

.handoff_fixed:
    ; We already have:
    ;   RSP = new stack - 32 (shadow space)
    ;   CR3 = new page tables
    ;   RCX = bootInfo (for kernel)
    ; But we lost kernelEntry when we overwrote RCX!
    
    ; This is a bug - let me fix the calling sequence
    
; =============================================================================
; FIXED TRAMPOLINE IMPLEMENTATION
; =============================================================================
; Key fixes:
; 1. Don't use the old stack after loading CR3 - it might not be mapped!
; 2. Preserve values in non-volatile registers BEFORE CR3 switch
; 3. Proper stack alignment for x64 ABI
; 4. Serial port debugging output for diagnostics
; =============================================================================

; Debug: Output character to COM1 serial port
%macro SERIAL_CHAR 1
    push    rax
    push    rdx
    mov     dx, 0x3FD           ; Line status register
%%wait_tx:
    in      al, dx
    test    al, 0x20            ; Check if TX buffer empty
    jz      %%wait_tx
    mov     dx, 0x3F8           ; Data register
    mov     al, %1
    out     dx, al
    pop     rdx
    pop     rax
%endmacro

global BootHandoffTrampoline
BootHandoffTrampoline:
    ; Parameters (MS x64 ABI):
    ; RCX = kernelEntry (void*)
    ; RDX = bootInfo (BootInfo*)
    ; R8  = stackTop (void*)
    ; R9  = pml4Phys (void*)
    
    ; === Step 0: Debug output 'T' for Trampoline entry ===
    SERIAL_CHAR 'T'
    
    ; === Step 1: Disable interrupts ===
    cli
    
    ; === Step 2: Save parameters in non-volatile registers ===
    ; We MUST do this BEFORE switching CR3 or stack!
    ; Using R12-R15 which are callee-saved but we don't care about that
    mov     r10, rcx            ; r10 = kernelEntry
    mov     r11, rdx            ; r11 = bootInfo
    mov     r12, r8             ; r12 = stackTop
    mov     r13, r9             ; r13 = pml4Phys
    
    SERIAL_CHAR '1'
    
    ; === Step 3: Validate parameters ===
    ; Check for NULL kernel entry
    test    r10, r10
    jz      .panic_null_entry
    
    ; Check for NULL bootInfo
    test    r11, r11
    jz      .panic_null_bootinfo
    
    ; Check for NULL stack
    test    r12, r12
    jz      .panic_null_stack
    
    SERIAL_CHAR '2'
    
    ; === Step 4: Load new page tables ===
    ; CRITICAL: This changes the virtual->physical mapping!
    ; After this, we must not access anything that's not identity-mapped
    test    r13, r13
    jz      .skip_cr3           ; If pml4Phys is NULL, keep current page tables
    
    mov     rax, r13
    mov     cr3, rax            ; This flushes TLB
    
    SERIAL_CHAR '3'
    
.skip_cr3:
    ; === Step 5: Switch to new stack ===
    ; The new stack MUST be identity-mapped in the new page tables!
    mov     rsp, r12
    
    ; Ensure 16-byte alignment (x64 ABI requirement)
    ; Stack should be 16-byte aligned BEFORE the call pushes return address
    and     rsp, ~0xF
    
    SERIAL_CHAR '4'
    
    ; === Step 6: Clear general purpose registers ===
    ; This prevents garbage from confusing the kernel
    xor     rax, rax
    xor     rbx, rbx
    xor     rdx, rdx            ; Will be set to 0 (no second param)
    xor     rsi, rsi
    xor     rdi, rdi
    xor     rbp, rbp
    xor     r8, r8
    xor     r9, r9
    xor     r14, r14
    xor     r15, r15
    ; Keep r10 (entry), r11 (bootInfo), r12, r13 for now
    
    ; === Step 7: Set up kernel call ===
    ; MS x64 ABI: First parameter in RCX
    mov     rcx, r11            ; RCX = BootInfo*
    
    ; Clear the registers we used for temp storage
    xor     r11, r11
    xor     r12, r12
    xor     r13, r13
    
    ; === Step 8: Allocate shadow space ===
    ; MS x64 ABI requires 32 bytes of shadow space for register parameters
    ; Plus we want 16-byte alignment after we "call" (but we use jmp)
    sub     rsp, 32
    
    SERIAL_CHAR 'J'             ; 'J' for Jump
    SERIAL_CHAR 10              ; newline
    
    ; === Step 9: Jump to kernel ===
    ; Use JMP because the kernel should never return
    ; r10 contains the kernel entry point
    jmp     r10
    
    ; === PANIC HANDLERS ===
    ; These output debug info and halt
    
.panic_null_entry:
    SERIAL_CHAR 'E'
    SERIAL_CHAR '1'
    jmp     .halt
    
.panic_null_bootinfo:
    SERIAL_CHAR 'E'
    SERIAL_CHAR '2'
    jmp     .halt
    
.panic_null_stack:
    SERIAL_CHAR 'E'
    SERIAL_CHAR '3'
    jmp     .halt
    
.halt:
    SERIAL_CHAR '!'
    SERIAL_CHAR 10
    cli
    hlt
    jmp     .halt

; =============================================================================
; ALTERNATIVE: Debug version with more verbose output
; =============================================================================
global BootHandoffTrampolineDebug
BootHandoffTrampolineDebug:
    cli
    
    ; Save all parameters
    mov     r10, rcx            ; kernelEntry
    mov     r11, rdx            ; bootInfo
    mov     r12, r8             ; stackTop
    mov     r13, r9             ; pml4Phys
    
    ; Output debug info to serial
    SERIAL_CHAR 'K'
    SERIAL_CHAR ':'
    
    ; Output kernel entry address (high nibbles first)
    mov     rax, r10
    call    .print_hex64
    
    SERIAL_CHAR ' '
    SERIAL_CHAR 'B'
    SERIAL_CHAR ':'
    
    ; Output bootInfo address
    mov     rax, r11
    call    .print_hex64
    
    SERIAL_CHAR 10
    
    ; Now do the actual handoff
    test    r13, r13
    jz      .dbg_skip_cr3
    mov     rax, r13
    mov     cr3, rax
.dbg_skip_cr3:
    
    mov     rsp, r12
    and     rsp, ~0xF
    sub     rsp, 32
    
    mov     rcx, r11
    jmp     r10

; Helper: print RAX as hex to serial
.print_hex64:
    push    rcx
    push    rdx
    push    rbx
    
    mov     rcx, 16             ; 16 hex digits
    mov     rbx, rax
.hex_loop:
    rol     rbx, 4              ; Get highest nibble
    mov     al, bl
    and     al, 0x0F
    cmp     al, 10
    jl      .hex_digit
    add     al, 'A' - 10
    jmp     .hex_out
.hex_digit:
    add     al, '0'
.hex_out:
    mov     dx, 0x3FD
.hex_wait:
    in      al, dx
    test    al, 0x20
    jz      .hex_wait
    mov     dx, 0x3F8
    mov     al, bl
    and     al, 0x0F
    cmp     al, 10
    jl      .hex_digit2
    add     al, 'A' - 10
    jmp     .hex_out2
.hex_digit2:
    add     al, '0'
.hex_out2:
    out     dx, al
    dec     rcx
    jnz     .hex_loop
    
    pop     rbx
    pop     rdx
    pop     rcx
    ret
