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

public class MZ1500SoundDriverGenerator
{
    public List<Channel> ChannelList { get; } = new();
    
    // MmlPlayerModel縺九ｉ貂｡縺輔ｌ繧九お繝ｳ繝吶Ο繝ｼ繝怜ｮ夂ｾｩ繝・・繧ｿ (EnvId -> 繝懊Μ繝･繝ｼ繝驟榊・)
    public Dictionary<int, EnvelopeData> VolumeEnvelopes { get; set; } = new();
    
    // HwPitchEnv繝・・繧ｿ
    public List<Z80SequenceCompiler.HwPitchEnvData> HwPitchEnvelopes { get; set; } = new();

    public void AppendChannel(Channel channel) => ChannelList.Add(channel);

    

    public byte[] Build(byte[]? pcgData = null)
    {
        var assembler = new Z80Assembler();
        
        assembler.ORG(0x1200);
        assembler.Label(new AsmLabel("main:"));
        assembler.DI();
        
        if (pcgData != null && pcgData.Length == 24000)
        {
            assembler.CALL(assembler.LabelRef(new AsmLabel("ImageLoader")));
            assembler.JP(assembler.LabelRef(new AsmLabel("main2:")));
            MZ1500PcgLoader.AppendImageLoader(assembler, pcgData);
            assembler.Label(new AsmLabel("main2:"));
        }

        assembler.IM1();

        // 蜑ｲ繧願ｾｼ縺ｿ繝吶け繧ｿ縺ｮ險ｭ螳・
        assembler.LD(assembler.HL, 0x1039);
        assembler.LD(assembler.DE, assembler.LabelRef(new AsmLabel("sound:")));
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
        assembler.Label(new AsmLabel("loop:"));
        
        assembler.JP(assembler.LabelRef(new AsmLabel("loop:")));

        // 蜑ｲ繧願ｾｼ縺ｿ繝上Φ繝峨Λ
        assembler.Label(new AsmLabel("sound:"));
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
            assembler.CALL(assembler.LabelRef(new AsmLabel(ch.Name)));
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
            AppendPlayChannelSource(new ChannelContext(ch.Name), assembler, ch.IOPort);
        }

        
        // ----- 逕滓・縺励◆蜻ｨ豕｢謨ｰ繝・・繝悶Ν縺ｮ霑ｽ蜉 -----
        assembler.Label(new AsmLabel("DataPsgFreqTable"));
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

