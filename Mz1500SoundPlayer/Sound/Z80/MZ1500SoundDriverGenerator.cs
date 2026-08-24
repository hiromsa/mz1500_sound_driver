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
    public static readonly MainRoutineContext MainCtx = new();
    private static readonly SharedRoutineContext _beepCtx = new(true);
    private static readonly SharedRoutineContext _psgCtx = new(false);
    private static SharedRoutineContext GetSharedCtx(bool isBeep) => isBeep ? _beepCtx : _psgCtx;

    private static Dictionary<string, ChannelDataContext> _channelCtxs = new();
    private static ChannelDataContext GetChannelCtx(string name)
    {
        if (!_channelCtxs.TryGetValue(name, out var ctx))
        {
            ctx = new ChannelDataContext(name);
            _channelCtxs[name] = ctx;
        }
        return ctx;
    }

    // ===== Channel Data (IX) Offsets =====
    public const sbyte IxStatSongDataPosition = 0; // 2 bytes
    public const sbyte IxStatLoopPosition = 2;     // 2 bytes
    public const sbyte IxStatLengthRemain = 4;     // 2 bytes
    public const sbyte IxStatLastLength = 6;       // 2 bytes
    public const sbyte IxStatGateRemain = 8;       // 2 bytes
    public const sbyte IxStatNoteOn = 10;          // 1 byte
    public const sbyte IxStatHwVolume = 11;        // 1 byte
    public const sbyte IxStatEnvActive = 12;       // 1 byte
    public const sbyte IxStatEnvDataPtr = 13;      // 2 bytes
    public const sbyte IxStatEnvPosOffset = 15;    // 1 byte
    public const sbyte IxStatPEnvActive = 16;      // 1 byte
    public const sbyte IxStatPEnvDataPtr = 17;     // 2 bytes
    public const sbyte IxStatPEnvPosOffset = 19;   // 1 byte
    public const sbyte IxPortType = 20;            // 1 byte (0xF2 or 0xE0)
    public const sbyte IxPsgChannelBits = 21;      // 1 byte (0x80, 0xA0, 0xC0, 0xE0)
    
    public const int ChannelDataSize = 22;

    public List<Channel> ChannelList { get; } = new();
    
    public Dictionary<int, EnvelopeData> VolumeEnvelopes { get; set; } = new();
    public List<Z80SequenceCompiler.HwPitchEnvData> HwPitchEnvelopes { get; set; } = new();

    public void AppendChannel(Channel channel) => ChannelList.Add(channel);

    public byte[] Build(byte[]? pcgData = null)
    {
        var assembler = new Z80Assembler();
        
        assembler.ORG(0x1200);
        assembler.Label(MainCtx.Main);
        assembler.DI();
        
        if (pcgData != null && pcgData.Length == 24000)
        {
            assembler.CALL(assembler.LabelRef(MainCtx.ImageLoader));
            assembler.JP(assembler.LabelRef(MainCtx.Main2));
            MZ1500PcgLoader.AppendImageLoader(assembler, pcgData);
            assembler.Label(MainCtx.Main2);
        }

        assembler.IM1();
        assembler.LD(assembler.HL, 0x1039);
        assembler.LD(assembler.DE, assembler.LabelRef(MainCtx.Sound));
        assembler.LD(assembler.HLref, assembler.E);
        assembler.INC(assembler.HL);
        assembler.LD(assembler.HLref, assembler.D);

        assembler.LD(assembler.HL, 0xE007);
        assembler.LD(assembler.HLref, 0xB0);
        assembler.LD(assembler.HLref, 0x74);
        assembler.DEC(assembler.HL);
        assembler.LD(assembler.HLref, 0x83);
        assembler.LD(assembler.HLref, 0x00);
        assembler.DEC(assembler.HL);
        assembler.LD(assembler.HLref, 0x02);

        assembler.EI();

        assembler.Label(MainCtx.Loop);
        assembler.JP(assembler.LabelRef(MainCtx.Loop));

        assembler.Label(MainCtx.Sound);
        assembler.PUSH(assembler.AF);
        assembler.PUSH(assembler.BC);
        assembler.PUSH(assembler.DE);
        assembler.PUSH(assembler.HL);
        assembler.PUSH(assembler.IX);
        assembler.PUSH(assembler.IY);
        
        assembler.LD(assembler.HL, 0xE006);
        assembler.LD(assembler.HLref, 0x83);
        assembler.LD(assembler.HLref, 0x00);

        foreach (var ch in ChannelList)
        {
            assembler.LD(assembler.IX, assembler.LabelRef(GetChannelCtx(ch.Name).DataBlock));
            if (ch.IOPort == 0xE0) {
                assembler.CALL(assembler.LabelRef(GetSharedCtx(true).UpdateChannel));
            } else {
                assembler.CALL(assembler.LabelRef(GetSharedCtx(false).UpdateChannel));
            }
        }

        assembler.POP(assembler.IY);
        assembler.POP(assembler.IX);
        assembler.POP(assembler.HL);
        assembler.POP(assembler.DE);
        assembler.POP(assembler.BC);
        assembler.POP(assembler.AF);
        assembler.EI();
        assembler.RET();

        foreach (var ch in ChannelList)
        {
            assembler.Label(GetChannelCtx(ch.Name).DataBlock);
            assembler.DW(assembler.LabelRef(GetChannelCtx(ch.Name).SongData));
            assembler.DW(assembler.LabelRef(GetChannelCtx(ch.Name).SongDataEnd));
            assembler.DW(assembler.Value((ushort)0));
            assembler.DW(assembler.Value((ushort)0));
            assembler.DW(assembler.Value((ushort)0));
            assembler.DB(0);
            assembler.DB(0);
            assembler.DB(0);
            assembler.DW(assembler.LabelRef(MainCtx.GlobalEnvDataEmpty));
            assembler.DB(0);
            assembler.DB(0);
            assembler.DW(assembler.LabelRef(MainCtx.GlobalPEnvDataEmpty));
            assembler.DB(0);
            assembler.DB(ch.IOPort);
            
            int psgCh = 0;
            if (ch.Name.StartsWith("track_P")) {
                if (int.TryParse(ch.Name.Substring(7), out int trkNum)) {
                    psgCh = Math.Max(0, trkNum - 1) % 3;
                }
            } else if (ch.Name.StartsWith("track_N")) {
                psgCh = 3;
            } else if (ch.Name.StartsWith("track_")) {
                int.TryParse(ch.Name.Substring(6), out psgCh);
            }
            byte chBits = (byte)(0x80 | ((psgCh & 0x03) << 5));
            assembler.DB(chBits);
        }

        AppendSharedPlayRoutine(assembler, false);
        AppendSharedPlayRoutine(assembler, true);
        AppendGlobalData(assembler);
        
        // Freq tables
        assembler.Label(MainCtx.DataPsgFreqTable);
        for (int i = 0; i < 96; i++) {
            double freq = 440.0 * Math.Pow(2.0, (i - 57) / 12.0);
            int baseReg = (int)Math.Round(111860.0 / freq);
            baseReg = Math.Clamp(baseReg, 0, 1023);
            ushort regU = (ushort)baseReg;
            byte c1 = (byte)(regU & 0x0F);
            byte c2 = (byte)((regU >> 4) & 0x3F);
            assembler.DB(c1);
            assembler.DB(c2);
        }

        assembler.Label(MainCtx.DataBeepFreqTable);
        for (int i = 0; i < 96; i++) {
            double freq = 440.0 * Math.Pow(2.0, (i - 57) / 12.0);
            double baseReg = 894886.0 / freq;
            int reg = (int)Math.Round(baseReg);
            reg = Math.Clamp(reg, 0, 65535);
            assembler.DB((byte)(reg & 0xFF));
            assembler.DB((byte)((reg >> 8) & 0xFF));
        }

        foreach (var ch in ChannelList)
        {
            assembler.Label(GetChannelCtx(ch.Name).SongData);
            assembler.DB(ch.SequenceData);
            assembler.Label(GetChannelCtx(ch.Name).SongDataEnd);
            assembler.DB(0xFF);
        }

        return assembler.Build();
    }
