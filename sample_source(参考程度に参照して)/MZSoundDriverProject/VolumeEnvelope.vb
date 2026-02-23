Option Explicit On
Option Strict Off

Public Class VolumeEnvelope

    Public No As Integer = 0

    Public VolumeList As New List(Of Volume)

    Public Sub Append(volumes As Integer())
        Me.ClearPositionFlags()
        Dim isRepeatPosition As Boolean = False
        Dim doSetRestartPositionForEnd As Boolean = True
        For Each volume As Integer In volumes
            If volume = -1 Then
                isRepeatPosition = True
                Continue For
            End If
            Dim vol As New Volume(volume)
            If isRepeatPosition Then
                vol.IsRestartPosition = True
                isRepeatPosition = False
                doSetRestartPositionForEnd = False
            End If
            Me.VolumeList.Add(vol)
        Next
        Me.MarkEndPositionFlags(doSetRestartPositionForEnd)
    End Sub


    Public Sub ClearPositionFlags()
        For Each volume As Volume In Me.VolumeList
            volume.ClearPositionFlags()
        Next
    End Sub
    Public Sub MarkEndPositionFlags(doSetRestartPositionForEnd As Boolean)
        Dim lastVolume As Volume = Me.VolumeList(Me.VolumeList.Count - 1)
        lastVolume.IsEndPosition = True
        If doSetRestartPositionForEnd Then
            lastVolume.IsRestartPosition = True
        End If
    End Sub

    Public Sub New(no As Integer)
        Me.No = no
    End Sub

    Public Sub New(no As Integer, volumes As Integer())
        Me.No = no
        Me.Append(volumes)
    End Sub


    Public Class Volume
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
            Return 15 - Me.Value
        End Function
    End Class

End Class
