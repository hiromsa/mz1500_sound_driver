Option Explicit On
Option Strict Off

Public Class ToneLengthCreator

    Public Property ZenOnpuClockLength As Integer = 120

    Public Sub New()
    End Sub

    Public Function GetClockLength(lengthNo As Integer, futenCount As Integer) As Integer

        Dim result As Integer = 0

        result += Me.ZenOnpuClockLength / lengthNo

        If futenCount > 0 Then
            result += Me.GetClockLength(lengthNo * 2, futenCount - 1)
        End If

        Return result

    End Function

End Class