private void AppendSharedPlayRoutine(Z80Assembler asm, bool isBeep)
    {
        asm.Label(GetSharedCtx(isBeep).UpdateChannel);
        
        // 1. 郢晢ｽｬ郢晢ｽｳ郢ｧ・ｰ郢ｧ・ｹ(Duration)邵ｺ・ｮ雋ょｸ幢ｽｰ莉｣竊定崕・､陞ｳ繝ｻ
        asm.LD(asm.L, asm.IXref(IxStatLengthRemain));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLengthRemain + 1)));
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.JP(asm.NZ, asm.LabelRef(GetSharedCtx(isBeep).DecDurLower));
        // lower is 0, check upper
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        asm.OR(asm.A);
        asm.DEC(asm.HL); // restore HL
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne)); // both 0 -> next command
        
        // dec upper
        asm.INC(asm.HL);
        asm.DEC(asm.HLref);
        asm.DEC(asm.HL);
        
        asm.Label(GetSharedCtx(isBeep).DecDurLower);
        asm.DEC(asm.HLref);

        // 2. 郢ｧ・ｲ郢晢ｽｼ郢昴・Gate)邵ｺ・ｮ陷・ｽｦ騾・・(驍・ｽ｡隴冗§・ｮ貅ｯ・｣繝ｻ Duration闕ｳ・ｭ邵ｺ・ｫGate邵ｺ謔溘・郢ｧ蠕娯螺郢ｧ陋ｾ豬ｹ鬩･荳奇ｽ定ｾ滂ｽ｡鬮ｻ・ｳ邵ｺ・ｫ邵ｺ蜷ｶ・狗ｸｺ・ｪ邵ｺ・ｩ邵ｺ・ｮ陷・ｽｦ騾・・窶ｲ陟｢繝ｻ・ｦ竏壺味邵ｺ蠕娯穐邵ｺ螢ｹ繝ｻ霎滂ｽ｡髫墓じ笘・ｹｧ荵敖ｰ髫補握・ｪ・ｿ隰ｨ・ｴ)
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).OutputSoundByStatus));

        
        // 3. 隹ｺ・｡邵ｺ・ｮ郢ｧ・ｳ郢晄ｧｭﾎｦ郢晏ｳｨ・帝坡・ｭ郢ｧﾂ陷・ｽｦ騾・・
        asm.Label(GetSharedCtx(isBeep).ReadSongDataOne);
        
        asm.LD(asm.E, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.D, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));

        // Fetch Command -> A
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A);

        // 0xFF (End)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).EndSong));

        // 0x60 (Rest)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadKyufuData));

        // 0xA0 (Set Voice / Env)
        asm.CP(asm.Value((byte)0xA0));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadEnvData));

        // 0xA1 (Set Volume)
        asm.CP(asm.Value((byte)0xA1));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadVolumeData));

        // 0xA2 (Set PEnv)
        asm.CP(asm.Value((byte)0xA2));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadPEnvData));

        // 0x90 (Long Length)
        asm.CP(asm.Value((byte)0x90));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadLongLen));

        // 0x08 (Loop Marker)
        asm.CP(asm.Value((byte)0x08));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadLoopMarker));

        // Noise (0x06) -> wait, we kept Noise as 0x06
        asm.CP(asm.Value((byte)0xA6));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadNoise));

        // Sync Noise (0x07)
        asm.CP(asm.Value((byte)0xA7));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).ReadSyncNoise));

        // If A < 0x60, it's Note ON (0x00 - 0x5F)
        asm.CP(asm.Value((byte)0x60));
        asm.JP(asm.C, asm.LabelRef(GetSharedCtx(isBeep).ReadToneData));
        
        // If A >= 0x80 AND A <= 0x8F, it's Short Length
        asm.SUB(asm.Value((byte)0x80));
        asm.CP(asm.Value((byte)0x10));
        asm.JP(asm.C, asm.LabelRef(GetSharedCtx(isBeep).ReadShortLen));

        // Unknown -> Ignore and read next
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // -- Read Long Length --
        asm.Label(GetSharedCtx(isBeep).ReadLongLen);
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A); // BC = 16-bit Length
        
        // Store to StatLastLength
        asm.LD(asm.L, asm.IXref(IxStatLastLength));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLastLength + 1)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // -- Read Short Length --
        asm.Label(GetSharedCtx(isBeep).ReadShortLen);
        // A is already (Cmd - 0x80).
        asm.AND(asm.Value((byte)0x0F));
        asm.INC(asm.A);
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0); // BC = Length
        
        // Store to StatLastLength
        asm.LD(asm.L, asm.IXref(IxStatLastLength));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLastLength + 1)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        
        // Save pos and read next
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));
// -- Read Loop Marker
        asm.Label(GetSharedCtx(isBeep).ReadLoopMarker);
        // Save current DE (which points to the instruction AFTER 0x08) to StatLoopPosition
        asm.LD(asm.L, asm.IXref(IxStatLoopPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLoopPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Also save DE to StatSongDataPosition so it won't read 0x08 forever
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // -- Read Envelope Command --
        asm.Label(GetSharedCtx(isBeep).ReadEnvData);
        asm.LD(asm.A, asm.DEref); // EnvelopeId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PrefixEnvOff));

        // Set Env Active
        asm.LD(asm.IXref(IxStatEnvActive), asm.Value((byte)0x01));
        
        // Offset = 0
        asm.LD(asm.IXref(IxStatEnvPosOffset), asm.Value((byte)0x00));

        // Compute BaseAddress = DataEnvTableBase + (EnvId * 2)
        // Since we have EnvId in B
        asm.LD(asm.HL, asm.LabelRef(GetSharedCtx(isBeep).DataEnvTableBase));
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

        asm.LD(asm.L, asm.IXref(IxStatEnvDataPtr));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatEnvDataPtr + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // Env Off
        asm.Label(GetSharedCtx(isBeep).PrefixEnvOff);
        asm.LD(asm.IXref(IxStatEnvActive), asm.Value((byte)0x00));
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // -- Read Pitch Envelope Command --
        asm.Label(GetSharedCtx(isBeep).ReadPEnvData);
        asm.LD(asm.A, asm.DEref); // EnvId (or 0xFF for Off)
        asm.INC(asm.DE);
        
        // Save pos
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Check if Off (0xFF)
        asm.LD(asm.B, asm.A);
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PrefixPenvOff));

        // Set PEnv Active
        asm.LD(asm.IXref(IxStatPEnvActive), asm.Value((byte)0x01));
        
        // Offset = 0
        asm.LD(asm.IXref(IxStatPEnvPosOffset), asm.Value((byte)0x00));

        // Compute BaseAddress = DataPEnvTableBase + (EnvId * 2)
        asm.LD(asm.HL, asm.LabelRef(GetSharedCtx(isBeep).DataPEnvTableBase));
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

        asm.LD(asm.L, asm.IXref(IxStatPEnvDataPtr));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatPEnvDataPtr + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // PEnv Off
        asm.Label(GetSharedCtx(isBeep).PrefixPenvOff);
        asm.LD(asm.IXref(IxStatPEnvActive), asm.Value((byte)0x00));
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        
        // -- Read Tone -- 
        asm.Label(GetSharedCtx(isBeep).ReadToneData);
        
        asm.PUSH(asm.BC); // Save B (Note Number)
        // Setup length from StatLastLength
        asm.LD(asm.C, asm.IXref(IxStatLastLength));
        asm.LD(asm.B, asm.IXref((sbyte)(IxStatLastLength + 1)));
        asm.LD(asm.L, asm.IXref(IxStatLengthRemain));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLengthRemain + 1)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);
        asm.POP(asm.BC); // Restore B (Note Number)

        // Reset offsets
        asm.LD(asm.IXref(IxStatEnvPosOffset), asm.Value((byte)0x00));
        asm.LD(asm.IXref(IxStatPEnvPosOffset), asm.Value((byte)0x00));

        // Set Note Active
        asm.LD(asm.IXref(IxStatNoteOn), asm.Value((byte)0x01));

        // B has the original Note ON command (Note Number)
        // Note: B was set to the command right after Fetch Command in ReadSongDataOne. Wait, B was overwritten?
        // Let's just use B since it was saved at the beginning of ReadSongDataOne and we didn't overwrite it for Tone,
        // EXCEPT we overwrote it during Long/Short Length. Wait! B is NOT overwritten for Tone because Tone is jumped to directly!
        // But let's verify. B was set: asm.LD(asm.B, asm.A); Yes!
        
        // Fetch HW register from Table
        if (isBeep) {
            asm.LD(asm.HL, asm.LabelRef(MainCtx.DataBeepFreqTable));
        } else {
            asm.LD(asm.HL, asm.LabelRef(MainCtx.DataPsgFreqTable));
        }
        
        asm.LD(asm.A, asm.B);
        asm.ADD(asm.A, asm.A); // A = Note * 2
        asm.LD(asm.C, asm.A);
        asm.LD(asm.B, 0);
        asm.ADD(asm.HL, asm.BC);
        
        // Output to hardware
        asm.LD(asm.A, asm.HLref); // low byte
        
        if (isBeep) {
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
            // Load PSG Channel bits from IX
            asm.LD(asm.B, asm.A); // Save A
            asm.LD(asm.A, asm.IXref(IxPsgChannelBits));
            asm.OR(asm.B);        // A = A | PsgChannelBits
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A); // OUT (C), A - wait, OUT (C), r exists! but OUT (C), A is standard.
            
            asm.INC(asm.HL);
            asm.LD(asm.A, asm.HLref); // high byte
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }

        // Save pos
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Apply Volume
        if (isBeep) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x01); // BEEP ON
        } else {
            asm.LD(asm.A, asm.IXref(IxStatHwVolume));
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).UpdateChannel));

        // -- Read Rest --
        asm.Label(GetSharedCtx(isBeep).ReadKyufuData);
        
        // Setup length from StatLastLength
        asm.LD(asm.C, asm.IXref(IxStatLastLength));
        asm.LD(asm.B, asm.IXref((sbyte)(IxStatLastLength + 1)));
        asm.LD(asm.L, asm.IXref(IxStatLengthRemain));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLengthRemain + 1)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.IXref(IxStatNoteOn), asm.Value((byte)0x00));

        // Send Volume=0 (0x0F) to mute
        if (isBeep) {
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, 0x00); // BEEP OFF
        } else {
            asm.LD(asm.A, asm.IXref(IxStatHwVolume));
            asm.AND(asm.Value((byte)0x60)); // Keep only channel bits
            asm.OR(asm.Value((byte)0x9F));  // Base Vol + 15 (Mute)
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }

        // Save pos
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).UpdateChannel));

