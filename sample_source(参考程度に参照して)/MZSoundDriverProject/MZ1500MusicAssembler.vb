Option Explicit On
Option Strict Off

Public Class MZ1500MusicAssembler

    Public VolueEnvelopeList As New List(Of VolumeEnvelope)
    Public Sub AppendVolueEnvelope(volumeEnvelope As VolumeEnvelope)
        Me.VolueEnvelopeList.Add(volumeEnvelope)
    End Sub
    Public PitchEnvelopeList As New List(Of PitchEnvelope)

    Public Sub AppendPitchEnvelope(pitchEnvelope As PitchEnvelope)
        Me.PitchEnvelopeList.Add(pitchEnvelope)
    End Sub

    Public ChhanelList As New List(Of Channel)
    Public Sub AppendChannel(channel As Channel)
        Me.ChhanelList.Add(channel)
    End Sub

    Public Function Build() As Byte()

        Const CHANNEL2_CYCLE_BYTE1 As Byte = &H83
        'Const CHANNEL2_CYCLE_BYTE1 As Byte = &H1
        Const CHANNEL2_CYCLE_BYTE2 As Byte = &H0

        'MZ1500アセンブラでマシン語をビルド
        Dim assembler As New MZ1500Assembler
        With assembler
            .ORG(&H1200)
            .Label("main:")

            .DI() '割り込み禁止

            .CALL(.LabelRef("ImageLoader"))

            .JP(.LabelRef("main2:"))

            ImageLoader(assembler)

            .Label("main2:")

            .IM1() 'Z80割り込みモード1

            .LD(.HL, &H1039) '割り込みのジャンプ先指定
            .LD(.DE, .LabelRef("sound:"))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            .LD(.HL, &HE007US) '8253 コントロールポート
            '.LD(.HLref, &HB0) 'チャネル2, モード0, 下位バイト→上位バイトの順にカウンタを設定
            '.LD(.HLref, &H74) 'チャネル1, モード2, 下位バイト→上位バイトの順にカウンタを設定
            .LD(.HLref, &HB0) 'チャネル2, モード0, 下位バイト→上位バイトの順にカウンタを設定
            .LD(.HLref, &H74) 'チャネル1, モード2, 下位バイト→上位バイトの順にカウンタを設定
            .DEC(.HL) '8253 チャネル2
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定
            .DEC(.HL) '8253 チャネル1
            .LD(.HLref, &H2)   ' チャネル1のカウント値の下位バイトを設定
            .LD(.HLref, &H0)   ' チャネル1のカウント値の上位バイトを設定

            .LD(.A, &H5) 'INTMSK = 1 に設定し、8253からの割り込みを許可する
            .LD(&HE003US, .A) ' 8255 ビットセット

            '***************
            'MZ700音源初期化
            '***************

            .LD(.A, &H1)
            .LD(.HL, &HE008)
            .LD(.HLref, .A)

            .LD(.HL, &HE007)
            .LD(.HLref, &H36)

            '.LD(.HL, &HE004)
            '.LD(.HLref, &HF2)
            '.LD(.HLref, &H7)

            .EI() ' 割り込みを許可

            .Label("loop:")
            .JP(.LabelRef("loop:"))

            .Label("sound:")

            .LD(.HL, &HE006US) '8253 コントロールポート
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定

            For Each channel As Channel In ChhanelList
                .CALL(.LabelRef(channel.Name))
            Next

            .JP(.LabelRef("exit:"))

            For Each channel As Channel In ChhanelList
                Me.AppendPlayChannelSource(channel.Name, assembler, channel.IOPort, channel.MusicBinary)
            Next

            '.CALL(MZ1500Assembler.CallFunc.LETNL)
            '.LD(.DE, .LabelRef("title:"))
            '.CALL(MZ1500Assembler.CallFunc.MSG)

            .Label("exit:")

            .EI() ' 割り込みを許可
            .RET()

            '.Label("title:")
            '.DB("PLAY")
            '.DB(&HD)
            '.Label("title-skip:")
            '.DB("SKIP")
            '.DB(&HD)

        End With
        Dim byteData() As Byte = assembler.Build()

        assembler.DebugOut(byteData)

        Return byteData
    End Function

    Private Sub ImageLoader(assembler As MZ1500Assembler)

        Dim pcgLabelPrefix As String = "pcg_"

        Dim pcgList As PCGList = PCGList.FromCSV("C:\Users\sato\Desktop\その他\MZSoundDriverProject\spehari.csv")

        With assembler

            .Label("ImageLoader")

            .CALL(.LabelRef(pcgLabelPrefix & "CLS"))

            .CALL(.LabelRef(pcgLabelPrefix & "start"))

            .RET()

            Dim blnk As Integer = &HDA6
            '画面クリア
            .Label(pcgLabelPrefix & "CLS")
            If True Then
                .CALL(blnk)
                .LD(.HL, &HD000)
                .LD(.BC, 40 * 25)
                .XOR(.A)
                '.LD(.A, &H0)
                .CALL(.LabelRef(pcgLabelPrefix & "MEMFIL"))

                .LD(.HL, &HD800)
                .LD(.BC, 40 * 25)
                .LD(.A, &H0)
                .CALL(.LabelRef(pcgLabelPrefix & "MEMFIL"))

                .RET()
            End If

            .Label(pcgLabelPrefix & "SCRCLR")
            If True Then
                .LD(.A, &H0)
                .LD(.HL, &HD000)
                .LD(.BC, 42 * 25)
                .CALL(.LabelRef(pcgLabelPrefix & "MEMFIL"))
                .RET()
            End If

            '埋め尽くす
            .Label(pcgLabelPrefix & "MEMFIL")
            If True Then
                .LD(.D, .H)
                .LD(.E, .L)
                .INC(.DE)
                .DEC(.BC)
                .LD(.HLref, .A)
                .LDIR()
                .RET()
            End If

            .Label(pcgLabelPrefix & "start")

            Dim byteF1 As Byte = &HF1
            .OUT(byteF1)

            '画面優先度・画面2表示設定
            .LD(.A, &H1)
            Dim byteF0 As Byte = &HF0
            .OUT(byteF0)

            'PCG パターン設定

            Dim byteE5 As Byte = &HE5

            .LD(.DE, .LabelRef(pcgLabelPrefix & "PSGData-Red-start")) 'PCGデータ開始アドレス
            .LD(.BC, .LabelRef(pcgLabelPrefix & "PSGData-Red-end")) 'CGデータ終了アドレス
            .LD(.HL, &HD000) 'PCG格納先アドレス
            .LD(.A, &H3)
            .OUT(byteE5) 'バンク切り替え
            .CALL(.LabelRef(pcgLabelPrefix & "LoopStart"))

            .LD(.DE, .LabelRef(pcgLabelPrefix & "PSGData-Green-start")) 'PCGデータ開始アドレス
            .LD(.BC, .LabelRef(pcgLabelPrefix & "PSGData-Green-end")) 'CGデータ終了アドレス
            .LD(.HL, &HD000) 'PCG格納先アドレス
            .LD(.A, &H2)
            .OUT(byteE5) 'バンク切り替え
            .CALL(.LabelRef(pcgLabelPrefix & "LoopStart"))

            .LD(.DE, .LabelRef(pcgLabelPrefix & "PSGData-Blue-start")) 'PCGデータ開始アドレス
            .LD(.BC, .LabelRef(pcgLabelPrefix & "PSGData-Blue-end")) 'CGデータ終了アドレス
            .LD(.HL, &HD000) 'PCG格納先アドレス
            .LD(.A, &H1)
            .OUT(byteE5) 'バンク切り替え
            .CALL(.LabelRef(pcgLabelPrefix & "LoopStart"))

            .JP(.LabelRef(pcgLabelPrefix & "LoopEnd"))

            .Label(pcgLabelPrefix & "LoopStart")


            .LD(.A, .DEref) 'PSGデータを1件取得
            .LD(.HLref, .A) 'VRAMへ転送
            .INC(.DE)
            .INC(.HL)

            .LD(.A, .B)
            .CP(.D)
            .JP(.NZ, .LabelRef(pcgLabelPrefix & "LoopStart"))
            .LD(.A, .C)
            .CP(.E)
            .JP(.NZ, .LabelRef(pcgLabelPrefix & "LoopStart"))

            .RET()

            .Label(pcgLabelPrefix & "LoopEnd")

            'PCG 描画

            .Label(pcgLabelPrefix & "VRAM-start")
            .LD(.HL, &HD400) 'VRAM
            .LD(.DE, &HDC00) '色情報

            'VRAM 画面2
            .LD(.B, &H0)
            .LD(.C, &B1000)
            .CALL(.LabelRef(pcgLabelPrefix & "VRAM-loop"))
            .LD(.B, &H0)
            .LD(.C, &B1001000)
            .CALL(.LabelRef(pcgLabelPrefix & "VRAM-loop"))
            .LD(.B, &H0)
            .LD(.C, &B10001000)
            .CALL(.LabelRef(pcgLabelPrefix & "VRAM-loop"))
            .LD(.B, &H0)
            .LD(.C, &B11001000)
            .CALL(.LabelRef(pcgLabelPrefix & "VRAM-loop"))
            .JP(.LabelRef(pcgLabelPrefix & "loop:"))

            .Label(pcgLabelPrefix & "VRAM-loop")
            If True Then

                .LD(.A, &H1)
                Dim byteE6 As Byte = &HE6
                .OUT(byteE6)

                .LD(.HLref, .B)
                .INC(.HL)

                '色情報 画面2
                .LD(.A, .C)
                .LD(.DEref, .A)
                .INC(.DE)

                Dim byteFF As Byte = &HFF
                .LD(.A, byteFF)
                .CP(.B)
                .JP(.Z, .LabelRef(pcgLabelPrefix & "VRAM-loop-return"))

                .INC(.B)

                .JP(.LabelRef(pcgLabelPrefix & "VRAM-loop"))

                .Label(pcgLabelPrefix & "VRAM-loop-return")
                .RET()
            End If

            .Label(pcgLabelPrefix & "loop:")

            .RET()

            .Label(pcgLabelPrefix & "PSGData-Red-start")
            .DB(pcgList.ToByteList(PCGOne.ColorType.Green))
            .Label(pcgLabelPrefix & "PSGData-Red-end")
            .Label(pcgLabelPrefix & "PSGData-Green-start")
            .DB(pcgList.ToByteList(PCGOne.ColorType.Red))
            .Label(pcgLabelPrefix & "PSGData-Green-end")
            .Label(pcgLabelPrefix & "PSGData-Blue-start")
            .DB(pcgList.ToByteList(PCGOne.ColorType.Blue))
            .Label(pcgLabelPrefix & "PSGData-Blue-end")

        End With


    End Sub

    Private Enum Labels
        OutputSoundByStatus
        OutputVolumeByStatus
        OutputVolumeByStatusModeDefault
        OutputVolumeByStatusModeDefaultForMZ700
        OutputVolumeByStatusModeDefaultForMZ700SoundOn
        OutputVolumeByStatusModeDefaultForMZ700SoundOff
        OutputVolumeByStatusModeDefaultForMZ700Next
        OutputSoundByStatusModeEnvelope
        OutputVolumeByStatusModeEnvelope
        OutputNoiseControl
        OutputSound
        OutputSoundEnd
        OutputVolumeOff
        DecrementLengthRemain
        DecrementNoteOnRemain
        ReadSongDataOne
        ReadToneData
        ReadKyufuData
        ReadNoiseTypeData
        ReadNoiseSyuhasuTypeData
        ReadVolumeData
        ReadAtVolumeData
        ReadPitchEnvelopeData
        ReadPitchEnvelopeOffData
        SearchAtVolumeDataIndex
        SearchPitchEnvelopeDataIndex
        ReadAtVolumeDataSomePosition
        ReadPitchEnvelopeDataSomePosition
        IncrementAtVolumeCurrentPosition
        IncrementPitchEnvelopeCurrentPosition
        StatSongDataPosition
        StatEnabled
        StatNoteOn
        StatLengthRemain
        StatNoteOnRemain
        StatSoundByte1
        StatSoundByte2
        StatPreviousSoundByte1ForMZ700
        StatPreviousSoundByte2ForMZ700
        StatVolumeMode
        StatVolume
        StatPreviousVolumeForMZ700
        StatNoiseType
        StatNoiseSyuhasuType
        StatPitchEnvelopeMode
        StatAtVolumeStartPosition
        StatAtVolumeEndPosition
        StatAtVolumeReStartPosition
        StatAtVolumeCurrentPosition
        StatPitchEnvelopeStartPosition
        StatPitchEnvelopeEndPosition
        StatPitchEnvelopeReStartPosition
        StatPitchEnvelopeCurrentPosition
        DataSong
        DataVolueEnvelopeIndex
        DataVolueEnvelopeStartPosition
        DataVolueEnvelopeEndPosition
        DataVolueEnvelopeReStartPosition
        DataPitchEnvelopeIndex
        DataPitchEnvelopeStartPosition
        DataPitchEnvelopeEndPosition
        DataPitchEnvelopeReStartPosition
        NoteOffVolumeByte
    End Enum

    Private Sub AppendPlayChannelSource(labelPrefix As String, assembler As MZ1500Assembler, ioPort As IOPort, musicBinary As MusicBinary)

        With assembler

            .Label(labelPrefix)

            '音長を取得
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatLengthRemain)))
            .LD(.A, &H0)

            '音長が0で無いならOutputSoundByStatusへ
            .CP(.HLref)
            .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputSoundByStatus)))
            .INC(.HL)
            .CP(.HLref)
            .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputSoundByStatus)))

            '音長が0ならnew-sound:へ
            .JP(.LabelRef(labelPrefix & NameOf(Labels.ReadSongDataOne)))

            .RET()

            '***************
            '次の音を準備する
            '***************
            .Label(labelPrefix & NameOf(Labels.ReadSongDataOne))
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition))) 'ステータスの先頭アドレス

            '音データの現在アドレスを取得(DE)
            .LD(.E, .HLref)
            .INC(.HL)
            .LD(.D, .HLref)

            '音データのコマンド種別を取得
            .LD(.A, .DEref)
            .LD(.B, .A)
            .INC(.DE)

            'コマンド種別：トーン
            .LD(.A, MusicBinary.SoundCommandType.Tone)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadToneData)))

            'コマンド種別：休符
            .LD(.A, MusicBinary.SoundCommandType.Kyufu)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadKyufuData)))

            'コマンド種別：ノイズタイプ
            .LD(.A, MusicBinary.SoundCommandType.NoiseType)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadNoiseTypeData)))

            'コマンド種別：ノイズ周波数タイプ
            .LD(.A, MusicBinary.SoundCommandType.NoiseSyuhasuType)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadNoiseSyuhasuTypeData)))

            'コマンド種別：ボリューム
            .LD(.A, MusicBinary.SoundCommandType.Volume)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadVolumeData)))

            'コマンド種別：ボリュームエンベロープ
            .LD(.A, MusicBinary.SoundCommandType.AtVolume)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadAtVolumeData)))

            'コマンド種別：ピッチエンベロープ
            .LD(.A, MusicBinary.SoundCommandType.PitchEnvelope)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadPitchEnvelopeData)))

            'コマンド種別：ピッチエンベロープOFF
            .LD(.A, MusicBinary.SoundCommandType.PitchEnvelopeOff)
            .CP(.B)
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadPitchEnvelopeOffData)))

            .RET()

            '**********************
            'コマンド：休符の処理
            '**********************
            .Label(labelPrefix & NameOf(Labels.ReadKyufuData))
            If True Then
                '音長残
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatLengthRemain)))
                .LD(.A, .DEref) '音長1バイト目
                .LD(.HLref, .A)
                .INC(.DE) '位置をずらす
                .INC(.HL) '位置をずらす
                .LD(.A, .DEref) '音長2バイト目
                .LD(.HLref, .A)
                .INC(.DE)

                '音データの現在アドレスをメモリに記録(次回のため)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                .JP(.LabelRef(labelPrefix & NameOf(Labels.OutputSoundByStatus)))
            End If

            '**************************
            'コマンド：ピッチエンベロープOFFの処理
            '**************************
            .Label(labelPrefix & NameOf(Labels.ReadPitchEnvelopeOffData))
            If True Then
                ''ピッチエンベロープインデックス1バイト
                '.LD(.A, .DEref)
                '.LD(.B, .A)
                '.INC(.DE)

                '音データの現在アドレスをメモリに記録(次回のため)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                'ボリュームモードを0(エンベロープボリューム)にする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeMode)))
                .LD(.HLref, &H0)

                '再度音データを読み直す
                .JP(.LabelRef(labelPrefix & NameOf(Labels.ReadSongDataOne)))
            End If

            '**************************
            'コマンド：ピッチエンベロープの処理
            '**************************
            .Label(labelPrefix & NameOf(Labels.ReadPitchEnvelopeData))
            If True Then
                'ピッチエンベロープインデックス1バイト
                .LD(.A, .DEref)
                .LD(.B, .A)
                .INC(.DE)

                '音データの現在アドレスをメモリに記録(次回のため)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                'インデックスデータを探索して、読み取り位置を特定する
                'インデックスデータ一つにつき6バイトあるので、インデックスデータの回数x6バイトスキップする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.DataPitchEnvelopeIndex))) 'インデックスデータの先頭アドレス
                If True Then
                    .Label(labelPrefix & NameOf(Labels.SearchPitchEnvelopeDataIndex))
                    .LD(.A, &H0)
                    .CP(.B)
                    .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadPitchEnvelopeDataSomePosition)))

                    '.CALL(MZ1500Assembler.CallFunc.LETNL)
                    '.LD(.DE, .LabelRef("title:"))
                    '.CALL(MZ1500Assembler.CallFunc.MSG)

                    .DEC(.B)
                    .INC(.HL) ' x6
                    .INC(.HL)
                    .INC(.HL)
                    .INC(.HL)
                    .INC(.HL)
                    .INC(.HL)

                    .JP(.LabelRef(labelPrefix & NameOf(Labels.SearchPitchEnvelopeDataIndex)))
                End If

                'インデックス内のデータを読み取る
                .Label(labelPrefix & NameOf(Labels.ReadPitchEnvelopeDataSomePosition))
                If True Then
                    .LD(.E, .L)
                    .LD(.D, .H)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeStartPosition))) '開始位置
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeEndPosition))) '終了位置
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeReStartPosition))) '再開位置
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition))) '現在位置
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeStartPosition))) '
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                End If

                '発音をOFFにする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOn)))
                .LD(.HLref, &H0)

                'ボリュームモードを1(エンベロープボリューム)にする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeMode)))
                .LD(.HLref, &H1)

                '再度音データを読み直す
                .JP(.LabelRef(labelPrefix & NameOf(Labels.ReadSongDataOne)))
            End If


            '**************************
            'コマンド：ボリュームエンベロープの処理
            '**************************
            .Label(labelPrefix & NameOf(Labels.ReadAtVolumeData))
            If True Then
                'ボリュームエンベロープインデックス1バイト
                .LD(.A, .DEref)
                .LD(.B, .A)
                .INC(.DE)

                '音データの現在アドレスをメモリに記録(次回のため)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                'インデックスデータを探索して、読み取り位置を特定する
                'インデックスデータ一つにつき6バイトあるので、インデックスデータの回数x6バイトスキップする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.DataVolueEnvelopeIndex))) 'インデックスデータの先頭アドレス
                If True Then
                    .Label(labelPrefix & NameOf(Labels.SearchAtVolumeDataIndex))
                    .LD(.A, &H0)
                    .CP(.B)
                    .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.ReadAtVolumeDataSomePosition)))

                    '.CALL(MZ1500Assembler.CallFunc.LETNL)
                    '.LD(.DE, .LabelRef("title:"))
                    '.CALL(MZ1500Assembler.CallFunc.MSG)

                    .DEC(.B)
                    .INC(.HL) ' x6
                    .INC(.HL)
                    .INC(.HL)
                    .INC(.HL)
                    .INC(.HL)
                    .INC(.HL)

                    .JP(.LabelRef(labelPrefix & NameOf(Labels.SearchAtVolumeDataIndex)))
                End If

                'インデックス内のデータを読み取る
                .Label(labelPrefix & NameOf(Labels.ReadAtVolumeDataSomePosition))
                If True Then
                    .LD(.E, .L)
                    .LD(.D, .H)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeStartPosition))) '開始位置
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeEndPosition))) '終了位置
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeReStartPosition))) '再開位置
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition))) '現在位置
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeStartPosition))) '
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.DE)

                End If

                '発音をOFFにする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOn)))
                .LD(.HLref, &H0)

                'ボリュームモードを1(エンベロープボリューム)にする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatVolumeMode)))
                .LD(.HLref, &H1)

                '再度音データを読み直す
                .JP(.LabelRef(labelPrefix & NameOf(Labels.ReadSongDataOne)))
            End If

            '**************************
            'コマンド：ボリュームの処理
            '**************************
            .Label(labelPrefix & NameOf(Labels.ReadVolumeData))
            If True Then
                'ボリューム1バイト
                .LD(.A, .DEref)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatVolume))) 'ステータスの先頭アドレス
                .LD(.HLref, .A)
                .INC(.DE)

                '発音をOFFにする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOn)))
                .LD(.HLref, &H0)

                'ボリュームモードを0(通常ボリューム)にする
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatVolumeMode)))
                .LD(.HLref, &H0)

                '音データの現在アドレスをメモリに記録(次回のため)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                '再度音データを読み直す
                .JP(.LabelRef(labelPrefix & NameOf(Labels.ReadSongDataOne)))
            End If

            '**************************
            'コマンド：ノイズタイプの処理
            '**************************
            .Label(labelPrefix & NameOf(Labels.ReadNoiseTypeData))
            If True Then
                'ノイズタイプ1バイト
                .LD(.A, .DEref)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseType))) 'ステータスの先頭アドレス
                .LD(.HLref, .A)
                .INC(.DE)

                '音データの現在アドレスをメモリに記録(次回のため)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                'ノイズコントロールを出力
                .CALL(.LabelRef(labelPrefix & NameOf(Labels.OutputNoiseControl)))

                '再度音データを読み直す
                .JP(.LabelRef(labelPrefix & NameOf(Labels.ReadSongDataOne)))
            End If

            '**************************
            'コマンド：ノイズ周波数タイプの処理
            '**************************
            .Label(labelPrefix & NameOf(Labels.ReadNoiseSyuhasuTypeData))
            If True Then
                'ノイズ周波数タイプ1バイト
                .LD(.A, .DEref)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseSyuhasuType))) 'ステータスの先頭アドレス
                .LD(.HLref, .A)
                .INC(.DE)

                '音データの現在アドレスをメモリに記録(次回のため)
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                'ノイズコントロールを出力
                .CALL(.LabelRef(labelPrefix & NameOf(Labels.OutputNoiseControl)))

                '再度音データを読み直す
                .JP(.LabelRef(labelPrefix & NameOf(Labels.ReadSongDataOne)))
            End If

            '*******************************
            'ノイズコントロールの出力
            '*******************************
            'ノイズコントロールバイト(※ステータスに記録せず出力する) ステータスに記録して都度出力してしまうと音が正しく発音できない
            .Label(labelPrefix & NameOf(Labels.OutputNoiseControl))
            If musicBinary.ToneType = ToneDataCreator.PSGToneType.Noise Then
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseType)))
                .LD(.A, .DEref)
                .SLA(.A) '左へ2ビットシフト
                .SLA(.A)
                .LD(.B, .A)
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseSyuhasuType)))
                .LD(.A, .DEref)
                .OR(.B)
                .LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData1OrOnryo Or ToneDataCreator.PSGToneType.Noise Or ToneDataCreator.PSGControlType.SyuhasuOrControl)
                .OR(.B)
                .OUT(ioPort)
            End If
            .RET()

            '**********************
            'コマンド：トーンの処理
            '**********************

            .Label(labelPrefix & NameOf(Labels.ReadToneData))

            '音データの現在アドレスから3バイト分をセット(ノイズの場合は4バイト)
            '発音をONにする
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOn)))
            .LD(.HLref, &H1)
            'サウンド1バイト
            .LD(.A, .DEref)
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1))) 'ステータスの先頭アドレス
            .LD(.HLref, .A)
            .INC(.DE)
            'サウンド2バイト
            .LD(.A, .DEref)
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte2))) 'ステータスの先頭アドレス
            .LD(.HLref, .A)
            .INC(.DE)
            ''ノイズコントロールバイト(※ステータスに記録せず出力する) ステータスに記録して都度出力してしまうと音が正しく発音できない
            'If musicBinary.ToneType = ToneDataCreator.PSGToneType.Noise Then
            '    .LD(.A, .DEref)
            '    '.OUT(ioPort)
            '    '.LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseControl))) 'ステータスの先頭アドレス
            '    '.LD(.HLref, .A)
            '    .INC(.DE)
            'End If
            '音長残
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatLengthRemain)))
            .LD(.A, .DEref) '音長1バイト目
            .LD(.HLref, .A)
            .INC(.DE) '位置をずらす
            .INC(.HL) '位置をずらす
            .LD(.A, .DEref) '音長2バイト目
            .LD(.HLref, .A)
            .INC(.DE)
            '発音残
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOnRemain)))
            .LD(.A, .DEref) '音長1バイト目
            .LD(.HLref, .A)
            .INC(.DE) '位置をずらす
            .INC(.HL) '位置をずらす
            .LD(.A, .DEref) '音長2バイト目
            .LD(.HLref, .A)
            .INC(.DE)
            '音データの現在アドレスをメモリに記録(次回のため)
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatSongDataPosition)))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            '音量エンベロープの位置をリセットする
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition))) '現在位置
            .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeStartPosition))) '
            .LD(.A, .DEref)
            .LD(.HLref, .A)
            .INC(.HL)
            .INC(.DE)
            .LD(.A, .DEref)
            .LD(.HLref, .A)

            'ピッチエンベロープの位置をリセットする
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition))) '現在位置
            .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeStartPosition))) '
            .LD(.A, .DEref)
            .LD(.HLref, .A)
            .INC(.HL)
            .INC(.DE)
            .LD(.A, .DEref)
            .LD(.HLref, .A)

            '前回のトーン値をリセットする(MZ-700のみ)
            If musicBinary.Device = ToneDataCreator.PSGDeviceType.MZ700 Then

                .LD(.A, &H0)
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousSoundByte1ForMZ700)))
                .LD(.DEref, .A)

                .LD(.A, &H0)
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousSoundByte2ForMZ700)))
                .LD(.DEref, .A)

            End If

            '***************************
            'ステータスにより音を発生する
            '***************************
            .Label(labelPrefix & NameOf(Labels.OutputSoundByStatus))

            'ノートON/OFFを取得
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOn)))
            .LD(.A, &H0)
            .CP(.HLref)
            'ノートOFFなら音量0で発音する処理へ
            .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeOff)))

            'ピッチエンベロープモードにより処理を行う
            If True Then

                'ピッチエンベロープモードを取得する
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeMode)))
                .LD(.A, .DEref)
                .LD(.B, .A)
                .LD(.A, &H0)
                .CP(.B)
                .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.OutputSound)))

                If True Then

                    'ピッチエンベロープモード1で発音
                    .Label(labelPrefix & NameOf(Labels.OutputSoundByStatusModeEnvelope))

                    '現在のアドレスのピッチエンベロープ値を取得
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition)))
                    .LD(.A, .DEref)
                    .LD(.L, .A)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.H, .A)
                    .INC(.DE)
                    'ピッチエンベロープ値(B)を取得
                    .LD(.A, .HLref)
                    .LD(.B, .A)

                    '現在のサウンドバイト値(HL)に加算する
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
                    .LD(.A, .DEref)
                    .LD(.L, .A)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.H, .A)

                    'エンベロープ値(B→A)を16ビット符号拡張する(DE)
                    .LD(.A, .B)
                    .LD(.E, .A)
                    .ADD(.A, .A)
                    .SBC(.A, .A)
                    .LD(.D, .A)

                    '                 LD  E, A
                    'ADD A, A      ; sign bit of A into carry
                    'SBC A, A      ; A = 0 If carry == 0, $FF otherwise
                    'LD  D, A      ; now DE Is sign extended A
                    'ADD HL, DE





                    .ADD(.HL, .DE)

                    '現在のサウンドバイト値として格納する
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
                    .LD(.A, .L)
                    .LD(.DEref, .A)
                    .INC(.DE)
                    .LD(.A, .H)
                    .LD(.DEref, .A)
                    .INC(.DE)

                    '.LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData1OrOnryo Or musicBinary.ToneType Or ToneDataCreator.PSGControlType.Onryo)
                    '.OR(.B)
                    '.OUT(ioPort)

                    '.JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementNoteOnRemain)))

                    '終わりの位置に達していたら、現在の座標は記録せずに、次の処理へスキップ
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeEndPosition)))
                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition)))
                    .LD(.A, .DEref)
                    .LD(.B, .HLref)
                    .CP(.B)
                    .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.IncrementPitchEnvelopeCurrentPosition)))
                    .INC(.DE)
                    .INC(.HL)
                    .LD(.A, .DEref)
                    .LD(.B, .HLref)
                    .CP(.B)
                    .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.IncrementPitchEnvelopeCurrentPosition)))

                    '繰り返し位置に設定
                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition))) '現在位置
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeReStartPosition))) '
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)

                    .JP(.LabelRef(labelPrefix & NameOf(Labels.OutputSound)))

                    .Label(labelPrefix & NameOf(Labels.IncrementPitchEnvelopeCurrentPosition))

                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition)))
                    .LD(.A, .DEref)
                    .LD(.L, .A)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.H, .A)
                    .INC(.DE)

                    .INC(.HL)
                    '.INC(.HL)

                    '次回のために記録する
                    .LD(.D, .H)
                    .LD(.E, .L)
                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition)))
                    .LD(.HLref, .E)
                    .INC(.HL)
                    .LD(.HLref, .D)

                    .JP(.LabelRef(labelPrefix & NameOf(Labels.OutputSound)))

                End If

            End If

            'サウンドバイトデータを取得
            .Label(labelPrefix & NameOf(Labels.OutputSound))

            '周波数タイプがチャンネル2利用でなければスキップ
            If musicBinary.ToneType = ToneDataCreator.PSGToneType.Noise Then
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseSyuhasuType)))
                .LD(.A, &H3)
                .CP(.HLref)
                .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputSoundEnd)))
            End If

            If musicBinary.Device = ToneDataCreator.PSGDeviceType.MZ1500 Then

                '1バイト目
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
                .LD(.A, .DEref)
                .LD(.B, .A)
                .LD(.A, &HF) '下位4ビットのみとする
                .AND(.B)
                Dim toneType As ToneDataCreator.PSGToneType = musicBinary.ToneType
                If toneType = ToneDataCreator.PSGToneType.Noise Then
                    toneType = ToneDataCreator.PSGToneType.Tone2
                End If
                .LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData1OrOnryo Or toneType Or ToneDataCreator.PSGControlType.SyuhasuOrControl)
                .OR(.B)
                .OUT(ioPort)

                '2バイト目
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte2)))
                .LD(.A, .DEref)
                .SLA(.A) '左へ4ビットシフト
                .SLA(.A)
                .SLA(.A)
                .SLA(.A)
                .LD(.B, .A)
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
                .LD(.A, .DEref)
                .SRL(.A) '右へ4ビットシフト
                .SRL(.A)
                .SRL(.A)
                .SRL(.A)
                .OR(.B)
                .LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData2)
                .OR(.B)
                .OUT(ioPort)

            ElseIf musicBinary.Device = ToneDataCreator.PSGDeviceType.MZ700 Then

                'ここでは出力しない(ボリュームの所で出力)

            End If

            'サウンドバイトデータを取得
            .Label(labelPrefix & NameOf(Labels.OutputSoundEnd))

            ''ノイズコントロールバイト(※ステータスに記録せず出力する) ステータスに記録して都度出力してしまうと音が正しく発音できない
            'If musicBinary.ToneType = ToneDataCreator.PSGToneType.Noise Then
            '    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseType)))
            '    .LD(.A, .DEref)
            '    .SLA(.A) '左へ2ビットシフト
            '    .SLA(.A)
            '    .LD(.B, .A)
            '    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatNoiseSyuhasuType)))
            '    .LD(.A, .DEref)
            '    .OR(.B)
            '    .LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData1OrOnryo Or ToneDataCreator.PSGToneType.Noise Or ToneDataCreator.PSGControlType.SyuhasuOrControl)
            '    .OR(.B)
            '    .OUT(ioPort)
            'End If

            '周波数第1バイト
            'syuhasuByte1 = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.SyuhasuOrControl Or (syuhasuRegValue And &HF)
            ''周波数第2バイト
            'syuhasuByte2 = PSGToneByteOrder.ToneData2 Or (syuhasuRegValue >> 4)

            ''サウンドバイト1
            '.LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
            '.LD(.A, .DEref)
            '.OUT(ioPort)
            ''サウンドバイト2
            '.LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte2)))
            '.LD(.A, .DEref)
            '.OUT(ioPort)

            '発音長を取得
            .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOnRemain)))

            '発音長が0で無いならボリュームをステータスから発音する
            .LD(.A, &H0)
            .CP(.HLref)
            .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatus)))
            .INC(.HL)
            .CP(.HLref)
            .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatus)))

            '発音長が0ならボリュームを0で発音する
            .JP(.LabelRef(labelPrefix & NameOf(Labels.OutputVolumeOff)))

            'ボリュームをステータスから発音する
            .Label(labelPrefix & NameOf(Labels.OutputVolumeByStatus))
            If True Then

                'ボリュームモードを取得する
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatVolumeMode)))
                .LD(.A, .DEref)
                .LD(.B, .A)
                .LD(.A, &H0)
                .CP(.B)
                .JP(.Z, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefault)))
                .JP(.LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeEnvelope)))

                If True Then

                    'ボリュームモード0で発音
                    .Label(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefault))
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatVolume)))
                    .LD(.A, .DEref)
                    If musicBinary.Device = ToneDataCreator.PSGDeviceType.MZ1500 Then
                        .LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData1OrOnryo Or musicBinary.ToneType Or ToneDataCreator.PSGControlType.Onryo)
                        .OR(.B)
                        .OUT(ioPort)
                    ElseIf musicBinary.Device = ToneDataCreator.PSGDeviceType.MZ700 Then

                        '前回の音量と異なるなら出力へ
                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatVolume)))
                        .LD(.A, .DEref)
                        .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousVolumeForMZ700)))
                        .LD(.B, .HLref)
                        .CP(.B)
                        .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700)))

                        '前回の音程と異なるなら出力へ
                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
                        .LD(.A, .DEref)
                        .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousSoundByte1ForMZ700)))
                        .LD(.B, .HLref)
                        .CP(.B)
                        .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700)))

                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte2)))
                        .LD(.A, .DEref)
                        .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousSoundByte2ForMZ700)))
                        .LD(.B, .HLref)
                        .CP(.B)
                        .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700)))

                        '上記すべて同じなら何も出力しない
                        .JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementNoteOnRemain)))

                        '出力する
                        .Label(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700))

                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatVolume)))
                        .LD(.A, .DEref)
                        .LD(.B, &HF)
                        .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700SoundOn)))

                        '発音をストップする
                        .Label(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700SoundOff))
                        If True Then
                            .LD(.A, &H0)
                            .LD(.HL, &HE008)
                            .LD(.HLref, .A)
                            .JP(.LabelRef(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700Next)))
                        End If

                        '発音を行う
                        .Label(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700SoundOn))
                        If True Then
                            '初期化
                            .LD(.A, &H1)
                            .LD(.HL, &HE008)
                            .LD(.HLref, .A)

                            .LD(.A, &H36)
                            .LD(.HL, &HE007)
                            .LD(.HLref, .A)


                            '出力
                            .LD(.HL, &HE004)
                            .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
                            .LD(.A, .DEref)
                            .LD(.HLref, .A)
                            .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte2)))
                            .LD(.A, .DEref)
                            .LD(.HLref, .A)
                        End If

                        .Label(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeDefaultForMZ700Next))

                        '次回の為に値を記録しておく
                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatVolume)))
                        .LD(.A, .DEref)
                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousVolumeForMZ700)))
                        .LD(.DEref, .A)

                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte1)))
                        .LD(.A, .DEref)
                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousSoundByte1ForMZ700)))
                        .LD(.DEref, .A)

                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatSoundByte2)))
                        .LD(.A, .DEref)
                        .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatPreviousSoundByte2ForMZ700)))
                        .LD(.DEref, .A)

                    End If
                    .JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementNoteOnRemain)))
                End If
                If True Then

                    'ボリュームモード1で発音
                    .Label(labelPrefix & NameOf(Labels.OutputVolumeByStatusModeEnvelope))

                    '現在のアドレスのボリュームで発音
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition)))
                    .LD(.A, .DEref)
                    .LD(.L, .A)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.H, .A)
                    .INC(.DE)
                    'ボリューム値を取得
                    .LD(.A, .HLref)
                    .LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData1OrOnryo Or musicBinary.ToneType Or ToneDataCreator.PSGControlType.Onryo)
                    .OR(.B)
                    .OUT(ioPort)

                    '.JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementNoteOnRemain)))

                    '終わりの位置に達していたら、現在の座標は記録せずに、次の処理へスキップ
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeEndPosition)))
                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition)))
                    .LD(.A, .DEref)
                    .LD(.B, .HLref)
                    .CP(.B)
                    .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.IncrementAtVolumeCurrentPosition)))
                    .INC(.DE)
                    .INC(.HL)
                    .LD(.A, .DEref)
                    .LD(.B, .HLref)
                    .CP(.B)
                    .JP(.NZ, .LabelRef(labelPrefix & NameOf(Labels.IncrementAtVolumeCurrentPosition)))

                    '繰り返し位置に設定
                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition))) '現在位置
                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeReStartPosition))) '
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)
                    .INC(.HL)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.HLref, .A)

                    .JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementNoteOnRemain)))

                    .Label(labelPrefix & NameOf(Labels.IncrementAtVolumeCurrentPosition))

                    .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition)))
                    .LD(.A, .DEref)
                    .LD(.L, .A)
                    .INC(.DE)
                    .LD(.A, .DEref)
                    .LD(.H, .A)
                    .INC(.DE)

                    .INC(.HL)
                    '.INC(.HL)

                    '次回のために記録する
                    .LD(.D, .H)
                    .LD(.E, .L)
                    .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition)))
                    .LD(.HLref, .E)
                    .INC(.HL)
                    .LD(.HLref, .D)

                    .JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementNoteOnRemain)))

                End If

            End If

            If True Then
                '発音長-1する
                .Label(labelPrefix & NameOf(Labels.DecrementNoteOnRemain))
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOnRemain)))
                .LD(.E, .HLref)
                .INC(.HL)
                .LD(.D, .HLref)
                .DEC(.DE)

                '発音長を次回のために記録する
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatNoteOnRemain)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)

                .JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementLengthRemain)))
            End If

            'ボリュームを0で発音する
            .Label(labelPrefix & NameOf(Labels.OutputVolumeOff))
            If True Then
                '音量バイト
                .LD(.DE, .LabelRef(labelPrefix & NameOf(Labels.NoteOffVolumeByte)))
                .LD(.A, .DEref)
                If musicBinary.Device = ToneDataCreator.PSGDeviceType.MZ1500 Then
                    .LD(.B, ToneDataCreator.PSGToneByteOrder.ToneData1OrOnryo Or musicBinary.ToneType Or ToneDataCreator.PSGControlType.Onryo)
                    .OR(.B)
                    .OUT(ioPort)
                Else
                    .LD(.A, &H0)
                    .LD(.HL, &HE008)
                    .LD(.HLref, .A)
                End If

                .JP(.LabelRef(labelPrefix & NameOf(Labels.DecrementLengthRemain)))
            End If

            '音長-1する
            .Label(labelPrefix & NameOf(Labels.DecrementLengthRemain))
            If True Then
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatLengthRemain)))
                .LD(.E, .HLref)
                .INC(.HL)
                .LD(.D, .HLref)
                .DEC(.DE)

                '音長を次回のために記録する
                .LD(.HL, .LabelRef(labelPrefix & NameOf(Labels.StatLengthRemain)))
                .LD(.HLref, .E)
                .INC(.HL)
                .LD(.HLref, .D)
            End If

            .RET()

            '******************
            '各種ステータス
            '*****************

            Dim byteEmpty As Byte = 0
            Dim byte1 As Byte = 0
            'チャンネルの有効性
            .Label(labelPrefix & NameOf(Labels.StatEnabled))
            .DB(byte1)
            '発音ON/OFF
            .Label(labelPrefix & NameOf(Labels.StatNoteOn))
            .DB(byteEmpty)
            '音長残
            .Label(labelPrefix & NameOf(Labels.StatLengthRemain))
            .DB(byteEmpty)
            .DB(byteEmpty)
            '発音残
            .Label(labelPrefix & NameOf(Labels.StatNoteOnRemain))
            .DB(byteEmpty)
            .DB(byteEmpty)
            '音程1
            .Label(labelPrefix & NameOf(Labels.StatSoundByte1))
            .DB(byteEmpty)
            '音程2
            .Label(labelPrefix & NameOf(Labels.StatSoundByte2))
            .DB(byteEmpty)
            '音程1(ひとつ前)
            .Label(labelPrefix & NameOf(Labels.StatPreviousSoundByte1ForMZ700))
            .DB(byteEmpty)
            '音程2(ひとつ前)
            .Label(labelPrefix & NameOf(Labels.StatPreviousSoundByte2ForMZ700))
            .DB(byteEmpty)
            '.Label(labelPrefix & NameOf(Labels.StatNoiseControl))
            '.DB(byteEmpty)
            '.Label(labelPrefix & NameOf(Labels.StatPreviousNoiseControl))
            '.DB(byteEmpty)
            'ノイズタイプ 0:同期ノイズ 1:ホワイトノイズ
            .Label(labelPrefix & NameOf(Labels.StatNoiseType))
            .DB(byteEmpty)
            'ノイズ周波数タイプ
            .Label(labelPrefix & NameOf(Labels.StatNoiseSyuhasuType))
            .DB(byteEmpty)
            'ボリュームモード 0:通常 1:エンベロープ
            .Label(labelPrefix & NameOf(Labels.StatVolumeMode))
            .DB(byteEmpty)
            'ボリューム
            .Label(labelPrefix & NameOf(Labels.StatVolume))
            .DB(byteEmpty)
            'ボリューム
            .Label(labelPrefix & NameOf(Labels.StatPreviousVolumeForMZ700))
            .DB(byteEmpty)
            'ボリュームエンベロープ：開始位置
            .Label(labelPrefix & NameOf(Labels.StatAtVolumeStartPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)
            'ボリュームエンベロープ：終了位置
            .Label(labelPrefix & NameOf(Labels.StatAtVolumeEndPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)
            'ボリュームエンベロープ：再開位置
            .Label(labelPrefix & NameOf(Labels.StatAtVolumeReStartPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)
            'ボリュームエンベロープ：現在位置
            .Label(labelPrefix & NameOf(Labels.StatAtVolumeCurrentPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)

            'ピッチエンベロープモード 0:OFF 1:ON
            .Label(labelPrefix & NameOf(Labels.StatPitchEnvelopeMode))
            .DB(byteEmpty)
            'ピッチエンベロープ：開始位置
            .Label(labelPrefix & NameOf(Labels.StatPitchEnvelopeStartPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)
            'ピッチエンベロープ：終了位置
            .Label(labelPrefix & NameOf(Labels.StatPitchEnvelopeEndPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)
            'ピッチエンベロープ：再開位置
            .Label(labelPrefix & NameOf(Labels.StatPitchEnvelopeReStartPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)
            'ピッチエンベロープ：現在位置
            .Label(labelPrefix & NameOf(Labels.StatPitchEnvelopeCurrentPosition))
            .DB(byteEmpty)
            .DB(byteEmpty)

            '音データの現在アドレス
            .Label(labelPrefix & NameOf(Labels.StatSongDataPosition))
            .DB(.LabelRef(labelPrefix & NameOf(Labels.DataSong)))

            '*****************
            '音データ等
            '****************

            '音データ
            .Label(labelPrefix & NameOf(Labels.DataSong))
            For Each sound As MusicBinary.SoundCommand In musicBinary.SoundCommandList
                .DB(sound.ToByteArray)
            Next

            '音量エンベロープデータ
            If True Then
                'アドレスのインデックス 
                .Label(labelPrefix & NameOf(Labels.DataVolueEnvelopeIndex))
                For Each volumeEnvelope As VolumeEnvelope In musicBinary.VolumeEnvelopeList
                    .DB(.LabelRef(labelPrefix & NameOf(Labels.DataVolueEnvelopeStartPosition) & CStr(volumeEnvelope.No)))
                    .DB(.LabelRef(labelPrefix & NameOf(Labels.DataVolueEnvelopeEndPosition) & CStr(volumeEnvelope.No)))
                    .DB(.LabelRef(labelPrefix & NameOf(Labels.DataVolueEnvelopeReStartPosition) & CStr(volumeEnvelope.No)))
                Next
                '実データ
                For Each volumeEnvelope As VolumeEnvelope In musicBinary.VolumeEnvelopeList
                    .Label(labelPrefix & NameOf(Labels.DataVolueEnvelopeStartPosition) & CStr(volumeEnvelope.No))
                    For Each volume As VolumeEnvelope.Volume In volumeEnvelope.VolumeList
                        If volume.IsEndPosition Then
                            .Label(labelPrefix & NameOf(Labels.DataVolueEnvelopeEndPosition) & CStr(volumeEnvelope.No))
                        End If
                        If volume.IsRestartPosition Then
                            .Label(labelPrefix & NameOf(Labels.DataVolueEnvelopeReStartPosition) & CStr(volumeEnvelope.No))
                        End If
                        .DB(volume.GetBinaryValue())
                    Next
                Next
            End If

            'ピッチエンベロープデータ
            If True Then
                'アドレスのインデックス 
                .Label(labelPrefix & NameOf(Labels.DataPitchEnvelopeIndex))
                For Each pitchEnvelope As PitchEnvelope In musicBinary.PitchEnvelopeList
                    .DB(.LabelRef(labelPrefix & NameOf(Labels.DataPitchEnvelopeStartPosition) & CStr(pitchEnvelope.No)))
                    .DB(.LabelRef(labelPrefix & NameOf(Labels.DataPitchEnvelopeEndPosition) & CStr(pitchEnvelope.No)))
                    .DB(.LabelRef(labelPrefix & NameOf(Labels.DataPitchEnvelopeReStartPosition) & CStr(pitchEnvelope.No)))
                Next
                '実データ
                For Each pitchEnvelope As PitchEnvelope In musicBinary.PitchEnvelopeList
                    .Label(labelPrefix & NameOf(Labels.DataPitchEnvelopeStartPosition) & CStr(pitchEnvelope.No))
                    For Each pitch As PitchEnvelope.Pitch In pitchEnvelope.PitchEnvelopeList
                        If pitch.IsEndPosition Then
                            .Label(labelPrefix & NameOf(Labels.DataPitchEnvelopeEndPosition) & CStr(pitchEnvelope.No))
                        End If
                        If pitch.IsRestartPosition Then
                            .Label(labelPrefix & NameOf(Labels.DataPitchEnvelopeReStartPosition) & CStr(pitchEnvelope.No))
                        End If
                        .DB(pitch.GetBinaryValue())
                    Next
                Next
            End If

            '音量0のデータ
            .Label(labelPrefix & NameOf(Labels.NoteOffVolumeByte))
            .DB(musicBinary.NoteOffVolumeByte)

        End With

    End Sub
End Class
