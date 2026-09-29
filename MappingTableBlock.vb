Module mdl_MappingTableBlock

    ''' <summary>
    ''' Class describing blocks that hold the mapping table
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class MappingTableBlock
        Inherits BlockClass

        Private _sectSize As UInt32
        Private _sectPerBlock As UInt32
        Private _fullBlockSize As UInt32
        Private _blockSize As UInt32
        Private _chunkSize As UInt32
        Private _entries As New List(Of MappingTableEntry)
        Private _entrySize As UInt32
        Private _sectorNumber As UInt32 = 0

        ''' <summary>
        ''' Mapping entries
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public ReadOnly Property Entries As List(Of MappingTableEntry)
            Get
                Return _entries
            End Get
        End Property

        ''' <summary>
        ''' Sector number
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Property SectorNumber As UInt32
            Get
                Return _sectorNumber
            End Get
            Set(value As UInt32)
                _sectorNumber = value
            End Set
        End Property

        ''' <summary>
        ''' Creates a new block holding mapping information
        ''' </summary>
        ''' <param name="sectorSize"></param>
        ''' <param name="sectorsPerBlock"></param>
        ''' <remarks></remarks>
        Friend Sub New(ByVal sectorSize As UInt32, ByVal sectorsPerBlock As UInt32, ByVal chunkSize As UInt32, ByVal entrySize As UInt32)
            _sectSize = sectorSize
            _sectPerBlock = sectorsPerBlock

            _chunkSize = chunkSize
            _fullBlockSize = (_sectSize + _chunkSize) * _sectPerBlock
            _blockSize = _sectSize * _sectPerBlock
            _entrySize = entrySize
        End Sub

        ''' <summary>
        ''' Clears the mapping table
        ''' </summary>
        ''' <remarks></remarks>
        Friend Overrides Sub Clear()
            _entries.Clear()
        End Sub

        ''' <summary>
        ''' Returns True if the specified sector is empty
        ''' </summary>
        ''' <param name="data"></param>
        ''' <param name="sectorIndex"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Function IsSectorEmpty(ByVal data() As Byte, ByVal sectorIndex As UInteger) As Boolean

            Dim start As UInt32 = sectorIndex * _sectSize
            For i = 0 To _sectSize - 1 'data.Length - 1
                If data(start + i) <> &HFF Then Return False
            Next
            Return True
        End Function

        ''' <summary>
        ''' Returns the number of used sectors
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function UsedSectors() As UInteger
            Dim entriesPerSector As UInt32 = _sectSize \ _entrySize
            Dim totalEntries As UInt32 = Entries.Count
            Dim sectorsCount As UInt32 = totalEntries \ entriesPerSector
            If totalEntries Mod entriesPerSector Then
                sectorsCount += 1
            End If
            Return sectorsCount
        End Function

        ''' <summary>
        ''' Returns a buffer with the block's data
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawData() As Byte()
            Dim data(_fullBlockSize) As Byte
            FillArray(data, &HFF)

            Dim totalEntries As UInt32 = Entries.Count
            Dim curEntry As UInt32 = 0

            Dim sectorsCount As UInt32 = UsedSectors()

            Dim entriesPerSector As UInt32 = _sectSize \ _entrySize
            For sect = 0 To sectorsCount - 1

                Dim entriesToProcess As UInt32 = entriesPerSector
                If (totalEntries - curEntry) < entriesToProcess Then
                    entriesToProcess = totalEntries - curEntry
                End If

                Dim curEntryInSect As UInt32 = 0
                For x = 0 To entriesToProcess - 1
                    'For x = 0 To Entries.Count - 1
                    Dim mtEntry As MappingTableEntry = Entries(curEntry)
                    mtEntry.Write(data, sect * (_sectSize + _chunkSize) + curEntryInSect * _entrySize)
                    'Next
                    curEntryInSect += 1
                    curEntry += 1
                Next
                Dim sInfo As New SectorInfoClass(_chunkSize)
                sInfo.sectorFlag = &HFF
                sInfo.logSectorNumber = SectorNumber + sect '(CUInt(BlockAddress / _fullBlockSize * _sectPerBlock)) + sect
                sInfo.sectorChecksum = 0
                sInfo.sectorChecksumHigh = 0
                sInfo.sectorChecksumLow = 0
                sInfo.sectorChecksum = sInfo.GetSectorChecksum(data, sect * (_sectSize + _chunkSize), _sectSize)
                sInfo.GetBytes.CopyTo(data, _sectSize + sect * (_sectSize + _chunkSize))
            Next

            Return data
        End Function

        ''' <summary>
        ''' Returns the size of the buffer with the block's data
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawDataSize() As UInteger
            Return _fullBlockSize
        End Function

        ''' <summary>
        ''' Returns the maximum number of entries in a block
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks>Assumes the block was initialized with the correct parameters </remarks>
        Friend Function GetMaximumEntriesCount() As UInt32
            Dim entriesPerSector As UInt32 = _sectSize \ _entrySize
            Return entriesPerSector * _sectPerBlock
        End Function

        ''' <summary>
        ''' Returns True if the block contains no entries
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function IsEmpty() As Boolean
            If _entries.Count = 0 Then
                Return True
            End If
            Return False
        End Function

    End Class

End Module