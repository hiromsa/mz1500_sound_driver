Option Explicit On
Option Strict Off

Public Class MnemonicKey
    Implements IEquatable(Of MnemonicKey)
    Public Property PartList As New List(Of Part)
    Public Sub AddPart(part As Part)
        Me.PartList.Add(part)
    End Sub
    Public Sub RemovePartTail()
        Me.PartList.RemoveAt(Me.PartList.Count - 1)
    End Sub
    Public Function PartCount() As Integer
        Return Me.PartList.Count
    End Function
    Public Function PartEmpty() As Boolean
        Return Me.PartCount = 0
    End Function
    Public Sub New()
    End Sub
    Public Sub New(part1 As Part)
        Me.PartList.Add(part1)
    End Sub
    Public Sub New(part1 As Part, part2 As Part)
        Me.PartList.Add(part1)
        Me.PartList.Add(part2)
    End Sub
    Public Sub New(part1 As Part, part2 As Part, part3 As Part)
        Me.PartList.Add(part1)
        Me.PartList.Add(part2)
        Me.PartList.Add(part3)
    End Sub

    Public Function Equals1(other As MnemonicKey) As Boolean Implements IEquatable(Of MnemonicKey).Equals
        If Me.PartList.Count <> other.PartList.Count Then
            Return False
        End If
        For i As Integer = 0 To Me.PartList.Count - 1
            If Not Me.PartList(i).Equals1(other.PartList(i)) Then
                Return False
            End If
        Next
        Return True
    End Function

    Public Overrides Function GetHashCode() As Integer
        Dim result As Integer = 0
        For Each part As Part In Me.PartList
            result = result Or part.GetHashCode()
        Next
        Return result
    End Function

End Class


