using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

class Program
{
    static void Main()
    {
        string path = @"..\Mz1500SoundPlayer\Sound\Z80\Z80DriverGenerator.cs";
        string code = File.ReadAllText(path);
        
        // 1. Add DataPsgFreqTable and DataBeepFreqTable to Build()
        string tableGeneration = @"
        // ----- 生成した周波数テーブルの追加 -----
        assembler.Label(""DataPsgFreqTable"");
        for (int i = 0; i < 96; i++) {
            double freq = 440.0 * Math.Pow(2.0, (i - 57) / 12.0);
            int baseReg = (int)Math.Round(111860.0 / freq);
            baseReg = Math.Clamp(baseReg, 0, 1023);
            ushort regU = (ushort)baseReg;
            // Byte1: 0000ffff, Byte2: 00ffffff
            byte c1 = (byte)(regU & 0x0F);
            byte c2 = (byte)((regU >> 4) & 0x3F);
            assembler.DB(c1);
            assembler.DB(c2);
        }

        assembler.Label(""DataBeepFreqTable"");
        for (int i = 0; i < 96; i++) {
            double freq = 440.0 * Math.Pow(2.0, (i - 57) / 12.0);
            double baseReg = 894886.0 / freq;
            int reg = (int)Math.Round(baseReg);
            reg = Math.Clamp(reg, 0, 65535);
            assembler.DB((byte)(reg & 0xFF));
            assembler.DB((byte)((reg >> 8) & 0xFF));
        }

        // ===== チャンネル独立のシーケンスデータ配置 =====";
        
        code = code.Replace("// ===== チャンネル独立のシーケンスデータ配置 =====", tableGeneration);
        
        // 2. Add StatLastLength label definition to Stat Variables
        string newStat = @"
        // -- Stat Variables --
        asm.Label(prefix + ""_"" + nameof(Labels.StatSongDataPosition));
        asm.DB(asm.LabelRef(prefix + ""_"" + nameof(Labels.DataSong))); // Initialize with Data Start Address
        
        asm.Label(prefix + ""_StatLastLength"");
        asm.DB(new byte[] { 0, 0 });
";
        code = code.Replace(@"
        // -- Stat Variables --
        asm.Label(prefix + ""_"" + nameof(Labels.StatSongDataPosition));
        asm.DB(asm.LabelRef(prefix + ""_"" + nameof(Labels.DataSong))); // Initialize with Data Start Address", newStat);
        
        // 3. Rewrite AppendPlayChannelSource's Fetch Command logic
        string parseLogic = @"
        // 3. 次のコマンドを読む処理
        asm.Label(prefix + ""_"" + nameof(Labels.ReadSongDataOne));
        
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Fetch Command -> A
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A);

        // 0xFF (End)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_end_song""));

        // 0x60 (Rest)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadKyufuData)));

        // 0xA0 (Set Voice / Env)
        asm.CP(asm.Value((byte)0xA0));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadEnvData)));

        // 0xA1 (Set Volume)
        asm.CP(asm.Value((byte)0xA1));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadVolumeData)));

        // 0xA2 (Set PEnv)
        asm.CP(asm.Value((byte)0xA2));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadPEnvData)));

        // 0x90 (Long Length)
        asm.CP(asm.Value((byte)0x90));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_read_long_len""));

        // 0x08 (Loop Marker)
        asm.CP(asm.Value((byte)0x08));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_read_loop_marker""));

        // Noise (0x06) -> wait, we kept Noise as 0x06
        asm.CP(asm.Value((byte)0x06));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_read_noise""));

        // Sync Noise (0x07)
        asm.CP(asm.Value((byte)0x07));
        asm.JP(asm.Z, asm.LabelRef(prefix + ""_read_sync_noise""));

        // If A < 0x60, it's Note ON (0x00 - 0x5F)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.C, asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadToneData)));
        
        // If A >= 0x80 AND A <= 0x8F, it's Short Length
        asm.SUB(asm.Value((byte)0x80));
        asm.CP(asm.Value((byte)0x10));
        asm.JP(asm.C, asm.LabelRef(prefix + ""_read_short_len""));

        // Unknown -> Ignore and read next
        asm.JP(asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadSongDataOne)));

        // -- Read Long Length --
        asm.Label(prefix + ""_read_long_len"");
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A); // BC = 16-bit Length
        
        // Store to StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_StatLastLength""));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadSongDataOne)));

        // -- Read Short Length --
        asm.Label(prefix + ""_read_short_len"");
        // A is already (Cmd - 0x80).
        asm.AND(asm.Value((byte)0x0F));
        asm.INC(asm.A);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0); // BC = Length
        
        // Store to StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_StatLastLength""));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + ""_"" + nameof(Labels.ReadSongDataOne)));
