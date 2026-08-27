using System;
using System.IO;

class Program {
    static void Main() {
        var bytes = File.ReadAllBytes(@"C:\tools\mz1500_sound_driver\Mz1500SoundPlayer\bin\Debug\net9.0\IPL.ROM");
        Console.WriteLine(BitConverter.ToString(bytes, 0x03A0, 32));
    }
}
