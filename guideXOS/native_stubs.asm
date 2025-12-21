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
; Signatures in C#: Out8(uint port, byte value), etc.
; Windows x64 ABI: RCX=port, RDX=value

global Out8
Out8:
    mov dx, cx
    mov al, dl
    out dx, al
    ret

global In8
In8:
    mov dx, cx
    xor eax, eax
    in al, dx
    ret

global Out16
Out16:
    mov dx, cx
    mov ax, dx
    out dx, ax
    ret

global In16
In16:
    mov dx, cx
    xor eax, eax
    in ax, dx
    ret

global Out32
Out32:
    mov dx, cx
    mov eax, edx
    out dx, eax
    ret

global In32
In32:
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
; Insw(ushort port, ushort* data, ulong count)
; Outsw(ushort port, ushort* data, ulong count)

global Insw
Insw:
    mov dx, cx
    mov rdi, rdx
    mov rcx, r8
    rep insw
    ret

global Outsw
Outsw:
    mov dx, cx
    mov rsi, rdx
    mov rcx, r8
    rep outsw
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
