Option Explicit On
Option Strict Off

Public Class PitchEnvelope

    Public No As Integer = 0

    Public PitchEnvelopeList As New List(Of Pitch)

    Public Sub Append(pitches As Integer())
        Me.ClearPositionFlags()
        Dim isRepeatPosition As Boolean = False
        Dim doSetRestartPositionForEnd As Boolean = True
        For Each pitch As Integer In pitches
            If pitch = -128 Then
                isRepeatPosition = True
                Continue For
            End If
            Dim vol As New Pitch(pitch)
            If isRepeatPosition Then
                vol.IsRestartPosition = True
                isRepeatPosition = False
                doSetRestartPositionForEnd = False
            End If
            Me.PitchEnvelopeList.Add(vol)
        Next
        Me.MarkEndPositionFlags(doSetRestartPositionForEnd)
    End Sub


    Public Sub ClearPositionFlags()
        For Each tone As Pitch In Me.PitchEnvelopeList
            tone.ClearPositionFlags()
        Next
    End Sub
    Public Sub MarkEndPositionFlags(doSetRestartPositionForEnd As Boolean)
        Dim lastTone As Pitch = Me.PitchEnvelopeList(Me.PitchEnvelopeList.Count - 1)
        lastTone.IsEndPosition = True
        If doSetRestartPositionForEnd Then
            lastTone.IsRestartPosition = True
        End If
    End Sub

    Public Sub New(no As Integer)
        Me.No = no
    End Sub

    Public Sub New(no As Integer, volumes As Integer())
        Me.No = no
        Me.Append(volumes)
    End Sub


    Public Class Pitch
        Public IsRestartPosition As Boolean = False
        Public IsEndPosition As Boolean = False
        Public Sub ClearPositionFlags()
            Me.IsRestartPosition = False
            Me.IsEndPosition = False
        End Sub
        Public Value As Integer = 0
        Public Sub New(value As Integer)
            Me.Value = value
        End Sub
        Public Function GetBinaryValue() As Byte
            '一度SByte(符号付)に変換してからバイトデータに変換して返す
            Dim result As Byte() = BitConverter.GetBytes(Convert.ToSByte(Me.Value))
            Return result(0)
        End Function
    End Class

End Class
