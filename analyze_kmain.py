#!/usr/bin/env python3
"""Analyze KMain entry point in PE file"""
import struct
import sys

def main():
    pe_path = sys.argv[1] if len(sys.argv) > 1 else "guideXOS.exe"
    d = open(pe_path, 'rb').read()
    
    # Find KMain in .managed section
    managed_raw = 0xE00  # From PE section header
    kmain_offset_in_managed = 0x1D90C  # From map file (KMain section offset)
    kmain_file_offset = managed_raw + kmain_offset_in_managed
    
    print(f"KMain expected at PE file offset: 0x{kmain_file_offset:X}")
    print()
    
    # Look for the actual function start
    print("Bytes around KMain:")
    for delta in range(-32, 33, 2):
        off = kmain_file_offset + delta
        b = d[off:off+8]
        
        marker = ''
        # Common 64-bit function starts
        if b[0] == 0x48 and b[1] in [0x89, 0x83, 0x8B, 0x8D]:
            marker = ' <-- 48 XX (REX.W prefix)'
        elif b[0] == 0x55:
            marker = ' <-- push rbp'
        elif b[0] == 0x40 and b[1] in [0x53, 0x55, 0x56, 0x57]:
            marker = ' <-- REX push'
        elif b[0:2] == bytes([0xCC, 0xCC]):
            marker = ' <-- INT3 padding'
        elif b[0] == 0x90:
            marker = ' <-- NOP'
        
        if delta == 0:
            marker += ' <<<< EXPECTED KMAIN'
            
        print(f"  {delta:+4d}: {b.hex(' ')}{marker}")
    
    print()
    
    # Check for the actual managed code start
    # NativeAOT functions often start with 48 83 EC XX (sub rsp, XX) or similar
    print("Looking for function prologues near KMain...")
    search_range = 64
    for i in range(-search_range, search_range):
        off = kmain_file_offset + i
        if off < 0 or off + 4 > len(d):
            continue
        b = d[off:off+4]
        
        # sub rsp, imm8
        if b[0:3] == bytes([0x48, 0x83, 0xEC]):
            print(f"  Found 'sub rsp, 0x{b[3]:02X}' at offset {i:+d} (file 0x{off:X})")
        
        # push rbp
        if b[0] == 0x55:
            print(f"  Found 'push rbp' at offset {i:+d} (file 0x{off:X})")
        
        # mov rbp, rsp
        if b[0:3] == bytes([0x48, 0x89, 0xE5]) or b[0:3] == bytes([0x48, 0x8B, 0xEC]):
            print(f"  Found 'mov rbp, rsp' at offset {i:+d} (file 0x{off:X})")

if __name__ == "__main__":
    main()
