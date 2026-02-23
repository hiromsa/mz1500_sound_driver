Option Explicit On
Option Strict On

Public Class QuickDiskImage

    Private _Data As New List(Of Byte)

    Private _Encoding As System.Text.Encoding = System.Text.Encoding.GetEncoding(932)

    Private Const FILE_SIZE As Integer = 81936

    Public Enum FileType As Byte
        [Object] = &H1
        [Basic] = &H2
    End Enum

    Public Enum DataType As Byte
        [Header] = &H0
        [Data] = &H1
    End Enum

    Public Sub AppendFileType(fileType As FileType)
        Me.AppendByte(fileType)
    End Sub

    Public Sub AppendByte([byte] As Byte)
        Me._Data.Add([byte])
    End Sub

    Public Sub AppendUShortByLE([data] As UShort)
        Dim byteArr() As Byte = BitConverter.GetBytes([data])
        Me.AppendByte(byteArr(0))
        Me.AppendByte(byteArr(1))
    End Sub

    Public Sub AppendByte([byte] As Byte, times As Integer)
        For i As Integer = 0 To times - 1
            Me.AppendByte([byte])
        Next
    End Sub

    Public Sub AppendFillByteToLength([byte] As Byte, [length] As Integer)
        Me.AppendByte([byte], [length] - Me.Length)
    End Sub

    Public Sub AppendFillZeroToDiskEnd()
        Me.AppendFillByteToLength(&H0, FILE_SIZE)
    End Sub

    Public ReadOnly Property Length As Integer
        Get
            Return Me._Data.Count
        End Get
    End Property

    Public Sub AppendStartData()
        Me.AppendByte(&HA5)
    End Sub

    Public Sub AppendDataType(dataType As DataType)
        Me.AppendByte(dataType)
    End Sub

    Public Function GetLengthByteArr() As Byte()
        Return BitConverter.GetBytes(Me._Data.Count)
    End Function

    Public Sub AppendBreak()
        Me.AppendByte(&H0)
    End Sub

    Public Sub AppendByte([byteArr]() As Byte)
        For Each [byte] As Byte In byteArr
            Me._Data.Add([byte])
        Next
    End Sub
    Public Sub AppendByte(byteList As List(Of Byte))
        For Each [byte] As Byte In byteList
            Me._Data.Add([byte])
        Next
    End Sub

    Public Sub AppendString(data As String)
        Me.AppendByte(_Encoding.GetBytes(data))
    End Sub

    Public Sub AppendFileName(data As String)
        Const FILE_NAME_MAX_LENGTH As Integer = &H10
        If data.Length > FILE_NAME_MAX_LENGTH Then
            data = data.Substring(0, FILE_NAME_MAX_LENGTH)
        End If
        Me.AppendString(data)
        If data.Length < FILE_NAME_MAX_LENGTH Then
            For i As Integer = 0 To (FILE_NAME_MAX_LENGTH - data.Length) - 1
                Me.AppendByte(&HD)
            Next
        End If
        Me.AppendByte(&HD)
    End Sub

    Public Sub AppendQDStart()
        Me.AppendString("-QD format-")
        Me.AppendByte(&HFF, 5)
        Me.AppendByte(0, &H12DA)
    End Sub

    Public Sub AppendStandardSYNC()
        Me.AppendByte(&H16, 10)
    End Sub

    Public Sub AppendDataStart()
        Me.AppendByte(&HA5)
    End Sub

    Public Sub AppendOtherQDImage(image As QuickDiskImage)
        Me.AppendByte(image._Data)
    End Sub

    Public Function GetHexString() As String
        Return BitConverter.ToString(Me._Data.ToArray)
    End Function

    Public Function GetCrcByteArr() As Byte()
        Dim crc As UShort = Me.GetCrc()
        Return BitConverter.GetBytes(crc)
    End Function

    Private Function GetCrc() As UShort
        Dim crc As UShort = &H0
        Dim data As Byte() = Me._Data.ToArray
        Dim pos = 0
        Do While (pos < data.Length)
            crc = crc Xor data(pos)
            Dim i = 8
            Do While (i <> 0)
                i = (i - 1)
                If (crc And &H1) <> 0 Then
                    crc >>= 1
                    crc = crc Xor &HA001US
                Else
                    crc >>= 1
                End If
            Loop
            pos += +1
        Loop
        Return crc
    End Function

    Public Sub Save(filePath As String)
        Dim dataArr() As Byte = Me._Data.ToArray()
        Using fs As New System.IO.FileStream(filePath, IO.FileMode.Create)
            fs.Write(dataArr, 0, dataArr.Length)
        End Using
    End Sub
    Public Shared Function CreateStandardExecutable(innerFileName As String, byteData As QuickDiskImage) As QuickDiskImage
        Return CreateStandardExecutable(innerFileName, byteData._Data.ToArray)
    End Function

    Public Shared Function CreateStandardExecutable(innerFileName As String, byteData() As Byte) As QuickDiskImage

        Dim qd As New QuickDiskImage
        qd.AppendQDStart() '-QD format-等
        qd.AppendStandardSYNC() '&H16いくつか
        If True Then
            Dim qdTemp As New QuickDiskImage
            qdTemp.AppendStartData() '&HA5
            qdTemp.AppendByte(2) 'ブロック数
            qd.AppendOtherQDImage(qdTemp)
            qd.AppendByte(qdTemp.GetCrcByteArr()) 'CRC-16
        End If
        qd.AppendStandardSYNC() '&H16いくつか

        qd.AppendByte(&H0, &HAEB) '空き

        'ファイル1ヘッダブロック
        If True Then

            qd.AppendBreak() '&H0
            qd.AppendStandardSYNC()

            '            qd.AppendDataType(QuickDiskImage.DataType.Header)
            If True Then
                Dim qdTemp As New QuickDiskImage()
                qdTemp.AppendStartData() '&HA5
                qdTemp.AppendBreak() '&H0
                qdTemp.AppendByte(&H40)
                qdTemp.AppendBreak() '&H0
                qdTemp.AppendFileType(QuickDiskImage.FileType.Object)
                qdTemp.AppendFileName(innerFileName)
                qdTemp.AppendBreak() '&H0
                qdTemp.AppendBreak() '&H0
                qdTemp.AppendUShortByLE(&HBE00) 'Data Size
                qdTemp.AppendUShortByLE(&H1200) 'Load Address
                qdTemp.AppendUShortByLE(&H1200) 'Exec Address
                qdTemp.AppendFillByteToLength(&H0, &H44)
                qd.AppendOtherQDImage(qdTemp)
                '                qd.AppendBreak() '&H0
                qd.AppendByte(qdTemp.GetCrcByteArr())
                qd.AppendStandardSYNC()
            End If

            qd.AppendByte(&H0, &HFF) '空き

        End If

        'ファイル1データブロック
        If True Then

            '            qd.AppendBreak() '&H0
            'qd.AppendByte(&H16, &HA)
            qd.AppendStandardSYNC()

            '            qd.AppendDataType(QuickDiskImage.DataType.Header)
            If True Then
                Dim qdTemp As New QuickDiskImage()
                qdTemp.AppendStartData() '&HA5
                'qd.AppendByte(&H1)
                qdTemp.AppendByte(&H5)
                qdTemp.AppendUShortByLE(&HBE00) 'Data Size
                'qdTemp.AppendByte({&HCD, &H6, &H0, &H11, &H13, &H12, &HCD, &H15, &H0, &HCD, &H6, &H0, &H11, &H23, &H12, &HCD})
                'qdTemp.AppendByte({&H15, &H0, &HC9, &H2D, &H20, &H48, &H45, &H4C, &H4C, &H4F, &H20, &H57, &H4F, &H52, &H4C, &H44})
                'qdTemp.AppendByte({&H20, &H2D, &HD, &H2D, &H20, &H49, &H4E, &H20, &H4D, &H5A, &H2D, &H37, &H30, &H30, &H20, &H2D})
                'qdTemp.AppendByte({&HD})
                qdTemp.AppendByte(byteData)
                qdTemp.AppendFillByteToLength(&H0, &HBE04)
                qd.AppendOtherQDImage(qdTemp)
                qd.AppendByte(qdTemp.GetCrcByteArr())
                qd.AppendStandardSYNC()
            End If

        End If



        qd.AppendFillZeroToDiskEnd() '余白(適当)
        Return qd

    End Function

End Class
