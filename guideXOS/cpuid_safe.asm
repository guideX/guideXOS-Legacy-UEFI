; cpuid_safe.asm - Safer CPUID implementation using pointers
; This version explicitly takes a pointer parameter to avoid ABI issues

[BITS 64]

section .text

global cpuid_supported
global cpuid_safe

;-----------------------------------------------------------------------------
; bool cpuid_supported()
;-----------------------------------------------------------------------------
cpuid_supported:
    mov eax, 1
    ret

;-----------------------------------------------------------------------------
; void cpuid_safe(CPUID* result, uint32_t leaf, uint32_t subleaf)
; 
; Parameters:
;   RCX = pointer to CPUID result struct
;   RDX = leaf
;   R8  = subleaf
;-----------------------------------------------------------------------------
cpuid_safe:
    push rbx                ; Save RBX
    push rcx                ; Save result pointer
    
    mov eax, edx            ; EAX = leaf
    mov ecx, r8d            ; ECX = subleaf
    
    cpuid                   ; Execute CPUID
    
    pop rcx                 ; Restore result pointer
    
    ; Store all results
    mov [rcx], eax          ; result->EAX
    mov [rcx+4], ebx        ; result->EBX  
    mov [rcx+8], ecx        ; result->ECX
    mov [rcx+12], edx       ; result->EDX
    
    pop rbx                 ; Restore RBX
    ret
