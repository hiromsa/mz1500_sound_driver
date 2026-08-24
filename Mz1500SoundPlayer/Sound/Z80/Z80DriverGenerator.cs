using System;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound.Z80;

public class Channel
{
    public string Name { get; set; }
    public byte IOPort { get; set; }
    public byte[] SequenceData { get; set; }

    public Channel(string name, byte ioPort, byte[] sequenceData)
    {
        Name = name;
        IOPort = ioPort;
        SequenceData = sequenceData;
    }
}

public class Z80DriverGenerator
{
    public List<Channel> ChannelList { get; } = new();
    
    // MmlPlayerModel縺九ｉ貂｡縺輔ｌ繧九お繝ｳ繝吶Ο繝ｼ繝怜ｮ夂ｾｩ繝・・繧ｿ (EnvId -> 繝懊Μ繝･繝ｼ繝驟榊・)
    public Dictionary<int, EnvelopeData> VolumeEnvelopes { get; set; } = new();
    
    // HwPitchEnv繝・・繧ｿ
    public List<Z80SequenceCompiler.HwPitchEnvData> HwPitchEnvelopes { get; set; } = new();

    public void AppendChannel(Channel channel) => ChannelList.Add(channel);

    private enum Labels
    {
        StatSongDataPosition,   // 2 bytes
        StatLoopPosition,       // 2 bytes 
        StatLengthRemain,       // 2 bytes
        StatGateRemain,         // 2 bytes
        StatNoteOn,             // 1 byte
        StatHwVolume,           // 1 byte (0=Louder, 15=Silent)
        StatEnvActive,          // 1 byte (0=Off, 1=On)
        StatEnvDataPtr,         // 2 bytes (Current Env Table Address)
        StatEnvPosOffset,       // 1 byte (Current offset in table)
        
        StatPEnvActive,         // 1 byte
        StatPEnvDataPtr,        // 2 bytes
        StatPEnvPosOffset,      // 1 byte
        
        // Routines
        OutputSoundByStatus,
        ReadSongDataOne,
        ReadToneData,
        ReadVolumeData,
        ReadEnvData,
        ReadPEnvData,
        ReadKyufuData,
        DataSong,
        DataEnvTableBase,
        DataPEnvTableBase
    }

