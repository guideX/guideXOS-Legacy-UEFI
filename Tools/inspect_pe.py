#!/usr/bin/env python3
"""Inspect PE file sections and entry point."""

import struct
import sys

def main():
    if len(sys.argv) < 2:
        print("Usage: inspect_pe.py <pe_file>")
        return 1
    
    pe = open(sys.argv[1], 'rb').read()
    
    pe_off = struct.unpack_from('<I', pe, 0x3C)[0]
    coff_off = pe_off + 4
    num_sec = struct.unpack_from('<H', pe, coff_off + 2)[0]
    opt_size = struct.unpack_from('<H', pe, coff_off + 16)[0]
    opt_off = coff_off + 20
    image_base = struct.unpack_from('<Q', pe, opt_off + 24)[0]
    entry_rva = struct.unpack_from('<I', pe, opt_off + 16)[0]
    
    print(f'Image Base: 0x{image_base:X}')
    print(f'Entry RVA: 0x{entry_rva:X}')
    print(f'Entry VA: 0x{image_base + entry_rva:X}')
    print()
    print('Sections:')
    
    sec_off = opt_off + opt_size
    entry_section = None
    entry_file_offset = None
    
    for i in range(num_sec):
        off = sec_off + i * 40
        name = pe[off:off+8].rstrip(b'\x00').decode('ascii', errors='replace')
        vsize = struct.unpack_from('<I', pe, off + 8)[0]
        vaddr = struct.unpack_from('<I', pe, off + 12)[0]
        raw_size = struct.unpack_from('<I', pe, off + 16)[0]
        raw_ptr = struct.unpack_from('<I', pe, off + 20)[0]
        chars = struct.unpack_from('<I', pe, off + 36)[0]
        
        exec_flag = 'X' if (chars & 0x20000000) else '-'
        read_flag = 'R' if (chars & 0x40000000) else '-'
        write_flag = 'W' if (chars & 0x80000000) else '-'
        
        print(f'  {name:8} VA=0x{vaddr:08X} VSize=0x{vsize:X} RawPtr=0x{raw_ptr:X} RawSize=0x{raw_size:X} [{exec_flag}{read_flag}{write_flag}]')
        
        if vaddr <= entry_rva < vaddr + vsize:
            entry_section = name
            entry_offset_in_sec = entry_rva - vaddr
            entry_file_offset = raw_ptr + entry_offset_in_sec
            print(f'    ^ Entry point is in this section at offset 0x{entry_offset_in_sec:X}')
    
    if entry_file_offset:
        print()
        print(f'Bytes at entry point (file offset 0x{entry_file_offset:X}):')
        bytes_at_entry = pe[entry_file_offset:entry_file_offset+32]
        hex_str = ' '.join(f'{b:02X}' for b in bytes_at_entry)
        print(f'  {hex_str}')
        
        # Try to identify prologue
        if bytes_at_entry[0:2] == b'\x48\x89':
            print('  Looks like: mov [rsp+...], ... (typical function prologue)')
        elif bytes_at_entry[0] == 0x55:
            print('  Looks like: push rbp (classic prologue)')
        elif bytes_at_entry[0:2] == b'\x40\x53':
            print('  Looks like: push rbx (with REX prefix)')
        elif bytes_at_entry[0:3] == b'\x48\x83\xEC':
            print('  Looks like: sub rsp, imm8 (stack allocation)')
        else:
            print('  Unknown prologue pattern')

if __name__ == '__main__':
    sys.exit(main() or 0)
