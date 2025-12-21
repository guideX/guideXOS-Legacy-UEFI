#!/usr/bin/env python3
import struct
import sys

def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "kernel.elf"
    d = open(path, 'rb').read()
    
    e_entry = struct.unpack('<Q', d[24:32])[0]
    phoff = struct.unpack('<Q', d[32:40])[0]
    phnum = struct.unpack('<H', d[56:58])[0]
    phentsize = struct.unpack('<H', d[54:56])[0]
    
    print(f"e_entry = 0x{e_entry:016X}")
    print(f"phoff = {phoff}")
    print(f"phnum = {phnum}")
    print(f"phentsize = {phentsize}")
    print()
    
    min_vaddr = 0xFFFFFFFFFFFFFFFF
    segments = []
    
    for i in range(phnum):
        off = phoff + i * phentsize
        p_type = struct.unpack('<I', d[off:off+4])[0]
        p_flags = struct.unpack('<I', d[off+4:off+8])[0]
        p_offset = struct.unpack('<Q', d[off+8:off+16])[0]
        p_vaddr = struct.unpack('<Q', d[off+16:off+24])[0]
        p_paddr = struct.unpack('<Q', d[off+24:off+32])[0]
        p_filesz = struct.unpack('<Q', d[off+32:off+40])[0]
        p_memsz = struct.unpack('<Q', d[off+40:off+48])[0]
        
        type_name = {0: 'NULL', 1: 'LOAD'}.get(p_type, f'0x{p_type:X}')
        
        if p_type == 1 and p_memsz > 0:  # PT_LOAD
            print(f"LOAD[{i}]: vaddr=0x{p_vaddr:016X} memsz=0x{p_memsz:X} filesz=0x{p_filesz:X} offset=0x{p_offset:X}")
            segments.append((p_vaddr, p_memsz, p_offset, p_filesz))
            if p_vaddr < min_vaddr:
                min_vaddr = p_vaddr
    
    print()
    print(f"min_vaddr = 0x{min_vaddr:016X}")
    entry_offset = e_entry - min_vaddr
    print(f"entry_offset (e_entry - min_vaddr) = 0x{entry_offset:016X}")
    
    # Check if entry point falls within a loaded segment
    print()
    print("Checking if entry point is in a loaded segment...")
    entry_in_segment = False
    for i, (vaddr, memsz, offset, filesz) in enumerate(segments):
        if vaddr <= e_entry < vaddr + memsz:
            rel_offset = e_entry - vaddr
            file_offset = offset + rel_offset
            print(f"  Entry IS in segment {i}: relative offset 0x{rel_offset:X}, file offset 0x{file_offset:X}")
            
            # Check bytes at that location
            if file_offset + 16 <= len(d):
                bytes_at_entry = d[file_offset:file_offset+16]
                print(f"  First 16 bytes at entry: {bytes_at_entry.hex(' ')}")
            entry_in_segment = True
            break
    
    if not entry_in_segment:
        print("  WARNING: Entry point is NOT within any loaded segment!")
        print()
        print("  Segment ranges:")
        for i, (vaddr, memsz, offset, filesz) in enumerate(segments):
            end = vaddr + memsz
            print(f"    Seg {i}: 0x{vaddr:X} - 0x{end:X}")

if __name__ == "__main__":
    main()
