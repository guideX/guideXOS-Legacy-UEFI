; native_stubs.asm
; Minimal native implementations to satisfy ILCompiler link on Windows.
; NOTE: These are STUBS (many are no-ops) to unblock the build.
; Proper implementations should replace these for a working kernel.

[BITS 64]

default rel

section .text

; Module pointer accessor for UEFI boot
; Returns the address of the __Module symbol (module table)
; This is used by KMain to initialize the NativeAOT runtime
extern __Module
global __modules_a
__modules_a:
    lea rax, [rel __Module]
    ret

; === KERNEL ENTRY WRAPPER ===
; This is the REAL entry point that the bootloader should call.
; It writes debug markers BEFORE any managed code runs, proving the jump succeeded.
; Then it calls the managed KMain function.
;
; Windows x64 ABI: RCX = bootInfo pointer
; We preserve RCX and pass it to KMain.
extern KMain
global KMainWrapper
KMainWrapper:
    ; === IMMEDIATE SERIAL OUTPUT ===
    ; Write 'WRAP' to COM1 to prove we got here
    ; Save all registers we'll use
    push rcx                ; Save bootInfo pointer (will be restored before jmp)
    push rdx
    push rax
    push r10
    push r11
    
    ; Write 'W'
    mov dx, 0x3F8
    mov al, 'W'
    out dx, al
    
    ; Write 'R'
    mov dx, 0x3FD
.wait_r:
    in al, dx
    test al, 0x20
    jz .wait_r
    mov dx, 0x3F8
    mov al, 'R'
    out dx, al
    
    ; Write 'A'
    mov dx, 0x3FD
.wait_a:
    in al, dx
    test al, 0x20
    jz .wait_a
    mov dx, 0x3F8
    mov al, 'A'
    out dx, al
    
    ; Write 'P'
    mov dx, 0x3FD
.wait_p:
    in al, dx
    test al, 0x20
    jz .wait_p
    mov dx, 0x3F8
    mov al, 'P'
    out dx, al
    
    ; Write '2' to mark this is the NEW code
    mov dx, 0x3FD
.wait_2:
    in al, dx
    test al, 0x20
    jz .wait_2
    mov dx, 0x3F8
    mov al, '2'
    out dx, al
    
    ; Write newline
    mov dx, 0x3FD
.wait_nl1:
    in al, dx
    test al, 0x20
    jz .wait_nl1
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl2:
    in al, dx
    test al, 0x20
    jz .wait_nl2
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; === Print KMain address ===
    ; Write 'J='
    mov dx, 0x3FD
.wait_j:
    in al, dx
    test al, 0x20
    jz .wait_j
    mov dx, 0x3F8
    mov al, 'J'
    out dx, al
    
    mov dx, 0x3FD
.wait_eq:
    in al, dx
    test al, 0x20
    jz .wait_eq
    mov dx, 0x3F8
    mov al, '='
    out dx, al
    
    ; Get KMain address and print it
    lea r10, [rel KMain]
    mov r11d, 8             ; 8 hex digits (low 32 bits)
.print_addr:
    rol r10d, 4
    mov al, r10b
    and al, 0x0F
    add al, '0'
    cmp al, '9'
    jbe .digit_ok
    add al, 7
.digit_ok:
    push rax
    mov dx, 0x3FD
.wait_digit:
    in al, dx
    test al, 0x20
    jz .wait_digit
    pop rax
    mov dx, 0x3F8
    out dx, al
    dec r11d
    jnz .print_addr
    
    ; Newline
    mov dx, 0x3FD
.wait_nl3:
    in al, dx
    test al, 0x20
    jz .wait_nl3
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl4:
    in al, dx
    test al, 0x20
    jz .wait_nl4
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; === Try to read first byte of KMain ===
    lea r10, [rel KMain]
    
    ; Write 'T' before trying to read (Test)
    mov dx, 0x3FD
.wait_t:
    in al, dx
    test al, 0x20
    jz .wait_t
    mov dx, 0x3F8
    mov al, 'T'
    out dx, al
    
    mov al, [r10]           ; Try to read first byte of KMain
    mov r11b, al            ; Save the byte
    
    ; Write 'R' = Read succeeded
    mov dx, 0x3FD
