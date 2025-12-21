#!/usr/bin/env python3
"""
GXM Builder - Python Tool
Builds GXM (guideXOS eXecutable Module) files with GUI scripts

Usage:
    python gxm_builder.py script.txt output.gxm
    python gxm_builder.py --sample hello
    python gxm_builder.py --sample all
"""

import sys
import struct
import os

def create_gxm_file(script_lines, output_path):
    """
    Creates a GXM file with a GUI script
    
    Args:
        script_lines: List of script command strings
        output_path: Path where the .gxm file will be saved
    
    Returns:
        True if successful, False otherwise
    """
    try:
        # Build script text
        script_text = '\n'.join(script_lines) + '\n'
        script_bytes = script_text.encode('utf-8')
        
        # Calculate total size: 16 (header) + 4 (GUI marker) + script + 1 (null)
        image_size = 16 + 4 + len(script_bytes) + 1
        
        with open(output_path, 'wb') as f:
            # Write GXM header (16 bytes)
            f.write(b'GXM\x00')  # Signature
            f.write(struct.pack('<I', 1))  # Version (little-endian uint32)
            f.write(struct.pack('<I', 0))  # Entry RVA (unused for GUI scripts)
            f.write(struct.pack('<I', image_size))  # Image size
            
            # Write GUI marker (4 bytes)
            f.write(b'GUI\x00')
            
            # Write script text
            f.write(script_bytes)
            
            # Write null terminator
            f.write(b'\x00')
        
        file_size = os.path.getsize(output_path)
        print(f"? Created: {output_path}")
        print(f"  Size: {file_size} bytes")
        print(f"  Script lines: {len(script_lines)}")
        return True
        
    except Exception as e:
        print(f"? Error creating GXM file: {e}")
        return False

def create_hello_world_sample(output_path):
    """Sample: Simple hello world application"""
    script = [
        "WINDOW|Hello World|320|200",
        "LABEL|Welcome to GXM Scripting!|16|16",
        "BUTTON|1|Click Me|16|60|120|28",
        "ONCLICK|1|MSG|Hello from GXM!",
        "BUTTON|2|Open Notepad|16|100|140|28",
        "ONCLICK|2|OPENAPP|Notepad",
        "BUTTON|99|Close|16|140|120|28",
        "ONCLICK|99|CLOSE|"
    ]
    return create_gxm_file(script, output_path)

def create_color_picker_sample(output_path):
    """Sample: Color picker with dropdown and list"""
    script = [
        "WINDOW|Color Picker|400|320",
        "LABEL|Select a color from the dropdown:|16|16",
        "DROPDOWN|1|16|46|180|28|Red;Green;Blue;Yellow;Orange;Purple;Pink;Cyan",
        "ONCHANGE|1|MSG|You selected: $VALUE",
        "LABEL|Or pick from the list:|16|90",
        "LIST|2|16|120|180|140|Rose;Emerald;Sapphire;Gold;Silver;Bronze;Pearl;Jade",
        "ONCHANGE|2|MSG|List selection: $VALUE",
        "LABEL|Quick Actions:|220|120",
        "BUTTON|3|Open Notepad|220|150|160|28",
        "ONCLICK|3|OPENAPP|Notepad",
        "BUTTON|4|Open Calculator|220|188|160|28",
        "ONCLICK|4|OPENAPP|Calculator",
        "BUTTON|99|Close|220|280|160|28",
        "ONCLICK|99|CLOSE|"
    ]
    return create_gxm_file(script, output_path)