    public byte[] Build(byte[]? pcgData = null)
    {
        var assembler = new MZ1500Assembler();
        
        assembler.ORG(0x1200);
        assembler.Label("main:");
        assembler.DI();
        
        if (pcgData != null && pcgData.Length == 24000)
        {
            assembler.CALL(assembler.LabelRef("ImageLoader"));
            assembler.JP(assembler.LabelRef("main2:"));
            AppendImageLoader(assembler, pcgData);
            assembler.Label("main2:");
        }

        assembler.IM1();

        // 蜑ｲ繧願ｾｼ縺ｿ繝吶け繧ｿ縺ｮ險ｭ螳・
        assembler.LD(assembler.HL, 0x1039);
        assembler.LD(assembler.DE, assembler.LabelRef("sound:"));
        assembler.LD(assembler.HLref, assembler.E);
        assembler.INC(assembler.HL);
        assembler.LD(assembler.HLref, assembler.D);

        // 8253繧ｿ繧､繝槭・險ｭ螳・(蜑ｲ繧願ｾｼ縺ｿ蜻ｨ譛・
        assembler.LD(assembler.HL, 0xE007);
        assembler.LD(assembler.HLref, 0xB0); // CH2 Mode0
        assembler.LD(assembler.HLref, 0x74); // CH1 Mode2
        assembler.DEC(assembler.HL);         // 0xE006 (CH2)
        assembler.LD(assembler.HLref, 0x83); 
        assembler.LD(assembler.HLref, 0x00); 
        assembler.DEC(assembler.HL);         // 0xE005 (CH1)
        assembler.LD(assembler.HLref, 0x02);
        assembler.LD(assembler.HLref, 0x00);

        // 蜑ｲ繧願ｾｼ縺ｿ險ｱ蜿ｯ (INTMSK)
        assembler.LD(assembler.A, 0x05);
        assembler.LD(0xE003, assembler.A);

        // MZ-700髻ｳ貅・BEEP)蛻晄悄蛹・(SN76489縺ｮ繝繝溘・縺倶ｺ呈鋤逕ｨ・・
        assembler.LD(assembler.A, 0x01);
        assembler.LD(assembler.HL, 0xE008);
        assembler.LD(assembler.HLref, assembler.A);
        assembler.LD(assembler.HL, 0xE007);
        assembler.LD(assembler.HLref, 0x36);

        // --- VRAM繧ｯ繝ｪ繧｢縺ｨ繝・せ繝域緒逕ｻ (繝輔Μ繝ｼ繧ｺ(辟｡蜿榊ｿ・縺励※縺・ｋ繧医≧縺ｫ隕九∴縺ｪ縺・◆繧√・蟇ｾ遲・ ---
        if (pcgData == null)
        {
            // VRAM(0xD000縲・xD3E7)繧偵け繝ｪ繧｢
            assembler.LD(assembler.HL, 0xD000);
            assembler.LD(assembler.DE, 0xD001);
            assembler.LD(assembler.BC, 0x03FF);
            assembler.LD(assembler.HLref, 0x00); // 0x00 (Space or Empty)
            assembler.LDIR();

            // 逕ｻ髱｢蟾ｦ荳・0xD000)縺ｫ 'PLAYING' 繧樽Z-1500縺ｮ繧｢繧ｹ繧ｭ繝ｼ譁・ｭ暦ｼ育判髱｢陦ｨ遉ｺ繧ｳ繝ｼ繝会ｼ峨〒逶ｴ譖ｸ縺・
            assembler.LD(assembler.HL, 0xD000);
            assembler.LD(assembler.HLref, 0x10); assembler.INC(assembler.HL); // P = 16 = 0x10
            assembler.LD(assembler.HLref, 0x0C); assembler.INC(assembler.HL); // L = 12 = 0x0C
            assembler.LD(assembler.HLref, 0x01); assembler.INC(assembler.HL); // A = 1  = 0x01
            assembler.LD(assembler.HLref, 0x19); assembler.INC(assembler.HL); // Y = 25 = 0x19
            assembler.LD(assembler.HLref, 0x09); assembler.INC(assembler.HL); // I = 9  = 0x09
            assembler.LD(assembler.HLref, 0x0E); assembler.INC(assembler.HL); // N = 14 = 0x0E
            assembler.LD(assembler.HLref, 0x07);                              // G = 7  = 0x07
        }
        // --- 謠冗判縺薙％縺ｾ縺ｧ ---

        assembler.EI();

        // 辟｡髯舌Ν繝ｼ繝・(繝｡繧､繝ｳ蜃ｦ逅・・蜑ｲ繧願ｾｼ縺ｿ縺ｫ莉ｻ縺帙ｋ)
        assembler.Label("loop:");
        assembler.JP(assembler.LabelRef("loop:"));

        // ===== 蜑ｲ繧願ｾｼ縺ｿ繝上Φ繝峨Λ =====
        assembler.Label("sound:");
        assembler.PUSH(assembler.AF);
        assembler.PUSH(assembler.BC);
        assembler.PUSH(assembler.DE);
        assembler.PUSH(assembler.HL);
        
        // 8253繧ｿ繧､繝槫・險ｭ螳・
        assembler.LD(assembler.HL, 0xE006);
        assembler.LD(assembler.HLref, 0x83);
        assembler.LD(assembler.HLref, 0x00);

        foreach (var ch in ChannelList)
        {
            assembler.CALL(assembler.LabelRef(ch.Name));
        }

        assembler.POP(assembler.HL);
        assembler.POP(assembler.DE);
        assembler.POP(assembler.BC);
        assembler.POP(assembler.AF);

        assembler.EI();
        assembler.RET();


        // ===== 繝√Ε繝ｳ繝阪Ν縺斐→縺ｮ蜃ｦ逅・Ν繝ｼ繝√Φ =====
        foreach (var ch in ChannelList)
        {
            AppendPlayChannelSource(ch.Name, assembler, ch.IOPort);
        }

        
        // ----- 逕滓・縺励◆蜻ｨ豕｢謨ｰ繝・・繝悶Ν縺ｮ霑ｽ蜉 -----
        assembler.Label("DataPsgFreqTable");
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

        assembler.Label("DataBeepFreqTable");
        for (int i = 0; i < 96; i++) {
            double freq = 440.0 * Math.Pow(2.0, (i - 57) / 12.0);
            double baseReg = 894886.0 / freq;
            int reg = (int)Math.Round(baseReg);
            reg = Math.Clamp(reg, 0, 65535);
            assembler.DB((byte)(reg & 0xFF));
            assembler.DB((byte)((reg >> 8) & 0xFF));
        }

        // ===== 繝√Ε繝ｳ繝阪Ν迢ｬ遶九・繧ｷ繝ｼ繧ｱ繝ｳ繧ｹ繝・・繧ｿ驟咲ｽｮ =====
        foreach (var ch in ChannelList)
        {
            assembler.Label(ch.Name + "_" + nameof(Labels.DataSong));
            assembler.DB(ch.SequenceData);
            assembler.Label(ch.Name + "_data_song_end");
            assembler.DB(0xFF); // 螳牙・逕ｨ縺ｮ邨らｫｯ繝槭・繧ｫ繝ｼ (L逵∫払譎ゅ↓縺薙％縺ｫ繧ｸ繝｣繝ｳ繝励＠縺ｦ蛛懈ｭ｢縺礼ｶ壹￠繧・
        }

        return assembler.Build();
    }