// -- Read Noise --
        asm.Label(GetSharedCtx(isBeep).ReadNoise);
        // DE is pointing to 3 bytes: NoiseCmd, DurL, DurH.
        // Similar to ReadTone, but only 1 byte for freq/ctrl instead of two.
        asm.LD(asm.IXref(IxStatEnvPosOffset), asm.Value((byte)0x00));
        asm.LD(asm.IXref(IxStatPEnvPosOffset), asm.Value((byte)0x00));
        asm.LD(asm.IXref(IxStatNoteOn), asm.Value((byte)0x01));

        // Fetch NoiseCmd and OUT
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        if (!isBeep) { asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A); }

        // Setup length from StatLastLength
        asm.LD(asm.C, asm.IXref(IxStatLastLength));
        asm.LD(asm.B, asm.IXref((sbyte)(IxStatLastLength + 1)));
        asm.LD(asm.L, asm.IXref(IxStatLengthRemain));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLengthRemain + 1)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        
        if (!isBeep) {
            asm.LD(asm.A, asm.IXref(IxStatHwVolume));
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).UpdateChannel));


        // -- Read Sync Noise --
        asm.Label(GetSharedCtx(isBeep).ReadSyncNoise);
        asm.LD(asm.IXref(IxStatEnvPosOffset), asm.Value((byte)0x00));
        asm.LD(asm.IXref(IxStatPEnvPosOffset), asm.Value((byte)0x00));
        asm.LD(asm.IXref(IxStatNoteOn), asm.Value((byte)0x01));

        if (!isBeep) {
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE); asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
            asm.LD(asm.A, asm.DEref); asm.INC(asm.DE);
            asm.LD(asm.IXref(IxStatHwVolume), asm.A);
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }

        // Setup length from StatLastLength
        asm.LD(asm.C, asm.IXref(IxStatLastLength));
        asm.LD(asm.B, asm.IXref((sbyte)(IxStatLastLength + 1)));
        asm.LD(asm.L, asm.IXref(IxStatLengthRemain));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLengthRemain + 1)));
        asm.LD(asm.HLref, asm.C);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.B);

        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).UpdateChannel));