        assembler.Label(new AsmLabel("DataBeepFreqTable"));
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
            assembler.Label(new ChannelContext(ch.Name).DataSong);
            assembler.DB(ch.SequenceData);
            assembler.Label(new ChannelContext(ch.Name).DataSongEnd);
            assembler.DB(0xFF); // 螳牙・逕ｨ縺ｮ邨らｫｯ繝槭・繧ｫ繝ｼ (L逵∫払譎ゅ↓縺薙％縺ｫ繧ｸ繝｣繝ｳ繝励＠縺ｦ蛛懈ｭ｢縺礼ｶ壹￠繧・
        }

        return assembler.Build();
    }

    private void AppendPlayChannelSource(ChannelContext ctx, Z80Assembler asm, byte port)
    {
        asm.Label(ctx.PrefixLabel);
        
        // 1. 繝ｬ繝ｳ繧ｰ繧ｹ(Duration)縺ｮ貂帛ｰ代→蛻､螳・
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLengthRemain));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.NZ, asm.LabelRef(ctx.DecDurLower));
        // lower is 0, check upper
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.DEC(asm.HL); // restore HL
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadSongDataOne)); // both 0 -> next command
        
        // dec upper
        asm.INC(asm.HL);
        asm.DEC(asm.HLref);
        asm.DEC(asm.HL);
        
        asm.Label(ctx.DecDurLower);
        asm.DEC(asm.HLref);

        // 2. 繧ｲ繝ｼ繝・Gate)縺ｮ蜃ｦ逅・(邁｡譏灘ｮ溯｣・ Duration荳ｭ縺ｫGate縺悟・繧後◆繧蛾浹驥上ｒ辟｡髻ｳ縺ｫ縺吶ｋ縺ｪ縺ｩ縺ｮ蜃ｦ逅・′蠢・ｦ√□縺後∪縺壹・辟｡隕悶☆繧九°隕∬ｪｿ謨ｴ)
        asm.JP(asm.LabelRef(ctx.OutputSoundByStatus));

        
        // 3. 谺｡縺ｮ繧ｳ繝槭Φ繝峨ｒ隱ｭ繧蜃ｦ逅・
        asm.Label(ctx.ReadSongDataOne);
        
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Fetch Command -> A
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A);

        // 0xFF (End)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(ctx.EndSong));

        // 0x60 (Rest)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadKyufuData));

        // 0xA0 (Set Voice / Env)
        asm.CP(asm.Value((byte)0xA0));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadEnvData));

        // 0xA1 (Set Volume)
        asm.CP(asm.Value((byte)0xA1));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadVolumeData));

        // 0xA2 (Set PEnv)
        asm.CP(asm.Value((byte)0xA2));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadPEnvData));

        // 0x90 (Long Length)
        asm.CP(asm.Value((byte)0x90));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadLongLen));

        // 0x08 (Loop Marker)
        asm.CP(asm.Value((byte)0x08));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadLoopMarker));

        // Noise (0x06) -> wait, we kept Noise as 0x06
        asm.CP(asm.Value((byte)0xA6));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadNoise));

        // Sync Noise (0x07)
        asm.CP(asm.Value((byte)0xA7));
        asm.JP(asm.Z, asm.LabelRef(ctx.ReadSyncNoise));

        // If A < 0x60, it's Note ON (0x00 - 0x5F)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.C, asm.LabelRef(ctx.ReadToneData));
        
        // If A >= 0x80 AND A <= 0x8F, it's Short Length
        asm.SUB(asm.Value((byte)0x80));
        asm.CP(asm.Value((byte)0x10));
        asm.JP(asm.C, asm.LabelRef(ctx.ReadShortLen));

        // Unknown -> Ignore and read next
        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        // -- Read Long Length --
        asm.Label(ctx.ReadLongLen);
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A); // BC = 16-bit Length
        
        // Store to StatLastLength
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLastLength));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        // -- Read Short Length --
        asm.Label(ctx.ReadShortLen);
        // A is already (Cmd - 0x80).
        asm.AND(asm.Value((byte)0x0F));
        asm.INC(asm.A);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0); // BC = Length
        
        // Store to StatLastLength
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLastLength));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));
// -- Read Loop Marker
        asm.Label(ctx.ReadLoopMarker);
        // Save current DE (which points to the instruction AFTER 0x08) to StatLoopPosition
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLoopPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Also save DE to StatSongDataPosition so it won't read 0x08 forever
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        // -- Read Envelope Command --
        asm.Label(ctx.ReadEnvData);
        asm.LD(asm.A, asm.DEref); // EnvelopeId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(new AsmLabel($"{ctx.Prefix}_env_off")));

        // Set Env Active
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvActive));
        asm.LD(asm.HLref, 0x01);
        
        // Offset = 0
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
        asm.LD(asm.HLref, 0x00);

        // Compute BaseAddress = DataEnvTableBase + (EnvId * 2)
        // Since we have EnvId in B
        asm.LD(asm.HL, asm.LabelRef(ctx.DataEnvTableBase));
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

        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvDataPtr));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        // Env Off
        asm.Label(new AsmLabel($"{ctx.Prefix}_env_off"));
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvActive));
        asm.LD(asm.HLref, 0x00);
        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        // -- Read Pitch Envelope Command --
        asm.Label(ctx.ReadPEnvData);
        asm.LD(asm.A, asm.DEref); // EnvId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(new AsmLabel($"{ctx.Prefix}_penv_off")));

        // Set PEnv Active
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvActive));
        asm.LD(asm.HLref, 0x01);
        
        // Offset = 0
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
        asm.LD(asm.HLref, 0x00);

        // Compute BaseAddress = DataPEnvTableBase + (EnvId * 2)
        asm.LD(asm.HL, asm.LabelRef(ctx.DataPEnvTableBase));
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

        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvDataPtr));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        // PEnv Off
        asm.Label(new AsmLabel($"{ctx.Prefix}_penv_off"));
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvActive));
        asm.LD(asm.HLref, 0x00);
        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        
        // -- Read Tone -- 
        asm.Label(ctx.ReadToneData);
        
        asm.PUSH(asm.BC); // Save B (Note Number)
        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLastLength));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLengthRemain));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        asm.POP(asm.BC); // Restore B (Note Number)

        // Reset offsets
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
        asm.LD(asm.HLref, 0x00);

        // Set Note Active
        asm.LD(asm.HL, asm.LabelRef(ctx.StatNoteOn));
        asm.LD(asm.HLref, 0x01);

        // B has the original Note ON command (Note Number)
        // Note: B was set to the command right after Fetch Command in ReadSongDataOne. Wait, B was overwritten?
        // Let's just use B since it was saved at the beginning of ReadSongDataOne and we didn't overwrite it for Tone,
        // EXCEPT we overwrote it during Long/Short Length. Wait! B is NOT overwritten for Tone because Tone is jumped to directly!
        // But let's verify. B was set: asm.LD(asm.B, asm.A); Yes!
        
        // Fetch HW register from Table
        if (port == 0xE0) {
            asm.LD(asm.HL, asm.LabelRef(new AsmLabel("DataBeepFreqTable")));
        } else {
            asm.LD(asm.HL, asm.LabelRef(new AsmLabel("DataPsgFreqTable")));
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
            // Determine PSG Channel from prefix (track_P1 -> 0, etc.)
            int psgCh = 0;
            if (ctx.Prefix.StartsWith("track_P")) {
                if (int.TryParse(ctx.Prefix.Substring(7), out int trkNum)) {
                    psgCh = Math.Max(0, trkNum - 1) % 3; // P1->0, P2->1, P3->2
                }
            } else if (ctx.Prefix.StartsWith("track_N")) {
                psgCh = 3; // Noise
            } else if (ctx.Prefix.StartsWith("track_")) {
                int.TryParse(ctx.Prefix.Substring(6), out psgCh);
            }
            byte chBits = (byte)(0x80 | ((psgCh & 0x03) << 5));
            asm.OR(asm.Value(chBits));
            asm.OUT(port);
            
            asm.INC(asm.HL);
            asm.LD(asm.A, asm.HLref); // high byte
            asm.OUT(port);
        }

        // Save pos
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Apply Volume
        if (port == 0xE0) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x01); // BEEP ON
        } else {
            asm.LD(asm.HL, asm.LabelRef(ctx.StatHwVolume));
            asm.LD(asm.A, asm.HLref);
            asm.OUT(port);
        }

        asm.JP(asm.LabelRef(ctx.PrefixLabel));

        // -- Read Rest --
        asm.Label(ctx.ReadKyufuData);
        
        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLastLength));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLengthRemain));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(ctx.StatNoteOn));
        asm.LD(asm.HLref, 0x00);

        // Send Volume=0 (0x0F) to mute
        if (port == 0xE0) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x00); // BEEP OFF
        } else {
            asm.LD(asm.HL, asm.LabelRef(ctx.StatHwVolume));
            asm.LD(asm.A, asm.HLref);
            asm.AND(asm.Value((byte)0x60)); // Keep only channel bits
            asm.OR(asm.Value((byte)0x9F));  // Base Vol + 15 (Mute)
            asm.OUT(port);
        }

        // Save pos
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(ctx.PrefixLabel));

