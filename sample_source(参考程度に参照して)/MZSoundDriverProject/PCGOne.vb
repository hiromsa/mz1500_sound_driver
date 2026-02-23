Option Explicit On
Option Strict Off

Public Class PCGOne

    Public RowList As New List(Of PCGByteRow)

    Public Sub New()
        For i As Integer = 0 To 8 - 1
            Me.RowList.Add(New PCGByteRow)
        Next
    End Sub

    Public Function GetByteRow(idx As Integer) As PCGByteRow
        Return Me.RowList(idx)
    End Function

    Public Function ToByteList(colorType As ColorType) As List(Of Byte)
        Dim result As New List(Of Byte)
        For Each row As PCGByteRow In Me.RowList
            Dim targetByte As PCGByteColor = Nothing
            Select Case colorType
                Case ColorType.Red
                    targetByte = row.Red
                Case ColorType.Green
                    targetByte = row.Green
                Case ColorType.Blue
                    targetByte = row.Blue
            End Select
            result.Add(targetByte.Value)
        Next
        Return result
    End Function

    Public Enum ColorType As Integer
        Red
        Green
        Blue
    End Enum

    Public Class PCGByteRow

        Public Red As New PCGByteColor()

        Public Blue As New PCGByteColor()

        Public Green As New PCGByteColor()

    End Class

    Public Class PCGByteColor

        Public Value As Byte = 0

        Public Sub SetValueByBitIdx(newValue As Byte, Idx As Integer)
            Me.Value = Me.Value Or (newValue << (7 - Idx))
        End Sub

    End Class

End Class
