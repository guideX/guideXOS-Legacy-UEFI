#!/usr/bin/env python3
"""Find the actual KMain function prologue in the kernel PE"""

def main():
    pe_path = r'D:\devgitlab\guidexos\guideXOS.UEFI\bin\Release\net7.0\win-x64\native\guideXOS.exe'
    
    with open(pe_path, 'rb') as f:
        sec_off = 0xE00  # .managed section raw offset
        va_off = 0x10003000  # .managed section VA
        
        # Symbol addresses from map file
        classid_getname = 0x100240fc
        kmain_sym = 0x10024264
        entry_sym = 0x100247fc
        
        # Prologue pattern
        full_prologue = bytes([0x55, 0x41, 0x57, 0x41, 0x56, 0x41, 0x55, 0x41, 0x54])
        
        print(f"KMain symbol: 0x{kmain_sym:X}")
        print(f"Entry symbol: 0x{entry_sym:X}")
        print(f"ClassID__GetName: 0x{classid_getname:X}")
        
        # Search backwards from KMain symbol
        print(f"\nSearching BACKWARDS from KMain symbol for prologue...")
        for offset in range(0, 0x300, 1):
            va = kmain_sym - offset
            file_off = va - va_off + sec_off
            f.seek(file_off)
            data = f.read(9)
            if data == full_prologue:
                f.seek(file_off)
                full = f.read(48)
                print(f'Found prologue at VA 0x{va:X} ({offset} bytes before KMain symbol)')
                print('Bytes:', ' '.join(f'{b:02X}' for b in full))
                return va
        
        print('No full prologue found before KMain')
        
        # Also check what's at the KMain symbol itself
        print(f"\nBytes at KMain symbol (0x{kmain_sym:X}):")
        file_off = kmain_sym - va_off + sec_off
        f.seek(file_off)
        print(' '.join(f'{b:02X}' for b in f.read(32)))
        
        return None


if __name__ == "__main__":
    result = main()
    if result:
        print(f"\n*** RECOMMENDED ENTRY POINT: 0x{result:X} ***")
    else:
        print("\n*** NO SUITABLE ENTRY POINT FOUND ***")
