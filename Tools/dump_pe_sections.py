#!/usr/bin/env python3
"""Dump PE sections."""
import sys

pe_path = sys.argv[1] if len(sys.argv) > 1 else r'D:\devgitlab\guideXOS\guideXOS.UEFI\bin\Release\net7.0\win-x64\native\guideXOS.exe'
pe = open(pe_path, 'rb').read()

pe_off = int.from_bytes(pe[0x3C:0x40], 'little')
opt_hdr_size = int.from_bytes(pe[pe_off+4+16:pe_off+4+18], 'little')
sec_off = pe_off + 4 + 20 + opt_hdr_size
num_secs = int.from_bytes(pe[pe_off+4+2:pe_off+4+4], 'little')
image_base = int.from_bytes(pe[pe_off+4+20+24:pe_off+4+20+32], 'little')

print(f'Image Base: 0x{image_base:X}')
print(f'Sections ({num_secs}):')
for i in range(num_secs):
    off = sec_off + i * 40
    name = pe[off:off+8].split(b'\x00')[0].decode()
    vsize = int.from_bytes(pe[off+8:off+12], 'little')
    vaddr = int.from_bytes(pe[off+12:off+16], 'little')
    raw_size = int.from_bytes(pe[off+16:off+20], 'little')
    raw_ptr = int.from_bytes(pe[off+20:off+24], 'little')
    loaded = "LOADED" if raw_size > 0 else "SKIPPED"
    print(f'  {name:10s} VA=0x{image_base+vaddr:016X} VSize=0x{vsize:08X} RawSize=0x{raw_size:08X} [{loaded}]')
