

Public MustInherit Class Part
    Implements IEquatable(Of Part)

    Public MustOverride Function Equals1(obj As Part) As Boolean

    Public Function Equals2(other As Part) As Boolean Implements IEquatable(Of Part).Equals
        Return Me.Equals1(other)
    End Function

    Public MustOverride Function GetInfo() As String
End Class

Public Class Z80OpecodePart
    Inherits Part

    Public Property Name As String
    Public Sub New(name As String)
        Me.Name = name
    End Sub

    Public Overrides Function Equals1(obj As Part) As Boolean
        If Not TypeOf obj Is Z80OpecodePart Then
            Return False
        End If
        Dim other As Z80OpecodePart = obj
        Return (Me.Name = other.Name)
    End Function

    Public Overrides Function GetInfo() As String
        Return $"{Me.Name}"
    End Function
End Class

Public MustInherit Class Z80OperandPart
    Inherits Part

End Class


Public Class ValueLabelRef
    Inherits Z80OperandPart

    Public Property Name As String
    Public Sub New(name As String)
        Me.Name = name
    End Sub
    Public Overrides Function Equals1(obj As Part) As Boolean
        If Not TypeOf obj Is ValueLabelRef Then
            Return False
        End If
        Return True
        'Dim other As ValueLabelRef = obj
        'Return (Me.Name = other.Name)
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return 0
    End Function

    Public Overrides Function GetInfo() As String
        Return $"(<00-00>)"
    End Function
End Class

Public Class ValueRef
    Inherits Z80OperandPart
    Public Property ByteList As New List(Of Byte)
    Public Sub New([byte] As Byte)
        Me.ByteList.Add([byte])
    End Sub

    Public Overrides Function Equals1(obj As Part) As Boolean
        If Not TypeOf obj Is ValueRef Then
            Return False
        End If
        Return True
    End Function

    Public Overrides Function GetInfo() As String
        Dim data As String = BitConverter.ToString(Me.ByteList.ToArray, 0, Me.ByteList.Count)
        Return $"(<{data}>)"
    End Function
End Class

Public Class Value
    Inherits Z80OperandPart

    Public Property ByteList As New List(Of Byte)
    Public Sub New([byte] As Byte)
        Me.ByteList.Add([byte])
    End Sub
    Public Sub New(ushortdata As UShort)
        Dim byteArr() As Byte = BitConverter.GetBytes(ushortdata)
        For Each b As Byte In byteArr
            Me.ByteList.Add(b)
        Next
    End Sub

    Public Function GetBytes() As Byte()
        Return Me.ByteList.ToArray
    End Function

    Public Overrides Function Equals1(obj As Part) As Boolean
        If Not TypeOf obj Is Value Then
            Return False
        End If
        Return True
        'Dim other As Value = obj
        'If Me.ByteList.Count <> other.ByteList.Count Then
        '    Return False
        'End If
        'For i As Integer = 0 To Me.ByteList.Count - 1
        '    If Me.ByteList(i) <> other.ByteList(i) Then
        '        Return False
        '    End If
        'Next
        'Return True
    End Function

    Public Overrides Function GetHashCode() As Integer
        Return 0
    End Function

    Public Overrides Function GetInfo() As String
        Dim data As String = BitConverter.ToString(Me.ByteList.ToArray, 0, Me.ByteList.Count)
        Return $"<{data}>"
    End Function
End Class

Public Class Register
    Inherits Z80OperandPart

    Public Property Name As String
    Public Sub New(name As String)
        Me.Name = name
    End Sub

    Public Overrides Function Equals1(obj As Part) As Boolean
        If Not TypeOf obj Is Register Then
            Return False
        End If
        Dim other As Register = obj
        Return (Me.Name = other.Name)
    End Function

    Public Overrides Function GetInfo() As String
        Return $"{Me.Name}"
    End Function
End Class



Public MustInherit Class Data
    Public Property Address As UShort
    Public Property MnemonicDescription As String = ""

    Public MustOverride Function HasBytes() As Boolean

    Public MustOverride Function GatBytes() As Byte()

End Class

Public Class DataLabel
    Inherits Data

    Public Name As String = ""
    Public Sub New(name As String)
        Me.Name = name
    End Sub

    Public Overrides Function HasBytes() As Boolean
        Return False
    End Function

    Public Overrides Function GatBytes() As Byte()
        Throw New NotImplementedException()
    End Function
