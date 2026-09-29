Module mdl_LogBlockClass

    ''' <summary>
    ''' Class describing log blocks
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class LogBlockClass
        Inherits BlockClass

        Private _logEntries As New List(Of LogEntryClass)
        Private _sectSize As UInt32
        Private _sectPerBlock As UInt32
        Private _chunkSize As UInt32

        Private _blockData() As Byte
        Private _fullBlockSize As UInt32 = 0

        ''' <summary>
        ''' Creates a new LogBlockClass
        ''' </summary>
        ''' <param name="sectorSize">Sector size</param>
        ''' <param name="sectorsPerBlock">Number of sectors per block</param>
        ''' <remarks></remarks>
        Friend Sub New(ByVal sectorSize As UInt32, ByVal sectorsPerBlock As UInt32, ByVal chunkSize As UInt32)
            _sectSize = sectorSize
            _sectPerBlock = sectorsPerBlock

            _chunkSize = chunkSize
            _fullBlockSize = (_sectSize + chunkSize) * _sectPerBlock
            ReDim _blockData(_fullBlockSize)
        End Sub

        ''' <summary>
        ''' Returns the list of LogEntry items
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend ReadOnly Property Entries As List(Of LogEntryClass)
            Get
                Return _logEntries
            End Get
        End Property

        ''' <summary>
        ''' Discard all entries from the log block
        ''' In effect, the block will then be filled with 0xFF
        ''' </summary>
        ''' <remarks></remarks>
        Friend Overrides Sub Clear()
            _logEntries.Clear()
        End Sub

        ''' <summary>
        ''' Returns TRUE if the block contains no entries
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function IsEmpty() As Boolean
            If _logEntries.Count > 0 Then
                Return False
            End If
            Return True
        End Function


        ''' <summary>
        ''' Returns a buffer with the block's data
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawData() As Byte()
            Dim _count As UInt32 = 0
            FillArray(_blockData, &HFF)
            For Each logEntry In _logEntries
                FillArray(_blockData, &H0, (_sectSize + _chunkSize) * _count, _sectSize + _chunkSize)
                logEntry.GetBytes.CopyTo(_blockData, (_sectSize + _chunkSize) * _count)
                Dim sInfo As New SectorInfoClass(_chunkSize)
                sInfo.sectorFlag = SectorInfoClass.GetSectorFlags(_blockData, (_sectSize + _chunkSize) * _count)
                sInfo.logSectorNumber = &HFFFFFFUI
                sInfo.sectorChecksum = 0UI
                sInfo.sectorChecksumHigh = 0UI
                sInfo.sectorChecksumLow = 0UI
                Dim pos As UInt32 = _sectSize + (_sectSize + _chunkSize) * _count
                sInfo.GetBytes.CopyTo(_blockData, pos)
                sInfo.sectorChecksum = sInfo.GetSectorChecksum(_blockData, (_sectSize + _chunkSize) * _count, _sectSize) ' + &H10
                sInfo.GetBytes.CopyTo(_blockData, pos)
                _count += 1
            Next
            Return _blockData
        End Function

        ''' <summary>
        ''' Returns the size of the buffer with the block's data
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawDataSize() As UInteger
            Return _fullBlockSize
        End Function

    End Class

End Module