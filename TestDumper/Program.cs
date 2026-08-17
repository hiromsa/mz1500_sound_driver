using System;
using System.Collections.Generic;
using Mz1500SoundPlayer.Sound;
using Mz1500SoundPlayer.Sound.Mml;

class Program {
    static void Main() {
        var compiler = new Z80SequenceCompiler();
        var parser = new MmlParser();
        var events = parser.Parse("T120 V15 O4 C4 D4 E4 R4 C8 C8 G4");
        var compiled = compiler.CompileTrack(events);
        Console.WriteLine(BytecodeDumper.Dump(compiled));
    }
}