.wait_read:
    in al, dx
    test al, 0x20
    jz .wait_read
    mov dx, 0x3F8
    mov al, 'R'
    out dx, al
    
    ; Print the byte as 2 hex digits
    mov al, r11b
    shr al, 4
    add al, '0'
    cmp al, '9'
    jbe .high_ok
    add al, 7
.high_ok:
    push rax
    mov dx, 0x3FD
.wait_high:
    in al, dx
    test al, 0x20
    jz .wait_high
    pop rax
    mov dx, 0x3F8
    out dx, al
    
    mov al, r11b
    and al, 0x0F
    add al, '0'
    cmp al, '9'
    jbe .low_ok
    add al, 7
.low_ok:
    push rax
    mov dx, 0x3FD
.wait_low:
    in al, dx
    test al, 0x20
    jz .wait_low
    pop rax
    mov dx, 0x3F8
    out dx, al
    
    ; Newline before jump
    mov dx, 0x3FD
.wait_nl5:
    in al, dx
    test al, 0x20
    jz .wait_nl5
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl6:
    in al, dx
    test al, 0x20
    jz .wait_nl6
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; Write 'GO' before jump
    mov dx, 0x3FD
.wait_g:
    in al, dx
    test al, 0x20
    jz .wait_g
    mov dx, 0x3F8
    mov al, 'G'
    out dx, al
    
    mov dx, 0x3FD
.wait_o:
    in al, dx
    test al, 0x20
    jz .wait_o
    mov dx, 0x3F8
    mov al, 'O'
    out dx, al
    
    mov dx, 0x3FD
.wait_nl7:
    in al, dx
    test al, 0x20
    jz .wait_nl7
    mov dx, 0x3F8
    mov al, 0x0D
    out dx, al
    
    mov dx, 0x3FD
