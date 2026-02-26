import sys

def hex_dump(filename, length=256):
    with open(filename, 'rb') as f:
        data = f.read(length)
    
    for i in range(0, len(data), 16):
        chunk = data[i:i+16]
        hex_str = ' '.join(f'{b:02X}' for b in chunk)
        ascii_str = ''.join(chr(b) if 32 <= b <= 126 else '.' for b in chunk)
        print(f'{i:08X}: {hex_str:<48} {ascii_str}')

hex_dump('c:/tools/mz1500_sound_driver/fmsSample/PenguinAdventure_Forest/PenguinAdventure_Forest.fms', 1024)