    private void AppendPlayChannelSource(string prefix, MZ1500Assembler asm, byte port)
    {
        asm.Label(prefix);
        
        // 1. 繝ｬ繝ｳ繧ｰ繧ｹ(Duration)縺ｮ貂帛ｰ代→蛻､螳・
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.NZ, asm.LabelRef(prefix + "_dec_dur_lower"));
        // lower is 0, check upper
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.DEC(asm.HL); // restore HL
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne))); // both 0 -> next command
        
        // dec upper
        asm.INC(asm.HL);
        asm.DEC(asm.HLref);
        asm.DEC(asm.HL);
        
        asm.Label(prefix + "_dec_dur_lower");
        asm.DEC(asm.HLref);

        // 2. 繧ｲ繝ｼ繝・Gate)縺ｮ蜃ｦ逅・(邁｡譏灘ｮ溯｣・ Duration荳ｭ縺ｫGate縺悟・繧後◆繧蛾浹驥上ｒ辟｡髻ｳ縺ｫ縺吶ｋ縺ｪ縺ｩ縺ｮ蜃ｦ逅・′蠢・ｦ√□縺後∪縺壹・辟｡隕悶☆繧九°隕∬ｪｿ謨ｴ)
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.OutputSoundByStatus)));

        
        // 3. 谺｡縺ｮ繧ｳ繝槭Φ繝峨ｒ隱ｭ繧蜃ｦ逅・
        asm.Label(prefix + "_" + nameof(Labels.ReadSongDataOne));
        
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Fetch Command -> A
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A);

        // 0xFF (End)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_end_song"));

        // 0x60 (Rest)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadKyufuData)));

        // 0xA0 (Set Voice / Env)
        asm.CP(asm.Value((byte)0xA0));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadEnvData)));

        // 0xA1 (Set Volume)
        asm.CP(asm.Value((byte)0xA1));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadVolumeData)));

        // 0xA2 (Set PEnv)
        asm.CP(asm.Value((byte)0xA2));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadPEnvData)));

        // 0x90 (Long Length)
        asm.CP(asm.Value((byte)0x90));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_read_long_len"));

        // 0x08 (Loop Marker)
        asm.CP(asm.Value((byte)0x08));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_read_loop_marker"));

        // Noise (0x06) -> wait, we kept Noise as 0x06
        asm.CP(asm.Value((byte)0xA6));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_read_noise"));

        // Sync Noise (0x07)
        asm.CP(asm.Value((byte)0xA7));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_read_sync_noise"));

        // If A < 0x60, it's Note ON (0x00 - 0x5F)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.C, asm.LabelRef(prefix + "_" + nameof(Labels.ReadToneData)));
        
        // If A >= 0x80 AND A <= 0x8F, it's Short Length
        asm.SUB(asm.Value((byte)0x80));
        asm.CP(asm.Value((byte)0x10));
        asm.JP(asm.C, asm.LabelRef(prefix + "_read_short_len"));

        // Unknown -> Ignore and read next
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // -- Read Long Length --
        asm.Label(prefix + "_read_long_len");
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A); // BC = 16-bit Length
        
        // Store to StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + "_StatLastLength"));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // -- Read Short Length --
        asm.Label(prefix + "_read_short_len");
        // A is already (Cmd - 0x80).
        asm.AND(asm.Value((byte)0x0F));
        asm.INC(asm.A);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0); // BC = Length
        
        // Store to StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + "_StatLastLength"));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));