def create_app_launcher_sample(output_path):
    """Sample: Application launcher grid"""
    script = [
        "WINDOW|App Launcher|480|400",
        "LABEL|Quick Launch Applications|16|16",
        "BUTTON|1|Notepad|16|50|140|32",
        "BUTTON|2|Calculator|166|50|140|32",
        "BUTTON|3|Paint|316|50|140|32",
        "BUTTON|4|Console|16|92|140|32",
        "BUTTON|5|Task Manager|166|92|140|32",
        "BUTTON|6|Clock|316|92|140|32",
        "BUTTON|7|Monitor|16|134|140|32",
        "BUTTON|8|Computer Files|166|134|140|32",
        "ONCLICK|1|OPENAPP|Notepad",
        "ONCLICK|2|OPENAPP|Calculator",
        "ONCLICK|3|OPENAPP|Paint",
        "ONCLICK|4|OPENAPP|Console",
        "ONCLICK|5|OPENAPP|Task Manager",
        "ONCLICK|6|OPENAPP|Clock",
        "ONCLICK|7|OPENAPP|Monitor",
        "ONCLICK|8|OPENAPP|Computer Files",
        "LABEL|Or select from list:|16|180",
        "LIST|10|16|210|440|140|Notepad;Calculator;Paint;Console;Task Manager;Clock;Monitor",
        "ONCHANGE|10|OPENAPP|$VALUE",
        "BUTTON|99|Close|16|360|140|28",
        "ONCLICK|99|CLOSE|"
    ]
    return create_gxm_file(script, output_path)

def create_form_demo_sample(output_path):
    """Sample: Complete form with all control types"""
    script = [
        "WINDOW|Form Demo|520|440",
        "LABEL|User Information Form|16|16",
        "LABEL|Title:|16|50",
        "DROPDOWN|1|80|46|120|28|Mr.;Ms.;Dr.;Prof.;Eng.",
        "ONCHANGE|1|MSG|Title set to: $VALUE",
        "LABEL|Country:|16|90",
        "DROPDOWN|2|80|86|180|28|USA;UK;Canada;Germany;France;Japan;Australia;Brazil",
        "ONCHANGE|2|MSG|Country selected: $VALUE",
        "LABEL|Department:|16|130",
        "DROPDOWN|3|80|126|180|28|Engineering;Sales;Marketing;Support;HR;Finance",
        "ONCHANGE|3|MSG|Department: $VALUE",
        "LABEL|Interests (select):|280|50",
        "LIST|4|280|80|220|180|Programming;Gaming;Music;Sports;Reading;Travel;Cooking;Art;Photography",
        "ONCHANGE|4|MSG|Interest selected: $VALUE",
        "LABEL|Actions:|16|180",
        "BUTTON|10|Submit Form|16|210|200|32",
        "ONCLICK|10|MSG|Form would be submitted!",
        "BUTTON|11|Open Notepad|16|252|200|32",
        "ONCLICK|11|OPENAPP|Notepad",
        "BUTTON|12|Open Calculator|16|294|200|32",
        "ONCLICK|12|OPENAPP|Calculator",
        "BUTTON|99|Close Window|16|390|200|32",
        "ONCLICK|99|CLOSE|"
    ]
    return create_gxm_file(script, output_path)

def create_contact_form_sample(output_path):
    """Sample: Contact form"""
    script = [
        "WINDOW|Contact Form|460|400",
        "LABEL|Contact Information|16|16",
        "LABEL|Salutation:|16|50",
        "DROPDOWN|1|120|46|140|28|Mr.;Ms.;Dr.;Prof.",
        "ONCHANGE|1|MSG|Title: $VALUE",
        "LABEL|Department:|16|90",
        "DROPDOWN|2|120|86|200|28|Sales;Support;Engineering;Management",
        "ONCHANGE|2|MSG|Department: $VALUE",
        "LABEL|Priority:|16|130",
        "LIST|3|16|160|200|120|Low;Medium;High;Urgent;Critical",
        "ONCHANGE|3|MSG|Priority: $VALUE",
        "LABEL|Actions:|240|160",
        "BUTTON|10|Send Email|240|190|180|32",
        "ONCLICK|10|MSG|Email would be sent...",
        "BUTTON|11|Open Notepad|240|232|180|32",
        "ONCLICK|11|OPENAPP|Notepad",
        "BUTTON|12|Schedule Meeting|240|274|180|32",
        "ONCLICK|12|OPENAPP|Clock",
        "BUTTON|99|Close|240|350|180|32",
        "ONCLICK|99|CLOSE|"
    ]
    return create_gxm_file(script, output_path)

def load_script_from_file(filename):
    """Load script lines from a text file"""
    try:
        with open(filename, 'r', encoding='utf-8') as f:
            lines = [line.strip() for line in f if line.strip() and not line.strip().startswith('#')]
        return lines
    except Exception as e:
        print(f"? Error reading script file: {e}")
        return None

