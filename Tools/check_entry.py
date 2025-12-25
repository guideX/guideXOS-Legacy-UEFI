#!/usr/bin/env python3
"""Check the entry point of a PE file."""
import sys

pe_path = sys.argv[1] if len(sys.argv) > 1 else r'D:\devgitlab\guideXOS\guideXOS.UEFI\bin\Release\net7.0\win-x64\native\guideXOS.exe'
pe = open(pe_path, 'rb').read()

pe_off = int.from_bytes(pe[0x3C:0x40], 'little')
ep_rva = int.from_bytes(pe[pe_off+4+20+16:pe_off+4+20+20], 'little')
image_base = int.from_bytes(pe[pe_off+4+20+24:pe_off+4+20+32], 'little')
print(f'Image Base: 0x{image_base:X}')
print(f'Entry RVA: 0x{ep_rva:X}')
print(f'Entry VA: 0x{image_base + ep_rva:X}')

# Find the section containing entry point
opt_hdr_size = int.from_bytes(pe[pe_off+4+16:pe_off+4+18], 'little')
sec_off = pe_off + 4 + 20 + opt_hdr_size
num_secs = int.from_bytes(pe[pe_off+4+2:pe_off+4+4], 'little')

for i in range(num_secs):
    off = sec_off + i * 40
    name = pe[off:off+8].split(b'\x00')[0].decode()
    vsize = int.from_bytes(pe[off+8:off+12], 'little')
    vaddr = int.from_bytes(pe[off+12:off+16], 'little')
    raw_size = int.from_bytes(pe[off+16:off+20], 'little')
    raw_ptr = int.from_bytes(pe[off+20:off+24], 'little')
    if vaddr <= ep_rva < vaddr + vsize:
        file_off = raw_ptr + (ep_rva - vaddr)
        print(f'Entry in section {name} at file offset 0x{file_off:X}')
        bytes_at_entry = pe[file_off:file_off+32]
        print('First 32 bytes at entry:')
        print(' '.join(f'{b:02X}' for b in bytes_at_entry))
        
        # Try to decode as x64 instructions
        print('\nPossible x64 instructions:')
        if bytes_at_entry[0] == 0x55:
            print('  0x55 = push rbp (standard prologue)')
        if bytes_at_entry[0:2] == bytes([0x41, 0x55]):
            print('  41 55 = push r13')
        if bytes_at_entry[0:2] == bytes([0x41, 0x56]):
            print('  41 56 = push r14')
        if bytes_at_entry[0:2] == bytes([0x41, 0x57]):
            print('  41 57 = push r15')
        break
