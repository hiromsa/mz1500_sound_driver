Public Class NoteMap


    Private Class NoteKey
        Implements IEquatable(Of NoteKey)
        Public Property NoteName = ""
        Public Property OctaveNo As Integer = 0

        Public Sub New(octaveNo As Integer, noteName As String)
            Me.NoteName = noteName
            Me.OctaveNo = octaveNo
        End Sub
        Public Function Equals1(other As NoteKey) As Boolean Implements IEquatable(Of NoteKey).Equals
            Return (Me.NoteName = other.NoteName) AndAlso (Me.OctaveNo = other.OctaveNo)
        End Function
        Public Overrides Function GetHashCode() As Integer
            Return Me.NoteName.GetHashCode() Or Me.OctaveNo.GetHashCode()
        End Function
    End Class

    Private _NoteMap As New Dictionary(Of NoteKey, NoteInfo)

    Public Function GetNoteInfo(octave As Integer, noteName As String) As NoteInfo
        Return Me._NoteMap.Item(New NoteKey(octave, noteName))
    End Function

    Public Function GetFreqencyRegValue(octave As Integer, noteName As String, Optional ByVal isNext As Boolean = False) As Integer
        Dim noteInfo As NoteInfo = Me.GetNoteInfo(octave, noteName)
        If isNext Then
            noteInfo = noteInfo.PreviousNote()
        End If
        Return noteInfo.FrequencyRegValue()
    End Function

    Public Function GetFreqencyRegValueForMZ700(octave As Integer, noteName As String, Optional ByVal isNext As Boolean = False) As Integer
        Dim noteInfo As NoteInfo = Me.GetNoteInfo(octave, noteName)
        If isNext Then
            noteInfo = noteInfo.PreviousNote()
        End If
        Return noteInfo.FrequencyRegValueForMZ700()
    End Function

    Public Function GetFreqencyRegValueForNoise(octave As Integer, noteName As String, Optional ByVal isNext As Boolean = False) As Integer
        Dim noteInfo As NoteInfo = Me.GetNoteInfo(octave, noteName)
        If isNext Then
            noteInfo = noteInfo.PreviousNote()
        End If
        Return noteInfo.FrequencyRegValueForNoise
    End Function


    Public Class NoteInfo
        Public Property NoteName As String = ""
        Public Property NoteAlias As String = ""
        Public Property OctaveNo As Integer = 0
        Public Property Frequency As Double = 0
        Public Property MIDINoteNo As Integer = 0

        Public Property PreviousNote As NoteInfo = Nothing

        Public Property NextNode As NoteInfo = Nothing

        Public ReadOnly Property FrequencyRegValue As Integer
            Get
                Return 111860.8 / Me.Frequency
            End Get
        End Property

        Public ReadOnly Property FrequencyRegValueForMZ700 As Integer
            Get
                Return 894886.25 / Me.Frequency
            End Get
        End Property

        Public ReadOnly Property FrequencyRegValueForNoise As Integer
            Get
                'Return 6991 / Me.Frequency
                Return 7457.385417 / Me.Frequency
            End Get
        End Property

    End Class

    Public Sub New()
        Me.Init()
    End Sub

    Private Sub Init()
        Me.Add("r", "", -1, 8.2, 0)
        Me.Add("c", "", -1, 8.2, 0)
        Me.Add("c+", "d-", -1, 8.7, 1)
        Me.Add("d", "", -1, 9.2, 2)
        Me.Add("d+", "e-", -1, 9.7, 3)
        Me.Add("e", "f-", -1, 10.3, 4)
        Me.Add("f", "", -1, 10.9, 5)
        Me.Add("f+", "g-", -1, 11.6, 6)
        Me.Add("g", "", -1, 12.2, 7)
        Me.Add("g+", "a-", -1, 13, 8)
        Me.Add("a", "", -1, 13.8, 9)
        Me.Add("a+", "b-", -1, 14.6, 10)
        Me.Add("b", "c-", -1, 15.4, 11)
        Me.Add("r", "", 0, 16.4, 12)
        Me.Add("c", "", 0, 16.4, 12)
        Me.Add("c+", "d-", 0, 17.3, 13)
        Me.Add("d", "", 0, 18.4, 14)
        Me.Add("d+", "e-", 0, 19.4, 15)
        Me.Add("e", "f-", 0, 20.6, 16)
        Me.Add("f", "", 0, 21.8, 17)
        Me.Add("f+", "g-", 0, 23.1, 18)
        Me.Add("g", "", 0, 24.5, 19)
        Me.Add("g+", "a-", 0, 26, 20)
        Me.Add("a", "", 0, 27.5, 21)
        Me.Add("a+", "b-", 0, 29.1, 22)
        Me.Add("b", "c-", 0, 30.9, 23)
        Me.Add("r", "", 1, 32.7, 24)
        Me.Add("c", "", 1, 32.7, 24)
        Me.Add("c+", "d-", 1, 34.6, 25)
        Me.Add("d", "", 1, 36.7, 26)
        Me.Add("d+", "e-", 1, 38.9, 27)
        Me.Add("e", "f-", 1, 41.2, 28)
        Me.Add("f", "", 1, 43.7, 29)
        Me.Add("f+", "g-", 1, 46.2, 30)
        Me.Add("g", "", 1, 49, 31)
        Me.Add("g+", "a-", 1, 51.9, 32)
        Me.Add("a", "", 1, 55, 33)
        Me.Add("a+", "b-", 1, 58.3, 34)
        Me.Add("b", "c-", 1, 61.7, 35)
        Me.Add("r", "", 2, 65.4, 36)
        Me.Add("c", "", 2, 65.4, 36)
        Me.Add("c+", "d-", 2, 69.3, 37)
        Me.Add("d", "", 2, 73.4, 38)
        Me.Add("d+", "e-", 2, 77.8, 39)
        Me.Add("e", "f-", 2, 82.4, 40)
        Me.Add("f", "", 2, 87.3, 41)
        Me.Add("f+", "g-", 2, 92.5, 42)
        Me.Add("g", "", 2, 98, 43)
        Me.Add("g+", "a-", 2, 103.8, 44)
        Me.Add("a", "", 2, 110, 45)
        Me.Add("a+", "b-", 2, 116.5, 46)
        Me.Add("b", "c-", 2, 123.5, 47)
        Me.Add("r", "", 3, 130.8, 48)
        Me.Add("c", "", 3, 130.8, 48)
        Me.Add("c+", "d-", 3, 138.6, 49)
        Me.Add("d", "", 3, 146.8, 50)
        Me.Add("d+", "e-", 3, 155.6, 51)
        Me.Add("e", "f-", 3, 164.8, 52)
        Me.Add("f", "", 3, 174.6, 53)
        Me.Add("f+", "g-", 3, 185, 54)
        Me.Add("g", "", 3, 196, 55)
        Me.Add("g+", "a-", 3, 207.7, 56)
        Me.Add("a", "", 3, 220, 57)
        Me.Add("a+", "b-", 3, 233.1, 58)
        Me.Add("b", "c-", 3, 246.9, 59)
        Me.Add("r", "", 4, 261.6, 60)
        Me.Add("c", "", 4, 261.6, 60)
        Me.Add("c+", "d-", 4, 277.2, 61)
        Me.Add("d", "", 4, 293.7, 62)
        Me.Add("d+", "e-", 4, 311.1, 63)
        Me.Add("e", "f-", 4, 329.6, 64)
        Me.Add("f", "", 4, 349.2, 65)
        Me.Add("f+", "g-", 4, 370, 66)
        Me.Add("g", "", 4, 392, 67)
        Me.Add("g+", "a-", 4, 415.3, 68)
        Me.Add("a", "", 4, 440, 69)
        Me.Add("a+", "b-", 4, 466.2, 70)
        Me.Add("b", "c-", 4, 493.9, 71)
        Me.Add("r", "", 5, 523.3, 72)
        Me.Add("c", "", 5, 523.3, 72)
        Me.Add("c+", "d-", 5, 554.4, 73)
        Me.Add("d", "", 5, 587.3, 74)
        Me.Add("d+", "e-", 5, 622.3, 75)
        Me.Add("e", "f-", 5, 659.3, 76)
        Me.Add("f", "", 5, 698.5, 77)
        Me.Add("f+", "g-", 5, 740, 78)
        Me.Add("g", "", 5, 784, 79)
        Me.Add("g+", "a-", 5, 830.6, 80)
        Me.Add("a", "", 5, 880, 81)
        Me.Add("a+", "b-", 5, 932.3, 82)
        Me.Add("b", "c-", 5, 987.8, 83)
        Me.Add("r", "", 6, 1046.5, 84)
        Me.Add("c", "", 6, 1046.5, 84)
        Me.Add("c+", "d-", 6, 1108.7, 85)
        Me.Add("d", "", 6, 1174.7, 86)
        Me.Add("d+", "e-", 6, 1244.5, 87)
        Me.Add("e", "f-", 6, 1318.5, 88)
        Me.Add("f", "", 6, 1396.9, 89)
        Me.Add("f+", "g-", 6, 1480, 90)
        Me.Add("g", "", 6, 1568, 91)
        Me.Add("g+", "a-", 6, 1661.2, 92)
        Me.Add("a", "", 6, 1760, 93)
        Me.Add("a+", "b-", 6, 1864.7, 94)
        Me.Add("b", "c-", 6, 1975.5, 95)
        Me.Add("r", "", 7, 2093, 96)
        Me.Add("c", "", 7, 2093, 96)
        Me.Add("c+", "d-", 7, 2217.5, 97)
        Me.Add("d", "", 7, 2349.3, 98)
        Me.Add("d+", "e-", 7, 2489, 99)
        Me.Add("e", "f-", 7, 2637, 100)
        Me.Add("f", "", 7, 2793.8, 101)
        Me.Add("f+", "g-", 7, 2960, 102)
        Me.Add("g", "", 7, 3136, 103)
        Me.Add("g+", "a-", 7, 3322.4, 104)
        Me.Add("a", "", 7, 3520, 105)
        Me.Add("a+", "b-", 7, 3729.3, 106)
        Me.Add("b", "c-", 7, 3951.1, 107)
        Me.Add("r", "", 8, 4186, 108)
        Me.Add("c", "", 8, 4186, 108)
        Me.Add("c+", "d-", 8, 4434.9, 109)
        Me.Add("d", "", 8, 4698.6, 110)
        Me.Add("d+", "e-", 8, 4978, 111)
        Me.Add("e", "f-", 8, 5274, 112)
        Me.Add("f", "", 8, 5587.7, 113)
        Me.Add("f+", "g-", 8, 5919.9, 114)
        Me.Add("g", "", 8, 6271.9, 115)
        Me.Add("g+", "a-", 8, 6644.9, 116)
        Me.Add("a", "", 8, 7040, 117)
        Me.Add("a+", "b-", 8, 7458.6, 118)
        Me.Add("b", "c-", 8, 7902.1, 119)
        Me.Add("r", "", 9, 8372, 120)
        Me.Add("c", "", 9, 8372, 120)
        Me.Add("c+", "d-", 9, 8869.8, 121)
        Me.Add("d", "", 9, 9397.3, 122)
        Me.Add("d+", "e-", 9, 9956.1, 123)
        Me.Add("e", "f-", 9, 10548.1, 124)
        Me.Add("f", "", 9, 11175.3, 125)
        Me.Add("f+", "g-", 9, 11839.8, 126)
        Me.Add("g", "", 9, 12543.9, 127)
    End Sub

    Private _PreviousNoteInfo As NoteInfo = Nothing

    Public Sub Add(noteName As String, noteAlias As String, octaveNo As Integer, freq As Double, noteNo As Integer)

        Dim noteInfo As New NoteInfo
        With noteInfo
            .NoteName = noteName
            .NoteAlias = noteAlias
            .OctaveNo = octaveNo
            .Frequency = freq
            .MIDINoteNo = noteNo
        End With

        Dim noteKey As New NoteKey(octaveNo, noteName)

        Me._NoteMap.Add(noteKey, noteInfo)

        If _PreviousNoteInfo IsNot Nothing Then
            _PreviousNoteInfo.NextNode = noteInfo
            noteInfo.PreviousNote = _PreviousNoteInfo
        End If

        If noteAlias <> "" Then

            Dim noteAliasKey As New NoteKey(octaveNo, noteAlias)
            Me._NoteMap.Add(noteAliasKey, noteInfo)

        End If

        _PreviousNoteInfo = noteInfo

    End Sub

End Class