def print_usage():
    """Print usage information"""
    print("""
GXM Builder - Create guideXOS GUI Script Files

Usage:
    python gxm_builder.py <script.txt> <output.gxm>
    python gxm_builder.py --sample <name>
    python gxm_builder.py --sample all

Examples:
    python gxm_builder.py myscript.txt myapp.gxm
    python gxm_builder.py --sample hello
    python gxm_builder.py --sample all

Available Samples:
    hello       - Simple hello world with buttons
    colorpicker - Color selection with dropdown and list
    launcher    - Application launcher grid
    formdemo    - Complete form with all controls
    contact     - Contact form example
    all         - Generate all samples

Script File Format:
    One command per line, separated by | characters
    Lines starting with # are comments (ignored)
    
    Example script.txt:
        WINDOW|My App|480|320
        LABEL|Hello World|16|16
        BUTTON|1|Click Me|16|60|120|28
        ONCLICK|1|MSG|Button clicked!

For more information, see GXM_Complete_Testing_Guide.md
""")

def main():
    if len(sys.argv) < 2:
        print_usage()
        return 1
    
    # Handle --sample flag
    if sys.argv[1] == '--sample':
        if len(sys.argv) < 3:
            print("? Error: Please specify sample name")
            print("  Available: hello, colorpicker, launcher, formdemo, contact, all")
            return 1
        
        sample_name = sys.argv[2].lower()
        
        samples = {
            'hello': ('hello.gxm', create_hello_world_sample),
            'colorpicker': ('colorpicker.gxm', create_color_picker_sample),
            'launcher': ('launcher.gxm', create_app_launcher_sample),
            'formdemo': ('formdemo.gxm', create_form_demo_sample),
            'contact': ('contact.gxm', create_contact_form_sample),
        }
        
        if sample_name == 'all':
            print("Creating all sample GXM files...\n")
            output_dir = 'GXMSamples'
            if not os.path.exists(output_dir):
                os.makedirs(output_dir)
            
            success_count = 0
            for name, (filename, func) in samples.items():
                output_path = os.path.join(output_dir, filename)
                if func(output_path):
                    success_count += 1
                print()
            
            print(f"\n? Created {success_count}/{len(samples)} sample files in '{output_dir}/'")
            print(f"\nTo test:")
            print(f"  1. Copy files from '{output_dir}/' to your guideXOS disk")
            print(f"  2. In guideXOS Console, type the filename (without .gxm)")
            print(f"  3. Example: hello")
            return 0
        
        elif sample_name in samples:
            filename, func = samples[sample_name]
            print(f"Creating sample: {sample_name}\n")
            if func(filename):
                print(f"\n? Sample created successfully")
                print(f"\nTo test:")
                print(f"  1. Copy '{filename}' to your guideXOS disk")
                print(f"  2. In guideXOS Console, type: {sample_name}")
                return 0
            return 1
        
        else:
            print(f"? Error: Unknown sample '{sample_name}'")
            print(f"  Available: {', '.join(samples.keys())}, all")
            return 1
    
    # Handle script file input
    if len(sys.argv) < 3:
        print("? Error: Please provide input script and output filename")
        print("  Usage: python gxm_builder.py <script.txt> <output.gxm>")
        return 1
    
    input_file = sys.argv[1]
    output_file = sys.argv[2]
    
    if not os.path.exists(input_file):
        print(f"? Error: Input file not found: {input_file}")
        return 1
    
    print(f"Loading script from: {input_file}")
    script_lines = load_script_from_file(input_file)
    
    if script_lines is None:
        return 1
    
    if len(script_lines) == 0:
        print("? Error: No valid commands found in script file")
        return 1
    
    print(f"Building GXM file: {output_file}\n")
    if create_gxm_file(script_lines, output_file):
        print(f"\n? GXM file created successfully")
        print(f"\nTo test:")
        print(f"  1. Copy '{output_file}' to your guideXOS disk")
        print(f"  2. In guideXOS Console, navigate to the file")
        print(f"  3. Type the filename (without .gxm extension)")
        return 0
    
    return 1

if __name__ == '__main__':
    sys.exit(main())
