; hello.asm - NASM source for hello.gxm
; Assemble with: nasm -f bin hello.asm -o hello.bin
; Then prepend 16-byte GXM header as done in the generated hello.gxm

BITS 32
org 0

; Entry returns RVA of message (relative to module base)
start:
    mov eax, 22
    ret

section .data
    message db "Hello from GuideXOS!", 0