// -- Read Loop Marker
        asm.Label(prefix + "_read_loop_marker");
        // Save current DE (which points to the instruction AFTER 0x08) to StatLoopPosition
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLoopPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Also save DE to StatSongDataPosition so it won't read 0x08 forever
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // -- Read Envelope Command --
        asm.Label(prefix + "_" + nameof(Labels.ReadEnvData));
        asm.LD(asm.A, asm.DEref); // EnvelopeId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_env_off"));

        // Set Env Active
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvActive)));
        asm.LD(asm.HLref, 0x01);
        
        // Offset = 0
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);

        // Compute BaseAddress = DataEnvTableBase + (EnvId * 2)
        // Since we have EnvId in B
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.DataEnvTableBase)));
        asm.LD(asm.A, asm.B); 
        asm.ADD(asm.A, asm.A); // A = EnvId * 2
        // Calculate HL + A
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0);
        asm.ADD(asm.HL, asm.BC); // HL points to address containing the pointer for this Env
        
        // Fetch Env data pointer -> DE (Read from address pointed by HL)
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref); // DE is now EnvData Pointer

        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvDataPtr)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // Env Off
        asm.Label(prefix + "_env_off");
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvActive)));
        asm.LD(asm.HLref, 0x00);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // -- Read Pitch Envelope Command --
        asm.Label(prefix + "_" + nameof(Labels.ReadPEnvData));
        asm.LD(asm.A, asm.DEref); // EnvId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_penv_off"));

        // Set PEnv Active
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvActive)));
        asm.LD(asm.HLref, 0x01);
        
        // Offset = 0
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);

        // Compute BaseAddress = DataPEnvTableBase + (EnvId * 2)
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.DataPEnvTableBase)));
        asm.LD(asm.A, asm.B); 
        asm.ADD(asm.A, asm.A); // A = EnvId * 2
        // Calculate HL + A
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0);
        asm.ADD(asm.HL, asm.BC); // HL points to address containing the pointer for this Env
        
        // Fetch Env data pointer -> DE
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref); // DE is now PEnvData Pointer

        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvDataPtr)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // PEnv Off
        asm.Label(prefix + "_penv_off");
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvActive)));
        asm.LD(asm.HLref, 0x00);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        
        // -- Read Tone -- 
        asm.Label(prefix + "_" + nameof(Labels.ReadToneData));
        
        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + "_StatLastLength"));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        // Reset offsets
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);

        // Set Note Active
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        // B has the original Note ON command (Note Number)
        // Note: B was set to the command right after Fetch Command in ReadSongDataOne. Wait, B was overwritten?
        // Let's just use B since it was saved at the beginning of ReadSongDataOne and we didn't overwrite it for Tone,
        // EXCEPT we overwrote it during Long/Short Length. Wait! B is NOT overwritten for Tone because Tone is jumped to directly!
        // But let's verify. B was set: asm.LD(asm.B, asm.A); Yes!
        
        // Fetch HW register from Table
        if (port == 0xE0) {
            asm.LD(asm.HL, asm.LabelRef("DataBeepFreqTable"));
        } else {
            asm.LD(asm.HL, asm.LabelRef("DataPsgFreqTable"));
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
            if (prefix.StartsWith("track_")) {
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
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Apply Volume
        if (port == 0xE0) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x01); // BEEP ON
        } else {
            asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.A, asm.HLref);
            asm.OUT(port);
        }

        asm.JP(asm.LabelRef(prefix));

        // -- Read Rest --
        asm.Label(prefix + "_" + nameof(Labels.ReadKyufuData));
        
        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + "_StatLastLength"));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x00);

        // Send Volume=0 (0x0F) to mute
        if (port == 0xE0) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x00); // BEEP OFF
        } else {
            asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.A, asm.HLref);
            asm.AND(asm.Value((byte)0x60)); // Keep only channel bits
            asm.OR(asm.Value((byte)0x9F));  // Base Vol + 15 (Mute)
            asm.OUT(port);
        }

        // Save pos
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix));