// -- Read Noise --
        asm.Label(ctx.ReadNoise);
        // DE is pointing to 3 bytes: NoiseCmd, DurL, DurH.
        // Similar to ReadTone, but only 1 byte for freq/ctrl instead of two.
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatNoteOn));
        asm.LD(asm.HLref, 0x01);

        // Fetch NoiseCmd and OUT
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        if (port != 0xE0) { asm.OUT(port); }

        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLastLength));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLengthRemain));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        
        if (port != 0xE0) {
            asm.LD(asm.HL, asm.LabelRef(ctx.StatHwVolume));
            asm.LD(asm.A, asm.HLref);
            asm.OUT(port);
        }
        asm.JP(asm.LabelRef(ctx.PrefixLabel));


        // -- Read Sync Noise --
        asm.Label(ctx.ReadSyncNoise);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatNoteOn));
        asm.LD(asm.HLref, 0x01);

        if (port != 0xE0) {
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.OUT(port);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE);
            asm.LD(asm.HL, asm.LabelRef(ctx.StatHwVolume));
            asm.LD(asm.HLref, asm.A);
            asm.OUT(port);
        }

        // Setup length from StatLastLength
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLastLength));
        asm.LD(asm.C, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.B, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLengthRemain));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(ctx.PrefixLabel));

