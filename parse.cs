using System;
using System.IO;

class Program
{
    static void Main()
    {
        var bytes = File.ReadAllBytes(@"C:\tools\mz1500_sound_driver\Mz1500SoundPlayer\qdfsample\sample.qdf");
        int ptr = 0;
        while (ptr < bytes.Length)
        {
            byte blockType = bytes[ptr++];
            ushort blockSize = (ushort)(bytes[ptr] | (bytes[ptr+1] << 8));
            ptr += 2;
            
            if (blockType == 1 || blockType == 3 || blockType == 5 || blockType == 7)
            {
                ushort loadAddr = (ushort)(bytes[ptr+18] | (bytes[ptr+19] << 8));
                ushort fileSize = (ushort)(bytes[ptr+20] | (bytes[ptr+21] << 8));
                ushort execAddr = (ushort)(bytes[ptr+22] | (bytes[ptr+23] << 8));
                Console.WriteLine($"Type: {blockType:X2} Load: {loadAddr:X4} Size: {fileSize} Exec: {execAddr:X4}");
            }
            
            ptr += blockSize + 2;
        }
    }
}
