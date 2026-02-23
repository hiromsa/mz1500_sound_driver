Option Explicit On
Option Strict Off

Public Class PCGList

    Public PCGList As New List(Of PCGOne)

    Public Sub New(pcgBlockRowCount As Integer, pcgBlockColCount As Integer)
        For i As Integer = 0 To (pcgBlockRowCount * pcgBlockColCount) - 1
            Me.PCGList.Add(New PCGOne)
        Next
    End Sub

    Public Function GetPCGOne(pcgBlockRowCount As Integer, pcgBlockColCount As Integer) As PCGOne

        Return Me.PCGList(pcgBlockRowCount * 40 + pcgBlockColCount)

    End Function

    Public Function ToByteList(colorType As PCGOne.ColorType) As List(Of Byte)
        Dim result As New List(Of Byte)
        For Each pcgOne As PCGOne In Me.PCGList
            result.AddRange(pcgOne.ToByteList(colorType))
        Next
        Return result
    End Function

    Public Shared Function FromCSV(filePath As String) As PCGList

        Dim rowIdx As Integer = 0
        Dim result As New PCGList(40, 25)
        Using io As New System.IO.StreamReader(filePath)

            While Not io.EndOfStream
                Dim strLine As String = io.ReadLine()
                Dim cells() As String = strLine.Split(",")
                Dim colIdx As Integer = 0
                For Each cell As String In cells
                    cell = cell.Trim({"("c, ")"c})
                    Dim rgbs() As String = cell.Split("-"c)
                    Dim pcg As PCGOne = result.GetPCGOne(Fix(rowIdx / 8), Fix(colIdx / 8))
                    Dim pcgByteRow As PCGOne.PCGByteRow = pcg.GetByteRow(rowIdx Mod 8)
                    pcgByteRow.Red.SetValueByBitIdx(Byte.Parse(rgbs(0)) / 255, colIdx Mod 8)
                    pcgByteRow.Green.SetValueByBitIdx(Byte.Parse(rgbs(1)) / 255, colIdx Mod 8)
                    pcgByteRow.Blue.SetValueByBitIdx(Byte.Parse(rgbs(2)) / 255, colIdx Mod 8)

                    colIdx += 1
                Next

                rowIdx += 1

            End While

        End Using
        Return result

    End Function

End Class