// -- Read Noise --
        asm.Label(prefix + "_read_noise");
        // DE is pointing to 3 bytes: NoiseCmd, DurL, DurH.
        // Similar to ReadTone, but only 1 byte for freq/ctrl instead of two.
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        // Fetch NoiseCmd and OUT
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        if (port != 0xE0) { asm.OUT(port); }

        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + "_StatLastLength"));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        
        if (port != 0xE0) {
            asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.A, asm.HLref);
            asm.OUT(port);
        }
        asm.JP(asm.LabelRef(prefix));


        // -- Read Sync Noise --
        asm.Label(prefix + "_read_sync_noise");
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        if (port != 0xE0) {
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE);
            asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.HLref, asm.A);
            asm.OUT(port);
        }

        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(prefix + "_StatLastLength"));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(prefix));

// -- Read Volume --
        asm.Label(prefix + "_" + nameof(Labels.ReadVolumeData));
        asm.LD(asm.A, asm.DEref); // raw volume hw byte (1 c c 1 v v v v)
        asm.INC(asm.DE);
        
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
        asm.LD(asm.HLref, asm.A); // save

        if (port != 0xE0) {
            // SN76489縺ｫ蜊ｳ譎ゅ・繝ｪ繝･繝ｼ繝/繝溘Η繝ｼ繝医ｒ蜿肴丐
            asm.OUT(port);
        }

        // Save pos & Read Next
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // -- End Song (Looping) --
        asm.Label(prefix + "_end_song");
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLoopPosition)));
        // Get Address from StatLoopPosition -> DE
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);
        
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        // And store DE to StatSongDataPosition
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Fetch the next command to execute
        asm.LD(asm.A, asm.DEref); // A = memory at DE
        asm.LD(asm.B, 0xFF);      // B = 0xFF (End marker)
        asm.CP(asm.B);            // Compare A with B
        
        // If not 0xFF, jump to parsing (valid loop target)
        asm.JP(asm.NZ, asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // If it is 0xFF, it means we are at a halt state (data_song_end) or an empty track.
        // Set LengthRemain to 0x7FFF (about 9 minutes at 60Hz) to prevent infinite loop within a frame.
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.HLref, 0xFF);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, 0x7F);
        asm.RET();


        // -- Output By Status --
        asm.Label(prefix + "_" + nameof(Labels.OutputSoundByStatus));
        // Check Note On
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_output_end")); // Note Off -> Do nothing

        // Check Env Active
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvActive)));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_output_penv_check")); // Env Off -> skip to PEnv

        // Read Env Data pointer
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvDataPtr)));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Read Env Pos Offset
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.C, asm.HLref);
        asm.LD(asm.B, 0);

        // Calculate Data Address (DE + BC)
        asm.LD(asm.H, asm.D);
        asm.LD(asm.L, asm.E);
        asm.ADD(asm.HL, asm.BC);

        // Read current envelope Volume into A
        asm.LD(asm.A, asm.HLref);

        // is it Loop Endpoint? (0xFE)
        asm.CP(asm.Value((byte)0xFE));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_env_loop_end"));

        // it might be End marker (0xFF)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_env_end"));

        // Valid Volume in A (0-15).
        // Save Volume to B
        asm.LD(asm.B, asm.A);

        // Extract channel bits from existing StatHwVolume
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
        asm.LD(asm.A, asm.HLref);
        asm.AND((byte)0x60); // Keep only channel bits: 0110 0000
        asm.OR((byte)0x90);  // Base Vol command: 1001 0000
        asm.LD(asm.C, asm.A); // C = 1001 c c 00

        // Envelope Vol is in B (0-15) where 0=silent, 15=max in MML.
        // HW requires 15=silent, 0=max.
        if (port == 0xE0) {
            // Envelope applies to BEEP ON/OFF
            asm.LD(asm.A, asm.B);
            asm.CP(asm.Value(0)); // If 0 (silent)
            asm.JP(asm.Z, asm.LabelRef(prefix + "_env_vol_mute"));
            asm.LD(asm.A, (byte)1);
            asm.JP(asm.LabelRef(prefix + "_env_vol_apply"));
            
            asm.Label(prefix + "_env_vol_mute");
            asm.LD(asm.A, (byte)0);
            
            asm.Label(prefix + "_env_vol_apply");
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, asm.A);
        } else {
            asm.LD(asm.A, (byte)15);
            asm.SUB(asm.B); // A = 15 - B
            asm.OR(asm.C);  // Combine with channel bits: 1001 c c X X

            // Save back for consistency and OUT
            asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
            asm.LD(asm.HLref, asm.A);
            asm.OUT(port);
        }

        // Increment Offset
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.INC(asm.HLref);

        asm.Label(prefix + "_output_penv_check");
        // Check PEnv Active
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvActive)));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_output_end")); // PEnv Off -> End

        // Read PEnv Data pointer
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvDataPtr)));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Read PEnv Pos Offset
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.C, asm.HLref);
        asm.LD(asm.B, 0);

        // Calculate Data Address (DE + BC * 2) since items are 2 bytes (ushort)
        asm.LD(asm.H, asm.D);
        asm.LD(asm.L, asm.E);
        asm.ADD(asm.HL, asm.BC);
        asm.ADD(asm.HL, asm.BC);

        // Read Byte 1 (Low byte = cmd1)
        asm.LD(asm.A, asm.HLref);
        
        // is it Loop Endpoint? (0xFE)
        asm.CP(asm.Value((byte)0xFE));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_penv_loop_end"));

        // it might be End marker (0xFF)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(prefix + "_penv_end"));

        // Output Byte 1
        if (port == 0xE0) {
            asm.PUSH(asm.HL); // Save HL (pointer to PEnvData)
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
            asm.POP(asm.HL);  // Restore HL
        } else {
            asm.OUT(port);
        }
        
        // Read Byte 2 (High byte = cmd2)
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        
        // Output Byte 2
        if (port == 0xE0) {
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
        } else {
            asm.OUT(port);
        }

        // Increment Offset
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.INC(asm.HLref);

        asm.Label(prefix + "_output_end");
        asm.RET();

        asm.Label(prefix + "_env_loop_end");
        // Read the next byte which contains the loop offset
        // HL currently points to the 0xFE byte. The offset is at HL+1.
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref); // A = loop offset
        // Store it to StatEnvPosOffset
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, asm.A);
        // JP back to OutputSoundByStatus to output the looped value in the same frame
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.OutputSoundByStatus)));
        
        asm.Label(prefix + "_env_end");
        // If 0xFF, stay at the last valid position
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.DEC(asm.HLref);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.OutputSoundByStatus)));

        // PEnv loop handlers
        asm.Label(prefix + "_penv_loop_end");
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, asm.A);
        asm.JP(asm.LabelRef(prefix + "_output_penv_check"));
        
        asm.Label(prefix + "_penv_end");
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.DEC(asm.HLref);
        asm.JP(asm.LabelRef(prefix + "_output_penv_check"));


        // -- Stat Variables --
        asm.Label(prefix + "_" + nameof(Labels.StatSongDataPosition));
        asm.DB(asm.LabelRef(prefix + "_" + nameof(Labels.DataSong))); // Initialize with Data Start Address
        
        asm.Label(prefix + "_" + nameof(Labels.StatLoopPosition));
        asm.DB(asm.LabelRef(prefix + "_data_song_end")); // Initialize loop point to End Address (No loop by default)

        asm.Label(prefix + "_" + nameof(Labels.StatLengthRemain));
        asm.DB(new byte[] { 0, 0 });
        
        asm.Label(prefix + "_StatLastLength");
        asm.DB(new byte[] { 0, 0 });
        
        asm.Label(prefix + "_" + nameof(Labels.StatGateRemain));
        asm.DB(new byte[] { 0, 0 });

        asm.Label(prefix + "_" + nameof(Labels.StatNoteOn));
        asm.DB(0);

        asm.Label(prefix + "_" + nameof(Labels.StatHwVolume));
        asm.DB(0); // Holds the raw SN76489 volume byte

        asm.Label(prefix + "_" + nameof(Labels.StatEnvActive));
        asm.DB(0); 

        asm.Label(prefix + "_" + nameof(Labels.StatEnvDataPtr));
        asm.DB(new byte[] { 0, 0 });

        asm.Label(prefix + "_" + nameof(Labels.StatEnvPosOffset));
        asm.DB(0);

        asm.Label(prefix + "_" + nameof(Labels.StatPEnvActive));
        asm.DB(0); 

        asm.Label(prefix + "_" + nameof(Labels.StatPEnvDataPtr));
        asm.DB(new byte[] { 0, 0 });

        asm.Label(prefix + "_" + nameof(Labels.StatPEnvPosOffset));
        asm.DB(0);

        // -- Envelope Data Tables --
        // To make it simpler, we embed the global VolumeEnvelopes table inside each channel's memory block,
        // or we can embed it once globally. Since we only loop channels here, we'll embed one copy per channel for simplicity of addressing.
        asm.Label(prefix + "_" + nameof(Labels.DataEnvTableBase));
        
        // Find max EnvId to allocate contiguous pointer table
        int maxEnvId = -1;
        foreach (var id in VolumeEnvelopes.Keys) if (id > maxEnvId) maxEnvId = id;

        for (int i = 0; i <= maxEnvId; i++)
        {
            if (VolumeEnvelopes.ContainsKey(i))
            {
                asm.DW(asm.LabelRef(prefix + "_env_data_" + i));
            }
            else
            {
                // Dummy/empty
                asm.DW(asm.LabelRef(prefix + "_env_data_empty"));
            }
        }

        // Dummy empty data
        asm.Label(prefix + "_env_data_empty");
        asm.DB(0xFF);

        // Env Array Definitions
        foreach (var kvp in VolumeEnvelopes)
        {
            asm.Label(prefix + "_env_data_" + kvp.Key);
            
            var envData = kvp.Value;
            foreach (var vol in envData.Values)
            {
                asm.DB((byte)(vol & 0xFF));
            }
            
            // Output loop or end marker
            if (envData.LoopIndex >= 0 && envData.LoopIndex < envData.Values.Count)
            {
                asm.DB(0xFE); // Loop marker
                asm.DB((byte)(envData.LoopIndex & 0xFF)); // Offset
            }
            else
            {
                asm.DB(0xFF); // Terminator (End marker)
            }
        }

        // PEnv Array Definitions
        asm.Label(prefix + "_" + nameof(Labels.DataPEnvTableBase));
        
        int maxPEnvId = -1;
        if (HwPitchEnvelopes.Count > 0)
        {
            maxPEnvId = HwPitchEnvelopes.Count - 1;
        }

        for (int i = 0; i <= maxPEnvId; i++)
        {
            if (i < HwPitchEnvelopes.Count)
            {
                asm.DW(asm.LabelRef(prefix + "_penv_data_" + i));
            }
            else
            {
                asm.DW(asm.LabelRef(prefix + "_penv_data_empty"));
            }
        }

        asm.Label(prefix + "_penv_data_empty");
        asm.DB(0xFF);
        asm.DB(0xFF); // Align 2 bytes

        foreach (var penv in HwPitchEnvelopes)
        {
            asm.Label(prefix + "_penv_data_" + penv.Id);
            
            foreach (ushort hwVal in penv.AbsoluteRegisters)
            {
                asm.DB((byte)(hwVal & 0xFF));
                asm.DB((byte)((hwVal >> 8) & 0xFF));
            }
            
            if (penv.LoopIndex >= 0 && penv.LoopIndex < penv.AbsoluteRegisters.Count)
            {
                asm.DB(0xFE);
                asm.DB((byte)(penv.LoopIndex & 0xFF));
            }
            else
            {
                asm.DB(0xFF);
                asm.DB(0xFF);
            }
        }
    }

    private void AppendImageLoader(MZ1500Assembler asm, byte[] pcgData)
    {
        string prefix = "pcg_";

        asm.Label("ImageLoader");
        asm.CALL(asm.LabelRef(prefix + "CLS"));
        asm.CALL(asm.LabelRef(prefix + "start"));
        asm.RET();

        asm.Label(prefix + "CLS");
        asm.CALL(asm.Value((ushort)0x0DA6)); // Basic CLS routine
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.BC, 40 * 25);
        asm.XOR(asm.A);
        asm.CALL(asm.LabelRef(prefix + "MEMFIL"));

        asm.LD(asm.HL, 0xD800);
        asm.LD(asm.BC, 40 * 25);
        asm.LD(asm.A, 0x0);
        asm.CALL(asm.LabelRef(prefix + "MEMFIL"));
        asm.RET();

        asm.Label(prefix + "MEMFIL");
        asm.LD(asm.D, asm.H);
        asm.LD(asm.E, asm.L);
        asm.INC(asm.DE);
        asm.DEC(asm.BC);
        asm.LD(asm.HLref, asm.A);
        asm.LDIR();
        asm.RET();

        asm.Label(prefix + "start");
        
        asm.OUT(0xF1); // F1 Output
        asm.LD(asm.A, 0x1);
        asm.OUT(0xF0); // F0 Output for Display Priority/Screen 2

        // PCG Pattern setup
        byte e5 = 0xE5;

        // Bank 3 (Green)
        asm.LD(asm.DE, asm.LabelRef(prefix + "PSGData-Green-start"));
        asm.LD(asm.BC, asm.LabelRef(prefix + "PSGData-Green-end"));
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.A, 0x3);
        asm.OUT(e5);
        asm.CALL(asm.LabelRef(prefix + "LoopStart"));

        // Bank 2 (Red)
        asm.LD(asm.DE, asm.LabelRef(prefix + "PSGData-Red-start"));
        asm.LD(asm.BC, asm.LabelRef(prefix + "PSGData-Red-end"));
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.A, 0x2);
        asm.OUT(e5);
        asm.CALL(asm.LabelRef(prefix + "LoopStart"));

        // Bank 1 (Blue)
        asm.LD(asm.DE, asm.LabelRef(prefix + "PSGData-Blue-start"));
        asm.LD(asm.BC, asm.LabelRef(prefix + "PSGData-Blue-end"));
        asm.LD(asm.HL, 0xD000);
        asm.LD(asm.A, 0x1);
        asm.OUT(e5);
        asm.CALL(asm.LabelRef(prefix + "LoopStart"));

        asm.JP(asm.LabelRef(prefix + "LoopEnd"));

        asm.Label(prefix + "LoopStart");
        asm.LD(asm.A, asm.DEref); // Get 1 byte of PCG
        asm.LD(asm.HLref, asm.A); // Write to VRAM
        asm.INC(asm.DE);
        asm.INC(asm.HL);

        asm.LD(asm.A, asm.B);
        asm.CP(asm.D);
        asm.JP(asm.NZ, asm.LabelRef(prefix + "LoopStart"));
        asm.LD(asm.A, asm.C);
        asm.CP(asm.E);
        asm.JP(asm.NZ, asm.LabelRef(prefix + "LoopStart"));
        asm.RET();

        asm.Label(prefix + "LoopEnd");

        // Set Screen VRAM characters to match PCG indices
        asm.Label(prefix + "VRAM-start");
        asm.LD(asm.HL, 0xD400); // Screen 2 VRAM
        asm.LD(asm.DE, 0xDC00); // Screen 2 Color Data
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b00001000); // 0x08
        asm.CALL(asm.LabelRef(prefix + "VRAM-loop"));
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b01001000); // 0x48
        asm.CALL(asm.LabelRef(prefix + "VRAM-loop"));
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b10001000); // 0x88
        asm.CALL(asm.LabelRef(prefix + "VRAM-loop"));
        
        asm.LD(asm.B, 0x00);
        asm.LD(asm.C, 0b11001000); // 0xC8
        asm.CALL(asm.LabelRef(prefix + "VRAM-loop"));
        
        asm.JP(asm.LabelRef(prefix + "loop_skip_pcg:")); // bypass loop symbol name collision

        asm.Label(prefix + "VRAM-loop");
        asm.LD(asm.A, 0x1);
        asm.OUT(0xE6); // PCG Access Enable?

        asm.LD(asm.HLref, asm.B); // Set char code 0~255
        asm.INC(asm.HL);

        asm.LD(asm.A, asm.C);
        asm.LD(asm.DEref, asm.A); // Set color attribute
        asm.INC(asm.DE);

        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "VRAM-loop-return"));
        asm.INC(asm.B);
        asm.JP(asm.LabelRef(prefix + "VRAM-loop"));

        asm.Label(prefix + "VRAM-loop-return");
        asm.RET();

        asm.Label(prefix + "loop_skip_pcg:");
        asm.RET();

        // Data payload
        var greenPlane = new byte[8000];
        var redPlane = new byte[8000];
        var bluePlane = new byte[8000];
        Array.Copy(pcgData, 0, greenPlane, 0, 8000);
        Array.Copy(pcgData, 8000, redPlane, 0, 8000);
        Array.Copy(pcgData, 16000, bluePlane, 0, 8000);

        asm.Label(prefix + "PSGData-Green-start");
        asm.DB(greenPlane);
        asm.Label(prefix + "PSGData-Green-end");

        asm.Label(prefix + "PSGData-Red-start");
        asm.DB(redPlane);
        asm.Label(prefix + "PSGData-Red-end");

        asm.Label(prefix + "PSGData-Blue-start");
        asm.DB(bluePlane);
        asm.Label(prefix + "PSGData-Blue-end");
    }
}
