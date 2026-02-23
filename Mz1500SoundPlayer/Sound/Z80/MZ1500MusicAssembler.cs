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

    public void AppendChannel(Channel channel) => ChannelList.Add(channel);

    private enum Labels
    {
        StatSongDataPosition,   // 2 bytes
        StatLengthRemain,       // 2 bytes
        StatGateRemain,         // 2 bytes
        StatNoteOn,             // 1 byte
        StatHwVolume,           // 1 byte (0=Louder, 15=Silent)
        
        // Routines
        OutputSoundByStatus,
        ReadSongDataOne,
        ReadToneData,
        ReadVolumeData,
        ReadKyufuData,
        DataSong
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
        
        // 8253タイマ再設定
        assembler.LD(assembler.HL, 0xE006);
        assembler.LD(assembler.HLref, 0x83);
        assembler.LD(assembler.HLref, 0x00);

        foreach (var ch in ChannelList)
        {
            assembler.CALL(assembler.LabelRef(ch.Name));
        }

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

        asm.RET(); // Unknown commmand

        // -- Read Tone -- 
        asm.Label(prefix + "_" + nameof(Labels.ReadToneData));
        // DE is now pointing to 6 bytes: Freq L, Freq H, Dur L, Dur H, Gate L, Gate H
        // Currently: HW port sending (simple SN76489)
        // HW port format:
        // Byte 1: 1 c c t d d d d (c=channel 0-2, t=0(freq)/1(vol), d=data)
        // Byte 2: 0 - d d d d d d 
        // We assume DE points to raw register values provided by compiler, to simplify
        
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

        // Read Dur (L, H)
        asm.LD(asm.HL, asm.LabelRef(prefix + "_" + nameof(Labels.StatLengthRemain)));
        asm.LD(asm.A, asm.DEref); // Dur L
        asm.LD(asm.HLref, asm.A);
        asm.INC(asm.DE);
        asm.INC(asm.HL);
        asm.LD(asm.A, asm.DEref); // Dur H
        asm.LD(asm.HLref, asm.A);
        asm.INC(asm.DE);

        // Save position
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
        // Format of mute: 1 c c 1 1 1 1 1
        // We will just do a simple mute (channel needs to be pre-calculated by compiler or tracked here,
        // Since SN76489 vol reg needs channel info, we should ideally let compiler provide raw vol command
        // For now, let compiler send pre-formatted vol byte in ReadVolume)
        
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
        // Play Gate logic or envelopes goes here.
        asm.RET();


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
    }
}
