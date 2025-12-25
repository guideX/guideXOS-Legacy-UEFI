#!/usr/bin/env python3
"""Dump ELF program headers."""
import struct
import sys

elf_path = sys.argv[1] if len(sys.argv) > 1 else r'D:\devgitlab\guideXOS\guideXOS.UEFI\ESP\kernel.elf'
elf = open(elf_path, 'rb').read()

e_phnum = struct.unpack_from('<H', elf, 56)[0]
print(f'Program headers: {e_phnum}')
for i in range(e_phnum):
    off = 64 + i * 56
    p_type, p_flags = struct.unpack_from('<II', elf, off)
    p_offset, p_vaddr, p_paddr, p_filesz, p_memsz = struct.unpack_from('<QQQQQ', elf, off+8)
    if p_type == 1:  # PT_LOAD
        print(f'  LOAD: vaddr=0x{p_vaddr:016X} filesz=0x{p_filesz:X} memsz=0x{p_memsz:X}')