// -- Read Volume --
        asm.Label(ctx.ReadVolumeData);
        asm.LD(asm.A, asm.DEref); // raw volume hw byte (1 c c 1 v v v v)
        asm.INC(asm.DE);
        
        asm.LD(asm.HL, asm.LabelRef(ctx.StatHwVolume));
        asm.LD(asm.HLref, asm.A); // save

        if (port != 0xE0) {
            // SN76489縺ｫ蜊ｳ譎ゅ・繝ｪ繝･繝ｼ繝/繝溘Η繝ｼ繝医ｒ蜿肴丐
            asm.OUT(port);
        }

        // Save pos & Read Next
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(ctx.ReadSongDataOne));

        // -- End Song (Looping) --
        asm.Label(ctx.EndSong);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLoopPosition));
        // Get Address from StatLoopPosition -> DE
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);
        
        asm.LD(asm.HL, asm.LabelRef(ctx.StatSongDataPosition));
        // And store DE to StatSongDataPosition
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Fetch the next command to execute
        asm.LD(asm.A, asm.DEref); // A = memory at DE
        asm.LD(asm.B, 0xFF);      // B = 0xFF (End marker)
        asm.CP(asm.B);            // Compare A with B
        
        // If not 0xFF, jump to parsing (valid loop target)
        asm.JP(asm.NZ, asm.LabelRef(ctx.ReadSongDataOne));

        // If it is 0xFF, it means we are at a halt state (data_song_end) or an empty track.
        // Set LengthRemain to 0x7FFF (about 9 minutes at 60Hz) to prevent infinite loop within a frame.
        asm.LD(asm.HL, asm.LabelRef(ctx.StatLengthRemain));
        asm.LD(asm.HLref, 0xFF);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, 0x7F);
        asm.RET();


        // -- Output By Status --
        asm.Label(ctx.OutputSoundByStatus);
        // Check Note On
        asm.LD(asm.HL, asm.LabelRef(ctx.StatNoteOn));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(ctx.OutputEnd)); // Note Off -> Do nothing

        // Check Env Active
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvActive));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(ctx.OutputPenvCheck)); // Env Off -> skip to PEnv

        // Read Env Data pointer
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvDataPtr));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Read Env Pos Offset
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
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
        asm.JP(asm.Z, asm.LabelRef(ctx.EnvLoopEnd));

        // it might be End marker (0xFF)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(ctx.EnvEnd));

        // Valid Volume in A (0-15).
        // Save Volume to B
        asm.LD(asm.B, asm.A);

        // Extract channel bits from existing StatHwVolume
        asm.LD(asm.HL, asm.LabelRef(ctx.StatHwVolume));
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
            asm.JP(asm.Z, asm.LabelRef(ctx.EnvVolMute));
            asm.LD(asm.A, (byte)1);
            asm.JP(asm.LabelRef(ctx.EnvVolApply));
            
            asm.Label(ctx.EnvVolMute);
            asm.LD(asm.A, (byte)0);
            
            asm.Label(ctx.EnvVolApply);
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, asm.A);
        } else {
            asm.LD(asm.A, (byte)15);
            asm.SUB(asm.B); // A = 15 - B
            asm.OR(asm.C);  // Combine with channel bits: 1001 c c X X

            // Save back for consistency and OUT
            asm.LD(asm.HL, asm.LabelRef(ctx.StatHwVolume));
            asm.LD(asm.HLref, asm.A);
            asm.OUT(port);
        }

        // Increment Offset
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
        asm.INC(asm.HLref);

        asm.Label(ctx.OutputPenvCheck);
        // Check PEnv Active
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvActive));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(ctx.OutputEnd)); // PEnv Off -> End

        // Read PEnv Data pointer
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvDataPtr));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Read PEnv Pos Offset
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
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
        asm.JP(asm.Z, asm.LabelRef(ctx.PenvLoopEnd));

        // it might be End marker (0xFF)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(ctx.PenvEnd));

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
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
        asm.INC(asm.HLref);

        asm.Label(ctx.OutputEnd);
        asm.RET();

        asm.Label(ctx.EnvLoopEnd);
        // Read the next byte which contains the loop offset
        // HL currently points to the 0xFE byte. The offset is at HL+1.
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref); // A = loop offset
        // Store it to StatEnvPosOffset
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
        asm.LD(asm.HLref, asm.A);
        // JP back to OutputSoundByStatus to output the looped value in the same frame
        asm.JP(asm.LabelRef(ctx.OutputSoundByStatus));
        
        asm.Label(ctx.EnvEnd);
        // If 0xFF, stay at the last valid position
        asm.LD(asm.HL, asm.LabelRef(ctx.StatEnvPosOffset));
        asm.DEC(asm.HLref);
        asm.JP(asm.LabelRef(ctx.OutputSoundByStatus));

        // PEnv loop handlers
        asm.Label(ctx.PenvLoopEnd);
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
        asm.LD(asm.HLref, asm.A);
        asm.JP(asm.LabelRef(ctx.OutputPenvCheck));
        
        asm.Label(ctx.PenvEnd);
        asm.LD(asm.HL, asm.LabelRef(ctx.StatPEnvPosOffset));
        asm.DEC(asm.HLref);
        asm.JP(asm.LabelRef(ctx.OutputPenvCheck));


        // -- Stat Variables --
        asm.Label(ctx.StatSongDataPosition);
        asm.DB(asm.LabelRef(ctx.DataSong)); // Initialize with Data Start Address
        
        asm.Label(ctx.StatLoopPosition);
        asm.DB(asm.LabelRef(ctx.DataSongEnd)); // Initialize loop point to End Address (No loop by default)

        asm.Label(ctx.StatLengthRemain);
        asm.DB(new byte[] { 0, 0 });
        
        asm.Label(ctx.StatLastLength);
        asm.DB(new byte[] { 0, 0 });
        
        asm.Label(ctx.StatGateRemain);
        asm.DB(new byte[] { 0, 0 });

        asm.Label(ctx.StatNoteOn);
        asm.DB(0);

        asm.Label(ctx.StatHwVolume);
        asm.DB(0); // Holds the raw SN76489 volume byte

        asm.Label(ctx.StatEnvActive);
        asm.DB(0); 

        asm.Label(ctx.StatEnvDataPtr);
        asm.DB(new byte[] { 0, 0 });

        asm.Label(ctx.StatEnvPosOffset);
        asm.DB(0);

        asm.Label(ctx.StatPEnvActive);
        asm.DB(0); 

        asm.Label(ctx.StatPEnvDataPtr);
        asm.DB(new byte[] { 0, 0 });

        asm.Label(ctx.StatPEnvPosOffset);
        asm.DB(0);

        // -- Envelope Data Tables --
        // To make it simpler, we embed the global VolumeEnvelopes table inside each channel's memory block,
        // or we can embed it once globally. Since we only loop channels here, we'll embed one copy per channel for simplicity of addressing.
        asm.Label(ctx.DataEnvTableBase);
        
        // Find max EnvId to allocate contiguous pointer table
        int maxEnvId = -1;
        foreach (var id in VolumeEnvelopes.Keys) if (id > maxEnvId) maxEnvId = id;

        for (int i = 0; i <= maxEnvId; i++)
        {
            if (VolumeEnvelopes.ContainsKey(i))
            {
                asm.DW(asm.LabelRef(new AsmLabel($"{ctx.Prefix}_env_data_{i}")));
            }
            else
            {
                // Dummy/empty
                asm.DW(asm.LabelRef(new AsmLabel($"{ctx.Prefix}_env_data_empty")));
            }
        }

        // Dummy empty data
        asm.Label(new AsmLabel($"{ctx.Prefix}_env_data_empty"));
        asm.DB(0xFF);

        // Env Array Definitions
        foreach (var kvp in VolumeEnvelopes)
        {
            asm.Label(new AsmLabel($"{ctx.Prefix}_env_data_{kvp.Key}"));
            
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
        asm.Label(ctx.DataPEnvTableBase);
        
        int maxPEnvId = -1;
        if (HwPitchEnvelopes.Count > 0)
        {
            maxPEnvId = HwPitchEnvelopes.Count - 1;
        }

        for (int i = 0; i <= maxPEnvId; i++)
        {
            if (i < HwPitchEnvelopes.Count)
            {
                asm.DW(asm.LabelRef(new AsmLabel($"{ctx.Prefix}_penv_data_{i}")));
            }
            else
            {
                asm.DW(asm.LabelRef(new AsmLabel($"{ctx.Prefix}_penv_data_empty")));
            }
        }

        asm.Label(new AsmLabel($"{ctx.Prefix}_penv_data_empty"));
        asm.DB(0xFF);
        asm.DB(0xFF); // Align 2 bytes

        foreach (var penv in HwPitchEnvelopes)
        {
            asm.Label(new AsmLabel($"{ctx.Prefix}_penv_data_{penv.Id}"));
            
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

    }
