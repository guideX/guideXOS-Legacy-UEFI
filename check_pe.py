#!/usr/bin/env python3
import struct
import sys

def main():
    pe_path = sys.argv[1] if len(sys.argv) > 1 else "guideXOS.exe"
    pe = open(pe_path, 'rb').read()
    
    pe_off = struct.unpack('<I', pe[0x3C:0x40])[0]
    coff_off = pe_off + 4
    num_sections = struct.unpack('<H', pe[coff_off+2:coff_off+4])[0]
    opt_size = struct.unpack('<H', pe[coff_off+16:coff_off+18])[0]
    opt_off = coff_off + 20
    image_base = struct.unpack('<Q', pe[opt_off+24:opt_off+32])[0]
    entry_rva = struct.unpack('<I', pe[opt_off+16:opt_off+20])[0]

    print(f'ImageBase: 0x{image_base:X}')
    print(f'EntryPointRVA: 0x{entry_rva:X}')
    print(f'Entry Absolute: 0x{image_base+entry_rva:X}')
    print(f'Number of sections: {num_sections}')
    print()

    sec_off = opt_off + opt_size
    kmain = 0x1002090C  # From map file
    
    for i in range(num_sections):
        off = sec_off + i*40
        name = pe[off:off+8].rstrip(b'\x00').decode('ascii', errors='replace')
        vsize = struct.unpack('<I', pe[off+8:off+12])[0]
        vaddr = struct.unpack('<I', pe[off+12:off+16])[0]
        raw_size = struct.unpack('<I', pe[off+16:off+20])[0]
        raw_ptr = struct.unpack('<I', pe[off+20:off+24])[0]
        
        abs_start = image_base + vaddr
        abs_end = abs_start + vsize
        
        marker = ""
        if abs_start <= kmain < abs_end:
            marker = " <-- KMain is HERE"
        
        print(f'{name:8} VA:0x{vaddr:08X} VSize:0x{vsize:06X} -> 0x{abs_start:X}-0x{abs_end:X}{marker}')
        print(f'         RawPtr:0x{raw_ptr:X} RawSize:0x{raw_size:X}')

if __name__ == "__main__":
    main()
