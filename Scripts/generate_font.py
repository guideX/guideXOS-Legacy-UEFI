#!/usr/bin/env python3
"""
Generate a clean bitmap font PNG for guideXOS
This creates a properly spaced font where each character is in the correct position
"""

from PIL import Image, ImageDraw, ImageFont
import sys

# Configuration
FONT_SIZE = 18
CHAR_WIDTH = 18
CHAR_HEIGHT = 18
# FIXED: Added leading space to match C# charset string in WindowManager.cs
CHARSET = ' !"#$%&\'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~'
CHARS_PER_ROW = 13
BG_COLOR = (0, 0, 0, 0)  # Transparent background
FG_COLOR = (255, 255, 255, 255)  # White text

def create_font_png(output_path='defaultfont.png'):
    """Create the font PNG file"""
    
    # Calculate image dimensions
    num_chars = len(CHARSET)
    num_rows = (num_chars + CHARS_PER_ROW - 1) // CHARS_PER_ROW
    
    width = CHARS_PER_ROW * CHAR_WIDTH
    height = num_rows * CHAR_HEIGHT
    
    print(f"Creating font PNG:")
    print(f"  Characters: {num_chars}")
    print(f"  Charset: {CHARSET}")
    print(f"  Image size: {width}x{height}")
    print(f"  Chars per row: {CHARS_PER_ROW}")
    print(f"  Rows: {num_rows}")
    
    # Create image with transparency
    img = Image.new('RGBA', (width, height), BG_COLOR)
    draw = ImageDraw.Draw(img)
    
    # Try to load a system font
    try:
        # Try common monospace fonts
        for font_name in ['DejaVuSansMono', 'Consolas', 'Courier New', 'monospace']:
            try:
                font = ImageFont.truetype(font_name, 14)
                print(f"  Using font: {font_name}")
                break
            except:
                continue
        else:
            # Fallback to default
            font = ImageFont.load_default()
            print("  Using default font")
    except:
        font = ImageFont.load_default()
        print("  Using default font")
    
    # Draw each character
    for i, char in enumerate(CHARSET):
        row = i // CHARS_PER_ROW
        col = i % CHARS_PER_ROW
        
        x = col * CHAR_WIDTH
        y = row * CHAR_HEIGHT
        
        # Center the character in the cell
        bbox = draw.textbbox((0, 0), char, font=font)
        char_width = bbox[2] - bbox[0]
        char_height = bbox[3] - bbox[1]
        
        text_x = x + (CHAR_WIDTH - char_width) // 2
        text_y = y + (CHAR_HEIGHT - char_height) // 2 - 2
        
        draw.text((text_x, text_y), char, font=font, fill=FG_COLOR)
        
        # Debug: print character positions
        if char in ['/', '>', 'Z', 'g', ' ']:
            print(f"  '{char}' at index {i}, position ({x}, {y})")
    
    # Save the image
    img.save(output_path, 'PNG')
    print(f"\nFont PNG saved to: {output_path}")
    print(f"Copy this file to: Ramdisk/Fonts/defaultfont.png")
    
    # Verify the image
    verify_img = Image.open(output_path)
    print(f"Verification: {verify_img.size[0]}x{verify_img.size[1]}, mode={verify_img.mode}")

if __name__ == '__main__':
    output = sys.argv[1] if len(sys.argv) > 1 else 'defaultfont.png'
    create_font_png(output)