End Class

Public Class DataLabelRef
    Inherits Data

    Public Name As String = ""
    Public Sub New(name As String)
        Me.Name = name
    End Sub

    Public Overrides Function HasBytes() As Boolean
        Return True
    End Function

    Public Overrides Function GatBytes() As Byte()
        Dim byteArr() As Byte = BitConverter.GetBytes(Me.Address)
        Return byteArr
    End Function
End Class

Public Class DataByte
    Inherits Data

    Public Data As Byte = &H0
    Public Sub New(data As Byte)
        Me.Data = data
    End Sub

    Public Overrides Function HasBytes() As Boolean
        Return True
    End Function

    Public Overrides Function GatBytes() As Byte()
        Return {Me.Data}
    End Function
End Class

Public Class MZ1500Assembler

    Private _DataList As New List(Of Data)

    Private _MnemonicMap As MnemonicByteDataMap = Nothing

    Private _StartAddress As UShort = 0

    Private _Encoding As System.Text.Encoding = System.Text.Encoding.GetEncoding(932)

    Public Sub New()
        _MnemonicMap = New MnemonicByteDataMap(Me)
    End Sub

    Public Enum CallFunc As UShort
        LETNL = &H6
        MSG = &H15
    End Enum

    Public Property A As New Register("A")

    Public Property B As New Register("B")
    Public Property C As New Register("C")

    Public Property D As New Register("D")
    Public Property E As New Register("E")
    Public Property H As New Register("H")

    Public Property L As New Register("L")
    Public Property Z As New Register("Z")


    Public Property BC As New Register("BC")

    Public Property DE As New Register("DE")

    Public Property DEref As New Register("(DE)")

    Public Property IX As New Register("IX")

    Public Property IY As New Register("IY")

    Public Property HL As New Register("HL")
    Public Property NZ As New Register("NZ")

    Public Property HLref As New Register("(HL)")

    Public Property OpCodeADD As New Z80OpecodePart("ADD")
    Public Property OpCodeAND As New Z80OpecodePart("AND")

    Public Property OpCodeDEC As New Z80OpecodePart("DEC")

    Public Property OpCodeRET As New Z80OpecodePart("RET")

    Public Property OpCodeJP As New Z80OpecodePart("JP")
    Public Property OpCodeCP As New Z80OpecodePart("CP")

    Public Property OpCodeINC As New Z80OpecodePart("INC")

    Public Property OpCodeLDIR As New Z80OpecodePart("LDIR")
    Public Property OpCodeLD As New Z80OpecodePart("LD")

    Public Property OpCodeDI As New Z80OpecodePart("DI")

    Public Property OpCodeEI As New Z80OpecodePart("EI")

    Public Property OpCodeIM_1 As New Z80OpecodePart("IM 1")

    Public Property OpCodeOR As New Z80OpecodePart("OR")

    Public Property OpCodeOUT As New Z80OpecodePart("OUT")
    Public Property OpCodePUSH As New Z80OpecodePart("PUSH")
    Public Property OpCodePOP As New Z80OpecodePart("POP")

    Public Property OpCodeSRL As New Z80OpecodePart("SRL")
    Public Property OpCodeSLA As New Z80OpecodePart("SLA")

    Public Property OpCodeSBC As New Z80OpecodePart("SBC")
    Public Property OpCodeXOR As New Z80OpecodePart("XOR")
    Public Property OpCodeCALL As New Z80OpecodePart("CALL")

    Public Function Build() As Byte()

        Dim addr As UShort = Me._StartAddress

        Dim labelAddressMap As New Dictionary(Of String, UShort)

        Dim dataList As New List(Of Data)
        For Each dat As Data In Me._DataList
            If TypeOf dat Is DataLabel Then
                Dim datLabel As DataLabel = dat
                labelAddressMap.Add(datLabel.Name, addr)
                dat.Address = addr
                Continue For
            ElseIf TypeOf dat Is DataLabelRef Then
                dataList.Add(dat)
                addr += 2
                Continue For
            End If
            dat.Address = addr
            dataList.Add(dat)
            addr += 1
        Next

        Dim byteList As New List(Of Byte)
        For Each dat As Data In dataList
            If TypeOf dat Is DataLabelRef Then
                Dim datRef As DataLabelRef = dat
                Dim refAddr As UShort = labelAddressMap.Item(datRef.Name)
                dat.Address = refAddr
                For Each b As Byte In dat.GatBytes()
                    byteList.Add(b)
                Next
                Continue For
            End If
            Dim datByte As DataByte = dat
            byteList.Add(datByte.Data)
        Next

        Dim debugStr As New System.Text.StringBuilder()
        For Each dat As Data In dataList
            If dat.MnemonicDescription <> "" Then
                debugStr.AppendLine()
                debugStr.Append(Hex(dat.Address) & " :" & dat.MnemonicDescription.PadRight(20, " "c) & " : ")
            End If
            If dat.HasBytes() Then
                For Each b As Byte In dat.GatBytes()
                    debugStr.Append(" ")
                    debugStr.Append(Hex(b).PadLeft(2, "0"c))
                Next
            End If
        Next
        Debug.Print("-assembly-")
        Debug.Print(debugStr.ToString)
        Debug.Print("--")

        Return byteList.ToArray

    End Function

    Public Sub [DebugOut](byteData() As Byte)
        Dim debugStr As New System.Text.StringBuilder()
        For Each b As Byte In byteData
            debugStr.Append(Hex(b))
            debugStr.Append(" ")
        Next
        Debug.WriteLine(debugStr.ToString)
    End Sub

    Public Sub Label(name As String)
        Me._DataList.Add(New DataLabel(name))
    End Sub

    Public Function LabelRef(name As String) As Part
        Return New ValueLabelRef(name)
    End Function

    Public Sub ORG(address As UShort)
        Me._StartAddress = address
    End Sub
    Public Sub ADD(arg1 As Part, arg2 As Part)
        Me.AppendData(Me.OpCodeADD, arg1, arg2)
    End Sub

    Public Sub [AND](arg1 As Part)
        Me.AppendData(Me.OpCodeAND, arg1)
    End Sub

    Public Sub JP(arg1 As Part, arg2 As Part)
        Me.AppendData(Me.OpCodeJP, arg1, arg2)
    End Sub

    Public Sub JP(arg1 As Part)
        Me.AppendData(Me.OpCodeJP, arg1)
    End Sub

    Public Sub DEC(arg1 As Part)
        Me.AppendData(Me.OpCodeDEC, arg1)
    End Sub

    Public Sub DI()
        Me.AppendData(Me.OpCodeDI)
    End Sub

    Public Sub EI()
        Me.AppendData(Me.OpCodeEI)
    End Sub

    Public Sub IM1()
        Me.AppendData(Me.OpCodeIM_1)
    End Sub

    Public Sub INC(arg1 As Part)
        Me.AppendData(Me.OpCodeINC, arg1)
    End Sub

    Public Sub LDIR()
        Me.AppendData(Me.OpCodeLDIR)
    End Sub


    Public Sub LD(arg1 As Part, arg2 As Part)
        Me.AppendData(Me.OpCodeLD, arg1, arg2)
    End Sub

    Public Sub LD(arg1 As UShort, arg2 As Part)
        Me.AppendData(Me.OpCodeLD, Me.Value(arg1), arg2)
    End Sub

    Public Sub LD(arg1 As Part, arg2 As UShort)
        Me.AppendData(Me.OpCodeLD, arg1, Me.Value(arg2))
    End Sub

    Public Sub LD(arg1 As Part, arg2 As Byte)
        Me.AppendData(Me.OpCodeLD, arg1, Me.Value(arg2))
    End Sub

    Public Sub [OR](arg1 As Part)
        Me.AppendData(Me.OpCodeOR, arg1)
    End Sub

    Public Sub CP(arg1 As Part)
        Me.AppendData(Me.OpCodeCP, arg1)
    End Sub

    Public Sub OUT(ioPort As IOPort)
        Me.OUT(Me.Value(ioPort))
    End Sub

    Public Sub OUT(arg1 As Byte)
        Me.OUT(Me.Value(arg1))
    End Sub

    Public Sub OUT(arg1 As Part)
        Me.AppendData(Me.OpCodeOUT, arg1)
    End Sub

    Public Sub POP(arg1 As Part)
        Me.AppendData(Me.OpCodePOP, arg1)
    End Sub

    Public Sub PUSH(arg1 As Part)
        Me.AppendData(Me.OpCodePUSH, arg1)
    End Sub


    Public Sub RET()
        Me.AppendData(Me.OpCodeRET)
    End Sub

    Public Sub SBC(arg1 As Part, arg2 As Part)
        Me.AppendData(Me.OpCodeSBC, arg1, arg2)
    End Sub

    Public Sub SLA(arg1 As Part)
        Me.AppendData(Me.OpCodeSLA, arg1)
    End Sub

    Public Sub SRL(arg1 As Part)
        Me.AppendData(Me.OpCodeSRL, arg1)
    End Sub

    Public Sub [XOR](arg1 As Part)
        Me.AppendData(Me.OpCodeXOR, arg1)
    End Sub

    Public Sub [CALL](func As CallFunc)
        Me.CALL(Me.Value(func))
    End Sub

    Public Sub [CALL](arg1 As Part)
        Me.AppendData(Me.OpCodeCALL, arg1)
    End Sub

    Public Function Value([value1] As UShort) As Value
        Return New Value(value1)
    End Function

    Public Function Ref([value1] As UShort) As ValueRef
        Return New ValueRef(value1)
    End Function

    Private Sub AppendData(arg1 As Part, Optional arg2 As Part = Nothing, Optional arg3 As Part = Nothing, Optional arg4 As Part = Nothing)
        'ニーモニックのパターンについて探索を行う
        If TypeOf arg1 Is Z80OpecodePart Then
            Dim mnemonicKey As New MnemonicKey()
            Dim debugStrList As New List(Of String)
            For Each p As Part In {arg1, arg2, arg3, arg4}
                If p Is Nothing Then
                    Continue For
                End If
                mnemonicKey.AddPart(p)
                debugStrList.Add(" " & p.GetInfo())
            Next
            While True
                Dim bytedata As MnemonicByteDataMap.MnemonicByteData = Nothing
                If _MnemonicMap.TryGet(mnemonicKey, bytedata) Then
                    Me.AppendData(bytedata.Description, bytedata.ByteData)
                    Exit While
                End If
                mnemonicKey.RemovePartTail()
                If mnemonicKey.PartEmpty Then
                    Throw New ArgumentException($"マシン語が未登録のニーモニック {String.Join(",", debugStrList.ToArray())}")
                End If
            End While
        End If
        'リテラルやラベル参照などを追加
        For Each p As Part In {arg1, arg2, arg3, arg4}
            If p Is Nothing Then
                Continue For
            End If
            If TypeOf p Is Value Then
                Dim v As Value = p
                Me.AppendData(v.GetBytes())
            ElseIf TypeOf p Is ValueLabelRef Then
                Dim v As ValueLabelRef = p
                Me._DataList.Add(New DataLabelRef(v.Name))
            End If
        Next
    End Sub

    Private Sub AppendData([byte] As Byte)
        Me._DataList.Add(New DataByte([byte]))
    End Sub

    Private Sub AppendData(mnemonicDesc As String, [byteArr]() As Byte)
        For Each b As Byte In byteArr
            Dim bData As New DataByte(b)
            If mnemonicDesc <> "" Then
                bData.MnemonicDescription = mnemonicDesc
                mnemonicDesc = ""
            End If
            Me._DataList.Add(bData)
        Next
    End Sub

    Private Sub AppendData([byteArr]() As Byte)
        For Each b As Byte In byteArr
            Me.AppendData(b)
        Next
    End Sub

    Public Sub DB(value As String)
        Dim byteArr() As Byte = Me._Encoding.GetBytes(value)
        Me.AppendData(byteArr)
    End Sub

    Public Sub DB(value As Integer)
        Dim byteArr() As Byte = BitConverter.GetBytes(value)
        Me.AppendData(byteArr)
    End Sub

    Public Sub DB(value As Byte)
        Me.AppendData(value)
    End Sub

    Public Sub DB(valueArr As Byte())
        Me.AppendData(valueArr)
    End Sub

    Public Sub DB(valueArr As List(Of Byte))
        Me.AppendData(valueArr.ToArray)
    End Sub


    Public Sub DB(address As Part)
        Me.AppendData(address)
    End Sub


End Class