Module mdl_CheckpointBlockClass

    ''' <summary>
    ''' Class describing checkpoints
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class CheckpointBlockClass
        Inherits BlockClass

        Private _sectSize As UInt32
        Private _sectPerBlock As UInt32

        Private _fullBlockSize As UInt32

        Private _chunkSize As UInt32

        Private _logicalBlocks As UInt32
        Private _mappingEntriesCount As UInt32

        Public CheckpointIndex As UInt32, ChainPhysicalSector As UInt32, _
                FreeBlockCount As UInt32, _
                SystemBlockCount As UInt32, _
                DataBlockArray(10) As UInt32, MappingTableBlockArray(3) As UInt32, _
                DataSectorNext As UInt32, FreeSectorCountData As UInt32, _
                MappingTableSectorNext As UInt32, _
                FreeSectorCountMappingTable As UInt32

        Private _mappingTableSectors As New List(Of UInt32)
        Private _freeBlocksBitmap As New List(Of UInt32)
        Private _systemBlocksBitmap As New List(Of UInt32)

        Private _blockCount As UInt32 = 0

        ''' <summary>
        ''' Entries from the mapping table
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public ReadOnly Property MappingTableSectorArray As List(Of UInt32)
            Get
                Return _mappingTableSectors
            End Get
        End Property

        ''' <summary>
        ''' Bitmaps of free blocks
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public ReadOnly Property FreeBlocksBitmap As List(Of UInt32)
            Get
                Return _freeBlocksBitmap
            End Get
        End Property

        ''' <summary>
        ''' Bitmaps of system blocks
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public ReadOnly Property SystemBlocksBitmap As List(Of UInt32)
            Get
                Return _systemBlocksBitmap
            End Get
        End Property

        ''' <summary>
        ''' Number of logical blocks in ROM (fill in at the end of processing!!!)
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Public Property LogicalBlockCount As UInt32
            Get
                Return _blockCount
            End Get
            Set(value As UInt32)
                _blockCount = value
            End Set
        End Property

        ''' <summary>
        ''' Creates a new checkpoint block
        ''' </summary>
        ''' <param name="sectorSize"></param>
        ''' <param name="sectorsPerBlock"></param>
        ''' <remarks></remarks>
        Friend Sub New(ByVal sectorSize As UInt32, ByVal sectorsPerBlock As UInt32, ByVal chunkSize As UInt32, Optional ByVal logicalBlocks As UInt32 = 0, Optional ByVal mappingTableSectorsCount As UInt32 = 0)
            _sectSize = sectorSize
            _sectPerBlock = sectorsPerBlock
            _logicalBlocks = logicalBlocks
            _mappingEntriesCount = mappingTableSectorsCount
            _chunkSize = chunkSize
            _fullBlockSize = (_sectSize + _chunkSize) * _sectPerBlock
        End Sub

        ''' <summary>
        ''' Compute the checksum
        ''' </summary>
        ''' <param name="checksum"></param>
        ''' <param name="b"></param>
        ''' <param name="size"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function GetLongChecksum(ByVal checksum As UInt32, ByVal b() As Byte, ByVal size As UInt32) As UInt32
            For x = 0 To b.Length - 1
                Dim prev As UInt32 = checksum
                checksum = CUInt(b(x)) + (checksum >> 1UI)
                If (prev And 1UI) <> 0 Then
                    Dim n As UInt64 = CULng(checksum) + &H80000000UL
                    checksum = n And &HFFFFFFFFUI
                End If
            Next
            Return checksum
        End Function

        ''' <summary>
        ''' Compute the checksum
        ''' </summary>
        ''' <param name="checksum"></param>
        ''' <param name="b"></param>
        ''' <param name="size"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function GetLongChecksum(ByVal checksum As UInt32, ByVal b() As Byte, ByVal index As UInt32, ByVal size As UInt32) As UInt32
            For x = index To index + size - 1
                Dim prev As UInt32 = checksum
                checksum = CUInt(b(x)) + (checksum >> 1UI)
                If (prev And 1UI) <> 0 Then
                    Dim n As UInt64 = CULng(checksum) + &H80000000UL
                    checksum = n And &HFFFFFFFFUI
                End If
            Next
            Return checksum
        End Function

        ''' <summary>
        ''' Compute the checksum
        ''' </summary>
        ''' <param name="checksum"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function GetLongChecksum(ByVal checksum As UInt32, ByVal list As List(Of UInt32)) As UInt32
            For i = 0 To list.Count - 1
                Dim b() As Byte = BitConverter.GetBytes(list(i))
                For x = 0 To 3
                    Dim prev As UInt32 = checksum
                    checksum = CUInt(b(x)) + (checksum >> 1UI)
                    If (prev And 1UI) <> 0 Then
                        Dim n As UInt64 = CULng(checksum) + &H80000000UL
                        checksum = n And &HFFFFFFFFUI
                    End If
                Next
            Next
            Return checksum
        End Function

        ''' <summary>
        ''' Reads a checkpoint from the buffer and creates a new instance of the CheckpointBlockClass class
        ''' </summary>
        ''' <param name="dataArray"></param>
        ''' <param name="sectorSize"></param>
        ''' <param name="sectorsPerBlock"></param>
        ''' <param name="chunkSize"></param>
        ''' <param name="logicalBlocks"></param>
        ''' <param name="mappingTableSectorsCount"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function Read(ByVal dataArray() As Byte, ByVal sectorSize As UInt32, ByVal sectorsPerBlock As UInt32, ByVal chunkSize As UInt32, ByVal logicalBlocks As UInt32, ByVal mappingTableSectorsCount As UInt32) As CheckpointBlockClass
            Dim block As New CheckpointBlockClass(sectorSize, sectorsPerBlock, chunkSize, logicalBlocks, mappingTableSectorsCount)
            block.CheckpointIndex = BitConverter.ToUInt32(dataArray, 0)
            block.ChainPhysicalSector = BitConverter.ToUInt32(dataArray, 4)
            Dim pos As UInt32 = 8
            For x = 0 To mappingTableSectorsCount - 1
                block._mappingTableSectors.Add(BitConverter.ToUInt32(dataArray, pos))
                pos += 4
            Next

            Dim dwCount As UInt32 = logicalBlocks \ 32
            If dwCount Mod 32 Or dwCount = 0 Then
                dwCount += 1
            End If
            For x = 0 To dwCount - 1
                block._freeBlocksBitmap.Add(BitConverter.ToUInt32(dataArray, pos))
                pos += 4
            Next
            block.FreeBlockCount = BitConverter.ToUInt32(dataArray, pos)
            pos += 4
            For x = 0 To dwCount - 1
                block._systemBlocksBitmap.Add(BitConverter.ToUInt32(dataArray, pos))
                pos += 4
            Next
            block.SystemBlockCount = BitConverter.ToUInt32(dataArray, pos)
            pos += 4

            For x = 0 To 9
                block.DataBlockArray(x) = BitConverter.ToUInt32(dataArray, pos)
                pos += 4
            Next

            For x = 0 To 2
                block.MappingTableBlockArray(x) = BitConverter.ToUInt32(dataArray, pos)
                pos += 4
            Next

            block.DataSectorNext = BitConverter.ToUInt32(dataArray, pos)
            pos += 4
            block.FreeSectorCountData = BitConverter.ToUInt32(dataArray, pos)
            pos += 4
            block.MappingTableSectorNext = BitConverter.ToUInt32(dataArray, pos)
            pos += 4
            block.FreeSectorCountMappingTable = BitConverter.ToUInt32(dataArray, pos)

            Return block
        End Function

        ''' <summary>
        ''' Clears the checkpoint
        ''' </summary>
        ''' <remarks>PROCEDURE NOT IMPLEMENTED</remarks>
        Friend Overrides Sub Clear()
            Throw New NotImplementedException
        End Sub

        Private Function PosCorrection(ByRef pos As UInt32) As UInt32
            If pos > _sectSize Then
                pos -= _sectSize
                Return 1
            End If
            Return 0
        End Function

        ''' <summary>
        ''' Gets a buffer containing the information from the block
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawData() As Byte()

            Dim magicCount As UInt32 = _sectSize \ 2
            Dim sectorsCount As UInt32 = LogicalBlockCount \ magicCount
            If LogicalBlockCount < magicCount Or (sectorsCount Mod magicCount) > 0 Then
                sectorsCount += 1
            End If

            Dim curSector As UInt32 = 0
            Dim pos As UInt32 = 0

            Dim data(_fullBlockSize) As Byte
            FillArray(data, &H0)
            For x = 0 To 10
                CheckpointIndex = GetLongChecksum(CheckpointIndex, MappingTableSectorArray)
            Next
            wrDwordArray(CheckpointIndex, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)

            wrDwordArray(ChainPhysicalSector, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)

            For x = 0 To MappingTableSectorArray.Count - 1
                wrDwordArray(MappingTableSectorArray(x), curSector * (_sectSize + _chunkSize) + pos, data)
                pos += 4
                curSector += PosCorrection(pos)
            Next
            For x = 0 To _freeBlocksBitmap.Count - 1
                wrDwordArray(_freeBlocksBitmap(x), curSector * (_sectSize + _chunkSize) + pos, data)
                pos += 4
                curSector += PosCorrection(pos)
            Next
            wrDwordArray(FreeBlockCount, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)
            For x = 0 To _systemBlocksBitmap.Count - 1
                wrDwordArray(_systemBlocksBitmap(x), curSector * (_sectSize + _chunkSize) + pos, data)
                pos += 4
                curSector += PosCorrection(pos)
            Next
            wrDwordArray(SystemBlockCount, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)
            For x = 0 To 9
                wrDwordArray(DataBlockArray(x), curSector * (_sectSize + _chunkSize) + pos, data)
                pos += 4
                curSector += PosCorrection(pos)
            Next
            For x = 0 To 2
                wrDwordArray(MappingTableBlockArray(x), curSector * (_sectSize + _chunkSize) + pos, data)
                pos += 4
                curSector += PosCorrection(pos)
            Next
            wrDwordArray(DataSectorNext, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)
            wrDwordArray(FreeSectorCountData, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)
            wrDwordArray(MappingTableSectorNext, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)
            wrDwordArray(FreeSectorCountMappingTable, curSector * (_sectSize + _chunkSize) + pos, data)
            pos += 4
            curSector += PosCorrection(pos)

            For x = 0 To sectorsCount - 1

                Dim si As New SectorInfoClass(_chunkSize)
                si.logSectorNumber = &HFFFFFF
                si.sectorFlag = &HFF
                si.sectorChecksum = si.GetSectorChecksum(data, x * (_sectSize + _chunkSize), _sectSize)
                si.GetBytes.CopyTo(data, _sectSize + (x * (_sectSize + _chunkSize)))
            Next
            '_sectSize + _chunkSize,
            Dim ffZone As UInt32 = (_sectSize + _chunkSize) * sectorsCount
            FillArray(data, &HFF, ffZone, _fullBlockSize - ffZone) 'ffIndex, _sectSize + _chunkSize) '_fullBlockSize - ffIndex)
            Return data
        End Function

        ''' <summary>
        ''' Returns the size of the block buffer
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawDataSize() As UInteger
            Return _fullBlockSize
        End Function

        ''' <summary>
        ''' Returns True if the checkpoint is empty
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function IsEmpty() As Boolean

            'checkpoints cannot be empty
            Return False
        End Function

        ''' <summary>
        ''' Returns the checkpoint information in text form
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function ToText() As String
            Dim resStr As String = ""

            resStr = resStr & "CheckpointIndex              = " & HexSTR(CheckpointIndex) & vbCrLf
            resStr = resStr & "ChainPhysicalSector          = " & HexSTR(ChainPhysicalSector) & vbCrLf
            resStr = resStr & "MappingTableSectorArray      = " & vbCrLf
            For x = 0 To _mappingTableSectors.Count - 1
                resStr = resStr & "                               " & HexSTR(_mappingTableSectors(x)) & vbCrLf
            Next

            resStr = resStr & "FreeBlocksBitmap             = " & vbCrLf
            For x = 0 To _freeBlocksBitmap.Count - 1
                resStr = resStr & "                               " & HexSTR(_freeBlocksBitmap(x)) & vbCrLf
            Next
            resStr = resStr & "FreeBlockCount               = " & HexSTR(FreeBlockCount) & vbCrLf

            resStr = resStr & "SystemBlocksBitmap           = " & vbCrLf
            For x = 0 To _systemBlocksBitmap.Count - 1
                resStr = resStr & "                               " & HexSTR(_systemBlocksBitmap(x)) & vbCrLf
            Next

            resStr = resStr & "SystemBlockCount             = " & HexSTR(SystemBlockCount) & vbCrLf

            resStr = resStr & "DataBlockArray               = " & vbCrLf
            For x = 0 To 9
                resStr = resStr & "                               " & HexSTR(DataBlockArray(x)) & vbCrLf
            Next

            resStr = resStr & "MappingTableBlockArray       = " & vbCrLf
            For x = 0 To 2
                resStr = resStr & "                               " & HexSTR(MappingTableBlockArray(x)) & vbCrLf
            Next

            resStr = resStr & "DataSectorNext               = " & HexSTR(DataSectorNext) & vbCrLf
            resStr = resStr & "FreeSectorCountData          = " & HexSTR(FreeSectorCountData) & vbCrLf

            resStr = resStr & "MappingTableSectorNext       = " & HexSTR(MappingTableSectorNext) & vbCrLf
            resStr = resStr & "FreeSectorCountMappingTable  = " & HexSTR(FreeSectorCountMappingTable) & vbCrLf

            Return resStr
        End Function

    End Class
End Module