Public Class MnemonicByteDataMap

    Public Class MnemonicByteData
        Public Property ByteData As Byte() = Nothing
        Public Property Description As String = ""
    End Class

    Private _Map As New Dictionary(Of MnemonicKey, MnemonicByteData)

    Public Sub New(assembler As MZ1500Assembler)
        Me.Init(assembler)
    End Sub

    Private Sub Init(assembler As MZ1500Assembler)
        With assembler
            Me.Add({ .LabelRef("")}, {&H0})
            Me.Add({ .OpCodeADD, .HL, .DE}, {&H19})
            Me.Add({ .OpCodeADD, .A, .A}, {&H87})
            Me.Add({ .OpCodeAND, .A}, {&HA7})
            Me.Add({ .OpCodeAND, .B}, {&HA0})
            Me.Add({ .OpCodeCALL, .Value(&H0)}, {&HCD})
            Me.Add({ .OpCodeDEC, .A}, {&H3D})
            Me.Add({ .OpCodeDEC, .B}, {&H5})
            Me.Add({ .OpCodeDEC, .BC}, {&HB})
            Me.Add({ .OpCodeDEC, .DE}, {&H1B})
            Me.Add({ .OpCodeDEC, .HL}, {&H2B})
            Me.Add({ .OpCodeDEC, .L}, {&H2D})
            Me.Add({ .OpCodeDI}, {&HF3})
            Me.Add({ .OpCodeEI}, {&HFB})
            Me.Add({ .OpCodeIM_1}, {&HED, &H56})
            Me.Add({ .OpCodeINC, .DE}, {&H13})
            Me.Add({ .OpCodeCALL, .LabelRef("")}, {&HCD})
            Me.Add({ .OpCodeCP, .HLref}, {&HBE})
            Me.Add({ .OpCodeCP, .A}, {&HBF})
            Me.Add({ .OpCodeCP, .B}, {&HB8})
            Me.Add({ .OpCodeCP, .C}, {&HB9})
            Me.Add({ .OpCodeCP, .D}, {&HBA})
            Me.Add({ .OpCodeCP, .E}, {&HBB})
            Me.Add({ .OpCodeCP, .H}, {&HBC})
            Me.Add({ .OpCodeCP, .L}, {&HBD})
            Me.Add({ .OpCodeINC, .HLref}, {&H34})
            Me.Add({ .OpCodeINC, .HL}, {&H23})
            Me.Add({ .OpCodeINC, .A}, {&H3C})
            Me.Add({ .OpCodeINC, .B}, {&H4})
            Me.Add({ .OpCodeJP, .Z}, {&HCA})
            Me.Add({ .OpCodeJP, .NZ}, {&HC2})
            Me.Add({ .OpCodeJP, .LabelRef("")}, {&HC3})
            Me.Add({ .OpCodeLD, .A, .A}, {&H7F})
            Me.Add({ .OpCodeLD, .A, .B}, {&H78})
            Me.Add({ .OpCodeLD, .A, .C}, {&H79})
            Me.Add({ .OpCodeLD, .A, .D}, {&H7A})
            Me.Add({ .OpCodeLD, .A, .E}, {&H7B})
            Me.Add({ .OpCodeLD, .A, .H}, {&H7C})
            Me.Add({ .OpCodeLD, .A, .L}, {&H7D})
            Me.Add({ .OpCodeLD, .A, .DEref}, {&H1A})
            Me.Add({ .OpCodeLD, .A, .HLref}, {&H7E})
            Me.Add({ .OpCodeLD, .A, .Value(&H0)}, {&H3E})
            Me.Add({ .OpCodeLD, .BC, .Value(&H0)}, {&H1})
            Me.Add({ .OpCodeLD, .BC, .LabelRef("")}, {&H1})
            Me.Add({ .OpCodeLD, .B, .Value(&H0)}, {&H6})
            Me.Add({ .OpCodeLD, .B, .A}, {&H47})
            Me.Add({ .OpCodeLD, .B, .HLref}, {&H46})
            Me.Add({ .OpCodeLD, .C, .Value(&H0)}, {&HE})
            Me.Add({ .OpCodeLD, .D, .A}, {&H57})
            Me.Add({ .OpCodeLD, .D, .B}, {&H50})
            Me.Add({ .OpCodeLD, .D, .H}, {&H54})
            Me.Add({ .OpCodeLD, .D, .HLref}, {&H56})
            Me.Add({ .OpCodeLD, .D, .Value(&H0)}, {&H16})
            Me.Add({ .OpCodeLD, .DE, .LabelRef("")}, {&H11})
            Me.Add({ .OpCodeLD, .DE, .Value(&H0)}, {&H11})
            Me.Add({ .OpCodeLD, .DEref, .A}, {&H12})
            Me.Add({ .OpCodeLD, .E, .A}, {&H5F})
            Me.Add({ .OpCodeLD, .E, .B}, {&H58})
            Me.Add({ .OpCodeLD, .E, .Value(&H0)}, {&H1E})
            Me.Add({ .OpCodeLD, .E, .HLref}, {&H5E})
            Me.Add({ .OpCodeLD, .E, .L}, {&H5D})
            Me.Add({ .OpCodeLD, .H, .A}, {&H67})
            Me.Add({ .OpCodeLD, .HL, .DE}, {&H62, &H6B})
            Me.Add({ .OpCodeLD, .HL, .LabelRef("")}, {&H21})
            Me.Add({ .OpCodeLD, .HL, .Value(&H0)}, {&H21})
            Me.Add({ .OpCodeLD, .HLref, .A}, {&H77})
            Me.Add({ .OpCodeLD, .HLref, .B}, {&H70})
            Me.Add({ .OpCodeLD, .HLref, .C}, {&H71})
            Me.Add({ .OpCodeLD, .HLref, .D}, {&H72})
            Me.Add({ .OpCodeLD, .HLref, .E}, {&H73})
            Me.Add({ .OpCodeLD, .HLref, .LabelRef("")}, {&H36})
            Me.Add({ .OpCodeLD, .HLref, .Value(&H0)}, {&H36})
            Me.Add({ .OpCodeLD, .L, .A}, {&H6F})
            Me.Add({ .OpCodeLDIR}, {&HED, &HB0})
            Me.Add({ .OpCodeLD, .Value(&H0), .A}, {&H32})
            Me.Add({ .OpCodeLD, .Value(&H0), .BC}, {&HED, &H43})
            Me.Add({ .OpCodeOR, .B}, {&HB0})
            Me.Add({ .OpCodeOUT, .Value(&H0)}, {&HD3})
            Me.Add({ .OpCodeRET}, {&HC9})

            Me.Add({ .OpCodePUSH, .DE}, {&HD5})
            Me.Add({ .OpCodePUSH, .HL}, {&HE5})

            Me.Add({ .OpCodePOP, .DE}, {&HD1})
            Me.Add({ .OpCodePOP, .HL}, {&HE1})

            Me.Add({ .OpCodeSBC, .A, .A}, {&H9F})
            Me.Add({ .OpCodeSLA, .A}, {&HCB, &H27})
            Me.Add({ .OpCodeSRL, .A}, {&HCB, &H3F})
            Me.Add({ .OpCodeXOR, .A}, {&HAF})
        End With
    End Sub

    Private Sub Add(partArr() As Part, byteArr() As Byte)
        Dim key As New MnemonicKey()
        Dim description As New System.Text.StringBuilder()
        Dim descriptionSub As New List(Of String)
        For Each part As Part In partArr
            key.AddPart(part)
            If TypeOf part Is Z80OpecodePart Then
                description.Append(part.GetInfo())
            Else
                descriptionSub.Add(part.GetInfo)
            End If
        Next
        description.Append(" " & String.Join(" , ", descriptionSub.ToArray()))
        Dim data As New MnemonicByteData()
        data.ByteData = byteArr
        data.Description = description.ToString()
        _Map.Add(key, data)
    End Sub

    Public Function TryGet(key As MnemonicKey, ByRef byteData As MnemonicByteData) As Boolean
        Return Me._Map.TryGetValue(key, byteData)
    End Function

End Class
