; minimal.asm - NASM source for minimal.gxm
; Assemble with: nasm -f bin minimal.asm -o minimal.bin
; Then prepend 16-byte GXM header as done in the generated minimal.gxm

BITS 32
org 0

start:
    xor eax, eax
    ret
