Option Explicit On
Option Strict Off

Public Class MusicBinary


    Public SoundCommandList As New List(Of SoundCommand)

    Public NoteOffVolumeByte As Byte = 0

    Public VolumeEnvelopeList As List(Of VolumeEnvelope) = Nothing

    Public PitchEnvelopeList As List(Of PitchEnvelope) = Nothing

    Public ToneType As ToneDataCreator.PSGToneType = ToneDataCreator.PSGToneType.Tone0

    Public Device As ToneDataCreator.PSGDeviceType = ToneDataCreator.PSGDeviceType.MZ1500

    Public Sub New(devieType As ToneDataCreator.PSGDeviceType, toneType As ToneDataCreator.PSGToneType)
        Me.ToneType = toneType
        Me.Device = devieType
        Me.NoteOffVolumeByte = 15 - 0
    End Sub

    Public RenpuStack As List(Of SoundCommandTone) = Nothing

    Public Sub AppendSoundCommand(sound As SoundCommand)
        Me.SoundCommandList.Add(sound)
        If RenpuStack IsNot Nothing Then
            Me.RenpuStack.Add(sound)
        End If
    End Sub

    Public Sub StartRenpuStack()
        Me.RenpuStack = New List(Of SoundCommandTone)
    End Sub
    Public Sub ClearRenpuStack()
        Me.RenpuStack = Nothing
    End Sub

    Public Function GetLatestSoundTone() As SoundCommandCountable
        Return Me.SoundCommandList(Me.SoundCommandList.Count - 1)
    End Function

    Public Enum SoundCommandType As Byte
        Tone = &H1
        Kyufu = &H2
        Volume = &H3
        AtVolume = &H4
        PitchEnvelope = &H5
        PitchEnvelopeOff = &H6
        NoiseType = &H7
        NoiseSyuhasuType = &H8
    End Enum

    Public MustInherit Class SoundCommand
        Public MustOverride ReadOnly Property CommandType As SoundCommandType
        Public Function ToByteArray() As Byte()
            Dim result As New List(Of Byte)
            result.Add(Me.CommandType)
            result.AddRange(Me.getByteList())
            Return result.ToArray()
        End Function

        Protected MustOverride Function getByteList() As List(Of Byte)

    End Class

    Public MustInherit Class SoundCommandCountable
        Inherits SoundCommand

        Public Length As Integer = 0
        Public ReadOnly Property LengthByte1 As Byte
            Get
                Return Me.Length And &HFF
            End Get
        End Property
        Public ReadOnly Property LengthByte2 As Byte
            Get
                Return (Me.Length And &HFF00) >> 8
            End Get
        End Property
        Public NoteOnLength As Integer = 0

        Public ReadOnly Property NoteOnLengthByte1 As Byte
            Get
                Return Me.NoteOnLength And &HFF
            End Get
        End Property
        Public ReadOnly Property NoteOnLengthByte2 As Byte
            Get
                Return (Me.NoteOnLength And &HFF00) >> 8
            End Get
        End Property

    End Class


    Public Class SoundCommandTone
        Inherits SoundCommandCountable

        Public IsNoise As Boolean = False
        Public SyuhasuByte1 As Byte = 0
        Public SyuhasuByte2 As Byte = 0
        'Public NoiseControlByte As Byte = 0

        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.Tone
            End Get
        End Property

        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            'result.Add(Me.CommandType)
            result.Add(Me.SyuhasuByte1)
            result.Add(Me.SyuhasuByte2)
            'If IsNoise Then
            '    result.Add(Me.NoiseControlByte)
            'End If
            result.Add(Me.LengthByte1)
            result.Add(Me.LengthByte2)
            result.Add(Me.NoteOnLengthByte1)
            result.Add(Me.NoteOnLengthByte2)
            Return result
        End Function
    End Class

    Public Class SoundCommandKyufu
        Inherits SoundCommandCountable
        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.Kyufu
            End Get
        End Property

        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            result.Add(Me.LengthByte1)
            result.Add(Me.LengthByte2)
            Return result
        End Function
    End Class

    Public Class SoundCommandNoiseType
        Inherits SoundCommand
        Public NoiseType As Byte = 0
        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.NoiseType
            End Get
        End Property

        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            result.Add(Me.NoiseType)
            Return result
        End Function
    End Class

    Public Class SoundCommandNoiseSyuhasuType
        Inherits SoundCommand
        Public NoiseSyuhasuType As Byte = 0
        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.NoiseSyuhasuType
            End Get
        End Property

        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            result.Add(Me.NoiseSyuhasuType)
            Return result
        End Function
    End Class

    Public Class SoundCommandVolume
        Inherits SoundCommand
        Public VolumeByte As Byte = 0
        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.Volume
            End Get
        End Property

        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            'result.Add(Me.CommandType)
            result.Add(Me.VolumeByte)
            Return result
        End Function
    End Class

    Public Class SoundCommandAtVolume
        Inherits SoundCommand
        Public AtVolumeIndex As Byte = 0
        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.AtVolume
            End Get
        End Property
        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            'result.Add(Me.CommandType)
            result.Add(Me.AtVolumeIndex)
            Return result
        End Function
    End Class

    Public Class SoundCommandPitchEnvelope
        Inherits SoundCommand
        Public PitchEnvelopeIndex As Byte = 0
        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.PitchEnvelope
            End Get
        End Property
        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            result.Add(Me.PitchEnvelopeIndex)
            Return result
        End Function
    End Class

    Public Class SoundCommandPitchEnvelopeOff
        Inherits SoundCommand
        Public Overrides ReadOnly Property CommandType As SoundCommandType
            Get
                Return SoundCommandType.PitchEnvelopeOff
            End Get
        End Property
        Protected Overrides Function getByteList() As List(Of Byte)
            Dim result As New List(Of Byte)
            Return result
        End Function
    End Class


End Class
