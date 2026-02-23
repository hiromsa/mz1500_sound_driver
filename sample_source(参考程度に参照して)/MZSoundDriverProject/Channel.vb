Option Explicit On
Option Strict Off

Public Class Channel

    Public MusicBinary As MusicBinary = Nothing

    Public IOPort As IOPort = IOPort.PSG1

    Public Name As String = ""

    Public Sub New(name As String, ioport As IOPort, binary As MusicBinary)
        Me.Name = name
        Me.IOPort = ioport
        Me.MusicBinary = binary
    End Sub

End Class
