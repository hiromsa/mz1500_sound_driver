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

public class MZ1500MusicAssembler
{
    public List<Channel> ChannelList { get; } = new();
    
    // MmlPlayerModelから渡されるエンベロープ定義データ (EnvId -> ボリューム配列)
    public Dictionary<int, EnvelopeData> VolumeEnvelopes { get; set; } = new();
    
    // HwPitchEnvデータ
    public List<MmlToZ80Compiler.HwPitchEnvData> HwPitchEnvelopes { get; set; } = new();

    public void AppendChannel(Channel channel) => ChannelList.Add(channel);

    private enum Labels
    {
        StatSongDataPosition,   // 2 bytes
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

    public byte[] Build()
    {
        var assembler = new MZ1500Assembler();
        
        assembler.ORG(0x1200);
        assembler.Label("main:");
        assembler.DI();
        assembler.IM1();

        // 割り込みベクタの設定
        assembler.LD(assembler.HL, 0x1039);
        assembler.LD(assembler.DE, assembler.LabelRef("sound:"));
        assembler.LD(assembler.HLref, assembler.E);
        assembler.INC(assembler.HL);
        assembler.LD(assembler.HLref, assembler.D);

        // 8253タイマー設定 (割り込み周期)
        assembler.LD(assembler.HL, 0xE007);
        assembler.LD(assembler.HLref, 0xB0); // CH2 Mode0
        assembler.LD(assembler.HLref, 0x74); // CH1 Mode2
        assembler.DEC(assembler.HL);         // 0xE006 (CH2)
        assembler.LD(assembler.HLref, 0x83); 
        assembler.LD(assembler.HLref, 0x00); 
        assembler.DEC(assembler.HL);         // 0xE005 (CH1)
        assembler.LD(assembler.HLref, 0x02);
        assembler.LD(assembler.HLref, 0x00);

        // 割り込み許可 (INTMSK)
        assembler.LD(assembler.A, 0x05);
        assembler.LD(0xE003, assembler.A);

        // MZ-700音源(BEEP)初期化 (SN76489のダミーか互換用？)
        assembler.LD(assembler.A, 0x01);
        assembler.LD(assembler.HL, 0xE008);
        assembler.LD(assembler.HLref, assembler.A);
        assembler.LD(assembler.HL, 0xE007);
        assembler.LD(assembler.HLref, 0x36);

        // --- VRAMクリアとテスト描画 (フリーズ(無反応)しているように見えないための対策) ---
        // VRAM(0xD000〜0xD3E7)をクリア
        assembler.LD(assembler.HL, 0xD000);
        assembler.LD(assembler.DE, 0xD001);
        assembler.LD(assembler.BC, 0x03FF);
        assembler.LD(assembler.HLref, 0x00); // 0x00 (Space or Empty)
        assembler.LDIR();

        // 画面左上(0xD000)に 'PLAYING' をMZ-1500のアスキー文字（画面表示コード）で直書き
        assembler.LD(assembler.HL, 0xD000);
        assembler.LD(assembler.HLref, 0x10); assembler.INC(assembler.HL); // P = 16 = 0x10
        assembler.LD(assembler.HLref, 0x0C); assembler.INC(assembler.HL); // L = 12 = 0x0C
        assembler.LD(assembler.HLref, 0x01); assembler.INC(assembler.HL); // A = 1  = 0x01
        assembler.LD(assembler.HLref, 0x19); assembler.INC(assembler.HL); // Y = 25 = 0x19
        assembler.LD(assembler.HLref, 0x09); assembler.INC(assembler.HL); // I = 9  = 0x09
        assembler.LD(assembler.HLref, 0x0E); assembler.INC(assembler.HL); // N = 14 = 0x0E
        assembler.LD(assembler.HLref, 0x07);                              // G = 7  = 0x07
        // --- 描画ここまで ---

        assembler.EI();

        // 無限ループ (メイン処理は割り込みに任せる)
        assembler.Label("loop:");
        assembler.JP(assembler.LabelRef("loop:"));

        // ===== 割り込みハンドラ =====
        assembler.Label("sound:");
        assembler.PUSH(assembler.AF);
        assembler.PUSH(assembler.BC);
        assembler.PUSH(assembler.DE);
        assembler.PUSH(assembler.HL);
        
        // 8253タイマ再設定
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


        // ===== チャンネルごとの処理ルーチン =====
        foreach (var ch in ChannelList)
        {
            AppendPlayChannelSource(ch.Name, assembler, ch.IOPort);
        }

        // ===== チャンネル独立のシーケンスデータ配置 =====
        foreach (var ch in ChannelList)
        {
            assembler.Label(ch.Name + "_" + nameof(Labels.DataSong));
            assembler.DB(ch.SequenceData);
        }

        return assembler.Build();
    }

