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

; === BOOT DEBUG HELPER ===
; This is called at the VERY START of KMain to prove we entered the kernel.
; It writes directly to serial port and framebuffer without any managed code.
; SerialDebugMarker() - writes 'K' to COM1 (0x3F8) and magenta pixel to framebuffer
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
    
    ; Write MAGENTA pixel to framebuffer at y=50, x=0
    ; Framebuffer = 0x80000000, y=50 with pitch 1280 = offset 50*1280*4 = 256000 = 0x3E800
    mov rax, 0x80000000
    add rax, 0x3E800        ; y=50, x=0
    mov dword [rax], 0x00FF00FF     ; MAGENTA pixel
    mov dword [rax+4], 0x00FF00FF   ; 2nd pixel
    mov dword [rax+8], 0x00FF00FF   ; 3rd pixel
    mov dword [rax+12], 0x00FF00FF  ; 4th pixel
    mov dword [rax+16], 0x00FF00FF  ; 5th pixel
    
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
