Module mdl_MappingTableEntry

    ''' <summary>
    ''' An entry in the mapping table
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class MappingTableEntry

        Private _entrySize As UInt32 = 3
        Private _physSectorNumber As UInt32 = 0

        ''' <summary>
        ''' Physical sector number
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Property PhysSectorNumber As UInt32
            Get
                Return _physSectorNumber
            End Get
            Set(value As UInt32)
                _physSectorNumber = value
            End Set
        End Property

        ''' <summary>
        ''' Size of a single entry
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Property EntrySize As UInt32
            Get
                Return _entrySize
            End Get
            Set(value As UInt32)
                _entrySize = value
            End Set
        End Property

        ''' <summary>
        ''' Creates an empty mapping table entry
        ''' </summary>
        ''' <remarks>Do not forget to set PhysSectorNumber and EntrySize</remarks>
        Friend Sub New()
        End Sub

        ''' <summary>
        ''' Creates a new mapping table entry
        ''' </summary>
        ''' <param name="newPhysSectorNumber"></param>
        ''' <param name="entrySize"></param>
        ''' <remarks></remarks>
        Friend Sub New(ByVal newPhysSectorNumber As UInt32, ByVal entrySize As UInt32)
            _physSectorNumber = newPhysSectorNumber
            _entrySize = entrySize
        End Sub


        ''' <summary>
        ''' Returns TRUE if the entry contains no useful data
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function IsEmpty() As Boolean
            If _entrySize = 3 And (_physSectorNumber = &HFFFFFFUI Or _physSectorNumber = 0) Then
                Return True
            ElseIf _entrySize = 2 And (_physSectorNumber = &HFFFFUI Or _physSectorNumber = 0) Then
                Return True
            End If
            Return False
        End Function

        ''' <summary>
        ''' Reads data from a buffer and returns a new MappingTableEntry instance
        ''' </summary>
        ''' <param name="dataArray"></param>
        ''' <param name="entrySize"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function Read(dataArray() As Byte, ByVal entrySize As UInt32) As MappingTableEntry
            Dim mEntry As New MappingTableEntry
            mEntry.EntrySize = entrySize
            If dataArray.Length >= entrySize Then
                Dim int As UInt32 = 0
                Dim d0 As UInt32 = CUInt(dataArray(0)) << 8
                Dim d1 As UInt32 = CUInt(dataArray(1))
                int = d0 Or d1
                If entrySize = 3 Then
                    int = (int << 8) Or CUInt(dataArray(2))
                End If
                mEntry._physSectorNumber = int
                Return mEntry
            End If
            Return Nothing
        End Function

        ''' <summary>
        ''' Writes the entry into a buffer
        ''' </summary>
        ''' <param name="dataArray"></param>
        ''' <param name="index"></param>
        ''' <remarks></remarks>
        Friend Sub Write(ByRef dataArray() As Byte, ByVal index As Integer)
            Dim b1 As Byte, b2 As Byte, b3 As Byte
            b1 = (_physSectorNumber And &HFF)
            b2 = (_physSectorNumber >> 8) And &HFF
            b3 = (_physSectorNumber >> 16) And &HFF
            If _entrySize = 2 Then
                dataArray(index) = b2
                dataArray(index + 1) = b1
            ElseIf _entrySize = 3 Then
                dataArray(index) = b3
                dataArray(index + 1) = b2
                dataArray(index + 2) = b1
            End If
        End Sub

        ''' <summary>
        ''' Outputs the full information about the entry
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function ToText() As String
            Dim resStr As String = "PhysSectorNumber    = " & HexSTR(_physSectorNumber) & vbCrLf
            Return resStr
        End Function

    End Class

End Module