";
        int indexStart = code.IndexOf("// 3. 次のコマンドを読む処理");
        int indexEnd = code.IndexOf("// -- Read Loop Marker");
        
        if (indexStart >= 0 && indexEnd > indexStart) {
            code = code.Substring(0, indexStart) + parseLogic + code.Substring(indexEnd);
        } else {
            Console.WriteLine("Parse logic replacement failed!");
            return;
        }

        // 4. Rewrite ReadToneData (NoteON)
        string toneLogic = @"
        // -- Read Tone -- 
        asm.Label(prefix + ""_"" + nameof(Labels.ReadToneData));
        
        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_StatLastLength""));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        // Reset offsets
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);

        // Set Note Active
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        // B has the original Note ON command (Note Number)
        // Note: B was set to the command right after Fetch Command in ReadSongDataOne. Wait, B was overwritten?
        // Let's just use B since it was saved at the beginning of ReadSongDataOne and we didn't overwrite it for Tone,
        // EXCEPT we overwrote it during Long/Short Length. Wait! B is NOT overwritten for Tone because Tone is jumped to directly!
        // But let's verify. B was set: asm.LD(asm.B, asm.A); Yes!
        
        // Fetch HW register from Table
        if (port == 0xE0) {
            asm.LD(asm.HL, asm.LabelRef(""DataBeepFreqTable""));
        } else {
            asm.LD(asm.HL, asm.LabelRef(""DataPsgFreqTable""));
        }
        
        asm.LD(asm.A, asm.B);
        asm.ADD(asm.A, asm.A); // A = Note * 2
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0);
        asm.ADD(asm.HL, asm.BC);
        
        // Output to hardware
        asm.LD(asm.A, asm.HLref); // low byte
        
        if (port == 0xE0) {
            asm.PUSH(asm.HL);
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
            asm.POP(asm.HL);
            
            asm.INC(asm.HL);
            asm.LD(asm.A, asm.HLref); // high byte
            
            asm.PUSH(asm.HL);
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
            asm.POP(asm.HL);
        } else {
            // Determine PSG Channel from prefix (track_0 -> 0, etc.)
            int psgCh = 0;
            if (prefix.StartsWith(""track_"")) {
                int.TryParse(prefix.Substring(6), out psgCh);
            }
            byte chBits = (byte)(0x80 | ((psgCh & 0x03) << 5));
            asm.OR(asm.Value(chBits));
            asm.OUT(port);
            
            asm.INC(asm.HL);
            asm.LD(asm.A, asm.HLref); // high byte
            asm.OUT(port);
        }

        // Save pos
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Apply Volume
        if (port == 0xE0) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x01); // BEEP ON
        } else {
            asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.A, asm.HLref);
            asm.OUT(port);
        }

        asm.JP(asm.LabelRef(prefix + ""_"" + nameof(Labels.OutputSoundByStatus)));
";
        int toneStart = code.IndexOf("// -- Read Tone --");
        int restStart = code.IndexOf("// -- Read Rest --");
        if (toneStart >= 0 && restStart > toneStart) {
            code = code.Substring(0, toneStart) + toneLogic + "\n" + code.Substring(restStart);
        } else {
            Console.WriteLine("Tone logic replacement failed!");
            return;
        }

        // 5. Rewrite ReadKyufuData (Rest)
        string restLogic = @"
        // -- Read Rest --
        asm.Label(prefix + ""_"" + nameof(Labels.ReadKyufuData));
        
        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_StatLastLength""));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x00);

        // Send Volume=0 (0x0F) to mute
        if (port == 0xE0) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x00); // BEEP OFF
        } else {
            asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.A, asm.HLref);
            asm.AND(asm.Value((byte)0x60)); // Keep only channel bits
            asm.OR(asm.Value((byte)0x9F));  // Base Vol + 15 (Mute)
            asm.OUT(port);
        }

        // Save pos
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + ""_"" + nameof(Labels.OutputSoundByStatus)));
";
        int noiseStart = code.IndexOf("// -- Read Noise --");
        if (noiseStart > restStart) {
            code = code.Substring(0, restStart) + restLogic + "\n" + code.Substring(noiseStart);
        } else {
            Console.WriteLine("Rest logic replacement failed!");
            return;
        }
        
        // Fix Noise and Sync Noise Length
        string noiseLogic = @"
        // -- Read Noise --
        asm.Label(prefix + ""_read_noise"");
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        // Fetch NoiseCmd and OUT
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        if (port != 0xE0) { asm.OUT(port); }

        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_StatLastLength""));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        
        if (port != 0xE0) {
            asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.A, asm.HLref);
            asm.OUT(port);
        }
        asm.JP(asm.LabelRef(prefix + ""_"" + nameof(Labels.OutputSoundByStatus)));


        // -- Read Sync Noise --
        asm.Label(prefix + ""_read_sync_noise"");
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        if (port != 0xE0) {
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE);
            asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.HLref, asm.A);
            asm.OUT(port);
        }

        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_StatLastLength""));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(prefix + ""_"" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(prefix + ""_"" + nameof(Labels.OutputSoundByStatus)));
";
        int volStart = code.IndexOf("// -- Read Volume --");
        if (volStart > noiseStart) {
            code = code.Substring(0, noiseStart) + noiseLogic + "\n" + code.Substring(volStart);
        } else {
            Console.WriteLine("Noise logic replacement failed!");
            return;
        }

        File.WriteAllText(path, code);
        Console.WriteLine("Done rewriting Z80DriverGenerator.cs");
    }
}