// -- Read Volume --
        asm.Label(GetSharedCtx(isBeep).ReadVolumeData);
        asm.LD(asm.A, asm.DEref); // raw volume hw byte (1 c c 1 v v v v)
        asm.INC(asm.DE);
        
        asm.LD(asm.IXref(IxStatHwVolume), asm.A); // save

        if (!isBeep) {
            // SN76489邵ｺ・ｫ陷奇ｽｳ隴弱ｅ繝ｻ郢晢ｽｪ郢晢ｽ･郢晢ｽｼ郢晢｣ｰ/郢晄ｺ佩礼ｹ晢ｽｼ郢晏現・定愾閧ｴ荳・
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }

        // Save pos & Read Next
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // -- End Song (Looping) --
        asm.Label(GetSharedCtx(isBeep).EndSong);
        asm.LD(asm.L, asm.IXref(IxStatLoopPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLoopPosition + 1)));
        // Get Address from StatLoopPosition -> DE
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);
        
        asm.LD(asm.L, asm.IXref(IxStatSongDataPosition));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatSongDataPosition + 1)));
        // And store DE to StatSongDataPosition
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Fetch the next command to execute
        asm.LD(asm.A, asm.DEref); // A = memory at DE
        asm.LD(asm.B, 0xFF);      // B = 0xFF (End marker)
        asm.CP(asm.B);            // Compare A with B
        
        // If not 0xFF, jump to parsing (valid loop target)
        asm.JP(asm.NZ, asm.LabelRef(GetSharedCtx(isBeep).ReadSongDataOne));

        // If it is 0xFF, it means we are at a halt state (data_song_end) or an empty track.
        // Set LengthRemain to 0x7FFF (about 9 minutes at 60Hz) to prevent infinite loop within a frame.
        asm.LD(asm.L, asm.IXref(IxStatLengthRemain));
        asm.LD(asm.H, asm.IXref((sbyte)(IxStatLengthRemain + 1)));
        asm.LD(asm.HLref, 0xFF);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, 0x7F);
        asm.RET();


        // -- Output By Status --
        asm.Label(GetSharedCtx(isBeep).OutputSoundByStatus);
        // Check Note On
        asm.LD(asm.A, asm.IXref(IxStatNoteOn));
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).OutputEnd)); // Note Off -> Do nothing

        // Check Env Active
        asm.LD(asm.A, asm.IXref(IxStatEnvActive));
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).OutputPenvCheck)); // Env Off -> skip to PEnv

        // Read Env Data pointer
        asm.LD(asm.E, asm.IXref(IxStatEnvDataPtr));
        asm.LD(asm.D, asm.IXref((sbyte)(IxStatEnvDataPtr + 1)));

        // Read Env Pos Offset
        asm.LD(asm.C, asm.IXref(IxStatEnvPosOffset));
        asm.LD(asm.B, 0);

        // Calculate Data Address (DE + BC)
        asm.LD(asm.H, asm.D);
        asm.LD(asm.L, asm.E);
        asm.ADD(asm.HL, asm.BC);

        // Read current envelope Volume into A
        asm.LD(asm.A, asm.HLref);

        // is it Loop Endpoint? (0xFE)
        asm.CP(asm.Value((byte)0xFE));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).EnvLoopEnd));

        // it might be End marker (0xFF)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).EnvEnd));

        // Valid Volume in A (0-15).
        // Save Volume to B
        asm.LD(asm.B, asm.A);

        // Extract channel bits from existing StatHwVolume
        asm.LD(asm.A, asm.IXref(IxStatHwVolume));
        asm.AND((byte)0x60); // Keep only channel bits: 0110 0000
        asm.OR((byte)0x90);  // Base Vol command: 1001 0000
        asm.LD(asm.C, asm.A); // C = 1001 c c 00

        // Envelope Vol is in B (0-15) where 0=silent, 15=max in MML.
        // HW requires 15=silent, 0=max.
        if (isBeep) {
            // Envelope applies to BEEP ON/OFF
            asm.LD(asm.A, asm.B);
            asm.CP(asm.Value(0)); // If 0 (silent)
            asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).EnvVolMute));
            asm.LD(asm.A, (byte)1);
            asm.JP(asm.LabelRef(GetSharedCtx(isBeep).EnvVolApply));
            
            asm.Label(GetSharedCtx(isBeep).EnvVolMute);
            asm.LD(asm.A, (byte)0);
            
            asm.Label(GetSharedCtx(isBeep).EnvVolApply);
            asm.LD(asm.HL, (ushort)0xE008);
            asm.LD(asm.HLref, asm.A);
        } else {
            asm.LD(asm.A, (byte)15);
            asm.SUB(asm.B); // A = 15 - B
            asm.OR(asm.C);  // Combine with channel bits: 1001 c c X X

            // Save back for consistency and OUT
            asm.LD(asm.IXref(IxStatHwVolume), asm.A);
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }

        // Increment Offset
        asm.INC(asm.IXref(IxStatEnvPosOffset));

        asm.Label(GetSharedCtx(isBeep).OutputPenvCheck);
        // Check PEnv Active
        asm.LD(asm.A, asm.IXref(IxStatPEnvActive));
        asm.OR(asm.A);
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).OutputEnd)); // PEnv Off -> End

        // Read PEnv Data pointer
        asm.LD(asm.E, asm.IXref(IxStatPEnvDataPtr));
        asm.LD(asm.D, asm.IXref((sbyte)(IxStatPEnvDataPtr + 1)));

        // Read PEnv Pos Offset
        asm.LD(asm.C, asm.IXref(IxStatPEnvPosOffset));
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
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PenvLoopEnd));

        // it might be End marker (0xFF)
        asm.CP(asm.Value((byte)0xFF));
        asm.JP(asm.Z, asm.LabelRef(GetSharedCtx(isBeep).PenvEnd));

        // Output Byte 1
        if (isBeep) {
            asm.PUSH(asm.HL); // Save HL (pointer to PEnvData)
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
            asm.POP(asm.HL);  // Restore HL
        } else {
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }
        
        // Read Byte 2 (High byte = cmd2)
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        
        // Output Byte 2
        if (isBeep) {
            asm.LD(asm.HL, (ushort)0xE004);
            asm.LD(asm.HLref, asm.A);
        } else {
            asm.LD(asm.C, asm.IXref(IxPortType));
            asm.OUT(asm.C, asm.A);
        }

        // Increment Offset
        asm.INC(asm.IXref(IxStatPEnvPosOffset));

        asm.Label(GetSharedCtx(isBeep).OutputEnd);
        asm.RET();

        asm.Label(GetSharedCtx(isBeep).EnvLoopEnd);
        // Read the next byte which contains the loop offset
        // HL currently points to the 0xFE byte. The offset is at HL+1.
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref); // A = loop offset
        // Store it to StatEnvPosOffset
        asm.LD(asm.IXref(IxStatEnvPosOffset), asm.A);
        // JP back to OutputSoundByStatus to output the looped value in the same frame
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).OutputSoundByStatus));
        
        asm.Label(GetSharedCtx(isBeep).EnvEnd);
        // If 0xFF, stay at the last valid position
        asm.DEC(asm.IXref(IxStatEnvPosOffset));
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).OutputSoundByStatus));

        // PEnv loop handlers
        asm.Label(GetSharedCtx(isBeep).PenvLoopEnd);
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        asm.LD(asm.IXref(IxStatPEnvPosOffset), asm.A);
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).OutputPenvCheck));
        
        asm.Label(GetSharedCtx(isBeep).PenvEnd);
        asm.DEC(asm.IXref(IxStatPEnvPosOffset));
        asm.JP(asm.LabelRef(GetSharedCtx(isBeep).OutputPenvCheck));


        // -- Envelope Data Tables --
        // To make it simpler, we embed the global VolumeEnvelopes table inside each channel's memory block,
        // or we can embed it once globally. Since we only loop channels here, we'll embed one copy per channel for simplicity of addressing.
        asm.Label(GetSharedCtx(isBeep).DataEnvTableBase);
        
        // Find max EnvId to allocate contiguous pointer table
        int maxEnvId = -1;
        foreach (var id in VolumeEnvelopes.Keys) if (id > maxEnvId) maxEnvId = id;

        for (int i = 0; i <= maxEnvId; i++)
        {
            if (VolumeEnvelopes.ContainsKey(i))
            {
                asm.DW(asm.LabelRef(GetSharedCtx(isBeep).GetEnvData(i)));
            }
            else
            {
                // Dummy/empty
                asm.DW(asm.LabelRef(GetSharedCtx(isBeep).PrefixEnvDataEmpty));
            }
        }

        // Dummy empty data
        asm.Label(GetSharedCtx(isBeep).PrefixEnvDataEmpty);
        asm.DB(0xFF);

        // Env Array Definitions
        foreach (var kvp in VolumeEnvelopes)
        {
            asm.Label(GetSharedCtx(isBeep).GetEnvData(kvp.Key));
            
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
        asm.Label(GetSharedCtx(isBeep).DataPEnvTableBase);
        
        int maxPEnvId = -1;
        if (HwPitchEnvelopes.Count > 0)
        {
            maxPEnvId = HwPitchEnvelopes.Count - 1;
        }

        for (int i = 0; i <= maxPEnvId; i++)
        {
            if (i < HwPitchEnvelopes.Count)
            {
                asm.DW(asm.LabelRef(GetSharedCtx(isBeep).GetPEnvData(i)));
            }
            else
            {
                asm.DW(asm.LabelRef(GetSharedCtx(isBeep).PrefixPenvDataEmpty));
            }
        }

        asm.Label(GetSharedCtx(isBeep).PrefixPenvDataEmpty);
        asm.DB(0xFF);
        asm.DB(0xFF); // Align 2 bytes

        foreach (var penv in HwPitchEnvelopes)
        {
            asm.Label(GetSharedCtx(isBeep).GetPEnvData(penv.Id));
            
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
    private void AppendGlobalData(Z80Assembler asm)
    {
        asm.Label(MainCtx.GlobalEnvTable);
        int maxEnvId = -1;
        if (VolumeEnvelopes.Count > 0) {
            foreach (var key in VolumeEnvelopes.Keys) if (key > maxEnvId) maxEnvId = key;
        }
        for (int i = 0; i <= maxEnvId; i++) {
            if (VolumeEnvelopes.ContainsKey(i))
                asm.DW(asm.LabelRef(MainCtx.GetGlobalEnvData(i)));
            else
                asm.DW(asm.LabelRef(MainCtx.GlobalEnvDataEmpty));
        }
        
        asm.Label(MainCtx.GlobalEnvDataEmpty);
        asm.DB(0xFF);
        
        foreach (var kvp in VolumeEnvelopes) {
            asm.Label(MainCtx.GetGlobalEnvData(kvp.Key));
            var envData = kvp.Value;
            foreach (var vol in envData.Values) asm.DB((byte)(vol & 0xFF));
            if (envData.LoopIndex >= 0 && envData.LoopIndex < envData.Values.Count) {
                asm.DB(0xFE); asm.DB((byte)(envData.LoopIndex & 0xFF));
            } else {
                asm.DB(0xFF);
            }
        }
        
        asm.Label(MainCtx.GlobalPEnvTable);
        int maxPEnvId = -1;
        if (HwPitchEnvelopes.Count > 0) maxPEnvId = HwPitchEnvelopes.Count - 1;
        for (int i = 0; i <= maxPEnvId; i++) {
            if (i < HwPitchEnvelopes.Count)
                asm.DW(asm.LabelRef(MainCtx.GetGlobalPEnvData(i)));
            else
                asm.DW(asm.LabelRef(MainCtx.GlobalPEnvDataEmpty));
        }
        
        asm.Label(MainCtx.GlobalPEnvDataEmpty);
        asm.DB(0xFF); asm.DB(0xFF);
        
        foreach (var penv in HwPitchEnvelopes) {
            asm.Label(MainCtx.GetGlobalPEnvData(penv.Id));
            foreach (ushort hwVal in penv.AbsoluteRegisters) {
                asm.DB((byte)(hwVal & 0xFF));
                asm.DB((byte)((hwVal >> 8) & 0xFF));
            }
            if (penv.LoopIndex >= 0 && penv.AbsoluteRegisters != null && penv.LoopIndex < penv.AbsoluteRegisters.Count) {
                asm.DB(0xFE); asm.DB((byte)(penv.LoopIndex & 0xFF));
            } else {
                asm.DB(0xFF); asm.DB(0xFF);
            }
        }
    }
}

