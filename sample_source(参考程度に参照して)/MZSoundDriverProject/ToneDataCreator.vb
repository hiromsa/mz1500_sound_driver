Option Explicit On
Option Strict Off

Public Class ToneDataCreator


    ''' <summary>周波数データのデータ順種別</summary>
    Public Enum PSGToneByteOrder As Byte
        ToneData1OrOnryo = &B10000000
        ToneData2 = &B0
    End Enum

    ''' <summary>
    ''' PSGデバイスのタイプ
    ''' </summary>
    Public Enum PSGDeviceType As Integer
        MZ1500
        MZ700
    End Enum

    ''' <summary>トーン/ノイズの種別</summary>
    Public Enum PSGToneType As Byte
        Tone0 = &B0
        Tone1 = &B100000
        Tone2 = &B1000000
        Noise = &B1100000
    End Enum

    ''' <summary>制御種別</summary>
    Public Enum PSGControlType As Byte
        SyuhasuOrControl = &B0
        Onryo = &B10000
    End Enum

    ''' <summary>ノイズのタイプ</summary>
    Public Enum PSGNoiseType As Byte
        DoukiNoise = &B0
        WhiteNoise = &B100
    End Enum

    ''' <summary>ノイズの周波数タイプ</summary>
    Public Enum PSGNoiseSyuhasuType
        Syuhasu6Dot99 = &B0
        Syuhasu3Dot49 = &B1
        Syuhasu1Dot75 = &B10
        Tone2Syuturyoku = &B11
    End Enum

    ''' <summary>ボリューム最大値</summary>
    Public Const VOLUME_MAX As Integer = 15


    Public Sub DebugRegValue()


        Dim tone0Syuhasu As Double = 440 'Hz Octave4 A
        Dim tone0SyuhasuRegValue As Integer = ToneSyuhasuToRegisterValue(tone0Syuhasu)





        '        DebugToneOutCommand("Octave4 A - PSG1 Tone0 ", PSGIOPort.PSG1, PSGToneType.Tone0, tone0SyuhasuRegValue, onryo:=15)

        ''tone1 440Hz(detune)

        'Dim tone1SyuhasuRegValue As Integer = tone0SyuhasuRegValue + 1 '周波数のレジスタ値+1でデチューンとする
        'DebugToneOutCommand("Octave4 A(Detune) - PSG1 Tone1 ", PSGIOPort.PSG1, PSGToneType.Tone1, tone1SyuhasuRegValue, onryo:=13)

        ''その他和音
        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone2, SyuhasuToRegisterValue(349), onryo:=13) 'Octave4 F
        'DebugToneOutCommand("Octave4 D - PSG2 Tone0", PSGIOPort.PSG2, PSGToneType.Tone0, SyuhasuToRegisterValue(294), onryo:=13) 'Octave4 D
        'DebugToneOutCommand("Octave3 B- - PSG2 Tone1", PSGIOPort.PSG2, PSGToneType.Tone1, SyuhasuToRegisterValue(233), onryo:=13) 'Octave3 B-
        'DebugToneOutCommand("Octave2 B- - PSG2 Tone2", PSGIOPort.PSG2, PSGToneType.Tone2, SyuhasuToRegisterValue(117), onryo:=15) 'Octave2 B-

        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone2, SyuhasuToRegisterValue(880), onryo:=0) 'Octave4 F
        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone2, SyuhasuToRegisterValue(440), onryo:=0) 'Octave4 F
        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone2, SyuhasuToRegisterValue(1200), onryo:=0) 'Octave4 F
        DebugToneOutCommand("Octave4 C - PSG1 Periodic Noise", IOPort.PSG1, PSGToneType.Tone2, ToneSyuhasuToRegisterValue(1975), onryo:=0) 'Octave4 F
        DebugNoiseOutCommand("PSG1 Noise ON ", IOPort.PSG1, PSGToneType.Noise, PSGNoiseType.DoukiNoise, PSGNoiseSyuhasuType.Tone2Syuturyoku, onryo:=15)
        DebugToneOutCommand("Octave4 D - PSG1 Periodic Noise", IOPort.PSG1, PSGToneType.Tone2, ToneSyuhasuToRegisterValue(2217), onryo:=0) 'Octave4 F
        DebugToneOutCommand("Octave4 E - PSG1 Periodic Noise", IOPort.PSG1, PSGToneType.Tone2, ToneSyuhasuToRegisterValue(2489), onryo:=0) 'Octave4 F

        'DebugToneOutCommand("Octave4 C - PSG1 Periodic Noise", PSGIOPort.PSG1, PSGToneType.Tone2, ToneSyuhasuToRegisterValue(1975), onryo:=0) 'Octave4 F
        'DebugNoiseOutCommand("PSG1 Noise ON ", PSGIOPort.PSG1, PSGToneType.Noise, PSGNoiseType.WhiteNoise, PSGNoiseSyuhasuType.Tone2Syuturyoku, onryo:=15)
        'DebugToneOutCommand("Octave4 D - PSG1 Periodic Noise", PSGIOPort.PSG1, PSGToneType.Tone2, ToneSyuhasuToRegisterValue(2217), onryo:=0) 'Octave4 F
        'DebugToneOutCommand("Octave4 E - PSG1 Periodic Noise", PSGIOPort.PSG1, PSGToneType.Tone2, ToneSyuhasuToRegisterValue(2489), onryo:=0) 'Octave4 F

        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone1, ToneSyuhasuToRegisterValue(440), onryo:=15) 'Octave4 F
        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone2, NoiseSyuhasuToRegisterValue(147), onryo:=0) 'Octave4 F
        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone2, NoiseSyuhasuToRegisterValue(165), onryo:=0) 'Octave4 F
        'DebugToneOutCommand("Octave4 F - PSG1 Tone2", PSGIOPort.PSG1, PSGToneType.Tone2, NoiseSyuhasuToRegisterValue(175), onryo:=0) 'Octave4 F




        'DebugNoiseOutCommand("", PSGIOPort.PSG1, PSGToneType.Noise, PSGNoiseType.WhiteNoise, PSGNoiseSyuhasuType.Syuhasu3Dot49, onryo:=15)

        'DebugNoiseOutCommand("", PSGIOPort.PSG1, PSGToneType.Noise, PSGNoiseType.WhiteNoise, PSGNoiseSyuhasuType.Syuhasu6Dot99, onryo:=15)

        'DebugNoiseOutCommand("", PSGIOPort.PSG1, PSGToneType.Noise, PSGNoiseType.DoukiNoise, PSGNoiseSyuhasuType.Syuhasu1Dot75, onryo:=15)

        'DebugNoiseOutCommand("", PSGIOPort.PSG1, PSGToneType.Noise, PSGNoiseType.DoukiNoise, PSGNoiseSyuhasuType.Syuhasu3Dot49, onryo:=15)

        'DebugNoiseOutCommand("", PSGIOPort.PSG1, PSGToneType.Noise, PSGNoiseType.DoukiNoise, PSGNoiseSyuhasuType.Syuhasu6Dot99, onryo:=15)

    End Sub

    ''' <summary>トーン出力用のBASIC OUTコマンドをデバッグ出力</summary>
    Private Sub DebugToneOutCommand(title As String, ioPort As IOPort, toneType As PSGToneType, syuhasuRegValue As Integer, onryo As Integer)
        Dim syuhasuByte1 As Byte = Nothing
        Dim syuhasuByte2 As Byte = Nothing
        Dim onryoByte As Byte = Nothing
        GetBytesData(toneType, syuhasuRegValue, onryo, syuhasuByte1, syuhasuByte2, onryoByte)

        'MZ-1500 BASICコード
        Dim debugData As New System.Text.StringBuilder()
        debugData.AppendLine($"'{title}")
        debugData.Append($"OUT@ ${Hex(ioPort)},${Hex(syuhasuByte1)}")
        debugData.Append($":OUT@ ${Hex(ioPort)},${Hex(syuhasuByte2)}")
        debugData.Append($":OUT@ ${Hex(ioPort)},${Hex(onryoByte)}")
        Debug.Print(debugData.ToString)

    End Sub

    Public Shared Sub GetBytesData(toneType As PSGToneType, syuhasuRegValue As Integer, onryo As Integer, ByRef syuhasuByte1 As Byte, ByRef syuhasuByte2 As Byte, ByRef onryoByte As Byte)
        '周波数第1バイト
        syuhasuByte1 = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.SyuhasuOrControl Or (syuhasuRegValue And &HF)
        '周波数第2バイト
        syuhasuByte2 = PSGToneByteOrder.ToneData2 Or (syuhasuRegValue >> 4)
        '音量
        onryoByte = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.Onryo Or (VOLUME_MAX - onryo)
    End Sub

    Public Shared Sub GetSyuhasuByteData(toneType As PSGToneType, syuhasuRegValue As Integer, ByRef syuhasuByte1 As Byte, ByRef syuhasuByte2 As Byte)
        '周波数第1バイト
        syuhasuByte1 = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.SyuhasuOrControl Or (syuhasuRegValue And &HF)
        '周波数第2バイト
        syuhasuByte2 = PSGToneByteOrder.ToneData2 Or (syuhasuRegValue >> 4)
    End Sub

    Public Shared Sub GetOnryoByteData(toneType As PSGToneType, onryo As Integer, ByRef onryoByte As Byte)
        '音量
        onryoByte = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.Onryo Or (VOLUME_MAX - onryo)
    End Sub

    Public Shared Sub GetNoizeControlByteData(noiseType As PSGNoiseType, syuhasuType As PSGNoiseSyuhasuType, ByRef noiseControlByte As Byte)
        Dim toneType As PSGToneType = PSGToneType.Noise
        '周波数第1バイト
        noiseControlByte = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.SyuhasuOrControl Or noiseType Or syuhasuType
    End Sub

    Private Sub DebugNoiseOutCommand(title As String, ioPort As IOPort, toneType As PSGToneType, noiseType As PSGNoiseType, syuhasuType As PSGNoiseSyuhasuType, onryo As Integer)

        '周波数第1バイト
        Dim syuhasuByte1 As Byte = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.SyuhasuOrControl Or noiseType Or syuhasuType
        '音量
        Dim onryoByte As Byte = PSGToneByteOrder.ToneData1OrOnryo Or toneType Or PSGControlType.Onryo Or (VOLUME_MAX - onryo)

        'MZ-1500 BASICコード
        Dim debugData As New System.Text.StringBuilder()
        debugData.AppendLine($"'{title}")
        debugData.Append($"OUT@ ${Hex(ioPort)},${Hex(syuhasuByte1)}")
        debugData.Append($":OUT@ ${Hex(ioPort)},${Hex(onryoByte)}")
        Debug.Print(debugData.ToString)

    End Sub


    Public Shared Function ToneSyuhasuToRegisterValue(syuhasu As Double) As Double
        Return 111860 / syuhasu
    End Function

    Private Function ToneRegisterValueToSyuhasu(registerValue As Double) As Double
        Return 111860 / registerValue
    End Function

    Private Function NoiseSyuhasuToRegisterValue(syuhasu As Double) As Double
        Return (111860 * 16) / syuhasu
    End Function

    Private Function NoiseRegisterValueToSyuhasu(registerValue As Double) As Double
        Return (111860 * 16) / registerValue
    End Function

End Class
