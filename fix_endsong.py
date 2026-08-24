import re

file_path = r"c:\tools\mz1500_sound_driver\Mz1500SoundPlayer\Sound\Z80\MZ1500SoundDriverGenerator.cs"
with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

# Replace EndSong
target = r"""        // -- End Song (Looping) --
        asm.Label(GetSharedCtx(isBeep).EndSong);
        // Get Address from StatLoopPosition -> DE
        asm.LD(asm.E, asm.IXref(IxStatLoopPosition));
        asm.LD(asm.D, asm.IXref((sbyte)(IxStatLoopPosition + 1)));
        
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        // And store DE to StatSongDataPosition
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Fetch the next command to execute
        asm.CP(asm.B);            // Compare A with B
        
        // If not 0xFF, jump to parsing (valid loop target)
        asm.JP(asm.NZ, asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));"""

replacement = """        // -- End Song (Looping) --
        asm.Label(GetSharedCtx(isBeep).EndSong);
        // Get Address from StatLoopPosition -> DE
        asm.LD(asm.E, asm.IXref(IxStatLoopPosition));
        asm.LD(asm.D, asm.IXref((sbyte)(IxStatLoopPosition + 1)));
        
        // Check if LoopPosition is 0 (uninitialized / no loop)
        asm.LD(asm.A, asm.E);
        asm.OR(asm.D);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).HaltSong));
        
        // And store DE to StatSongDataPosition
        asm.LD(asm.IXref(IxStatSongDataPosition), asm.E);
        asm.LD(asm.IXref((sbyte)(IxStatSongDataPosition + 1)), asm.D);
        
        // Jump back to read the looped command
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));
        
        asm.Label(GetSharedCtx(isBeep).HaltSong);"""

content = content.replace(target, replacement)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Fixed EndSong")
