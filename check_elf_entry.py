import struct
import sys

with open('ESP/kernel.elf', 'rb') as f:
    # ELF header: e_entry is at offset 24 (8 bytes for 64-bit)
    f.seek(24)
    entry = struct.unpack('<Q', f.read(8))[0]
    print(f"Entry point: 0x{entry:X}")
    
    # Expected: 0x10001698 (from PE check)
    if entry == 0x10001698:
        print("? Entry point is CORRECT!")
        sys.exit(0)
    else:
        print(f"? Entry point is WRONG! Expected 0x10001698")
        sys.exit(1)