    private void AppendPlayChannelSource(string prefix, MZ1500Assembler asm, byte port)
    {
        asm.Label(prefix);
        
        // 1. レングス(Duration)の減少と判定
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

        // 2. ゲート(Gate)の処理 (簡易実装: Duration中にGateが切れたら音量を無音にするなどの処理が必要だがまずは無視するか要調整)
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.OutputSoundByStatus)));

        // 3. 次のコマンドを読む処理
        asm.Label(prefix + "_" + nameof(Labels.ReadSongDataOne));
        
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.E, asm.HLref);
        asm.INC(asm.HL);
        asm.LD(asm.D, asm.HLref);

        // Fetch Command
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.LD(asm.B, asm.A);

        // 0xFF (End)
        asm.LD(asm.A, 0xFF);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_end_song"));

        // 0x01 (Tone)
        asm.LD(asm.A, 0x01);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadToneData)));
        
        // 0x02 (Rest)
        asm.LD(asm.A, 0x02);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadKyufuData)));

        // 0x03 (Volume)
        asm.LD(asm.A, 0x03);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadVolumeData)));

        // 0x04 (Envelope)
        asm.LD(asm.A, 0x04);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadEnvData)));

        // 0x05 (Pitch Envelope)
        asm.LD(asm.A, 0x05);
        asm.CP(asm.B);
        asm.JP(asm.Z, asm.LabelRef(prefix + "_" + nameof(Labels.ReadPEnvData)));

        asm.RET(); // Unknown commmand

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
        // DE is now pointing to 6 bytes: Freq L, Freq H, Dur L, Dur H, Gate L, Gate H
        // Currently: HW port sending (simple SN76489)
        // HW port format:
        // Byte 1: 1 c c t d d d d (c=channel 0-2, t=0(freq)/1(vol), d=data)
        // Byte 2: 0 - d d d d d d 
        // We assume DE points to raw register values provided by compiler, to simplify
        
        // Reset Env Pos Offset for the new note
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);

        // Reset PEnv Pos Offset for the new note
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatPEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);
        asm.LD(asm.HLref, 0x00);

        // Note On flag (1)
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        // Read Freq (L, H) -> Hardware (using port for SN76489)
        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.OUT(port);

        asm.LD(asm.A, asm.DEref);
        asm.INC(asm.DE);
        asm.OUT(port);

        // Fetch Duration (2 bytes) -> StatLengthRemain
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.A, asm.DEref); // Dur L
        asm.LD(asm.HLref, asm.A);
        asm.INC(asm.DE);
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.DEref); // Dur H
        asm.LD(asm.HLref, asm.A);
        asm.INC(asm.DE);

        // Turn on Note Active
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x01);

        // Reset Envelope Pos Offset to 0 so note re-triggers envelope correctly
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatEnvPosOffset)));
        asm.LD(asm.HLref, 0x00);

        // Save pos
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        // Apply Volume
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
        asm.LD(asm.A, asm.HLref);
        asm.OUT(port);

        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.OutputSoundByStatus)));


        // -- Read Rest --
        asm.Label(prefix + "_" + nameof(Labels.ReadKyufuData));
        // Dur L, Dur H
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.A, asm.DEref); // Dur L
        asm.LD(asm.HLref, asm.A);
        asm.INC(asm.DE);
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.DEref); // Dur H
        asm.LD(asm.HLref, asm.A);
        asm.INC(asm.DE);

        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatNoteOn)));
        asm.LD(asm.HLref, 0x00);

        // Send Volume=0 (0x0F) to mute
        // Extract channel bits from existing StatHwVolume, append 0x0F to mute
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
        asm.LD(asm.A, asm.HLref);
        asm.AND((byte)0x60); // Keep only channel bits: 0110 0000
        asm.OR((byte)0x9F);  // Base Vol command + 15 (Mute): 1001 1111
        asm.OUT(port);
        // Note: we do not save this mute volume to StatHwVolume so it remembers original channel base

        // Save pos
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.OutputSoundByStatus)));

        // -- Read Volume --
        asm.Label(prefix + "_" + nameof(Labels.ReadVolumeData));
        asm.LD(asm.A, asm.DEref); // raw volume hw byte (1 c c 1 v v v v)
        asm.INC(asm.DE);
        
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
        asm.LD(asm.HLref, asm.A); // save

        // SN76489に即時ボリューム/ミュートを反映
        asm.OUT(port);

        // Save pos & Read Next
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);
        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));

        // -- End Song (Looping) --
        asm.Label(prefix + "_end_song");
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatSongDataPosition)));
        // Get Address to DE directly
        asm.LD(asm.DE, asm.LabelRef(prefix + "_" + nameof(Labels.DataSong)));
        // And store DE to StatSongDataPosition (DE -> (HL))
        asm.LD(asm.HLref, asm.E);
        asm.INC(asm.HL);
        asm.LD(asm.HLref, asm.D);

        asm.JP(asm.LabelRef(prefix + "_" + nameof(Labels.ReadSongDataOne)));


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
        asm.LD(asm.A, (byte)15);
        asm.SUB(asm.B); // A = 15 - B
        asm.OR(asm.C);  // Combine with channel bits: 1001 c c X X

        // Save back for consistency and OUT
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatHwVolume)));
        asm.LD(asm.HLref, asm.A);
        asm.OUT(port);

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
        asm.OUT(port);
        
        // Read Byte 2 (High byte = cmd2)
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.HLref);
        
        // Output Byte 2
        asm.OUT(port);

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
        
        asm.Label(prefix + "_" + nameof(Labels.StatLengthRemain));
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
}
