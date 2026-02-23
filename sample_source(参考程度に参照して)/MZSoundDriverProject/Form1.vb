Public Class Form1


    ''' <summary>ボタンクリックで出力</summary>
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        Dim pcgList As PCGList = PCGList.FromCSV("C:\Users\sato\Desktop\その他\MZSoundDriverProject\ys2.csv")

        'MZ1500アセンブラでマシン語をビルド
        Dim assembler As New MZ1500Assembler
        Dim pcgLabelPrefix As String = "pcg_"
        With assembler
            .ORG(&H1200)
            .Label("main:")
            '.CALL(MZ1500Assembler.CallFunc.LETNL)
            '.LD(.DE, .LabelRef("title:"))
            '.CALL(MZ1500Assembler.CallFunc.MSG)

            .CALL(.LabelRef(pcgLabelPrefix & "CLS"))

            .JP(.LabelRef(pcgLabelPrefix & "start"))
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
            .JP(.LabelRef(pcgLabelPrefix & "loop:"))

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
        Dim byteData() As Byte = assembler.Build()

        'クイックディスクのファイルを作成
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte(byteData)
        qdTemp.Save("c:\temp\hotatemusic.bin")
        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("HOTATEMUSIC", qdTemp)
        qd.Save("c:\temp\hotatemusic.qdf")

    End Sub


    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte({&HCD, &H6, &H0, &H11, &H13, &H12, &HCD, &H15, &H0, &HCD, &H6, &H0, &H11, &H23, &H12, &HCD})
        qdTemp.AppendByte({&H15, &H0, &HC9, &H2D, &H20, &H48, &H45, &H4C, &H4C, &H4F, &H20, &H57, &H4F, &H52, &H4C, &H44})
        qdTemp.AppendByte({&H20, &H2D, &HD, &H2D, &H20, &H49, &H4E, &H20, &H4D, &H5A, &H2D, &H37, &H30, &H30, &H20, &H2D})
        qdTemp.AppendByte({&HD})
        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("TEST", qdTemp)
        qd.Save("c:\temp\test.qdf")
    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click


        Dim noteMap As New NoteMap()


        'MZ1500アセンブラでマシン語をビルド
        Dim assembler As New MZ1500Assembler
        With assembler
            .ORG(&H1200)
            .Label("main:")
            .CALL(MZ1500Assembler.CallFunc.LETNL)
            .LD(.DE, .LabelRef("title:"))
            .CALL(MZ1500Assembler.CallFunc.MSG)

            '.LD(.BC, .LabelRef("sound:"))
            '.LD(&H38US, .BC)
            .LD(.HL, &H1038US)
            .LD(.HLref, &H1234US)

            .DI() '割り込み禁止
            .IM1() 'Z80割り込みモード1
            .LD(.HL, &HE007US) '8253 コントロールポート
            .LD(.HLref, &HB0) 'チャネル2, モード0, 下位バイト→上位バイトの順にカウンタを設定
            .LD(.HLref, &H74) 'チャネル1, モード2, 下位バイト→上位バイトの順にカウンタを設定
            .DEC(.L) '8253 チャネル2

            .LD(.HLref, &H1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, &H0)   ' チャネル2のカウント値の下位バイトを設定

            .DEC(.L) '8253 チャネル1

            .LD(.HLref, &H1)   ' チャネル1のカウント値の下位バイトを設定
            .LD(.HLref, &H0)   ' チャネル1のカウント値の下位バイトを設定

            .LD(.A, &H5) 'INTMSK = 1 に設定し、8253からの割り込みを許可する
            .LD(&HE003US, .A) ' 8255 ビットセット

            .EI() ' 割り込みを許可

            .CALL(MZ1500Assembler.CallFunc.LETNL)
            .LD(.DE, .LabelRef("title:"))
            .CALL(MZ1500Assembler.CallFunc.MSG)


            .Label("loop:")
            .JP(.LabelRef("loop:"))

            .Label("sound:")

            .CALL(MZ1500Assembler.CallFunc.LETNL)
            .LD(.DE, .LabelRef("title2:"))
            .CALL(MZ1500Assembler.CallFunc.MSG)


            If True Then
                Dim syuhasuByte1 As Byte = Nothing
                Dim syuhasuByte2 As Byte = Nothing
                Dim onryoByte As Byte = Nothing
                Dim syuhasuRegValue As Integer = noteMap.GetFreqencyRegValue(3, "c")
                ToneDataCreator.GetBytesData(ToneDataCreator.PSGToneType.Tone0, syuhasuRegValue, 15, syuhasuByte1, syuhasuByte2, onryoByte)

                .LD(.A, syuhasuByte1)
                .OUT(IOPort.PSG1)
                .LD(.A, syuhasuByte2)
                .OUT(IOPort.PSG1)
                .LD(.A, onryoByte)
                .OUT(IOPort.PSG1)

            End If
            .Label("loop1:")
            .JP(.LabelRef("loop1:"))

            .RET()
            .Label("title:")
            .DB("HOTATE-NO-MUSIC")
            .DB(&HD)
            .Label("title2:")
            .DB("WAKAME")
            .DB(&HD)
        End With
        Dim byteData() As Byte = assembler.Build()

        assembler.DebugOut(byteData)

        'クイックディスクのファイルを作成
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte(byteData)
        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("HOTATEMUSIC", qdTemp)
        qd.Save("c:\temp\hotatemusic.qdf")

    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click


        Dim noteMap As New NoteMap()

        Const CHANNEL2_CYCLE_BYTE1 As Byte = &H7
        Const CHANNEL2_CYCLE_BYTE2 As Byte = &H1


        'MZ1500アセンブラでマシン語をビルド
        Dim assembler As New MZ1500Assembler
        With assembler
            .ORG(&H1200)
            .Label("main:")

            .DI() '割り込み禁止
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
            .LD(.HLref, &H1)   ' チャネル1のカウント値の下位バイトを設定
            .LD(.HLref, &H0)   ' チャネル1のカウント値の上位バイトを設定

            .LD(.A, &H5) 'INTMSK = 1 に設定し、8253からの割り込みを許可する
            .LD(&HE003US, .A) ' 8255 ビットセット

            .EI() ' 割り込みを許可

            .Label("loop:")
            .JP(.LabelRef("loop:"))

            .Label("sound:")
            '.CALL(MZ1500Assembler.CallFunc.LETNL)
            '.LD(.DE, .LabelRef("title:"))
            '.CALL(MZ1500Assembler.CallFunc.MSG)

            '.LD(.HL, .LabelRef("title:"))
            '.INC(.HLref)

            'Dim octave As Integer = 4
            'For Each psgPort As Byte In {IOPort.PSG1, IOPort.PSG2} 'PSG1 と PSG2
            '    Dim psgDic As New Dictionary(Of Byte, String)
            '    psgDic.Add(ToneDataCreator.PSGToneType.Tone0, "c") 'ド
            '    psgDic.Add(ToneDataCreator.PSGToneType.Tone1, "e") 'ミ
            '    psgDic.Add(ToneDataCreator.PSGToneType.Tone2, "g") 'ソ
            '    For Each toneNoWithTone As KeyValuePair(Of Byte, String) In psgDic
            '        Dim syuhasuByte1 As Byte = Nothing
            '        Dim syuhasuByte2 As Byte = Nothing
            '        Dim onryoByte As Byte = Nothing
            '        Dim syuhasuRegValue As Integer = noteMap.GetFreqencyRegValue(octave:=octave, noteName:=toneNoWithTone.Value)
            '        ToneDataCreator.GetBytesData(toneNoWithTone.Key, syuhasuRegValue, 15, syuhasuByte1, syuhasuByte2, onryoByte)
            '        .LD(.A, syuhasuByte1)
            '        .OUT(psgPort)
            '        .LD(.A, syuhasuByte2)
            '        .OUT(psgPort)
            '        .LD(.A, onryoByte)
            '        .OUT(psgPort)
            '    Next
            '    octave += 1
            'Next


            .Label("psg1-play")
            '.LD(.HL, .LabelRef("psg1-status:"))

            '.LD(.E, .HLref)
            '.INC(.HL)
            '.LD(.D, .HLref)

            '.LD(.E, .HLref)
            '.INC(.HL)
            '.LD(.D, .HLref)

            '.LD(.HL, .DE)


            'For Each psgPort As Byte In {IOPort.PSG1} 'PSG1 と PSG2
            '    Dim psgDic As New Dictionary(Of String, String)
            '    psgDic.Add("c", "c") 'ド
            '    psgDic.Add("d", "d") 'ド
            '    psgDic.Add("e", "e") 'ド
            '    psgDic.Add("f", "f") 'ド
            '    psgDic.Add("g", "g") 'ド
            '    For Each toneNoWithTone As KeyValuePair(Of String, String) In psgDic
            '        Dim syuhasuByte1 As Byte = Nothing
            '        Dim syuhasuByte2 As Byte = Nothing
            '        Dim onryoByte As Byte = Nothing
            '        Dim syuhasuRegValue As Integer = noteMap.GetFreqencyRegValue(octave:=4, noteName:=toneNoWithTone.Value)
            '        ToneDataCreator.GetBytesData(ToneDataCreator.PSGToneType.Tone0, syuhasuRegValue, 15, syuhasuByte1, syuhasuByte2, onryoByte)
            '        .LD(.A, syuhasuByte1)
            '        .OUT(IOPort.PSG1)
            '        .LD(.A, syuhasuByte2)
            '        .OUT(IOPort.PSG1)
            '        .LD(.A, onryoByte)
            '        .OUT(IOPort.PSG1)
            '    Next
            'Next

            '音長を取得
            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.A, &H0)

            '            .JP(.LabelRef("dec-remaining:"))

            '音長が0で無いならdec-remaining:へ
            .CP(.HLref)
            .JP(.NZ, .LabelRef("dec-remaining:"))
            .INC(.HL)
            .CP(.HLref)
            .JP(.NZ, .LabelRef("dec-remaining:"))

            '音長が0ならnew-sound:へ
            .JP(.LabelRef("new-sound:"))

            '音長-1する
            .Label("dec-remaining:")

            '.CALL(MZ1500Assembler.CallFunc.LETNL)
            '.LD(.DE, .LabelRef("title-skip:"))
            '.CALL(MZ1500Assembler.CallFunc.MSG)

            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.E, .HLref)
            .INC(.HL)
            .LD(.D, .HLref)
            .DEC(.DE)

            '音長を次回のために記録する
            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            .LD(.HL, &HE006US) '8253 コントロールポート
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定

            .EI() ' 割り込みを許可
            .RET()

            .Label("new-sound:")

            '試し
            .LD(.HL, .LabelRef("psg1-song-position:")) 'ステータスの先頭アドレス

            '音データの現在アドレスを取得(DE)
            .LD(.E, .HLref)
            .INC(.HL)
            .LD(.D, .HLref)

            '音データの現在アドレスから3バイト分をセット
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)

            '音長を記録
            .LD(.HL, .LabelRef("psg1-on-remaining:"))

            .LD(.A, .DEref) '音長1バイト目
            .LD(.HLref, .A)

            .INC(.DE) '位置をずらす
            .INC(.HL) '位置をずらす

            .LD(.A, .DEref) '音長2バイト目
            .LD(.HLref, .A)

            .INC(.DE)

            '音データの現在アドレスを更新(次回のため)
            .LD(.HL, .LabelRef("psg1-song-position:"))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            .LD(.HL, &HE006US) '8253 コントロールポート
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定

            .EI() ' 割り込みを許可
            .RET()

            .Label("psg1-on-remaining:")
            Dim byte1 As Byte = 0
            Dim byte2 As Byte = 0
            .DB(byte1) '発音時間残
            .DB(byte2)
            .Label("psg1-song-position:")
            .DB(.LabelRef("psg1-song:")) 'POSITION

            .Label("psg1-song:")
            For Each psgPort As Byte In {IOPort.PSG1} 'PSG1 と PSG2
                Dim psgDic As New Dictionary(Of String, String)
                psgDic.Add("c", "c") 'ド
                psgDic.Add("d", "d") 'ド
                psgDic.Add("e", "e") 'ド
                psgDic.Add("f", "f") 'ド
                psgDic.Add("g", "g") 'ド
                For Each toneNoWithTone As KeyValuePair(Of String, String) In psgDic
                    Dim syuhasuByte1 As Byte = Nothing
                    Dim syuhasuByte2 As Byte = Nothing
                    Dim lengthByte1 As Byte = 60
                    Dim lengthByte2 As Byte = 0
                    Dim onryoByte As Byte = Nothing
                    Dim syuhasuRegValue As Integer = noteMap.GetFreqencyRegValue(octave:=4, noteName:=toneNoWithTone.Value)
                    ToneDataCreator.GetBytesData(ToneDataCreator.PSGToneType.Tone0, syuhasuRegValue, 15, syuhasuByte1, syuhasuByte2, onryoByte)
                    .DB(syuhasuByte1)
                    .DB(syuhasuByte2)
                    .DB(onryoByte)
                    .DB(lengthByte1)
                    .DB(lengthByte2)
                Next
            Next
            .Label("title:")
            .DB("PLAY")
            .DB(&HD)
            .Label("title-skip:")
            .DB("SKIP")
            .DB(&HD)

        End With
        Dim byteData() As Byte = assembler.Build()

        assembler.DebugOut(byteData)

        'クイックディスクのファイルを作成
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte(byteData)
        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("HOTATEMUSIC", qdTemp)
        qd.Save("c:\temp\hotatemusic.qdf")

    End Sub

    Private Sub Button5_Click(sender As Object, e As EventArgs) Handles Button5.Click

        Dim mmlParser As New MMLParser()
        Dim musicBinary As MusicBinary = mmlParser.Parse("@t86o4<b-6>f12b-4a-4l16gfe-gf8.e-f2e-6f12>c4<b-4l8a-gfe-f2.", ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, Nothing, Nothing)

        'Return

        'Dim noteMap As New NoteMap()

        Const CHANNEL2_CYCLE_BYTE1 As Byte = &H7
        Const CHANNEL2_CYCLE_BYTE2 As Byte = &H1


        'MZ1500アセンブラでマシン語をビルド
        Dim assembler As New MZ1500Assembler
        With assembler
            .ORG(&H1200)
            .Label("main:")

            .DI() '割り込み禁止
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
            .LD(.HLref, &H1)   ' チャネル1のカウント値の下位バイトを設定
            .LD(.HLref, &H0)   ' チャネル1のカウント値の上位バイトを設定

            .LD(.A, &H5) 'INTMSK = 1 に設定し、8253からの割り込みを許可する
            .LD(&HE003US, .A) ' 8255 ビットセット

            .EI() ' 割り込みを許可

            .Label("loop:")
            .JP(.LabelRef("loop:"))

            .Label("sound:")

            '.CALL(MZ1500Assembler.CallFunc.LETNL)
            '.LD(.DE, .LabelRef("title:"))
            '.CALL(MZ1500Assembler.CallFunc.MSG)

            .Label("psg1-play")

            '音長を取得
            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.A, &H0)

            '            .JP(.LabelRef("dec-remaining:"))

            '音長が0で無いならdec-remaining:へ
            .CP(.HLref)
            .JP(.NZ, .LabelRef("dec-remaining:"))
            .INC(.HL)
            .CP(.HLref)
            .JP(.NZ, .LabelRef("dec-remaining:"))

            '音長が0ならnew-sound:へ
            .JP(.LabelRef("new-sound:"))

            '音長-1する
            .Label("dec-remaining:")

            '.CALL(MZ1500Assembler.CallFunc.LETNL)
            '.LD(.DE, .LabelRef("title-skip:"))
            '.CALL(MZ1500Assembler.CallFunc.MSG)

            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.E, .HLref)
            .INC(.HL)
            .LD(.D, .HLref)
            .DEC(.DE)

            '音長を次回のために記録する
            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            .LD(.HL, &HE006US) '8253 コントロールポート
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定

            .EI() ' 割り込みを許可
            .RET()

            .Label("new-sound:")

            '試し
            .LD(.HL, .LabelRef("psg1-song-position:")) 'ステータスの先頭アドレス

            '音データの現在アドレスを取得(DE)
            .LD(.E, .HLref)
            .INC(.HL)
            .LD(.D, .HLref)

            '音データの現在アドレスから3バイト分をセット
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)

            '音長を記録(次回のため)
            .LD(.HL, .LabelRef("psg1-on-remaining:"))

            .LD(.A, .DEref) '音長1バイト目
            .LD(.HLref, .A)

            .INC(.DE) '位置をずらす
            .INC(.HL) '位置をずらす

            .LD(.A, .DEref) '音長2バイト目
            .LD(.HLref, .A)

            .INC(.DE)

            '音データの現在アドレスをメモリに記録(次回のため)
            .LD(.HL, .LabelRef("psg1-song-position:"))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            .LD(.HL, &HE006US) '8253 コントロールポート
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定

            .EI() ' 割り込みを許可
            .RET()

            .Label("psg1-on-remaining:")
            Dim byte1 As Byte = 0
            Dim byte2 As Byte = 0
            .DB(byte1) '発音時間残
            .DB(byte2)
            .Label("psg1-song-position:")
            .DB(.LabelRef("psg1-song:")) 'POSITION

            .Label("psg1-song:")
            For Each sound As MusicBinary.SoundCommand In musicBinary.SoundCommandList
                .DB(sound.ToByteArray)
            Next
            .Label("title:")
            .DB("PLAY")
            .DB(&HD)
            .Label("title-skip:")
            .DB("SKIP")
            .DB(&HD)

        End With
        Dim byteData() As Byte = assembler.Build()

        assembler.DebugOut(byteData)

        'クイックディスクのファイルを作成
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte(byteData)
        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("HOTATEMUSIC", qdTemp)
        qd.Save("c:\temp\hotatemusic.qdf")


    End Sub

    Private Sub Button6_Click(sender As Object, e As EventArgs) Handles Button6.Click

        Dim chhanelList As New List(Of Channel)

        'YS2 OP
        Dim musicAssembler As New MZ1500MusicAssembler()
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(0, {12, 13, 14, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(1, {14, 14, 14, 14, 13, 13, 13, 13, 12, 12, 12, 12}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(2, {15, 15, 14, 14, 13, 13, 12, 12, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(3, {13, 9, 5, 0}))

        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(5, {13, 10}))


        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(11, {14, 5, 3}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(12, {14, 14, 13, 13, 12, 12, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(21, {15, 15, 15, 14, 14, 14, 13, 13, 13, 12, 12, 12, 11, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(22, {15, 15, 14, 14, 13, 13}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(0, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 1, 0, 0, 0, -1, 0, 0, 0}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(5, {0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, -128, 2, 2, 2, 2, -2, -2, -2, -2}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(6, {0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, -128, 5, 5, 5, 5, 5, 5, 5, -5, -5, -5, -5, -5, -5, -5}))

        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(11, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 1, 1, 1, -1, -1, -1}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(51, {80}))
        Dim commonMML As String = "@t64"
        'メロディ
        musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
            New MMLParser().Parse(commonMML & "r4. v15 EP5l8o3d+ega4<aaa>c+<a>d<a>e<a>d<a> o4  l16efefefef ab-ab-ab-ab- l8efg+fefg+a" &
                                            "o3 >c+<ab-gafgeb-gafgefd c+defgab->c+ ec+<b->fc+<b- <rr>>" &
                                            "EP6 <<r4^2 ab->c+4^2 EP5 ef gfefgab->c+ e4e4f4g4r",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        Dim partB As String = "r4. @v5 o5 l8 @q0 EP255 l8e1g1  >b-gec+<b-gec+   efg+fefg+a" &
                                            "o5 EP11 b-1>e1g2.f4e1< b-1>e1g1< EP255 e4c+4d4e4r"
        'SSG1 PSG1 CH0
        musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1,
            New MMLParser().Parse(commonMML & partB,
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'SSG2 PSG1 CH1
        musicAssembler.AppendChannel(New Channel("C", IOPort.PSG1,
            New MMLParser().Parse(commonMML & "r4. @v5 o5 l8 @q0 EP255 l8c+1e1 >gec+<b-gec+<b-> c+dedc+def" &
                                            "o5  EP11 e1g1b-2.a4g1 e1g1b-1 EP255 c+4<a4b4>c+4r",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone2, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'ハイハット・スネア PSG1 CH2+Noize
        musicAssembler.AppendChannel(New Channel("D", IOPort.PSG1,
            New MMLParser().Parse(commonMML & "zt1zs0l8@v11c8@v12c4  @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 " &
                                              "@v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12ccc " &
                                              "@v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11c@v12cccccccr ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'SSG2(ディチューン) PSG2 CH1
        musicAssembler.AppendChannel(New Channel("F", IOPort.PSG2,
            New MMLParser().Parse(commonMML & "D1" & partB,
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'スネア(シンセ風)
        musicAssembler.AppendChannel(New Channel("H", IOPort.PSG2,
            New MMLParser().Parse(commonMML & "v15o3l8q3EP51 dbr  ddbr ddbr ddbr ddbr ddbr ddbr ddbr ddbr   " &
            "dddbrbdd dddbrbdd dddbrbdd dddbrbbb " &
            "dddbrbdd dddbrbdd dddbrbdd dbbbbbbb ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'メロディ
        musicAssembler.AppendChannel(New Channel("E", IOPort.PSG2,
            New MMLParser().Parse(commonMML & "r4. v14 zt0zs3l8o3d+ega4<aaa>c+<a>d<a>e<a>d<a> o3   l16rrrrrrrr rrrrrrrr l8rrrrrrrr" &
                                            "D0o3 >c+<ab-gafgeb-gafgefd c+defgab->c+ ec+<b->fc+<b- <ef>>" &
                                            " <<g4^2 ab->c+4^2  ef gfefgab->c+ e4e4f4g4r",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'ベース(MZ700)
        musicAssembler.AppendChannel(New Channel("I", IOPort.PSG1,
            New MMLParser().Parse(commonMML & " v15r4.q7  o2l8zt0zs3<aaaa>c+<a>d<a>e<a>f<a>g<a>f<a>e<a>f<a>e<a>d<a>c+<ab-gab->c+<b->" &
            "l8 <aaa>>a4.<a4a<aaraa>c+d <aaa>>a4.<a4a<aaraa>c+d " &
            "l8 <aaa>>a4.<a4a<aaraa>c+d <aaa>>a4.<a4<aa>a<abb>c+c+r",
            ToneDataCreator.PSGDeviceType.MZ700, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))



        'New MMLParser().Parse(commonMML & "r4. v15 l8o03d+ega4<aaa>c+<a>d<a>e<a>d<a> o4  l16efefefef ab-ab-ab-ab- l8efg+fefg+a" &

        'musicAssembler.AppendChannel(New Channel("F", IOPort.PSG2,
        '                                    "o3 >c+<ab-gafgeb-gafgefd c+defgab->c+ ec+<b->fc+<b- <ef>>" &
        '                                    "<<g4^2 ab->c+4^2 EP0 ef gfefgab->c+ e4e4f4g4",
        'ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))


        'o6 


        'YS SEEYOU
        'Dim musicAssembler As New MZ1500MusicAssembler()
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(0, {12, 13, 14, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13}))
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(1, {14, 14, 14, 14, 13, 13, 13, 13, 12, 12, 12, 12}))
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(2, {15, 15, 14, 14, 13, 13, 12, 12, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(3, {13, 9, 5, 0}))
        'musicAssembler.AppendPitchEnvelope(New PitchEnvelope(0, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 1, 0, 0, 0, -1, 0, 0, 0}))
        'musicAssembler.AppendPitchEnvelope(New PitchEnvelope(1, {127}))
        'Dim commonMML As String = "@t80"
        'musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
        '    New MMLParser().Parse(commonMML & "o5@v0l8D1dererdcg^2a4g4>c4.<a4g4.e4dc4e4<g a2ra>cfrergredcd4.<a4.>c4<b4>cdr<g4g>r",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1,
        '    New MMLParser().Parse(commonMML & "o5@v1l8rcrcr<g4.>d4ec4d4.e4.<a4.>e4e4.<g4.>e4rfrfrfedrgrgrgfef4efrfrfg4fgrgrgr",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("D", IOPort.PSG1,
        '    New MMLParser().Parse(commonMML & "o4@v3l16zt1zs3 @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5b@v3o7bbb @v3o7bbbb@v2o5bgg@v3o7b r",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("E", IOPort.PSG2,
        '    New MMLParser().Parse(commonMML & "o5@v1l8D1rcrcr<g4.>d4ec4d4.e4.<a4.>e4e4.<g4.>e4rfrfrfedrgrgrgfef4efrfrfg4fgrgrgr",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("F", IOPort.PSG2,
        '    New MMLParser().Parse(commonMML & "o6@v0l8dererdcg^2a4g4>c4.<a4g4.e4dc4e4<g a2ra>cfrergredcd4.<a4.>c4<b4>cdr<g4g>r",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("H", IOPort.PSG2,
        '    New MMLParser().Parse(commonMML & "v15l8zt0zs3o2c>c<c>c<c>c<c>c< <b>b<b>b<b>b<b>b <a>a<a>a<a>a<a>a <g>g<g>g<g>g<fe f>f<f>f<f>f<f>f <e>e<e>e<e>e<e>e <d>d<d>d<d>d<d>d <g>g<g>g<g>g<g>g r",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("I", IOPort.PSG1,
        '    New MMLParser().Parse(commonMML & "o3l16q6EP1 crrrbrrrcrccbrrr crrrbrrrcrccbrrr crrrbrrrcrccbrrr crrrbrrrcrccbrrr crrrbrrrcrccbrrr crrrbrrrcrccbrrr crrrbrrrcrccbrrr crrrbrrrcrccbgd ",
        '    ToneDataCreator.PSGDeviceType.MZ700, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))


        'NOISE TEST
        'Dim musicAssembler As New MZ1500MusicAssembler()
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(0, {12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13, 12, 12, 12, 12, 12, 11, 11, 11, 11, 11, 10, 10, 10, 10, 10}))
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(1, {13, 13, 10, 10, 11, 12, 13, 13, 13, 13, 12, 12, 12, 12, 11, 11, 11, 11, 11, 10, 10, 10, 10, 10}))
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(2, {15, 15, 14, 14, 13, 13, 12, 12, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        'musicAssembler.AppendPitchEnvelope(New PitchEnvelope(0, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 5, 0, 0, 0, -5, 0, 0, 0}))
        'musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
        '    New MMLParser().Parse("@t144o4@v2l4zt0zs3cdefgfed zt1 cdefgfed zs0 cdefgfed zs3 o5 cdefgfed o6 cdefgfed o7 cdefg o8 cdefg  zs1 cdefgfed zs2 cdefgfed r",
        '    ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        ''ZELDA OP
        'Dim musicAssembler As New MZ1500MusicAssembler()
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(0, {12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 15, 15, 14, 14, 14, 14, 14, 13, 13, 13, 13, 13, 12, 12, 12, 12, 12, 11, 11, 11, 11, 11, 10, 10, 10, 10, 10}))
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(1, {13, 13, 10, 10, 11, 12, 13, 13, 13, 13, 12, 12, 12, 12, 11, 11, 11, 11, 11, 10, 10, 10, 10, 10}))
        'musicAssembler.AppendPitchEnvelope(New PitchEnvelope(0, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 5, 0, 0, 0, -5, 0, 0, 0}))
        'musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
        '    New MMLParser().Parse("@t144o4@v0l8EP0b-2rffb-a-16g-16a-8^4r2 EP255b-2rg-g-b-a16g16a8^4r2r",
        '    ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("C", IOPort.PSG1,
        '    New MMLParser().Parse("@t144o4@v0l8d2^8dddc16c16c8^2^8rc+2^8c+c+c+c16c16c8^2^4r",
        '    ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1,
        '    New MMLParser().Parse("@t144zt0zs3o3@v1l4<b->rr2<a->rr2<g->rr2<f>rr2r",
        '    ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("D", IOPort.PSG2,
        '    New MMLParser().Parse("@t144o3@v1l4<r>fb-2<r>e-a-2<r>d-g-2<r>cf2r",
        '    ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("E", IOPort.PSG2,
        '    New MMLParser().Parse("@t144zt0zs3v14o2l1b-a-g-fr",
        '    ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))

        'DRUAGA OP
        'Dim musicAssembler As New MZ1500MusicAssembler()
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(0, {12, 13, 14, 15, 15, 15, 14, 14, 14, 13, 13, 13, 12, 12, 12, 11, 11, 11, 10, 10, 10, 9, 9, 9, 8, 8, 8, 7, 7, 7, 6, 6, 6, 5, 5}))
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(12, {15, 15, 10, 10, 10, 10, 10, -1, 15, 13, 11}))
        'musicAssembler.AppendPitchEnvelope(New PitchEnvelope(0, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 2, 0, 2, 0, -2, 0, -2, 0}))
        'musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
        '    New MMLParser().Parse("@t96@v0@q1EP0o4<b-6>f12b-4a-4l16gfe-gf8.e-f2e-6f12>c4<b-4l8a-gfe-f2.",
        '    ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1,
        '    New MMLParser().Parse("@t96@v0@q1EP0o4<r6>d12f4e-4l16ddce-d8.cd2c6d12a-4g4l8fe-dcd2.",
        '    ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("C", IOPort.PSG1,
        '    New MMLParser().Parse("@t96@v0@q1EP0o2b-1^4q4b-12f12f12l8b-fb-f@q1b-1l4q4b-f<b->",
        '    ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("D", IOPort.PSG2,
        '    New MMLParser().Parse("@t96@v0@q1EP0o4<r6>r12d4c4l16<b-b-a-b-b-8.a-b-2a-6b-12>e-4e-4l8d-c<b-a-b-2.>",
        '    ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))

        'DRUAGA MAIN BGM
        'musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1, New MMLParser().Parse("@t56o5v15@q1l8<b-rb-b-b-b-b->cdcr<b->f2.r4.e-f gfrrb-2ab-afdc1.<brbbbbb>c+d+c+r<b>f+2.r4.g+f+ef+rrg+f+ef+red+<bg+> c+2.<brrrrr>", ToneDataCreator.PSGToneType.Tone0)))
        'musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1, New MMLParser().Parse("@t56o4v14@q1l8ddde-rrddde-rr ddde-rrfrrb-4. arrb-4.arrfrr e-rre-rrfrrfrr d+d+d+errd+d+d+err d+d+d+errf+rrb4. a+rrb4.a+rrf+rr a+rra+g+a+d+rrrrr", ToneDataCreator.PSGToneType.Tone1)))
        'musicAssembler.AppendChannel(New Channel("C", IOPort.PSG1, New MMLParser().Parse("@t56o2v14@q1l8b-rrfrrb-rrfrrb-rrfrrb-rr>g4.frrg4.frr<b- rr>crrcrr<arrfrr brrf+rrbrrf+rr brrf+rrbrr>g+4. f+rrg+4.f+rr<brr>c+rr<f+f+f+b>", ToneDataCreator.PSGToneType.Noise)))
        'musicAssembler.AppendChannel(New Channel("D", IOPort.PSG2, New MMLParser().Parse("@t56o4v15@q1l8<b-b-b->crr<b-b-b->crr <b-b-b->crrdrrg4. frrg4.frrdrr crr<grragagrf> <bbb>c+rr<bbb>c+rr <bbb>c+rrd+rrg+4. f+rrg+4.f+rrd+rr f+rr<a+g+a+brrrrr>", ToneDataCreator.PSGToneType.Tone2)))
        'musicAssembler.AppendChannel(New Channel("E", IOPort.PSG2, New MMLParser().Parse("@t56o5v12@q1l8D1<b-rb-b-b-b-b->cdcr<b->f2.r4.e-f gfrrb-2ab-afdc1.<brbbbbb>c+d+c+r<b>f+2.r4.g+f+ef+rrg+f+ef+red+<bg+> c+2.<brrrrr>", ToneDataCreator.PSGToneType.Tone1)))

        'PERIODIC NOISE TEST
        'musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1, New MMLParser().Parse("@t64o3v15l4cdefgab>cdefgab", ToneDataCreator.PSGToneType.Tone0)))
        'musicAssembler.AppendChannel(New Channel("C", IOPort.PSG1, New MMLParser().Parse("@t64o3v15l4cdefgab>cdefgab", ToneDataCreator.PSGToneType.Noise)))

        'FAMICOM TANTEI CLUB
        ''musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
        ''    New MMLParser().Parse("@t110v13o4l8>c2<c8b4ga2rab4>c2c<b4ga4bg+^2",
        ''    ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1,
        '    New MMLParser().Parse("@t110o4v13l8aeaegdgdfcfcfcgd aeaegdgdfcfce<b>e<b>v0",
        '    ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("C", IOPort.PSG1,
        '    New MMLParser().Parse("@t110o3v13l8q4aaraggrgffrfggrgaaraggrgffrfeerev0",
        '   ToneDataCreator.PSGToneType.Tone2, musicAssembler.VolueEnvelopeList)))

        'TEMPO TEST
        'musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
        '    New MMLParser().Parse("@t112v13o4l8>c2<c8b4ga2aab4>c2c<b4ga4bg+^2r",
        '    ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1,
        '    New MMLParser().Parse("@t112o4v13l8aeaegdgdfcfcfcgd aeaegdgdfcfce<b>e<b>r",
        '    ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList)))


        'クイックディスクのファイルを作成
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte(musicAssembler.Build())
        qdTemp.Save("c:\temp\hotatemusic.bin")

        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("YS2", qdTemp)
        qd.Save("c:\temp\hotatemusic.qdf")

        Return
    End Sub

    Private Sub Button7_Click(sender As Object, e As EventArgs) Handles Button7.Click


        Dim noteMap As New NoteMap()

        Const CHANNEL2_CYCLE_BYTE1 As Byte = &H7
        Const CHANNEL2_CYCLE_BYTE2 As Byte = &H1


        'MZ1500アセンブラでマシン語をビルド
        Dim assembler As New MZ1500Assembler
        With assembler
            .ORG(&H1200)
            .Label("main:")

            .DI() '割り込み禁止
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
            .LD(.HLref, &H1)   ' チャネル1のカウント値の下位バイトを設定
            .LD(.HLref, &H0)   ' チャネル1のカウント値の上位バイトを設定

            .LD(.A, &H5) 'INTMSK = 1 に設定し、8253からの割り込みを許可する
            .LD(.HL, &HE003US)   '
            .LD(.HLref, .A) ' 8255 ビットセット

            .CALL(MZ1500Assembler.CallFunc.LETNL)
            .LD(.DE, .LabelRef("title:"))
            .CALL(MZ1500Assembler.CallFunc.MSG)

            .EI() ' 割り込みを許可

            .Label("loop:")
            .JP(.LabelRef("loop:"))

            .Label("sound:")
            .CALL(MZ1500Assembler.CallFunc.LETNL)
            .LD(.DE, .LabelRef("title:"))
            .CALL(MZ1500Assembler.CallFunc.MSG)

            '.LD(.HL, .LabelRef("title:"))
            '.INC(.HLref)

            'Dim octave As Integer = 4
            'For Each psgPort As Byte In {IOPort.PSG1, IOPort.PSG2} 'PSG1 と PSG2
            '    Dim psgDic As New Dictionary(Of Byte, String)
            '    psgDic.Add(ToneDataCreator.PSGToneType.Tone0, "c") 'ド
            '    psgDic.Add(ToneDataCreator.PSGToneType.Tone1, "e") 'ミ
            '    psgDic.Add(ToneDataCreator.PSGToneType.Tone2, "g") 'ソ
            '    For Each toneNoWithTone As KeyValuePair(Of Byte, String) In psgDic
            '        Dim syuhasuByte1 As Byte = Nothing
            '        Dim syuhasuByte2 As Byte = Nothing
            '        Dim onryoByte As Byte = Nothing
            '        Dim syuhasuRegValue As Integer = noteMap.GetFreqencyRegValue(octave:=octave, noteName:=toneNoWithTone.Value)
            '        ToneDataCreator.GetBytesData(toneNoWithTone.Key, syuhasuRegValue, 15, syuhasuByte1, syuhasuByte2, onryoByte)
            '        .LD(.A, syuhasuByte1)
            '        .OUT(psgPort)
            '        .LD(.A, syuhasuByte2)
            '        .OUT(psgPort)
            '        .LD(.A, onryoByte)
            '        .OUT(psgPort)
            '    Next
            '    octave += 1
            'Next


            .Label("psg1-play")
            '.LD(.HL, .LabelRef("psg1-status:"))

            '.LD(.E, .HLref)
            '.INC(.HL)
            '.LD(.D, .HLref)

            '.LD(.E, .HLref)
            '.INC(.HL)
            '.LD(.D, .HLref)

            '.LD(.HL, .DE)


            'For Each psgPort As Byte In {IOPort.PSG1} 'PSG1 と PSG2
            '    Dim psgDic As New Dictionary(Of String, String)
            '    psgDic.Add("c", "c") 'ド
            '    psgDic.Add("d", "d") 'ド
            '    psgDic.Add("e", "e") 'ド
            '    psgDic.Add("f", "f") 'ド
            '    psgDic.Add("g", "g") 'ド
            '    For Each toneNoWithTone As KeyValuePair(Of String, String) In psgDic
            '        Dim syuhasuByte1 As Byte = Nothing
            '        Dim syuhasuByte2 As Byte = Nothing
            '        Dim onryoByte As Byte = Nothing
            '        Dim syuhasuRegValue As Integer = noteMap.GetFreqencyRegValue(octave:=4, noteName:=toneNoWithTone.Value)
            '        ToneDataCreator.GetBytesData(ToneDataCreator.PSGToneType.Tone0, syuhasuRegValue, 15, syuhasuByte1, syuhasuByte2, onryoByte)
            '        .LD(.A, syuhasuByte1)
            '        .OUT(IOPort.PSG1)
            '        .LD(.A, syuhasuByte2)
            '        .OUT(IOPort.PSG1)
            '        .LD(.A, onryoByte)
            '        .OUT(IOPort.PSG1)
            '    Next
            'Next

            '音長を取得
            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.A, &H0)

            '            .JP(.LabelRef("dec-remaining:"))

            '音長が0で無いならdec-remaining:へ
            .CP(.HLref)
            .JP(.NZ, .LabelRef("dec-remaining:"))
            .INC(.HL)
            .CP(.HLref)
            .JP(.NZ, .LabelRef("dec-remaining:"))

            '音長が0ならnew-sound:へ
            .JP(.LabelRef("new-sound:"))

            '音長-1する
            .Label("dec-remaining:")

            '.CALL(MZ1500Assembler.CallFunc.LETNL)
            '.LD(.DE, .LabelRef("title-skip:"))
            '.CALL(MZ1500Assembler.CallFunc.MSG)

            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.E, .HLref)
            .INC(.HL)
            .LD(.D, .HLref)
            .DEC(.DE)

            '音長を次回のために記録する
            .LD(.HL, .LabelRef("psg1-on-remaining:"))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            .LD(.HL, &HE006US) '8253 コントロールポート
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定

            .EI() ' 割り込みを許可
            .RET()

            .Label("new-sound:")

            '試し
            .LD(.HL, .LabelRef("psg1-song-position:")) 'ステータスの先頭アドレス

            '音データの現在アドレスを取得(DE)
            .LD(.E, .HLref)
            .INC(.HL)
            .LD(.D, .HLref)

            '音データの現在アドレスから3バイト分をセット
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)
            .LD(.A, .DEref)
            .OUT(IOPort.PSG1)
            .INC(.DE)

            '音長を記録
            .LD(.HL, .LabelRef("psg1-on-remaining:"))

            .LD(.A, .DEref) '音長1バイト目
            .LD(.HLref, .A)

            .INC(.DE) '位置をずらす
            .INC(.HL) '位置をずらす

            .LD(.A, .DEref) '音長2バイト目
            .LD(.HLref, .A)

            .INC(.DE)

            '音データの現在アドレスを更新(次回のため)
            .LD(.HL, .LabelRef("psg1-song-position:"))
            .LD(.HLref, .E)
            .INC(.HL)
            .LD(.HLref, .D)

            .LD(.HL, &HE006US) '8253 コントロールポート
            .LD(.HLref, CHANNEL2_CYCLE_BYTE1)   ' チャネル2のカウント値の下位バイトを設定
            .LD(.HLref, CHANNEL2_CYCLE_BYTE2)   ' チャネル2のカウント値の上位バイトを設定

            .EI() ' 割り込みを許可
            .RET()

            .Label("psg1-on-remaining:")
            Dim byte1 As Byte = 0
            Dim byte2 As Byte = 0
            .DB(byte1) '発音時間残
            .DB(byte2)
            .Label("psg1-song-position:")
            .DB(.LabelRef("psg1-song:")) 'POSITION

            .Label("psg1-song:")
            For Each psgPort As Byte In {IOPort.PSG1} 'PSG1 と PSG2
                Dim psgDic As New Dictionary(Of String, String)
                psgDic.Add("c", "c") 'ド
                psgDic.Add("d", "d") 'ド
                psgDic.Add("e", "e") 'ド
                psgDic.Add("f", "f") 'ド
                psgDic.Add("g", "g") 'ド
                For Each toneNoWithTone As KeyValuePair(Of String, String) In psgDic
                    Dim syuhasuByte1 As Byte = Nothing
                    Dim syuhasuByte2 As Byte = Nothing
                    Dim lengthByte1 As Byte = 60
                    Dim lengthByte2 As Byte = 0
                    Dim onryoByte As Byte = Nothing
                    Dim syuhasuRegValue As Integer = noteMap.GetFreqencyRegValue(octave:=4, noteName:=toneNoWithTone.Value)
                    ToneDataCreator.GetBytesData(ToneDataCreator.PSGToneType.Tone0, syuhasuRegValue, 15, syuhasuByte1, syuhasuByte2, onryoByte)
                    .DB(syuhasuByte1)
                    .DB(syuhasuByte2)
                    .DB(onryoByte)
                    .DB(lengthByte1)
                    .DB(lengthByte2)
                Next
            Next
            .Label("title:")
            .DB("PLAY")
            .DB(&HD)
            .Label("title-skip:")
            .DB("SKIP")
            .DB(&HD)

        End With
        Dim byteData() As Byte = assembler.Build()

        assembler.DebugOut(byteData)

        'クイックディスクのファイルを作成
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte(byteData)
        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("HOTATEMUSIC", qdTemp)
        qd.Save("c:\temp\hotatemusic.qdf")


    End Sub

    Private Sub Button8_Click(sender As Object, e As EventArgs) Handles Button8.Click


        Dim chhanelList As New List(Of Channel)

        'YS2 OP
        Dim musicAssembler As New MZ1500MusicAssembler()
        'musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(0, {
        '                                                      15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,
        '                                                      14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14, 14,
        '                                                      13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
        '                                                      12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12
        '                                                      }))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(0, {
                                                              11, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13, 13,
                                                              12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12,
                                                              11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
                                                              10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10
                                                              }))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(1, {
                                                              9, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11,
                                                              10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,
                                                              9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9,
                                                              8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8
                                                              }))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(2, {15, 15, 14, 14, 13, 13, 12, 12, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(3, {13, 9, 5, 0}))

        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(5, {13, 10}))


        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(11, {12, 3, 1}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(13, {10, 3, 0}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(12, {12, 12, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(21, {15, 15, 15, 14, 14, 14, 13, 13, 13, 12, 12, 12, 11, 11, 11, 10, 10, 9, 9, 8, 8, 7, 7, 6, 6, 5, 5, 4, 4, 3, 3, 2, 2, 1, 1}))
        musicAssembler.AppendVolueEnvelope(New VolumeEnvelope(22, {15, 15, 14, 14, 13, 13}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(0, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 1, 0, 0, 0, -1, 0, 0, 0}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(5, {0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, -128, 2, 2, 2, 2, -2, -2, -2, -2}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(6, {0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, 0, -1, 0, 1, -128, 5, 5, 5, 5, 5, 5, 5, -5, -5, -5, -5, -5, -5, -5}))

        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(11, {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -128, 1, 1, 1, -1, -1, -1}))
        musicAssembler.AppendPitchEnvelope(New PitchEnvelope(51, {80, 80, 80, 80, 80, 80, 80, 80, 0}))
        Dim commonMML As String = "@t96"
        'メロディ
        musicAssembler.AppendChannel(New Channel("A", IOPort.PSG1,
            New MMLParser().Parse(commonMML & "l8@v0o4c2.cc^1c2rcrc^1 c2.cc^1c2rcrc^1" &
            "o5c2.cc^1c2rcrc^1 c2.cc^1 q4dddrrq7drcr1q8 " &
            "o4 f4.ee2^2<g4a4> f4.ee2^1 f4.ee2^2d4e4 f1a-4.g4.f4" &
            " f4.ee2^2<g4a4> f4.ee2^1 f4.ee2^2d4e4 f1a-4.g4.f4  ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'Dim partB As String = "r4. @v5 o5 l8 @q0 EP255 l8e1g1  >b-gec+<b-gec+   efg+fefg+a" &
        '                                    "o5 EP11 b-1>e1g2.f4e1< b-1>e1g1< EP255 e4c+4d4e4r"
        ''SSG1 PSG1 CH0
        musicAssembler.AppendChannel(New Channel("B", IOPort.PSG1,
            New MMLParser().Parse(commonMML & "l8@v1o3g2.ga^1a-2ra-rg^1 g2.ga^1a-2ra-rg^1" &
            "o4g2.ga^1a-2ra-rg^1 g2.ga^1 q4a-a-a-rrq7a-rgr1q8  " &
            "o4c4.<brr>d4.crr<b>crr c4.<b-rr>d4.crr<b->crr c4.<brr>d4.crr<b4>c4 c2^8ded^2 c4d4 " &
            "o4c4.<brr>d4.crr<b>crr c4.<b-rr>d4.crr<b->crr c4.<brr>d4.crr<b4>c4 c2^8ded^2 c4d4 ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        ''SSG2 PSG1 CH1
        musicAssembler.AppendChannel(New Channel("C", IOPort.PSG1,
            New MMLParser().Parse(commonMML & "l8@v1o3e2.ef^1f2rfre^1 e2.ef^1f2rfre^1 " &
            "o4e2.ef^1f2rfre^1 e2.ef^1 q4fffrrq7frer1q8 " &
            "o3g4.grrg4.grrggrr f4.f-rrf4.frrffrr e4.erre4.erre4e4 f2^8 a4.a-2f2 " &
            "o3g4.grrg4.grrggrr f4.f-rrf4.frrffrr e4.erre4.erre4e4 f2^8 a4.a-2f2 ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone2, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        ''ハイハット・スネア PSG1 CH2+Noize
        musicAssembler.AppendChannel(New Channel("D", IOPort.PSG1,
            New MMLParser().Parse(commonMML & "zt1zs0l16 @v11c@v13ccc@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc @v11c@v13ccc@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc " &
            "@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc @v11c@v13ccc@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc " &
            "@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc @v11c@v13ccc@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc " &
            "@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc@v11c@v13ccc @v11c@v13ccc@v12c8@v13cc@v11r@v13rrr@v12c8@v13rr " &
            "" &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "l8@v12cccrrcrcrrrrrrrr " &
            "l16" &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "" &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc " &
            "@v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc @v11c@v13ccc@v12c8@v13cc@v11c@v13ccc@v12c8@v13cc ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'musicAssembler.AppendChannel(New Channel("D", IOPort.PSG1,
        '    New MMLParser().Parse(commonMML & "zt1zs0l8@v11c8@v12c4  @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 @v11cc@v12c4 " &
        '                                      "@v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12ccc " &
        '                                      "@v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11ccc@v12c@v11c@v12c@v11cc @v11c@v12cccccccr ",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'SSG2(ディチューン) PSG2 CH1
        musicAssembler.AppendChannel(New Channel("F", IOPort.PSG2,
            New MMLParser().Parse(commonMML & "D1l8@v1o4c2.cc^1c2rcrc^1 c2.cc^1c2rcrc^1" &
            "o5c2.cc^1c2rcrc^1 c2.cc^1 q4dddrrq7drcr1q8 " &
            "o4 f4.ee2^2<g4a4> f4.ee2^1 f4.ee2^2d4e4 f1a-4.g4.f4" &
            " f4.ee2^2<g4a4> f4.ee2^1 f4.ee2^2d4e4 f1a-4.g4.f4  ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'スネア(シンセ風)
        musicAssembler.AppendChannel(New Channel("H", IOPort.PSG2,
            New MMLParser().Parse(commonMML & "v15o3l16q2EP51 drrrdrrrdrrrdrrr drrrdrrrdrrrdrrr  drrrdrrrdrrrdrrr  drrrdrrrdrrrdrrr " &
            "drrrdrrrdrrrdrrr drrrdrrrdrrrdrrr  drrrdrrrdrrrdrrr  drrrgrrrbraggrba " &
            "drrrgrrrdrrrgrrr drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr " &
            "drrrgrrrdrrrgrrr drrrgrrrdrrrgrrr  grgrgrq4agdq2rgrrrgr  rrrr q4{aaa}8aa {fff}8ff {ddd}8dd" &
            "drrrgrrrdrrrgrrr drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr " &
            "drrrgrrrdrrrgrrr drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr " &
            "drrrgrrrdrrrgrrr drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr " &
            "drrrgrrrdrrrgrrr drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr  drrrgrrrdrrrgrrr ",
            ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Tone1, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        ''メロディ
        'musicAssembler.AppendChannel(New Channel("E", IOPort.PSG2,
        '    New MMLParser().Parse(commonMML & "r4. v14 zt0zs3l8o3d+ega4<aaa>c+<a>d<a>e<a>d<a> o3   l16rrrrrrrr rrrrrrrr l8rrrrrrrr" &
        '                                    "D0o3 >c+<ab-gafgeb-gafgefd c+defgab->c+ ec+<b->fc+<b- <ef>>" &
        '                                    " <<g4^2 ab->c+4^2  ef gfefgab->c+ e4e4f4g4r",
        '    ToneDataCreator.PSGDeviceType.MZ1500, ToneDataCreator.PSGToneType.Noise, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))
        'ベース(MZ700)
        musicAssembler.AppendChannel(New Channel("I", IOPort.PSG1,
            New MMLParser().Parse(commonMML & " v15q8  o2l8 c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c< c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c< " &
            "c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c< c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c<" &
            "c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c< c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c<" &
           "c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c< r1r1" &
            "" &
            "c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c< b->b-<b->b-<b->b-<b->b-<b->b-<b->b-<b->b-<b->b-< " &
            "a>a<a>a<a>a<a>a<a>a<a>a<a>a<a>a< f>f<f>f<f>f<f>f<f>f<f>f<f>f<f>f<" &
            "c>c<c>c<c>c<c>c<c>c<c>c<c>c<c>c< b->b-<b->b-<b->b-<b->b-<b->b-<b->b-<b->b-<b->b-< " &
            "a>a<a>a<a>a<a>a<a>a<a>a<a>a<a>a< f>f<f>f<f>f<f>f<f>f<f>f<f>f<f>f<",
            ToneDataCreator.PSGDeviceType.MZ700, ToneDataCreator.PSGToneType.Tone0, musicAssembler.VolueEnvelopeList, musicAssembler.PitchEnvelopeList)))


        'クイックディスクのファイルを作成
        Dim qdTemp As New QuickDiskImage()
        qdTemp.AppendByte(musicAssembler.Build())
        qdTemp.Save("c:\temp\hotatemusic.bin")

        Dim qd As QuickDiskImage = QuickDiskImage.CreateStandardExecutable("SPACE HARRIER", qdTemp)
        qd.Save("c:\temp\hotatemusic.qdf")

        Return
    End Sub
End Class
