Option Explicit On
Option Strict Off

Public Class MMLParser

    Private _Context As New ChannelContext

    Public Sub New()
        Me.InitCmdMap(_Context)
    End Sub

    Public Function Parse(mml As String, device As ToneDataCreator.PSGDeviceType, channel As ToneDataCreator.PSGToneType, volumeEnvelopeList As List(Of VolumeEnvelope), pitchEnvelopeList As List(Of PitchEnvelope)) As MusicBinary

        Dim mmlCharArr() = mml.ToCharArray()

        _Context.VolumeEnvelopeList = volumeEnvelopeList
        _Context.PitchEnvelopeList = pitchEnvelopeList
        _Context.Channel = channel
        _Context.Device = device
        _Context.MusicBinary = New MusicBinary(device, channel)
        _Context.MusicBinary.VolumeEnvelopeList = volumeEnvelopeList
        _Context.MusicBinary.PitchEnvelopeList = pitchEnvelopeList

        Dim cmdWithParamList As New List(Of CmdWithParam)
        Dim currrentCmdWithParam As CmdWithParam = Nothing
        Dim cmdNameOrParam As String = ""
        For Each mmlChar As Char In mmlCharArr
            If mmlChar = " " Then
                Continue For
            End If
            If IsNumeric(mmlChar) Then
                ValidateCmd(cmdNameOrParam)
                currrentCmdWithParam.Param.ParamStr &= mmlChar
                Continue For
            End If
            If mmlChar = "+" Then
                ValidateCmd(cmdNameOrParam)
                currrentCmdWithParam.Param.Sharp = mmlChar
                Continue For
            End If
            If mmlChar = "-" Then
                ValidateCmd(cmdNameOrParam)
                currrentCmdWithParam.Param.Flat = mmlChar
                Continue For
            End If
            If mmlChar = "." Then
                ValidateCmd(cmdNameOrParam)
                currrentCmdWithParam.Param.FutenCount += 1
                Continue For
            End If
            cmdNameOrParam &= mmlChar
            Dim cmd As Command = Nothing
            If Me.TryGetCommand(cmdNameOrParam, cmd) Then
                currrentCmdWithParam = New CmdWithParam()
                currrentCmdWithParam.Cmd = cmd
                cmdWithParamList.Add(currrentCmdWithParam)
                cmdNameOrParam = ""
            End If
        Next

        For Each cmdWithParam As CmdWithParam In cmdWithParamList
            cmdWithParam.Execute()
        Next

        Return Me._Context.MusicBinary

    End Function

    Private Sub ValidateCmd(cmdName As String)
        If cmdName <> "" Then
            Throw New ArgumentException($"{cmdName}は不明なコマンド")
        End If
    End Sub

    Private Function TryGetCommand(key As String, ByRef cmd As Command) As Boolean
        For Each command As Command In _CommandList
            If command.Name = key Then
                cmd = command
                Return True
            End If
        Next
        Return False
    End Function

    Private _CommandList As New List(Of Command)

    Private Sub InitCmdMap(context As ChannelContext)
        Dim result As New List(Of Command)
        result.Add(New CommandOnpu("c", context))
        result.Add(New CommandOnpu("d", context))
        result.Add(New CommandOnpu("e", context))
        result.Add(New CommandOnpu("f", context))
        result.Add(New CommandOnpu("g", context))
        result.Add(New CommandOnpu("a", context))
        result.Add(New CommandOnpu("b", context))
        result.Add(New CommandKyufu(context))
        result.Add(New commandAtTime(context))
        result.Add(New CommandVolume(context))
        result.Add(New CommandNoiseType(context))
        result.Add(New CommandNoiseSyuhasuType(context))
        result.Add(New CommandAtVolume(context))
        result.Add(New CommandPitchEnvelope(context))
        result.Add(New CommandOctave(context))
        result.Add(New CommandOctaveUp(context))
        result.Add(New CommandOctaveDown(context))
        result.Add(New CommandQuantize(context))
        result.Add(New CommandAtQuantize(context))
        result.Add(New CommandDetune(context))
        result.Add(New CommandLength(context))
        result.Add(New CommandTai(context))
        result.Add(New CommandRenpuStart(context))
        result.Add(New CommandRenpuEnd(context))

        result.Sort(Function(a As Command, b As Command) As Integer
                        Return b.Name.Length.CompareTo(a.Name.Length)
                    End Function
                )
        _CommandList = result
    End Sub

    Private MustInherit Class Command
        Public Context As ChannelContext = Nothing
        Public Name As String = ""
        Public Sub New(name As String, context As ChannelContext)
            Me.Name = name
            Me.Context = context
        End Sub
        Public MustOverride Sub Execute(param As CommandParam)
    End Class

    Private Class CommandKyufu
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("r", context)
        End Sub

        Public Overrides Sub Execute(param As CommandParam)
            Dim clockLength As Integer = Me.Context.GetBinaryLength(param.ParamAsInt, param.FutenCount)
            Dim noteOnLentgh As Integer = Me.Context.GetQuantizedBinaryLength(clockLength)

            Dim newSound As New MusicBinary.SoundCommandKyufu()
            newSound.Length = clockLength
            newSound.NoteOnLength = noteOnLentgh

            Me.Context.MusicBinary.AppendSoundCommand(newSound)
        End Sub
    End Class

    Private Class CommandOnpu
        Inherits Command

        Public Sub New(name As String, context As ChannelContext)
            MyBase.New(name, context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim freqNoteValue As Integer = 0
            'freqNoteValue = Me.Context.NoteMap.GetFreqencyRegValue(Me.Context.Ocave, Me.Name & param.Sharp & param.Flat)
            If Me.Context.Device = ToneDataCreator.PSGDeviceType.MZ1500 Then
                If Me.Context.UsePeriodicNoise AndAlso Me.Context.Channel = ToneDataCreator.PSGToneType.Noise Then
                    'freqNoteValue = Me.Context.NoteMap.GetFreqencyRegValue(Me.Context.Ocave + 4, Me.Name & param.Sharp & param.Flat, isNext:=True)
                    freqNoteValue = Me.Context.NoteMap.GetFreqencyRegValueForNoise(Me.Context.Ocave, Me.Name & param.Sharp & param.Flat)
                Else
                    freqNoteValue = Me.Context.NoteMap.GetFreqencyRegValue(Me.Context.Ocave, Me.Name & param.Sharp & param.Flat)
                End If
            Else
                freqNoteValue = Me.Context.NoteMap.GetFreqencyRegValueForMZ700(Me.Context.Ocave, Me.Name & param.Sharp & param.Flat)
            End If
            freqNoteValue += Me.Context.Detune

            Dim syuhasuByte1 As Byte = 0
            Dim syuhasuByte2 As Byte = 0
            Dim onryoByte As Byte = 0
            Dim syuhasuChannel As ToneDataCreator.PSGToneType = Me.Context.Channel
            If Me.Context.UsePeriodicNoise AndAlso Me.Context.Channel = ToneDataCreator.PSGToneType.Noise Then
                syuhasuChannel = ToneDataCreator.PSGToneType.Tone2
            End If

            'ToneDataCreator.GetSyuhasuByteData(syuhasuChannel, freqNoteValue, syuhasuByte1, syuhasuByte2)
            syuhasuByte1 = freqNoteValue And &HFF
            syuhasuByte2 = freqNoteValue >> 8

            'Dim noiseControByte As Byte = 0
            'If Me.Context.Channel = ToneDataCreator.PSGToneType.Noise Then
            '    ToneDataCreator.GetNoizeControlByteData(ToneDataCreator.PSGNoiseType.DoukiNoise, ToneDataCreator.PSGNoiseSyuhasuType.Tone2Syuturyoku, noiseControByte)
            'End If

            Dim clockLength As Integer = Me.Context.GetBinaryLength(param.ParamAsInt, param.FutenCount)
            Dim noteOnLentgh As Integer = Me.Context.GetQuantizedBinaryLength(clockLength)

            Dim newSound As New MusicBinary.SoundCommandTone()
            newSound.SyuhasuByte1 = syuhasuByte1
            newSound.SyuhasuByte2 = syuhasuByte2
            'newSound.NoiseControlByte = noiseControByte
            newSound.Length = clockLength
            newSound.NoteOnLength = noteOnLentgh
            newSound.IsNoise = (Me.Context.Channel = ToneDataCreator.PSGToneType.Noise)

            Me.Context.MusicBinary.AppendSoundCommand(newSound)
        End Sub
    End Class

    Private Class CommandTai
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("^", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim length As Integer = param.ParamAsInt()
            Dim futenCount As Integer = param.FutenCount
            If length = 0 Then
                length = Me.Context.Length
                futenCount = Me.Context.FutenCount
            End If
            Dim clockLength As Integer = Me.Context.ToneLengthCreator.GetClockLength(length, futenCount)
            Me.Context.MusicBinary.GetLatestSoundTone().Length += clockLength
            Me.Context.MusicBinary.GetLatestSoundTone().NoteOnLength += clockLength
        End Sub
    End Class

    Private Class CommandRenpuStart
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("{", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.MusicBinary.StartRenpuStack()
        End Sub
    End Class

    Private Class CommandRenpuEnd
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("}", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim onpuLength As Integer = param.ParamAsInt()
            Dim futenCount As Integer = param.FutenCount
            If onpuLength = 0 Then
                onpuLength = Me.Context.Length
                futenCount = Me.Context.FutenCount
            End If
            Dim length As Integer = Me.Context.ToneLengthCreator.GetClockLength(onpuLength, futenCount)
            Dim totalLength As Integer = 0
            For Each sound As MusicBinary.SoundCommandTone In Me.Context.MusicBinary.RenpuStack
                sound.Length = Fix(CDbl(length) / CDbl(Me.Context.MusicBinary.RenpuStack.Count))
                sound.NoteOnLength = Me.Context.GetQuantizedBinaryLength(sound.Length)
                totalLength += sound.Length
            Next
            Dim diffCount As Integer = Math.Abs(length - totalLength)
            If diffCount > 0 Then
                Dim addValue As Integer = (length - totalLength) / diffCount
                Me.Context.MusicBinary.RenpuStack.Reverse()
                For Each sound As MusicBinary.SoundCommandTone In Me.Context.MusicBinary.RenpuStack
                    sound.Length += addValue
                    sound.NoteOnLength = Me.Context.GetQuantizedBinaryLength(sound.Length)
                    diffCount -= 1
                    If diffCount = 0 Then
                        Exit For
                    End If
                Next
            End If
            Me.Context.MusicBinary.ClearRenpuStack()
        End Sub
    End Class

    Private Class CommandLength
        Inherits Command

        Public Sub New(context As ChannelContext)
            MyBase.New("l", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.Length = param.ParamAsInt()
            Me.Context.FutenCount = param.FutenCount
        End Sub
    End Class

    Private Class CommandNoiseType
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("zt", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim noiseType As Integer = param.ParamAsInt()
            Dim sound As New MusicBinary.SoundCommandNoiseType()
            sound.NoiseType = noiseType
            Me.Context.MusicBinary.AppendSoundCommand(sound)
        End Sub
    End Class

    Private Class CommandNoiseSyuhasuType
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("zs", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim noiseSyuhasuType As Integer = param.ParamAsInt()
            Dim sound As New MusicBinary.SoundCommandNoiseSyuhasuType()
            sound.NoiseSyuhasuType = noiseSyuhasuType
            Me.Context.MusicBinary.AppendSoundCommand(sound)
        End Sub
    End Class


    Private Class CommandVolume
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("v", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            'Me.Context.Volume = param.ParamAsInt()
            Dim volume As Integer = param.ParamAsInt()
            'Dim onryoByte As Byte = 0
            'ToneDataCreator.GetOnryoByteData(Me.Context.Channel, volume, onryoByte)
            Dim sound As New MusicBinary.SoundCommandVolume()
            'sound.VolumeByte = onryoByte
            sound.VolumeByte = 15 - volume
            Me.Context.MusicBinary.AppendSoundCommand(sound)
        End Sub
    End Class

    Private Class CommandAtVolume
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("@v", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim volume As Integer = param.ParamAsInt()
            'Dim onryoByte As Byte = 0
            'ToneDataCreator.GetOnryoByteData(Me.Context.Channel, volume, onryoByte)
            Dim sound As New MusicBinary.SoundCommandAtVolume()
            'sound.VolumeByte = onryoByte
            sound.AtVolumeIndex = Me.Context.GetVolumeEnvelopeIndex(param.ParamAsInt())
            Me.Context.MusicBinary.AppendSoundCommand(sound)
        End Sub
    End Class

    Private Class CommandPitchEnvelope
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("EP", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim envelopeNo As Integer = param.ParamAsInt()
            If envelopeNo = 255 Then
                Dim sound As New MusicBinary.SoundCommandPitchEnvelopeOff()
                Me.Context.MusicBinary.AppendSoundCommand(sound)
            Else
                Dim sound As New MusicBinary.SoundCommandPitchEnvelope()
                sound.PitchEnvelopeIndex = Me.Context.GetPitchEnvelopeIndex(param.ParamAsInt())
                Me.Context.MusicBinary.AppendSoundCommand(sound)
            End If
        End Sub
    End Class

    Private Class CommandPitchEnvelopeOff
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("EPOF", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Dim volume As Integer = param.ParamAsInt()
            Dim sound As New MusicBinary.SoundCommandPitchEnvelope()
            sound.PitchEnvelopeIndex = Me.Context.GetPitchEnvelopeIndex(param.ParamAsInt())
            Me.Context.MusicBinary.AppendSoundCommand(sound)
        End Sub
    End Class


    Private Class CommandQuantize
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("q", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.Quantize = param.ParamAsInt()
            Me.Context.QuantizeMode = ChannelContext.QuantizeModes.Quantize
        End Sub
    End Class

    Private Class CommandAtQuantize
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("@q", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.AtQuantize = param.ParamAsInt()
            Me.Context.QuantizeMode = ChannelContext.QuantizeModes.AtQuantize
        End Sub
    End Class


    Private Class CommandDetune
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("D", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.Detune = param.ParamAsInt()
        End Sub
    End Class

    Private Class CommandOctave
        Inherits Command
        Public Sub New(context As ChannelContext)
            MyBase.New("o", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.Ocave = param.ParamAsInt()
        End Sub
    End Class


    Private Class CommandOctaveUp
        Inherits Command

        Public Sub New(context As ChannelContext)
            MyBase.New(">", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.Ocave += 1
        End Sub
    End Class

    Private Class CommandOctaveDown
        Inherits Command

        Public Sub New(context As ChannelContext)
            MyBase.New("<", context)
        End Sub
        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.Ocave -= 1
        End Sub
    End Class

    Private Class commandAtTime
        Inherits Command

        Public Sub New(context As ChannelContext)
            MyBase.New("@t", context)
        End Sub

        Public Overrides Sub Execute(param As CommandParam)
            Me.Context.ToneLengthCreator.ZenOnpuClockLength = param.ParamAsInt()
        End Sub

    End Class

    Private Class CmdWithParam
        Public Cmd As Command = Nothing
        Public Param As New CommandParam
        Public Sub Execute()
            Me.Cmd.Execute(Me.Param)
        End Sub
    End Class

    Private Class CommandParam
        Public ParamStr As String = ""
        Public Function ParamStrExists() As Boolean
            Return (Me.ParamStr <> "")
        End Function
        Public Function ParamAsInt() As Integer
            Dim result As Integer = 0
            Integer.TryParse(Me.ParamStr, result)
            Return result
        End Function
        Public Sharp As String = ""
        Public Flat As String = ""
        Public FutenCount As Integer = 0
    End Class

    Private Class ChannelContext
        Public Enum QuantizeModes
            Quantize
            AtQuantize
        End Enum
        Public PitchEnvelopeList As List(Of PitchEnvelope) = Nothing
        Public Function GetPitchEnvelopeIndex(pitchEnvelopeNo As Integer) As Integer
            For i As Integer = 0 To Me.PitchEnvelopeList.Count - 1
                Dim pitchEnvelope As PitchEnvelope = Me.PitchEnvelopeList(i)
                If pitchEnvelope.No = pitchEnvelopeNo Then
                    Return i
                End If
            Next
            Return 0
        End Function
        Public VolumeEnvelopeList As List(Of VolumeEnvelope) = Nothing
        Public Function GetVolumeEnvelopeIndex(volumeEnvelopeIndex As Integer) As Integer
            For i As Integer = 0 To Me.VolumeEnvelopeList.Count - 1
                Dim volumeEnvelope As VolumeEnvelope = Me.VolumeEnvelopeList(i)
                If volumeEnvelope.No = volumeEnvelopeIndex Then
                    Return i
                End If
            Next
            Return 0
        End Function
        Public ToneLengthCreator As New ToneLengthCreator()
        Public ToneDataCreator As New ToneDataCreator()
        Public NoteMap As New NoteMap()
        Public Ocave As Integer = 4
        'Public Volume As Integer = 15
        Public Length As Integer = 4
        Public FutenCount As Integer = 0
        Public MusicBinary As MusicBinary = Nothing
        Public Channel As ToneDataCreator.PSGToneType = ToneDataCreator.PSGToneType.Tone0
        Public UsePeriodicNoise As Boolean = True
        Public Quantize As Integer = 8
        Public AtQuantize As Integer = 0
        Public QuantizeMode As QuantizeModes = QuantizeModes.Quantize
        Public Detune As Integer = 0
        Public Device As ToneDataCreator.PSGDeviceType = ToneDataCreator.PSGDeviceType.MZ1500
        Public Function GetBinaryLength(baseLength As Integer, baseFutenCount As Integer)
            Dim length As Integer = baseLength
            Dim futenCount As Integer = baseFutenCount
            If length = 0 Then
                length = Me.Length
                futenCount = Me.FutenCount
            End If
            Dim clockLength As Integer = Me.ToneLengthCreator.GetClockLength(length, futenCount)
            Return clockLength
        End Function

        Public Function GetQuantizedBinaryLength(baseLength As Integer) As Integer
            Select Case Me.QuantizeMode
                Case ChannelContext.QuantizeModes.Quantize
                    If Me.Quantize < 8 Then
                        Return baseLength * Me.Quantize / 8
                    End If
                Case ChannelContext.QuantizeModes.AtQuantize
                    Dim noteOnLentgh = baseLength - Me.AtQuantize
                    If noteOnLentgh < 0 Then
                        noteOnLentgh = 0
                    End If
                    Return noteOnLentgh
            End Select
            Return baseLength
        End Function
    End Class

End Class