.wait_nl8:
    in al, dx
    test al, 0x20
    jz .wait_nl8
    mov dx, 0x3F8
    mov al, 0x0A
    out dx, al
    
    ; Restore registers
    pop r11
    pop r10
    pop rax
    pop rdx
    pop rcx                 ; Restore bootInfo pointer
    
    ; === JUMP TO MANAGED KMain ===
    ; RCX = bootInfo pointer (MS x64 ABI first argument)
    jmp KMain
    
    ; If KMain returns (shouldn't happen), halt
.hang:
    hlt
    jmp .hang

; === BOOT DEBUG HELPER ===
; This is called at the VERY START of KMain to prove we entered the kernel.
; It writes directly to serial port - framebuffer writes removed since we
; don't know the framebuffer address here without the bootInfo parameter.
; SerialDebugMarker() - writes 'K!' to COM1 (0x3F8)
global SerialDebugMarker
SerialDebugMarker:
    push rax
    push rdx
    
    ; Write 'K' to COM1 (0x3F8)
    mov dx, 0x3FD           ; Line status register
.wait_serial:
    in al, dx
    test al, 0x20           ; Check THRE bit
    jz .wait_serial
    mov dx, 0x3F8           ; Data register
    mov al, 'K'
    out dx, al
    
    ; Write '!' to show we're still going
    mov dx, 0x3FD
.wait_serial2:
    in al, dx
    test al, 0x20
    jz .wait_serial2
    mov dx, 0x3F8
    mov al, '!'
    out dx, al
    
    ; NOTE: Framebuffer write removed - we don't have bootInfo here
    ; The managed KMain code will do framebuffer writes after validating bootInfo
    
    pop rdx
    pop rax
    ret

global Hlt
Hlt:
    hlt
    ret

global Cli
Cli:
    cli
    ret

global Sti
Sti:
    sti
    ret

global Nop
Nop:
    nop
    ret

global Rdtsc
Rdtsc:
    rdtsc
    shl rdx, 32
    or rax, rdx
    ret

global ReadCR2
ReadCR2:
    mov rax, cr2
    ret

global WriteCR3
WriteCR3:
    mov cr3, rcx
    ret

global Invlpg
Invlpg:
    invlpg [rcx]
    ret

; Port IO
; Signatures in C#: Out8(ushort port, byte value), etc.
; Windows x64 ABI: RCX=port, RDX=value
; IMPORTANT: Must save RDX before clobbering it with the port!

global Out8
Out8:
    ; RCX = port, RDX = value
    mov eax, edx    ; Save value to EAX FIRST (before we clobber DX)
    mov dx, cx      ; Now move port to DX
    out dx, al      ; Output AL (value) to port DX
    ret

global In8
In8:
    ; RCX = port, returns byte in AL (zero-extended to EAX)
    mov dx, cx
    xor eax, eax
    in al, dx
    ret

global Out16
Out16:
    ; RCX = port, RDX = value (16-bit)
    mov eax, edx    ; Save value to EAX FIRST
    mov dx, cx      ; Now move port to DX
    out dx, ax      ; Output AX (value) to port DX
    ret

global In16
In16:
    ; RCX = port, returns word in AX (zero-extended to EAX)
    mov dx, cx
    xor eax, eax
    in ax, dx
    ret

global Out32
Out32:
    ; RCX = port, RDX = value (32-bit)
    mov eax, edx    ; Save value to EAX FIRST
    mov dx, cx      ; Now move port to DX
    out dx, eax     ; Output EAX (value) to port DX
    ret

global In32
In32:
    ; RCX = port, returns dword in EAX
    mov dx, cx
    in eax, dx
    ret

; rep helpers
; Stosb(void* p, byte value, ulong count) => RCX=p, RDX=value, R8=count

global Stosb
Stosb:
    mov rdi, rcx
    mov al, dl
    mov rcx, r8
    rep stosb
    ret

; Stosd(void* p, uint value, ulong count) => RCX=p, EDX=value, R8=count

global Stosd
Stosd:
    mov rdi, rcx
    mov eax, edx
    mov rcx, r8
    rep stosd
    ret

; Movsb(void* dest, void* source, ulong count) => RCX=dest, RDX=src, R8=count

global Movsb
Movsb:
    mov rdi, rcx
    mov rsi, rdx
    mov rcx, r8
    rep movsb
    ret

; Movsd(uint* dest, uint* source, ulong count) => RCX=dest, RDX=src, R8=count

global Movsd
Movsd:
    mov rdi, rcx
    mov rsi, rdx
    mov rcx, r8
    rep movsd
    ret

; Descriptor table loaders. Expect pointer refs passed by ref in C#.

global Load_GDT
Load_GDT:
    lgdt [rcx]
    ret

global Load_IDT
Load_IDT:
    lidt [rcx]
    ret

; The following are currently stubbed as no-ops or trivial returns.
; They must be replaced with real implementations for a functional OS.

global set_idt_entries
set_idt_entries:
    ret

global enable_sse
enable_sse:
    ; enable SSE: set CR0/CR4 bits minimally
    mov rax, cr0
    and rax, 0xFFFFFFFFFFFFFFFB  ; clear EM
    or  rax, 0x2                 ; set MP
    mov cr0, rax
    mov rax, cr4
    or  rax, (1<<9) | (1<<10)    ; OSFXSR | OSXMMEXCPT
    mov cr4, rax
    ret

global vmware_send
vmware_send:
    xor eax, eax
    ret

; insw/outsw stubs (PIO string ops)
; Windows x64 ABI: RCX=port, RDX=data ptr, R8=count
; Insw(ushort port, ushort* data, ulong count)
; Outsw(ushort port, ushort* data, ulong count)

global Insw
Insw:
    ; RCX = port, RDX = data pointer, R8 = count
    push rdi            ; Save RDI (callee-saved in Windows ABI)
    mov rdi, rdx        ; RDI = data buffer (destination for insw)
    mov rdx, rcx        ; DX = port (insw uses DX for port)
    mov rcx, r8         ; RCX = count for rep
    rep insw
    pop rdi             ; Restore RDI
    ret

global Outsw
Outsw:
    ; RCX = port, RDX = data pointer, R8 = count
    push rsi            ; Save RSI (callee-saved in Windows ABI)
    mov rsi, rdx        ; RSI = data buffer (source for outsw)
    mov rdx, rcx        ; DX = port (outsw uses DX for port)
    mov rcx, r8         ; RCX = count for rep
    rep outsw
    pop rsi             ; Restore RSI
    ret

; misc helpers

global mystrtoul
mystrtoul:
    xor eax, eax
    ret

global lodepng_decode_memory
lodepng_decode_memory:
    mov eax, 1
    ret

global Schedule_Next
Schedule_Next:
    ret
