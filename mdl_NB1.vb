Imports System
Imports System.IO
Imports System.Text

Friend Class NBD000FFClass

    Friend Const Version As String = "v1.01"

    Friend dataBlocksPerEntry As System.UInt32 = &HA
    Friend mappingTableBlocksPerEntry = &H3
    Friend checkpointsBlocksPerEntry = &H1

    ''' <summary>
    ''' List of partition headers
    ''' </summary>
    ''' <remarks></remarks>
    Friend W7PhdrList As New List(Of WP7PartitionHeader)
    ''' <summary>
    ''' List of original partition headers
    ''' </summary>
    ''' <remarks></remarks>
    Friend W7PhdrListOrig As New List(Of WP7PartitionHeader)


    ''' <summary>
    ''' Absolute addresses of partitions within the file
    ''' </summary>
    ''' <remarks></remarks>
    Friend W7StartAddr As New SortedList(Of Integer, Long)
    ''' <summary>
    ''' Name of the file the data was read from
    ''' </summary>
    ''' <remarks></remarks>
    Friend CurrentFileName As String = ""

    ''' <summary>
    ''' Number of blocks in the ROM file
    ''' </summary>
    ''' <remarks></remarks>
    Friend BlocksCount As System.UInt32 = 0
    ''' <summary>
    ''' Maximum possible number of blocks in the ROM
    ''' </summary>
    ''' <remarks></remarks>
    Friend BlocksCountMax As System.UInt32 = 0
    ''' <summary>
    ''' Number of sectors per block
    ''' </summary>
    ''' <remarks></remarks>
    Friend SectorsPerBlock As System.UInt32 = 0
    ''' <summary>
    ''' Sector size in bytes
    ''' </summary>
    ''' <remarks></remarks>
    Friend SectorSize As System.UInt32
    ''' <summary>
    ''' Size of the extra data
    ''' </summary>
    ''' <remarks></remarks>
    Friend ChunkSize As System.UInt32 = 0

    ''' <summary>
    ''' List of blocks to skip when dumping
    ''' </summary>
    ''' <remarks></remarks>
    Private skipBlocksList As New SortedList(Of System.UInt32, System.UInt32)

    ''' <summary>
    ''' For reading an NB file
    ''' </summary>
    ''' <remarks></remarks>
    Friend Sub New()

    End Sub



    ''' <summary>
    ''' For creating and writing a new NB file (partitions must be added later)
    ''' </summary>
    ''' <param name="blkCountMax">Maximum number of blocks</param>
    ''' <param name="sectPerBlock">Number of sectors per block</param>
    ''' <param name="sectSize">Sector size in bytes</param>
    ''' <remarks></remarks>
    Friend Sub New(blkCountMax As System.UInt32, sectPerBlock As System.UInt32, sectSize As System.UInt32, Optional ByVal eChunkSize As System.UInt32 = 6)
        BlocksCountMax = blkCountMax
        SectorsPerBlock = sectPerBlock
        SectorSize = sectSize
        ChunkSize = eChunkSize
    End Sub

    ''' <summary>
    ''' Reads from a file and returns a new NBD000FFClass instance
    ''' </summary>
    ''' <param name="fileName"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Shared Function Read(fileName As String) As NBD000FFClass
        Dim ePrompt As String = ""
        Dim NB1 As New NBD000FFClass
        Dim bRdr As New BinaryReader(File.OpenRead(fileName))

        Dim d0 As Long = FindSignature(0, bRdr.BaseStream, Sign_D000FF, 8)
        If d0 <> 0 Then '                                                                               D000FF signature not found\n
E1:         ePrompt = "Unknown file format"
            GoTo errPoint
        End If

        bRdr.BaseStream.Position = 7

        Dim mbCount As System.Int32 = bRdr.ReadUInt32 '                                                 Read the total number of blocks
        Dim mSize As System.UInt32 = bRdr.ReadUInt32 '                                                  Presumably this is the number of blocks in the entire ROM, including the user area

        NB1.BlocksCount = mbCount
        NB1.BlocksCountMax = mSize

        Dim sectSize As System.UInt32 = 0 '                                                             Size of the clean sector
        Dim chunkSize As System.UInt32 = 0 '                                                            Size of the extra data per sector

        Dim currentPartIndex As Integer = 0 '                                                           Index of the current partition
        Dim currentAbsBlock As Integer = 0

        Dim bAddr As System.UInt32 = 0
        Dim bSize As System.UInt32 = 0

        For i = 0 To mbCount - 1
            If (bRdr.BaseStream.Position + 4) >= bRdr.BaseStream.Length Then
                Exit For
            End If
            bAddr = bRdr.ReadUInt32
            bSize = bRdr.ReadUInt32
            Dim bArray() As Byte = bRdr.ReadBytes(bSize)

            If i = 0 Then '============================================================================ THIS IS THE VERY FIRST BLOCK
                Dim m1 As Long = FindSignatureArray(0, bArray, StringToBytesASC("FLPART10")) '          Look for the partition header
                If m1 = 0 Then '                                                                        at the very start of the block
                    Dim xsSize As System.UInt32 = &H1000
                    While True '----------------------------------------------------------------------- Loop through and find all headers
                        Dim w7Hdr As WP7PartitionHeader = WP7PartitionHeader.Read(bArray, m1, xsSize) ' Read from the array at offset m1
                        If w7Hdr.IsFLPART10 Then '                                                      The header is valid
                            If NB1.W7PhdrList.Count = 0 Then '                                          This is the very first header:
                                sectSize = w7Hdr.BytesPerSector '                                       Take the CLEAN sector size from the very first header
                                Dim clnBlockSize As System.UInt32 = _
                                    w7Hdr.SectorsPerBlock * w7Hdr.BytesPerSector '                      Size of the CLEAN block
                                chunkSize = (bSize - clnBlockSize) / w7Hdr.SectorsPerBlock '            Size of the extra data per sector

                                NB1.SectorSize = sectSize
                                NB1.SectorsPerBlock = w7Hdr.SectorsPerBlock
                                NB1.ChunkSize = chunkSize

                                xsSize = sectSize '                                                     Sector size determined
                                w7Hdr = WP7PartitionHeader.Read(bArray, m1, xsSize) '                   And re-read the very first header again, now with the computed sector size

                                w7Hdr.addrFirst = bSize * 2 '                                           Start address of its partition = (block size * 2)
                                w7Hdr.addrNext = w7Hdr.addrFirst + (w7Hdr.LogicalBlocks * bSize) '      Compute the address of the next partition from the length of the current one

                                '                                                                       - FINISHED WITH THE FIRST HEADER -

                            Else '                                                                      And this is not the first header:
                                w7Hdr.addrFirst = NB1.W7PhdrList(NB1.W7PhdrList.Count - 1).addrNext '   - take the start address of its partition from the previous header                            
                                w7Hdr.addrNext = w7Hdr.addrFirst + (w7Hdr.LogicalBlocks * bSize) '      Compute the address of the next partition from the length of the current one
                            End If
                            NB1.W7PhdrList.Add(w7Hdr)
                            NB1.W7PhdrListOrig.Add(w7Hdr.Clone)
                            m1 = m1 + sectSize + chunkSize '                                            Move to the address of the next header
                        Else
                            Exit While
                        End If
                    End While '------------------------------------------------------------------------ All headers have been read
                Else '                                                                                  header not found - format error
                    GoTo E1
                End If
            ElseIf i = 1 Then '======================================================================== SECOND BLOCK
                Dim m1 As Long = FindSignatureArray(0, bArray, StringToBytesASC("FLPART10")) '          Look for the partition header in the backup partition table
                If m1 <> 0 Then GoTo E1 '                                                               Missing - format error
            Else '===================================================================================== PARTITION DATA BLOCKS
                If i = 2 Then '                                                                         Only for the very first partition
                    NB1.W7StartAddr.Add(currentPartIndex, bRdr.BaseStream.Position - bSize - 8)
                End If
            End If
            currentAbsBlock = (bAddr \ bSize)
            If bAddr >= NB1.W7PhdrList(currentPartIndex).addrNext Then
                currentPartIndex += 1
                NB1.W7StartAddr.Add(currentPartIndex, bRdr.BaseStream.Position - bSize - 8)
            End If

            Dim logSign As System.UInt32 = BitConverter.ToUInt32(bArray, 0)
            If logSign = &H474F4C01UI Or _
               logSign = &H474F4C02UI Or _
               logSign = &H474F4C03UI Or _
               logSign = &H474F4C04UI Then
                NB1.skipBlocksList.Add(currentAbsBlock, currentAbsBlock)
                Dim sSize As System.UInt32 = NB1.W7PhdrList(currentPartIndex).BytesPerSector
                For x = 0 To NB1.W7PhdrList(currentPartIndex).SectorsPerBlock - 1
                    'LOG_TYPE_CHECKPOINT = 0x474F4C01
                    Dim sArray(0 To sSize + chunkSize - 1) As Byte
                    Array.Copy(bArray, x * (sSize + chunkSize), sArray, 0, (sSize + chunkSize))
                    Dim lSign As System.UInt32 = BitConverter.ToUInt32(sArray, 0)
                    If logSign = &H474F4C01UI Or _
                       logSign = &H474F4C02UI Or _
                       logSign = &H474F4C03UI Or _
                       logSign = &H474F4C04UI Then
                        Dim lEntry As LogEntryClass = LogEntryClass.Read(sArray)
                        If (lEntry.EntryType = &H474F4C01UI) And (lEntry.MappingTableFlag = 0) Then

                            'If Not NB1.skipBlocksList.ContainsKey(lEntry.PtrBlocksArray) Then
                            '    NB1.skipBlocksList.Add(lEntry.PtrBlocksArray, currentAbsBlock)
                            'End If
                            For Each bl In lEntry.PtrBlocksArray
                                NB1.skipBlocksList.Add(bl, currentAbsBlock)
                            Next
                        ElseIf (lEntry.EntryType = &H474F4C02UI) And (lEntry.MappingTableFlag = 1) Then
                            For Each bl In lEntry.PtrBlocksArray
                                'NB1.skipBlocksList.Add(lEntry.PtrBlocksArray, currentAbsBlock)
                                NB1.skipBlocksList.Add(bl, currentAbsBlock)
                            Next
                        End If
                    End If
                Next
            End If
        Next

        bRdr.Close()
        bRdr.Dispose()
        bRdr = Nothing
        NB1.CurrentFileName = fileName
        Return NB1

errPoint:
        bRdr.Close()
        bRdr.Dispose()
        bRdr = Nothing
        NB1 = Nothing
        Throw New System.Exception(ePrompt)
    End Function

    ''' <summary>
    ''' Create a payload file from D000FF
    ''' </summary>
    ''' <param name="payloadFile"></param>
    ''' <remarks></remarks>
    Friend Sub WritePayload(payloadFile As String)

        Dim bRdr As BinaryReader
        bRdr = New BinaryReader(File.OpenRead(CurrentFileName))

        RemoveFile(payloadFile)
        Dim fWrt As FileStream = File.Open(payloadFile, FileMode.Create, FileAccess.ReadWrite)

        Dim hBlock(0 To (SectorSize * SectorsPerBlock) - 1) As Byte
        fillArray(hBlock, &HFF)

        fWrt.Write(hBlock, 0, hBlock.Length) '                              Write the two header blocks
        fWrt.Write(hBlock, 0, hBlock.Length)

        For pIndex = 0 To W7PhdrList.Count - 1
            Dim W7Hdr As WP7PartitionHeader = W7PhdrList(pIndex)

            If Not W7StartAddr.ContainsKey(pIndex) Then
                W7Hdr.PhysicalBlocks = 0
                W7Hdr.LogicalBlocks = 0
                Continue For
            End If

            bRdr.BaseStream.Position = W7StartAddr(pIndex)

            Dim stAddr As System.Int32 = W7PhdrList(pIndex).addrFirst
            Dim enAddr As System.Int32 = W7PhdrList(pIndex).addrNext

            If (W7Hdr.PartitionFlags And 1) <> 0 Then '-------------------- DIRECT_MAPPING
                Dim bCount As System.UInt32 = 0
                While True
                    If bRdr.BaseStream.Position = bRdr.BaseStream.Length Then Exit While
                    Dim bAddr As System.UInt32 = bRdr.ReadUInt32
                    Dim bSize As System.UInt32 = bRdr.ReadUInt32
                    If bAddr >= enAddr Then Exit While
                    Dim xBlock() As Byte = bRdr.ReadBytes(bSize)
                    For s = 0 To SectorsPerBlock - 1
                        Dim sArray(0 To SectorSize - 1) As Byte
                        Array.Copy(xBlock, s * (SectorSize + ChunkSize), sArray, 0, SectorSize)
                        fWrt.Write(sArray, 0, sArray.Length)
                    Next
                    ''fWrt.Write(xBlock, 0, xBlock.Length)
                    bCount += 1
                End While
                W7Hdr.PhysicalBlocks = bCount
                W7Hdr.LogicalBlocks = bCount
            Else '--------------------------------------------------------- LEVELED
                Dim xList As New SortedList(Of System.UInt32, Byte()) '     Collection of sectors
                While True
                    If bRdr.BaseStream.Position = bRdr.BaseStream.Length Then Exit While
                    Dim bAddr As System.UInt32 = bRdr.ReadUInt32
                    Dim bSize As System.UInt32 = bRdr.ReadUInt32
                    Dim currAbsBlock As System.UInt32 = (bAddr \ bSize)
                    Dim xBlock() As Byte = bRdr.ReadBytes(bSize)
                    If skipBlocksList.ContainsKey(currAbsBlock) Then Continue While
                    If bAddr >= enAddr Then Exit While
                    For i = 0 To SectorsPerBlock - 1
                        Dim sArray(0 To SectorSize - 1) As Byte
                        Dim xArray(0 To ChunkSize - 1) As Byte
                        Array.Copy(xBlock, (i * (SectorSize + ChunkSize)) + SectorSize, xArray, 0, ChunkSize)
                        Array.Copy(xBlock, i * (SectorSize + ChunkSize), sArray, 0, SectorSize)
                        If SectorIsEmpty(xArray) Then Continue For '            extra data = FF FF FF FF FF FF - skip

                        If xArray(5) <> &HFF Then Continue For
                        Dim sNumber As System.UInt32 = (BitConverter.ToUInt32(xArray, 0) And &HFFFFFF)
                        If sNumber = &HFFFFFF Then Continue For

                        If xList.ContainsKey(sNumber) Then xList.Remove(sNumber)
                        xList.Add(sNumber, sArray)
                    Next
                End While
                Dim bCount As System.UInt32 = xList.Count \ SectorsPerBlock
                If (xList.Count Mod SectorsPerBlock) <> 0 Then bCount += 1
                Dim sCount As Integer = 0
                For b = 0 To bCount - 1
                    Dim xBlock(0 To (SectorSize * SectorsPerBlock) - 1) As Byte
                    fillArray(xBlock, &HFF)
                    For s = 0 To SectorsPerBlock - 1
                        If sCount >= xList.Count Then
                            Exit For
                        End If
                        If xList.ContainsKey((b * SectorsPerBlock) + s) Then xList.Values((b * SectorsPerBlock) + s).CopyTo(xBlock, s * SectorSize)
                        sCount += 1
                        If sCount >= xList.Count Then
                            Exit For
                        End If
                    Next
                    fWrt.Write(xBlock, 0, xBlock.Length)
                Next
                W7Hdr.PhysicalBlocks = bCount
                W7Hdr.LogicalBlocks = bCount
            End If
        Next

        fWrt.Position = 0 '---------------------------------------------------------------------------- Write the main partition table
        For Each w7hdr As WP7PartitionHeader In W7PhdrList
            w7hdr.ChecksumWritten = w7hdr.RealChecksum '                                                Update the checksums
            w7hdr.Write(fWrt)
        Next
        fWrt.Position = SectorsPerBlock * SectorSize '-------------------------------------------------- Write the backup partition table (ORIGINAL!!! HEADERS!!!)
        For Each w7hdr As WP7PartitionHeader In W7PhdrListOrig
            w7hdr.Write(fWrt)
        Next

        fWrt.Position = fWrt.Length '                                                                   Append the original data to the end of the file
        fWrt.Write(osbApp.appGuidMain.ToByteArray, 0, 16) '                                             Start marker - osb GUID
        fWrt.Write(BitConverter.GetBytes(BlocksCount), 0, 4) '                                          Write the original number of blocks
        fWrt.Write(BitConverter.GetBytes(BlocksCountMax), 0, 4) '                                       Write the original value (presumably the number of blocks in the entire ROM, including the user area)
        fWrt.Write(BitConverter.GetBytes(0), 0, 4)
        fWrt.Write(BitConverter.GetBytes(0), 0, 4)

        fWrt.Close()
        fWrt.Dispose()
        fWrt = Nothing

        bRdr.Close()
        bRdr.Dispose()
        bRdr = Nothing

    End Sub

    ''' <summary>
    ''' Write the NB header to the stream
    ''' </summary>
    ''' <param name="stream"></param>
    ''' <param name="fullSectSize"></param>
    ''' <param name="chunkSize"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function WriteNbHeaderToStream(ByRef stream As IO.FileStream, ByVal fullSectSize As UInt32, ByVal chunkSize As UInt32) As UInt64
        Dim result As UInt64 = 0
        For Each part In W7PhdrList
            part.ChecksumWritten = part.RealChecksum
            result += part.Write(stream, fullSectSize - chunkSize)

            Dim xBlock(chunkSize) As Byte '                                            Data array the size of a full block including extra data
            fillArray(xBlock, &HFF)
            stream.Write(xBlock, 0, chunkSize)
        Next
        Return result
    End Function


    ''' <summary>
    ''' Enumeration of the possible results of writing a partition
    ''' </summary>
    ''' <remarks></remarks>
    Friend Enum WritePartitionState As UInt32
        Success = 0
        FileInaccessible = 1
        BadArguments = 2
        TooBigPartition = 3
    End Enum

    ''' <summary>
    ''' Structure holding the result of writing a partition to a file
    ''' </summary>
    ''' <remarks></remarks>
    Friend Structure WritePartitionResult
        Public state As WritePartitionState
        Public physicalBlocks As UInt32
        Public logicalBlocks As UInt32
    End Structure

    ''' <summary>
    ''' Write a directly mapped partition.
    ''' </summary>
    ''' <param name="bWrt"></param>
    ''' <param name="part"></param>
    ''' <param name="xBlock"></param>
    ''' <param name="currentBlockAddr"></param>
    ''' <param name="chunkSize"></param>
    ''' <param name="globalOffset"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function WriteDirectMappedPartition(ByVal bWrt As BinaryWriter, ByVal part As WP7PartitionHeader, ByVal xBlock() As Byte, ByRef currentBlockAddr As UInt32, ByVal chunkSize As UInt32, ByRef globalOffset As Int32) As WritePartitionResult
        Dim result As New WritePartitionResult
        If bWrt Is Nothing Or part Is Nothing Or xBlock Is Nothing Or chunkSize = 0 Then
            result.state = WritePartitionState.BadArguments
            Return result
        End If

        Dim fullSectSize, fullBlockSize, sectorSize, blockSize, sectorsPerBlock As UInt32
        GetBlockSectorInfo(part, fullSectSize, fullBlockSize, sectorSize, blockSize, sectorsPerBlock, chunkSize)

        Dim realBlockOffset As UInt32 = GetOutputFileOffset(currentBlockAddr, fullBlockSize)
        bWrt.Seek(globalOffset + realBlockOffset, SeekOrigin.Begin)
        Dim bRdr As BinaryReader
        Try
            bRdr = New BinaryReader(File.OpenRead(part.partFileName))
        Catch ex As Exception
            result.state = WritePartitionState.FileInaccessible
            Return result
        End Try
        Dim fileLen As Long = bRdr.BaseStream.Length
        Dim psCount As System.UInt32 = (fileLen \ part.BytesPerSector) '                         Number of sectors
        If (fileLen Mod part.BytesPerSector) <> 0 Then psCount += 1
        Dim blCount As System.UInt32 = psCount \ part.SectorsPerBlock '                      Number of blocks needed to build the partition
        If (psCount Mod part.SectorsPerBlock) <> 0 Then blCount += 1

        Dim sNumber As System.UInt32 = 0
        For bNumber = 0 To blCount - 1
            'bWrt.Seek(
            fillArray(xBlock, &HFF)
            For s = 0 To part.SectorsPerBlock - 1
                If sNumber >= psCount Then Exit For
                Dim sArray() As Byte = bRdr.ReadBytes(part.BytesPerSector)
                sArray.CopyTo(xBlock, fullSectSize * s)
                sNumber += 1
            Next

            bWrt.Write(currentBlockAddr) '                                              Write the block address
            bWrt.Write(fullBlockSize) '                                                 Write the block length
            bWrt.Write(xBlock) '                                                        Write the block itself
            currentBlockAddr += fullBlockSize '                                         Advance the block address
        Next
        bRdr.Close()
        bRdr.Dispose()
        bRdr = Nothing

        ' even though there are no system blocks with DIRECT MAPPING (FullReservedBlocks = ReservedBlocks),
        ' we still stick to the reservation concept
        Dim reservedBlocks As UInt32 = part.FullReservedBlocks
        If part.ReserveMethod = WP7PartitionHeader.ReserveBlockMethod.Normal Then
            reservedBlocks = part.ReservedBlocks
        End If

        currentBlockAddr += fullBlockSize * reservedBlocks
        globalOffset -= reservedBlocks * (fullBlockSize + 8)
        result.physicalBlocks = blCount
        result.logicalBlocks = blCount + reservedBlocks

        Return result
    End Function


    ''' <summary>
    ''' Shift partitions with an address >= minimalOffset by shift.
    ''' </summary>
    ''' <param name="minimalOffset"></param>
    ''' <param name="shift"></param>
    ''' <param name="logBlkList"></param>
    ''' <param name="chkBlkList"></param>
    ''' <param name="mapBlkList"></param>
    ''' <param name="datBlkList"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function ShiftOffsets(ByVal minimalOffset As UInt32, ByVal shift As Int32, _
                                ByRef logBlkList As SortedList(Of UInt32, LogBlockClass), _
                                ByRef chkBlkList As SortedList(Of UInt32, CheckpointBlockClass), _
                                ByRef mapBlkList As SortedList(Of UInt32, MappingTableBlock), _
                                ByRef datBlkList As SortedList(Of UInt32, DataBlockClass)) As Boolean
        Dim shiftedOne As Boolean = False
        For Each block In logBlkList
            If block.Value.BlockAddress >= minimalOffset Then
                block.Value.BlockAddressOffset += shift
                shiftedOne = True
            End If
        Next
        For Each block In chkBlkList
            If block.Value.BlockAddress >= minimalOffset Then
                block.Value.BlockAddressOffset += shift
                shiftedOne = True
            End If
        Next

        For Each block In mapBlkList
            If block.Value.BlockAddress >= minimalOffset Then
                block.Value.BlockAddressOffset += shift
                shiftedOne = True
            End If
        Next

        For Each block In datBlkList
            If block.Value.BlockAddress >= minimalOffset Then
                block.Value.BlockAddressOffset += shift
                shiftedOne = True
            End If
        Next
        Return shiftedOne

    End Function

    ''' <summary>
    ''' Determine the offset in the output file
    ''' </summary>
    ''' <param name="currentOffset"></param>
    ''' <param name="blockSize"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function GetOutputFileOffset(ByVal currentOffset As UInt32, ByVal blockSize As UInt32) As UInt32
        Return 15 + currentOffset / blockSize * (blockSize + 8)
    End Function

    ''' <summary>
    ''' Determine the block number from its address
    ''' </summary>
    ''' <param name="addr"></param>
    ''' <param name="blockSize"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Private Function BlockAddrToBlockNum(ByVal addr As UInt32, ByVal blockSize As UInt32) As UInt32
        If addr = &HFFFFFFFFUI Then
            Return addr
        End If
        Return addr \ blockSize
    End Function


    ''' <summary>
    ''' Compute the various variables needed to build the partition
    ''' </summary>
    ''' <param name="part"></param>
    ''' <param name="fullSectSize"></param>
    ''' <param name="fullBlockSize"></param>
    ''' <param name="sectSize"></param>
    ''' <param name="blockSize"></param>
    ''' <param name="sectorsPerBlock"></param>
    ''' <param name="chunkSize"></param>
    ''' <remarks></remarks>
    Private Sub GetBlockSectorInfo(ByVal part As WP7PartitionHeader, ByRef fullSectSize As UInt32, ByRef fullBlockSize As UInt32, _
                                   ByRef sectSize As UInt32, ByRef blockSize As UInt32, ByRef sectorsPerBlock As UInt32, ByVal chunkSize As UInt32)



        sectSize = part.BytesPerSector
        blockSize = sectSize * part.SectorsPerBlock

        fullSectSize = part.BytesPerSector + chunkSize
        fullBlockSize = fullSectSize * part.SectorsPerBlock

        sectorsPerBlock = part.SectorsPerBlock
    End Sub


    ''' <summary>
    ''' Write a LogChain partition 
    ''' </summary>
    ''' <param name="bWrt"></param>
    ''' <param name="part"></param>
    ''' <param name="xBlock"></param>
    ''' <param name="currentBlockAddr"></param>
    ''' <param name="chunkSize"></param>
    ''' <param name="globalOffset"></param>
    ''' <returns></returns>
    ''' <remarks>ATTENTION! part.FileName must have been read.</remarks>
    Private Function WriteLogChainPartition(ByVal bWrt As BinaryWriter, ByVal part As WP7PartitionHeader, ByVal xBlock() As Byte, ByRef currentBlockAddr As UInt32, ByVal chunkSize As UInt32, ByRef globalOffset As Int32, ByVal logBlockLimit As UInt32) As WritePartitionResult

        Dim blockModifier As Int32 = 0
        Dim result As New WritePartitionResult
        Dim tempGlobalOffset As Int32 = 0

        Dim startBlockAddr As UInt32 = currentBlockAddr

        Dim fullSectSize, fullBlockSize, sectorSize, blockSize, sectorsPerBlock As UInt32
        GetBlockSectorInfo(part, fullSectSize, fullBlockSize, sectorSize, blockSize, sectorsPerBlock, chunkSize)

        Dim logentries As New List(Of LogEntryClass)
        Dim logBlkList As New SortedList(Of System.UInt32, LogBlockClass) '             List of LOG blocks
        Dim chkBlkList As New SortedList(Of System.UInt32, CheckpointBlockClass) '                    List of CHECKPOINT blocks
        Dim mapBlkList As New SortedList(Of System.UInt32, MappingTableBlock) '                    List of MAPPING TABLE blocks
        Dim datBlkList As New SortedList(Of System.UInt32, DataBlockClass) '                    List of data blocks

        Dim logIndex As Integer = 0
        Dim chkIndex As Integer = 0
        Dim mapIndex As Integer = 0

        Dim firstPhysicalSector As UInt32 = currentBlockAddr / fullBlockSize * sectorsPerBlock

        ' create the first two logical blocks
        Dim lb1 As New LogBlockClass(sectorSize, sectorsPerBlock, chunkSize)
        lb1.BlockAddress = currentBlockAddr
        logBlkList.Add(currentBlockAddr, lb1)

        currentBlockAddr += fullBlockSize

        lb1 = New LogBlockClass(sectorSize, sectorsPerBlock, chunkSize)
        logBlkList.Add(currentBlockAddr, lb1)
        lb1.BlockAddress = currentBlockAddr
        currentBlockAddr += fullBlockSize

        fillArray(xBlock, &HFF)

        ' first checkpoint
        Dim checkpoint1 As New CheckpointBlockClass(sectorSize, sectorsPerBlock, chunkSize)
        With checkpoint1
            .BlockAddress = currentBlockAddr
            chkBlkList.Add(currentBlockAddr, checkpoint1)
            currentBlockAddr += fullBlockSize
        End With

        'Dim prevBlockAbs As UInt32 = &HFFFFFFFFUI
        Dim curBlockAbs As UInt32 = currentBlockAddr / fullBlockSize

        ' first LOGENTRY pointing to the checkpoint:
        Dim logEntryFirst As New LogEntryClass
        With logEntryFirst
            .EntryType = LogEntryType.LOG_TYPE_CHECKPOINT '&H474F4C01UI
            .PrevBlock = 0 'prevBlockAbs
            .ChainFirstPhysSector = &HFFFFFFFFUI
            .Reserved0 = 0
            .Reserved1 = 0
            .MappingTableFlag = 0
            .BlocksPerEntry = 1
            .PtrBlocksArray.Add((chkBlkList.Keys(chkIndex) \ fullBlockSize))

            'add the LOGENTRY to the first logical block
            logBlkList.Values(logIndex).Entries.Add(logEntryFirst)
            logentries.Add(logEntryFirst)
        End With

        Dim bRdr As BinaryReader
        Try
            bRdr = New BinaryReader(File.OpenRead(part.partFileName))
        Catch ex As Exception
            result.state = WritePartitionState.FileInaccessible
            Return result
        End Try
        Dim fileLen As Long = bRdr.BaseStream.Length
        Dim currSectorNumber As System.UInt32 = 0
        Dim totalBlocks As UInt32 = fileLen / (sectorsPerBlock * sectorSize)
        Dim modResult As UInt32 = totalBlocks Mod (sectorsPerBlock * sectorSize)
        If modResult > 0 Or totalBlocks = 0 Then totalBlocks += 1

        Dim tempDataBlocksCount As UInt32 = 0
        Dim alreadyKnownCount As UInt32 = 0
        While True
            Dim dataBlock(0 To fullBlockSize - 1) As Byte
            fillArray(dataBlock, &HFF)
            Dim sArray() As Byte = bRdr.ReadBytes(blockSize)
            Dim blockArrayAddr As UInt32 = currentBlockAddr
            If datBlkList.Count < totalBlocks Then
                Dim db1 As New DataBlockClass(sectorSize, sectorsPerBlock, chunkSize, sArray, sArray.Length, tempDataBlocksCount)
                db1.BlockAddress = currentBlockAddr
                datBlkList.Add(currentBlockAddr, db1)
                currentBlockAddr += fullBlockSize
                tempDataBlocksCount += 1
                alreadyKnownCount += 1
            Else
                Exit While
            End If

            If alreadyKnownCount >= sectorsPerBlock * 10 Then
                Dim newLb As New LogBlockClass(sectorSize, sectorsPerBlock, chunkSize)
                newLb.BlockAddress = currentBlockAddr
                logBlkList.Add(currentBlockAddr, newLb)
                currentBlockAddr += fullBlockSize
                alreadyKnownCount = 0
            End If

            Dim logBlock As LogBlockClass = logBlkList.Values(logIndex)
            If logBlock.Entries.Count = sectorsPerBlock Then
                If logIndex = logBlkList.Count - 1 Then
                    'if there are no free blocks, add one more empty logical block
                    Dim newLb As New LogBlockClass(sectorSize, sectorsPerBlock, chunkSize)
                    newLb.BlockAddress = currentBlockAddr
                    logBlkList.Add(currentBlockAddr, newLb)
                    currentBlockAddr += fullBlockSize
                End If

                'move on to the next logical block
                logIndex += 1
                logBlock = logBlkList.Values(logIndex)
            End If

            Dim le As LogEntryClass = Nothing
            If logentries.Count > 0 Then
                le = logentries(logentries.Count - 1)
                If le.EntryType <> LogEntryType.LOG_TYPE_BLOCK_WRITES Or le.MappingTableFlag = 1 Then
                    le = Nothing
                End If
            End If
            Dim createNew As Boolean = False
            If le Is Nothing Then
                createNew = True
            ElseIf le.PtrBlocksArray.Count >= 10 Then
                createNew = True
            End If
            If createNew = True Then
                le = New LogEntryClass
                With le
                    .EntryType = LogEntryType.LOG_TYPE_BLOCK_WRITES '&H474F4C02UI
                    .PrevBlock = 0 'prevBlockAbs
                    .ChainFirstPhysSector = firstPhysicalSector
                    .Reserved0 = 0
                    .Reserved1 = 0
                    .MappingTableFlag = 0
                    logBlock.Entries.Add(le)
                    logentries.Add(le)
                End With
            End If
            le.PtrBlocksArray.Add(blockArrayAddr \ fullBlockSize)
            le.BlocksPerEntry += 1
            'End If
            'logBlkList.Values(logIndex).Entries.Add(le)
            'logentries.Add(le)
        End While

        Dim FreeSectorCountData As UInt32 = sectorsPerBlock
        If datBlkList.Count > 0 Then
            For x = 0 To datBlkList.Values.Count - 1
                Dim db As DataBlockClass = datBlkList.Values(x)
                Dim sectors As UInt32 = db.UsedSectors
                If sectors <> 0 Then
                    FreeSectorCountData = sectorsPerBlock - sectors
                End If
            Next
        End If

        If logentries.Count > 0 Then
            Dim le As LogEntryClass = Nothing
            If logentries(logentries.Count - 1).BlocksPerEntry < 10 Then
                le = logentries(logentries.Count - 1)
                If le.EntryType <> LogEntryType.LOG_TYPE_BLOCK_WRITES Then
                    le = Nothing
                End If

                If le IsNot Nothing Then
                    Dim curBlocksPerEntry As UInt32 = le.BlocksPerEntry
                    For x As UInt32 = curBlocksPerEntry To 9UI
                        Dim newDb As New DataBlockClass(sectorSize, sectorsPerBlock, chunkSize)
                        newDb.BlockAddress = currentBlockAddr
                        datBlkList.Add(currentBlockAddr, newDb)
                        le.PtrBlocksArray.Add(currentBlockAddr \ fullBlockSize)
                        le.BlocksPerEntry += 1
                        currentBlockAddr += fullBlockSize
                        tempDataBlocksCount += 1
                    Next
                End If
            End If
        End If
        bRdr.Close()

        Dim entrySize As UInt32 = 3
        If part.PhysicalBlocks * sectorsPerBlock < &H10000 Then
            entrySize = 2
        End If

        Dim blocksOverall As Integer = datBlkList.Count
        Dim maxEntries As UInt32
        'For x = 0 To 2

        Dim nMtBlock As New MappingTableBlock(sectorSize, sectorsPerBlock, chunkSize, entrySize)
        nMtBlock.BlockAddress = currentBlockAddr
        mapBlkList.Add(currentBlockAddr, nMtBlock)
        maxEntries = nMtBlock.GetMaximumEntriesCount()
        currentBlockAddr += fullBlockSize
        'Next
        Dim mapBlkIndex As UInt32 = 0
        Dim tempMapBlock As MappingTableBlock = mapBlkList.Values(mapBlkIndex)
        For x = 0 To blocksOverall - 1
            Dim block As DataBlockClass = datBlkList.Values(x)
            Dim usedSectors As UInt32 = block.UsedSectors()
            For j = 0 To usedSectors - 1
                Dim mtEntry As New MappingTableEntry((block.BlockAddress - logBlkList.Values(0).BlockAddress) \ fullSectSize + j, entrySize)
                If tempMapBlock.Entries.Count = maxEntries Then
                    mapBlkIndex += 1
                    If mapBlkIndex = mapBlkList.Count Then
                        'not enough mapping table blocks. Add one more
                        Dim mtBlock As New MappingTableBlock(sectorSize, sectorsPerBlock, chunkSize, entrySize)
                        mtBlock.BlockAddress = currentBlockAddr
                        mapBlkList.Add(currentBlockAddr, mtBlock)
                        currentBlockAddr += fullBlockSize
                    End If
                    tempMapBlock = mapBlkList.Values(mapBlkIndex)
                End If
                tempMapBlock.Entries.Add(mtEntry)
            Next
        Next

        Dim MappingTableSectorNext As Int32 = -1
        Dim FreeSectorCountMappingTable As UInt32 = sectorsPerBlock
        For x = 0 To mapBlkList.Count - 1
            Dim mt As MappingTableBlock = mapBlkList.Values(x)
            If mt.IsEmpty = False Then
                Dim usedSectors As UInt32 = mt.UsedSectors
                FreeSectorCountMappingTable = sectorsPerBlock - usedSectors
                MappingTableSectorNext = (mt.BlockAddress / fullBlockSize * sectorsPerBlock) + usedSectors
            Else
                If MappingTableSectorNext = -1 Then
                    MappingTableSectorNext = mt.BlockAddress / fullBlockSize * sectorsPerBlock
                End If
            End If
            'If mt.IsEmpty = False Then
        Next

        'add two more mapping table blocks
        For x = 0 To 1
            Dim mtBlock As New MappingTableBlock(sectorSize, sectorsPerBlock, chunkSize, entrySize)
            mtBlock.BlockAddress = currentBlockAddr
            mapBlkList.Add(currentBlockAddr, mtBlock)
            currentBlockAddr += fullBlockSize
        Next

        Dim mtBlockLogEntriesCount As UInt32 = mapBlkList.Count \ 3
        If mapBlkList.Count < 3 Or (mapBlkList.Count Mod 3) > 0 Then
            mtBlockLogEntriesCount += 1
        End If

        For x = 0 To mtBlockLogEntriesCount - 1
            Dim count As UInt32 = 3
            If x = mtBlockLogEntriesCount - 1 Then
                count = mapBlkList.Count Mod 3
                If count = 0 Then count = 3
            End If
            Dim logEntryMapping As New LogEntryClass
            ' ... and writing them into the LogEntry
            With logEntryMapping
                .EntryType = LogEntryType.LOG_TYPE_BLOCK_WRITES
                .PrevBlock = 0
                .ChainFirstPhysSector = firstPhysicalSector
                .Reserved0 = 0
                .Reserved1 = 0
                .MappingTableFlag = 1
                .BlocksPerEntry = count
                For bl = 0 To count - 1
                    .PtrBlocksArray.Add(mapBlkList.Values(x * 3 + bl).BlockAddress / fullBlockSize)
                Next
                '.PtrBlocksArray.Add(mapBlkList.Values(0).BlockAddress / fullBlockSize)  '(chkBlkList.Keys(chkIndex) \ fullBlockSize)
                '.PtrBlocksArray.Add(mapBlkList.Values(1).BlockAddress / fullBlockSize)
                '.PtrBlocksArray.Add(mapBlkList.Values(2).BlockAddress / fullBlockSize)
                If logBlkList.Values(logIndex).Entries.Count = sectorsPerBlock Then
                    logIndex += 1
                    'prevBlockAbs = curBlockAbs
                    curBlockAbs += 1
                    If logIndex = logBlkList.Count Then
                        'add one more empty logical block
                        Dim newLb As New LogBlockClass(sectorSize, sectorsPerBlock, chunkSize)
                        newLb.BlockAddress = currentBlockAddr
                        logBlkList.Add(currentBlockAddr, newLb)
                        currentBlockAddr += fullBlockSize
                    End If
                End If

                logBlkList.Values(logIndex).Entries.Add(logEntryMapping)
                logentries.Add(logEntryMapping)
            End With
        Next
        'there must always be at least one COMPLETELY free logical block

        Dim bCreateNewBlock As Boolean = False
        'separated the block in this clever way so that the names do not clash
        If True Then
            If logIndex = logBlkList.Count - 1 Then
                bCreateNewBlock = True
            Else
                Dim templb As LogBlockClass = Nothing
                If logIndex < logBlkList.Count And logBlkList.Count > 1 Then
                    templb = logBlkList.Values(logBlkList.Count - 2)
                    If templb.Entries.Count >= (sectorsPerBlock - 3) Then
                        bCreateNewBlock = True
                    End If
                End If
            End If
        End If
        If bCreateNewBlock = True Then
            Dim newLb1 As New LogBlockClass(sectorSize, sectorsPerBlock, chunkSize)
            newLb1.BlockAddress = currentBlockAddr
            logBlkList.Add(currentBlockAddr, newLb1)
            currentBlockAddr += fullBlockSize
        End If

        ' --- LAST CHECKPOINT
        Dim checkpoint2 As New CheckpointBlockClass(sectorSize, sectorsPerBlock, chunkSize)
        'checkpoint2.BlockAddress = currentBlockAddr
        'chkBlkList.Add(currentBlockAddr, checkpoint2)
        With checkpoint2
            .BlockAddress = currentBlockAddr
            If datBlkList.Count > 0 Then
                'walk the data blocks and find the first unused sector
                For x = 0 To datBlkList.Count - 1
                    Dim db As DataBlockClass = datBlkList.Values(x)
                    Dim sectors As UInt32 = db.UsedSectors
                    If sectors <> sectorsPerBlock Then
                        Dim key As UInt32 = datBlkList.Keys(x)
                        .DataSectorNext = key / fullBlockSize * sectorsPerBlock + sectors
                        Exit For
                    End If
                Next
                '.DataSectorNext = datBlkList.Keys(0) / fullBlockSize * sectorsPerBlock + 1
            End If
            .FreeSectorCountMappingTable = FreeSectorCountMappingTable '&H40 - usedSectors Mod 32
            .MappingTableSectorNext = MappingTableSectorNext
            chkBlkList.Add(currentBlockAddr, checkpoint2)
            currentBlockAddr += fullBlockSize
        End With

        ' last LOGENTRY checkpoint
        Dim logEntryLatest As New LogEntryClass
        With logEntryLatest
            .EntryType = LogEntryType.LOG_TYPE_CHECKPOINT '&H474F4C01UI
            .PrevBlock = 0 'prevBlockAbs
            .ChainFirstPhysSector = firstPhysicalSector
            .Reserved0 = 0
            .Reserved1 = 0
            .MappingTableFlag = 0
            .BlocksPerEntry = 1
            .PtrBlocksArray.Add(checkpoint2.BlockAddress / fullBlockSize)  '(chkBlkList.Keys(chkIndex) \ fullBlockSize)
            If logBlkList.Values(logIndex).Entries.Count = sectorsPerBlock Then
                logIndex += 1
                'prevBlockAbs = curBlockAbs
                curBlockAbs += 1
                If logIndex = logBlkList.Count Then
                    'add one more empty logical block
                    Dim newLb As New LogBlockClass(sectorSize, sectorsPerBlock, chunkSize)
                    newLb.BlockAddress = currentBlockAddr
                    logBlkList.Add(currentBlockAddr, newLb)
                    currentBlockAddr += fullBlockSize
                End If
            End If

            'add the LOGENTRY to the logical block
            logBlkList.Values(logIndex).Entries.Add(logEntryLatest)
            logentries.Add(logEntryLatest)
        End With
        ' --- 


        'Fix up the first checkpoint

        '???
        'checkpoint1.FreeBlockCount = 2 + logBlkList.Count + chkBlkList.Count + datBlkList.Count + mapBlkList.Count - 3
        'checkpoint1.SystemBlockCount = 3

        Dim systemBlocks As UInt32 = chkBlkList.Count + mapBlkList.Count + logBlkList.Count
        Dim reservedBlocks As UInt32 = part.ReservedBlocks
        If part.ReserveMethod = WP7PartitionHeader.ReserveBlockMethod.HtcRie Then
            If part.FullReservedBlocks > systemBlocks Then
                reservedBlocks = part.FullReservedBlocks - systemBlocks
            Else
                reservedBlocks = 1
            End If
        End If


        'fill in the bitmaps
        'Dim logBlocksCount As UInt32 = chkBlkList.Count + mapBlkList.Count + datBlkList.Count + logBlkList.Count + 1
        'Dim dwBlocks As UInt32 = logBlocksCount \ 32
        'If dwBlocks Mod 32 Or dwBlocks = 0 Then
        '    dwBlocks += 1
        'End If



        For x = 0 To 9
            checkpoint1.DataBlockArray(x) = &HFFFFFFFFUI
        Next
        For x = 0 To 2
            checkpoint1.MappingTableBlockArray(x) = &HFFFFFFFFUI
        Next

        checkpoint1.DataSectorNext = 0
        checkpoint1.FreeSectorCountData = 0
        checkpoint1.MappingTableSectorNext = 0
        checkpoint1.FreeSectorCountMappingTable = 0

        '2 - data
        '1 - system
        '0 - free
        Dim blocksMap As New List(Of UInt32)

        For x = startBlockAddr To currentBlockAddr - fullBlockSize Step fullBlockSize
            If chkBlkList.ContainsKey(x) Or mapBlkList.ContainsKey(x) Or logBlkList.ContainsKey(x) Then
                If mapBlkList.ContainsKey(x) Then
                    Dim mb As MappingTableBlock = mapBlkList(x)
                    If mb.UsedSectors > 0 Then
                        blocksMap.Add(1)
                    Else
                        blocksMap.Add(2)
                    End If
                Else
                    blocksMap.Add(1)
                End If
            ElseIf datBlkList.ContainsKey(x) Then
                blocksMap.Add(2)
            Else
                blocksMap.Add(0)
            End If
        Next

        'we need to reserve a number of blocks equal to the number of service blocks
        currentBlockAddr += fullBlockSize * (systemBlocks)
        tempGlobalOffset -= (systemBlocks) * (fullBlockSize + 8)
        For x = 0 To systemBlocks - 1
            blocksMap.Add(0)
        Next

        If blocksMap.Count > logBlockLimit Then
            result.state = WritePartitionState.TooBigPartition
            Return result
        End If
        Dim diff As Int32 = logBlockLimit - blocksMap.Count - reservedBlocks

        If diff < 0 Then
            blockModifier = diff
        End If

        'reserve the data blocks
        currentBlockAddr += fullBlockSize * (reservedBlocks + blockModifier)
        tempGlobalOffset -= (reservedBlocks + blockModifier) * (fullBlockSize + 8)
        For x = 0 To reservedBlocks + blockModifier - 1
            blocksMap.Add(0)
        Next

        'bring the number of blocks up to the upper limit
        'Dim diff As Int32 = logBlockLimit - blocksMap.Count
        If diff >= 0 Then
            currentBlockAddr += fullBlockSize * diff
            tempGlobalOffset -= diff * (fullBlockSize + 8)
            For x = 0 To diff - 1
                blocksMap.Add(0)
            Next
            'Else
            '   blockModifier = diff
        End If
        checkpoint1.FreeBlockCount = blocksMap.Count - 3 'logBlocksCount + reservedBlocks - 3
        checkpoint1.SystemBlockCount = 3

        Dim bitsFilled As UInt32 = 0
        Dim sys As UInt32 = 0, free As UInt32 = 0, freeChk1 As UInt32 = 0, sysChk1 As UInt32 = 0
        Dim sysBlocksCount As UInt32 = 0, freeBlocksCount As UInt32 = 0
        'Dim freeBlocksChk1 As New List(Of UInt32)

        For x = 0 To blocksMap.Count - 1
            If x >= 3 Then
                freeChk1 = freeChk1 Or (1UI << bitsFilled)
            End If
            If x < 3 Then
                sysChk1 = sysChk1 Or (1UI << bitsFilled)
            End If
            If blocksMap(x) = 1 Then 'sys
                sys = sys Or (1UI << bitsFilled)
                sysBlocksCount += 1
            ElseIf blocksMap(x) = 0 Then
                free = free Or (1UI << bitsFilled)
                freeBlocksCount += 1
            End If
            bitsFilled += 1
            If bitsFilled = 32 Then
                checkpoint2.FreeBlocksBitmap.Add(free)
                checkpoint2.SystemBlocksBitmap.Add(sys)
                checkpoint1.FreeBlocksBitmap.Add(freeChk1)
                checkpoint1.SystemBlocksBitmap.Add(sysChk1)
                bitsFilled = 0
                sys = 0
                free = 0
                freeChk1 = 0
                sysChk1 = 0
            End If
        Next

        If Not bitsFilled = 0 Then
            checkpoint2.FreeBlocksBitmap.Add(free)
            checkpoint2.SystemBlocksBitmap.Add(sys)
            checkpoint1.FreeBlocksBitmap.Add(freeChk1)
            checkpoint1.SystemBlocksBitmap.Add(sysChk1)
        End If


        checkpoint2.FreeBlockCount = freeBlocksCount
        checkpoint2.SystemBlockCount = sysBlocksCount

        Dim currentDataBlockIndex As UInt32 = 0
        'data blocks
        For x = 0 To datBlkList.Count - 1
            Dim db As DataBlockClass = datBlkList.Values(x)
            If db.IsEmpty() Then
                checkpoint2.DataBlockArray(currentDataBlockIndex) = db.BlockAddress / fullBlockSize
                currentDataBlockIndex += 1
                If currentDataBlockIndex = checkpoint2.DataBlockArray.Length Then
                    Exit For
                End If
            End If
        Next
        For x = currentDataBlockIndex To 9UI
            checkpoint2.DataBlockArray(x) = &HFFFFFFFFUI
        Next

        checkpoint2.FreeSectorCountData = FreeSectorCountData
        'For x = 0 To 9
        '    For y = 0 To datBlkList.Count - 1
        '        Dim db As DataBlockClass = datBlkList.Values(y)
        '        If db.IsEmpty() Then
        '    Next
        '    If datBlkList.Count > (x + 1) Then
        '        Dim db As DataBlockClass = datBlkList.Values(x + 1)
        '        Dim key As UInt32 = datBlkList.Keys(x + 1)
        '        checkpoint2.DataBlockArray(x) = key / fullBlockSize
        '    Else
        '        checkpoint2.DataBlockArray(x) = &HFFFFFFFFUI
        '    End If
        'Next

        'For x = 0 To usedSectors - 1

        'checkpoint1.ma()
        'Next


        'fix up ChainPhysSector in the checkpoints
        For x = 0 To logBlkList.Count - 1
            Dim lb As LogBlockClass = logBlkList.Values(x)
            Dim key As UInt32 = logBlkList.Keys(x)
            For y = 0 To lb.Entries.Count - 1
                Dim le As LogEntryClass = lb.Entries(y)
                If le.EntryType = LogEntryType.LOG_TYPE_CHECKPOINT Then
                    For Each checkpoint In chkBlkList.Values
                        For Each entry In le.PtrBlocksArray
                            If entry = checkpoint.BlockAddress / fullBlockSize Then
                                checkpoint.ChainPhysicalSector = key / fullBlockSize * sectorsPerBlock + y
                            End If
                        Next
                    Next
                End If
            Next
        Next

        Dim log As String = ""

        For Each le In logentries
            log += le.ToText + vbCrLf
        Next
        'MessageBox.Show(log)

        Dim prevBlockAddr As UInt32 = &HFFFFFFFFUI

        'Link the entries to one another
        For x = 0 To logBlkList.Count - 1
            Dim key As UInt32 = logBlkList.Keys(x)
            Dim key2 As UInt32 = 0

            If x < logBlkList.Count - 1 Then
                key2 = logBlkList.Keys(x + 1)
            End If

            Dim logBlock As LogBlockClass = logBlkList(key)
            For Each entry In logBlock.Entries
                entry.NextBlock = BlockAddrToBlockNum(key2, fullBlockSize)
                entry.PrevBlock = BlockAddrToBlockNum(prevBlockAddr, fullBlockSize)
            Next
            prevBlockAddr = key
        Next


        result.physicalBlocks = datBlkList.Count + systemBlocks + diff
        result.logicalBlocks = blocksMap.Count


        Dim entriesPerSector As UInt32 = sectorSize \ entrySize
        Dim mappingTableSectorsCount As UInt32 = 0
        Dim physSectors As UInt32 = (datBlkList.Count + systemBlocks + diff) * sectorsPerBlock
        mappingTableSectorsCount = physSectors \ entriesPerSector
        If (mappingTableSectorsCount = 0 And physSectors > 0) Or physSectors \ entriesPerSector Mod entriesPerSector Then
            mappingTableSectorsCount += 1
        End If


        For x = 0 To mappingTableSectorsCount - 1
            checkpoint1.MappingTableSectorArray.Add(&HFFFFFFFFUI)
        Next

        'Fix up the last checkpoint and add sector numbers to the mapping tables

        Dim mappingTableBlockArrayIndex As UInt32 = 0
        Dim tempCount As UInt32 = 0
        'mapping table
        For x = 0 To mapBlkList.Count - 1
            Dim mtb As MappingTableBlock = mapBlkList.Values(x)
            Dim key As UInt32 = mapBlkList.Keys(x)
            Dim used As UInt32 = mtb.UsedSectors
            mtb.SectorNumber = physSectors + x * sectorsPerBlock
            If used > 0 Then
                For y = 0 To used - 1
                    checkpoint2.MappingTableSectorArray.Add(key / fullBlockSize * sectorsPerBlock + y)
                    tempCount += 1
                Next
            Else
                checkpoint2.MappingTableBlockArray(mappingTableBlockArrayIndex) = key / fullBlockSize
                mappingTableBlockArrayIndex += 1
            End If
        Next

        For x = tempCount To mappingTableSectorsCount - 1
            checkpoint2.MappingTableSectorArray.Add(&HFFFFFFFFUI)
        Next
        For x As UInt32 = mappingTableBlockArrayIndex To 2
            checkpoint2.MappingTableBlockArray(x) = &HFFFFFFFFUI
        Next


        checkpoint1.LogicalBlockCount = result.logicalBlocks
        checkpoint2.LogicalBlockCount = result.logicalBlocks

        '-------------------------------------------------------------------------------------------
        '  blocks must not be modified below this point! the addressing may change, and then the apocalypse will begin

        'Check whether there are any completely empty blocks. We exclude them and shift the remaining blocks (real addresses)
        For x = logBlkList.Count - 1 To 0 Step -1
            Dim key As UInt32 = logBlkList.Keys(x)
            Dim block As BlockClass = logBlkList(key)
            If block.IsEmpty = True Then
                logBlkList.Remove(key)
                Dim shift As Int32 = block.GetRawDataSize
                If ShiftOffsets(key, -shift, logBlkList, chkBlkList, mapBlkList, datBlkList) = True Then
                    tempGlobalOffset -= shift / fullBlockSize * (fullBlockSize + 8)
                End If
            End If
        Next

        For x = datBlkList.Count - 1 To 0 Step -1
            Dim key As UInt32 = datBlkList.Keys(x)
            Dim block As BlockClass = datBlkList(key)
            If block.IsEmpty = True Then
                datBlkList.Remove(key)
                Dim shift As Int32 = block.GetRawDataSize
                If ShiftOffsets(key, -shift, logBlkList, chkBlkList, mapBlkList, datBlkList) = True Then
                    tempGlobalOffset -= shift / fullBlockSize * (fullBlockSize + 8)
                End If
            End If
        Next

        For x = chkBlkList.Count - 1 To 0 Step -1
            Dim key As UInt32 = chkBlkList.Keys(x)
            Dim block As BlockClass = chkBlkList(key)
            If block.IsEmpty = True Then
                chkBlkList.Remove(key)
                Dim shift As Int32 = block.GetRawDataSize
                If ShiftOffsets(key, -shift, logBlkList, chkBlkList, mapBlkList, datBlkList) = True Then
                    tempGlobalOffset -= shift / fullBlockSize * (fullBlockSize + 8)
                End If
            End If
        Next

        For x = mapBlkList.Count - 1 To 0 Step -1
            Dim key As UInt32 = mapBlkList.Keys(x)
            Dim block As BlockClass = mapBlkList(key)
            If block.IsEmpty = True Then
                mapBlkList.Remove(key)
                Dim shift As Int32 = block.GetRawDataSize
                If ShiftOffsets(key, -shift, logBlkList, chkBlkList, mapBlkList, datBlkList) = True Then
                    tempGlobalOffset -= shift / fullBlockSize * (fullBlockSize + 8)
                End If
            End If
        Next

        Dim offset As Int32 = 0

        Dim physBlocks As UInt32 = 0
        'write the partition
        'first write the logical blocks
        For Each key In logBlkList.Keys
            Dim block As LogBlockClass = logBlkList(key)
            Dim realBlockOffset As UInt32 = GetOutputFileOffset(block.GetRealOffset(), fullBlockSize)
            bWrt.Seek(globalOffset + realBlockOffset, SeekOrigin.Begin)

            bWrt.Write(key)             '                                               Write the block address
            bWrt.Write(fullBlockSize)   '                                                 Write the block length

            Dim b() As Byte = block.GetRawData()
            Dim bSize As UInt32 = block.GetRawDataSize()

            bWrt.Write(b, 0, bSize)
        Next

        ''then the checkpoints
        For Each key In chkBlkList.Keys
            Dim block As CheckpointBlockClass = chkBlkList(key)

            Dim realBlockOffset As UInt32 = GetOutputFileOffset(block.GetRealOffset(), fullBlockSize)
            bWrt.Seek(globalOffset + realBlockOffset, SeekOrigin.Begin)

            bWrt.Write(key)             '                                               Write the block address
            bWrt.Write(fullBlockSize) '                                                 Write the block length

            Dim b() As Byte = block.GetRawData()
            Dim bSize As UInt32 = block.GetRawDataSize()

            bWrt.Write(b, 0, bSize)
        Next

        ''then the mapping tables
        For Each key In mapBlkList.Keys
            Dim block As BlockClass = mapBlkList(key)
            Dim realBlockOffset As UInt32 = GetOutputFileOffset(block.GetRealOffset(), fullBlockSize)
            bWrt.Seek(globalOffset + realBlockOffset, SeekOrigin.Begin)
            bWrt.Write(key)             '                                               Write the block address
            bWrt.Write(fullBlockSize) '                                                 Write the block length
            Dim b() As Byte = block.GetRawData()
            Dim bSize As UInt32 = block.GetRawDataSize()
            bWrt.Write(b, 0, bSize)
        Next

        'then the data itself
        For Each key In datBlkList.Keys
            Dim block As DataBlockClass = datBlkList(key)
            Dim realBlockOffset As UInt32 = GetOutputFileOffset(block.GetRealOffset(), fullBlockSize)
            bWrt.Seek(globalOffset + realBlockOffset, SeekOrigin.Begin)
            bWrt.Write(key)             '                                               Write the block address
            bWrt.Write(fullBlockSize)   '                                               Write the block length
            Dim bSize As UInt32 = block.GetRawDataSize
            Dim b() As Byte = block.GetRawData
            bWrt.Write(b, 0, bSize)
        Next

        globalOffset += tempGlobalOffset

        result.state = WritePartitionState.Success
        Return result
    End Function

    ''' <summary>
    ''' Count the number of physical blocks in the file based on the NB size
    ''' </summary>
    ''' <param name="fileSize">NB file size</param>
    ''' <param name="fullBlockSize">Full block size</param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Function FileSizeToPhysicalBlockCount(ByVal fileSize As UInt32, ByVal fullBlockSize As UInt32) As UInt32
        Return fileSize - 15 - fileSize \ (fullBlockSize + 8) * fullBlockSize
    End Function

    ''' <summary>
    ''' Result of enlarging a partition
    ''' </summary>
    ''' <remarks></remarks>
    Friend Enum EnlargePartitionResult As UInt32
        Success = 0
        InvalidLogicalBlockCount = 1
        InvalidPartition = 2
    End Enum

    ''' <summary>
    ''' Enlarge a partition to the specified number of logical blocks
    ''' </summary>
    ''' <param name="part"></param>
    ''' <param name="currentBlockAddr"></param>
    ''' <param name="globalOffset"></param>
    ''' <param name="fullBlockSize"></param>
    ''' <param name="LogicalBlocks"></param>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Function EnlargePartition(ByVal part As WP7PartitionHeader, ByRef currentBlockAddr As UInt32, ByRef globalOffset As Int32, ByVal fullBlockSize As UInt32, ByVal LogicalBlocks As UInt32) As EnlargePartitionResult
        If part.LogicalBlocks > LogicalBlocks Then
            Return EnlargePartitionResult.InvalidLogicalBlockCount
            'Throw New InvalidOperationException("Partition logical block count is bigger than requested")
        End If
        If (part.PartitionFlags And PartitionFlags.FLASH_PARTITION_FLAG_DIRECT_MAP) = 0 Then
            ' We cannot enlarge a LogChain partition, because
            ' doing so would make the checkpoints invalid
            Return EnlargePartitionResult.InvalidPartition
            'Throw New InvalidOperationException("Cannot perform enlarging on LogChain partition")
        End If
        Dim diff As UInt32 = LogicalBlocks - part.LogicalBlocks
        Dim reserved As UInt32 = part.LogicalBlocks - part.PhysicalBlocks
        part.LogicalBlocks = LogicalBlocks
        part.PhysicalBlocks = LogicalBlocks - reserved
        currentBlockAddr += fullBlockSize * diff
        globalOffset -= (fullBlockSize + 8) * diff

        Return EnlargePartitionResult.Success
    End Function

    ''' <summary>
    ''' NB writer status
    ''' </summary>
    ''' <remarks></remarks>
    Friend Enum WriteNbState
        Success = 0
        OutputFileTooBig = 1
        PartitionError = 2
    End Enum

    ''' <summary>
    ''' Result of writing NB
    ''' </summary>
    ''' <remarks></remarks>
    Friend Structure WriteNbResult
        Public State As WriteNbState
        Public BadPartition As String
        Public WritePartitionResult As WritePartitionResult

        Public NoteAboutRomSize As Boolean
    End Structure

    ''' <summary>
    ''' Write the firmware file to a stream
    ''' </summary>
    ''' <param name="stream">Stream</param>
    ''' <remarks></remarks>
    Friend Function WriteNb(ByRef stream As IO.FileStream, ByVal settingMaxBlockCount As UInt32) As WriteNbResult
        If settingMaxBlockCount > 0 Then
            BlocksCountMax = settingMaxBlockCount
        End If

        Dim WriteResult As New WriteNbResult

        Dim fullSectSize As System.UInt32 = W7PhdrList(0).BytesPerSector + ChunkSize
        Dim fullBlockSize As System.UInt32 = fullSectSize * W7PhdrList(0).SectorsPerBlock
        Dim currentBlockAddr As System.UInt32 = 0

        Dim xBlock(0 To fullBlockSize - 1) As Byte '                                            Data array the size of a full block including extra data
        fillArray(xBlock, &HFF)

        Dim bWrt As New BinaryWriter(stream)

        '' write the signature and the initial number of blocks (we'll change it later)
        bWrt.Write(Sign_D000FF)
        bWrt.Write(&H50FUI)
        bWrt.Write(BlocksCountMax)

        'Write 2 header blocks (empty for now); we'll come back to them at the end
        bWrt.Write(currentBlockAddr)
        bWrt.Write(fullBlockSize)

        Dim headerPos As Integer = stream.Position
        bWrt.Write(xBlock)
        currentBlockAddr += fullBlockSize

        bWrt.Write(currentBlockAddr)
        bWrt.Write(fullBlockSize)

        Dim headerPos2 As Integer = stream.Position
        bWrt.Write(xBlock)
        currentBlockAddr += fullBlockSize

        Dim globalOffset As Int32 = 0
        Dim previousBlockAddr As UInt32 = currentBlockAddr
        'start writing the partitions
        For x = 0 To W7PhdrList.Count - 1
            Dim part As WP7PartitionHeader = W7PhdrList(x)

            Dim result As WritePartitionResult = Nothing

            Dim stockLogBlocks As UInt32 = part.LogicalBlocks
            If part.PartitionFlags And PartitionFlags.FLASH_PARTITION_FLAG_DIRECT_MAP Then


                result = WriteDirectMappedPartition(bWrt, part, xBlock, currentBlockAddr, ChunkSize, globalOffset)
                If result.state = WritePartitionState.Success Then
                    part.PhysicalBlocks = result.physicalBlocks
                    part.LogicalBlocks = result.logicalBlocks

                    If part.LogicalBlocks > stockLogBlocks Then
                        GoTo L_DIRECTMAPPING_Error
                    End If
                    EnlargePartition(part, currentBlockAddr, globalOffset, fullBlockSize, stockLogBlocks) 'part.InitialLogicalBlocks)

                    previousBlockAddr = currentBlockAddr
                Else
L_DIRECTMAPPING_Error:
                    WriteResult.BadPartition = part.Name
                    WriteResult.WritePartitionResult = result

                    stream.Close()
                    WriteResult.State = WriteNbState.PartitionError
                    Return WriteResult
                End If
            Else
                result = WriteLogChainPartition(bWrt, part, xBlock, currentBlockAddr, ChunkSize, globalOffset, stockLogBlocks) 'part.InitialLogicalBlocks)
                If result.state = WritePartitionState.Success Then
                    part.PhysicalBlocks = result.physicalBlocks
                    part.LogicalBlocks = result.logicalBlocks

                    previousBlockAddr = currentBlockAddr
                Else
                    WriteResult.BadPartition = part.Name
                    WriteResult.WritePartitionResult = result

                    stream.Close()
                    WriteResult.State = WriteNbState.PartitionError
                    Return WriteResult
                End If
            End If
        Next


        WriteResult.State = WriteNbState.Success

        'write the number of blocks
        stream.Seek(7, SeekOrigin.Begin)
        Dim n As UInt32 = FileSizeToPhysicalBlockCount(stream.Length, fullBlockSize) \ fullBlockSize 'stream.Length \ fullBlockSize
        If n > BlocksCountMax Then
            'invalid max number of blocks

            If BlocksCountMax = 0 Then
                'The max number of blocks is zero. This happens in some firmware images.
                'We won't treat it as an error, but we'll ask the user to be more careful.
                WriteResult.NoteAboutRomSize = True
            Else
                'Continue execution, but remind the user
                WriteResult.State = WriteNbState.OutputFileTooBig
            End If

        End If
        bWrt.Write(n)

        ' write the headers
        stream.Seek(headerPos, SeekOrigin.Begin)
        WriteNbHeaderToStream(stream, fullSectSize, ChunkSize)
        stream.Seek(headerPos2, SeekOrigin.Begin)
        WriteNbHeaderToStream(stream, fullSectSize, ChunkSize)

        Return WriteResult
    End Function

    ' ''' <summary>
    ' ''' Write a ROM file. Partition headers with partition file names must be added beforehand (PartAdd method)
    ' ''' </summary>
    ' ''' <param name="fileName"></param>
    ' ''' <remarks></remarks>
    'Friend Sub WriteROM2(fileName As String)

    '    Dim fullSectSize As System.UInt32 = SectorSize + ChunkSize
    '    Dim fullBlockSize As System.UInt32 = fullSectSize * SectorsPerBlock
    '    Dim currentBlockAddr As System.UInt32 = 0

    '    RemoveFile(fileName)
    '    Dim bWrt As New BinaryWriter(File.Open(fileName, FileMode.Create))

    '    bWrt.Write(Sign_D000FF) '                                                               Write the D000FF signature
    '    bWrt.Write(0UI) '                                                                       Write 2 zeros (block count and max block count); we'll come back here at the end
    '    bWrt.Write(0UI)

    '    Dim xBlock(0 To fullBlockSize - 1) As Byte '                                            Data array the size of a full block including extra data
    '    fillArray(xBlock, &HFF)

    '    bWrt.Write(currentBlockAddr) '                                                          Write 2 header blocks (empty for now); we'll come back to them at the end
    '    bWrt.Write(fullBlockSize)
    '    bWrt.Write(xBlock)
    '    currentBlockAddr += fullBlockSize
    '    bWrt.Write(currentBlockAddr)
    '    bWrt.Write(fullBlockSize)
    '    bWrt.Write(xBlock)
    '    currentBlockAddr += fullBlockSize

    '    For pIndex = 0 To W7PhdrList.Count - 1
    '        Dim W7Hdr As WP7PartitionHeader = W7PhdrList(pIndex)
    '        If ((W7Hdr.PartitionFlags And 1) <> 0) Then '-------------------------------------- Write a DIRECT_MAPPED partition
    '            Dim bRdr As New BinaryReader(File.OpenRead(W7Hdr.partFileName))
    '            Dim fileLen As Long = bRdr.BaseStream.Length
    '            Dim psCount As System.UInt32 = (fileLen \ SectorSize) '                         Number of sectors
    '            If (fileLen Mod SectorSize) <> 0 Then psCount += 1
    '            Dim blCount As System.UInt32 = psCount \ SectorsPerBlock '                      Number of blocks needed to build the partition
    '            If (psCount Mod SectorsPerBlock) <> 0 Then blCount += 1

    '            Dim sNumber As System.UInt32 = 0
    '            For bNumber = 0 To blCount - 1
    '                fillArray(xBlock, &HFF)
    '                For s = 0 To SectorsPerBlock - 1
    '                    If sNumber >= psCount Then Exit For
    '                    Dim sArray() As Byte = bRdr.ReadBytes(SectorSize)
    '                    sArray.CopyTo(xBlock, fullSectSize * s)
    '                    sNumber += 1
    '                Next
    '                bWrt.Write(currentBlockAddr) '                                              Write the block address
    '                bWrt.Write(fullBlockSize) '                                                 Write the block length
    '                bWrt.Write(xBlock) '                                                        Write the block itself
    '                currentBlockAddr += fullBlockSize '                                         Advance the block address
    '            Next
    '            bRdr.Close()
    '            bRdr.Dispose()
    '            bRdr = Nothing
    '            Dim emptyBlocks As Integer = W7Hdr.LogicalBlocks - blCount
    '            'If emptyBlocks < 1 Then '                                                      Resize the partition
    '            'Else '                                                                         Advance the address by the number of dummy blocks
    '            currentBlockAddr = currentBlockAddr + (fullBlockSize * emptyBlocks)
    '            'End If
    '        Else '----------------------------------------------------------------------------- Write a LEVELED partition
    '            Dim logBlkList As New SortedList(Of System.UInt32, LogBlockClass) '             List of LOG blocks
    '            Dim chkBlkList As New SortedList(Of System.UInt32, Byte()) '                    List of CHECKPOINT blocks
    '            Dim mapBlkList As New SortedList(Of System.UInt32, Byte()) '                    List of MAPPING TABLE blocks
    '            Dim datBlkList As New SortedList(Of System.UInt32, Byte()) '                    List of data blocks

    '            Dim logIndex As Integer = 0
    '            Dim chkIndex As Integer = 0
    '            Dim mapIndex As Integer = 0

    '            Dim lEntry As LogEntryClass

    '            'Dim logPrevBlock As System.UInt32 = &HFFFFFFFFUI
    '            'Dim logNextBlock As System.UInt32

    '            Dim chainFhysSector As System.UInt32 = currentBlockAddr * SectorsPerBlock


    '            logBlkList.Add(currentBlockAddr, _
    '                           New LogBlockClass(SectorSize, SectorsPerBlock)) '                Add the first empty LOG block
    '            currentBlockAddr += fullBlockSize
    '            logBlkList.Add(currentBlockAddr, _
    '                           New LogBlockClass(SectorSize, SectorsPerBlock)) '                Add the second empty LOG block
    '            currentBlockAddr += fullBlockSize

    '            fillArray(xBlock, &HFF)
    '            chkBlkList.Add(currentBlockAddr, xBlock) '                                      Add the first CHECKPOINT block
    '            currentBlockAddr += fullBlockSize

    '            lEntry = New LogEntryClass '                                                    Add the first LOG_ENTRY
    '            lEntry.EntryType = &H474F4C01UI
    '            lEntry.PrevBlock = &HFFFFFFFFUI
    '            lEntry.NextBlock = (chkBlkList.Keys(logIndex + 1) \ fullBlockSize)
    '            lEntry.ChainFirstPhysSector = &HFFFFFFFFUI
    '            lEntry.BlocksPerEntry = 1
    '            lEntry.MappingTableFlag = 0
    '            lEntry.BlocksPerEntry = 1
    '            lEntry.PtrBlocksArray = (chkBlkList.Keys(chkIndex) \ fullBlockSize)
    '            logBlkList.Values(logIndex).AddEntry(lEntry)

    '            Dim bRdr As New BinaryReader(File.OpenRead(W7Hdr.partFileName))
    '            Dim fileLen As Long = bRdr.BaseStream.Length
    '            Dim currSectorNumber As System.UInt32 = 0
    '            While True
    '                Dim dataBlock(0 To fullBlockSize - 1) As Byte
    '                fillArray(dataBlock, &HFF)
    '                For s = 0 To SectorsPerBlock - 1
    '                    If bRdr.BaseStream.Position = bRdr.BaseStream.Length Then
    '                        Exit For
    '                    End If
    '                    Dim sArray() As Byte = bRdr.ReadBytes(SectorSize)
    '                Next
    '            End While


    '        End If



    '    Next


    'End Sub

    ''' <summary>
    ''' Output text information
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Friend Function ToText() As String
        Dim resStr As String = ""
        resStr = resStr & "Blocks            = " & HexSTR(BlocksCount) & vbCrLf
        resStr = resStr & "Sectors per block = " & HexSTR(SectorsPerBlock) & vbCrLf
        resStr = resStr & "Sector size       = " & HexSTR(SectorSize) & vbCrLf
        resStr = resStr & "Extra chunk size  = " & HexSTR(ChunkSize) & vbCrLf & vbCrLf
        resStr = resStr & "Partitions found:" & vbCrLf '                                     Output the list of found partitions
        For Each w7Hdr As WP7PartitionHeader In W7PhdrListOrig
            resStr = resStr & w7Hdr.Name.PadRight(18) & _
                              "Phys=" & HexSTR(w7Hdr.PhysicalBlocks) & "   " & _
                              "Log=" & HexSTR(w7Hdr.LogicalBlocks) & vbCrLf
        Next
        Return resStr
    End Function

    ' ''' <summary>
    ' ''' Returns True if this is a data sector (logSectorNumber is not 0xFFFFFF)
    ' ''' </summary>
    ' ''' <param name="sectExtraData"></param>
    ' ''' <returns></returns>
    ' ''' <remarks></remarks>
    'Friend Shared Function IsDataSector(sectExtraData As SectorInfoClass) As Boolean
    '    If sectExtraData.logSectorNumber = &HFFFFFF Then
    '        Return False
    '    Else
    '        Return True
    '    End If
    'End Function

    ' ''' <summary>
    ' ''' Computes the checksum of a sector
    ' ''' </summary>
    ' ''' <param name="sectorData">Sector byte array</param>
    ' ''' <param name="sectorInfoData">SectorInfoClass byte array</param>
    ' ''' <returns></returns>
    ' ''' <remarks></remarks>
    'Friend Shared Function GetSectorChecksum(sectorData() As Byte, sectorInfoData As SectorInfoClass) As System.UInt16
    '    Dim checksum As System.UInt32 = 0
    '    Dim sInfo() As Byte = sectorInfoData.GetBytes

    '    For i = 0 To sectorData.Length - 1
    '        checksum += My.Resources.MZbitNBtbl(sectorData(i))
    '    Next

    '    For i = 0 To sInfo.Length - 1
    '        checksum += My.Resources.MZbitNBtbl(sInfo(i))
    '    Next

    '    If IsDataSector(sectorInfoData) Then
    '        checksum -= &H10
    '    Else
    '        checksum -= &H8
    '    End If

    '    Return (checksum And &HFFFF)
    'End Function

End Class
