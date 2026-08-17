using System;
using System.Collections.Generic;
using Mz1500SoundPlayer.Sound;
using Mz1500SoundPlayer.Sound.Mml;

class Program {
    static void Main() {
        var compiler = new Z80SequenceCompiler();
        var parser = new MultiTrackMmlParser();
        var ast = parser.Parse("P1 T120 V15 O4 C4 D4 E4 R4 C8 C8 G4");
        var expander = new TrackEventExpander();
        var expanded = expander.Expand(ast.Tracks["P1"]);
        var compiled = compiler.CompileTrack(expanded);
        Console.WriteLine(BytecodeDumper.Dump(compiled));
    }
}
