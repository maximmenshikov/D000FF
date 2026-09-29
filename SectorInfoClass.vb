Module mdl_SectorInfoClass

    ''' <summary>
    ''' Class for sector extra data (6-8 bytes)
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class SectorInfoClass

        Private _chunkSize As UInt32 = 6
        ''' <summary>
        ''' Sector number
        ''' </summary>
        ''' <remarks></remarks>
        Friend logSectorNumber As System.UInt32
        ''' <summary>
        ''' Checksum
        ''' </summary>
        ''' <remarks></remarks>
        Friend sectorChecksum As System.UInt16

        Friend sectorChecksumLow As System.Byte
        Friend sectorChecksumHigh As System.Byte

        ''' <summary>
        ''' Sector flag
        ''' </summary>
        ''' <remarks></remarks>
        Friend sectorFlag As Byte

        ''' <summary>
        ''' New SectorInfoClass instance
        ''' </summary>
        ''' <remarks></remarks>
        Friend Sub New(ByVal chunkSize As UInt32)
            logSectorNumber = &HFFFFFF
            sectorChecksum = &HFFFF
            sectorFlag = &HFF
            _chunkSize = chunkSize
        End Sub

        ''' <summary>
        ''' Reads from a data array and returns a new SectorInfoClass instance
        ''' </summary>
        ''' <param name="dataArray">Data array (6-8 bytes)</param>
        ''' <remarks></remarks>
        Friend Shared Function Read(dataArray() As Byte, ByVal chunkSize As UInt32, Optional ByVal pos As UInt32 = 0) As SectorInfoClass

            Dim N1 As New SectorInfoClass(chunkSize)

            Select Case chunkSize
                Case 6
                    N1.logSectorNumber = (BitConverter.ToUInt32(dataArray, pos + 0) And &HFFFFFF)
                    N1.sectorChecksum = BitConverter.ToUInt16(dataArray, pos + 3)
                    N1.sectorFlag = dataArray(pos + 5)
                Case 8
                    N1.logSectorNumber = (BitConverter.ToUInt32(dataArray, pos + 0) And &HFFFFFF)
                    N1.sectorChecksumLow = dataArray(pos + 3)
                    N1.sectorChecksumHigh = dataArray(pos + 6)
                    N1.sectorFlag = dataArray(pos + 7)
            End Select

            Return N1
        End Function

        ''' <summary>
        ''' Builds a new SectorInfoClass from the given variables
        ''' </summary>
        ''' <param name="lSectNumber">Sector number</param>
        ''' <param name="sectFlag">Sector flag</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function Read(lSectNumber As System.UInt32, sectFlag As Byte, chunkSize As UInt32) As SectorInfoClass
            Dim N1 As New SectorInfoClass(chunkSize)
            N1.logSectorNumber = (lSectNumber And &HFFFFFF)
            N1.sectorFlag = sectFlag
            Return N1
        End Function

        ''' <summary>
        ''' Returns an array of 6-8 bytes
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function GetBytes() As Byte()
            Select Case _chunkSize
                Case 6
                    Dim xArray(0 To 5) As Byte
                    Array.Copy(BitConverter.GetBytes(logSectorNumber), 0, xArray, 0, 3)
                    Array.Copy(BitConverter.GetBytes(sectorChecksum), 0, xArray, 3, 2)
                    xArray(5) = sectorFlag
                    ' Console.WriteLine("N = " + Hex(logSectorNumber).ToString + " flag = " + Hex(sectorFlag).ToString)
                    Return xArray
                Case 8
                    Dim xArray(0 To 7) As Byte
                    Array.Copy(BitConverter.GetBytes(logSectorNumber), 0, xArray, 0, 3)
                    Array.Copy(BitConverter.GetBytes(sectorChecksum And &HFF), 0, xArray, 3, 1)
                    Array.Copy(BitConverter.GetBytes((sectorChecksum >> 8) And &HFF), 0, xArray, 6, 1)
                    xArray(7) = sectorFlag
                    Return xArray
            End Select
            Return Nothing
        End Function

        ''' <summary>
        ''' Returns the flag for a sector
        ''' </summary>
        ''' <param name="sectorData">Sector byte array</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function GetSectorFlags(sectorData() As Byte, Optional index As UInt32 = 0) As Byte
            Dim fDWORD As System.UInt32 = BitConverter.ToUInt32(sectorData, index)

            If fDWORD = LogEntryType.LOG_TYPE_CHECKPOINT Then
                Return &HF9  '                                      THIS IS a LOG sector (CHECKPOINT)
            ElseIf fDWORD = LogEntryType.LOG_TYPE_BLOCK_WRITES Then
                Return &HFD '                                       THIS IS a LOG sector (BLOCK WRITES)
            ElseIf fDWORD = LogEntryType.LOG_TYPE_BLOCK_ERASES Then
                Return &HFD '                                       THIS IS a LOG sector (BLOCK ERASES)
            ElseIf fDWORD = LogEntryType.LOG_TYPE_BLOCK_WRITE_RECOVERIES Then
                Return &HFD '                                       THIS IS a LOG sector (BLOCK WRITE RECOVERIES)
            Else
                Return &HFF '                                       THIS IS a REGULAR SECTOR
            End If

        End Function

        ''' <summary>
        ''' Determines the sector type from its number in the extra data
        ''' </summary>
        ''' <param name="i"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Shared Function _IsDataSector(ByVal i As UInt32) As Boolean
            If i = &HFFFFFFUI Then
                Return False
            Else
                Return True
            End If
        End Function

        ''' <summary>
        ''' Returns True if this is a data sector (logSectorNumber is not equal to 0xFFFFFF)
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function IsDataSector() As Boolean
            Return _IsDataSector(logSectorNumber)
        End Function

        ''' <summary>
        ''' Returns True if this is a data sector (logSectorNumber is not equal to 0xFFFFFF)
        ''' </summary>
        ''' <param name="sectExtraData"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function IsDataSector(sectExtraData As SectorInfoClass) As Boolean
            Return _IsDataSector(sectExtraData.logSectorNumber)
        End Function

        ''' <summary>
        ''' Buffer containing the table used when computing the checksum
        ''' </summary>
        ''' <remarks>Filled on first use.
        ''' Used to reduce the number of ResourceManager calls</remarks>
        Private Shared _MZBitNBtbl() As Byte = Nothing

        ''' <summary>
        ''' Computes the checksum
        ''' </summary>
        ''' <param name="sectorData"></param>
        ''' <param name="sectorInfoData"></param>
        ''' <param name="indexStart"></param>
        ''' <param name="bufferSize"></param>
        ''' <param name="isDataSector"></param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Private Shared Function _GetSectorChecksum(sectorData() As Byte, sectorInfoData() As Byte, ByVal indexStart As UInt32, ByVal bufferSize As UInt32, ByVal isDataSector As Boolean) As UInt16
            Dim checksum As System.UInt32 = 0
            Dim sInfo() As Byte = sectorInfoData

            If _MZBitNBtbl Is Nothing Then
                _MZBitNBtbl = My.Resources.MZbitNBtbl
            End If

            'take the checksum of the data
            For i As UInt32 = indexStart To CUInt((indexStart + bufferSize - 1))
                checksum += _MZBitNBtbl(sectorData(CInt(i)))
                'TEST += 1
            Next

            'take the checksum of the sector number
            For i = 0 To 2
                checksum += _MZBitNBtbl(sInfo(i))
            Next

            'checksum of the sector type
            checksum += _MZBitNBtbl(sInfo(sInfo.Length - 1))

            If isDataSector = False Then
                checksum += &H8UI
            End If

            Return CUShort((checksum And &HFFFF))
        End Function

        ''' <summary>
        ''' Computes the sector checksum
        ''' </summary>
        ''' <param name="sectorData">Sector byte array</param>
        ''' <param name="sectorInfoData">SectorInfoClass byte array</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function GetSectorChecksum(sectorData() As Byte, ByVal indexStart As UInt32, bufferSize As UInt32, sectorInfoData As SectorInfoClass) As System.UInt16
            Return _GetSectorChecksum(sectorData, sectorInfoData.GetBytes, indexStart, bufferSize, IsDataSector(sectorInfoData))
        End Function

        ''' <summary>
        ''' Computes the sector checksum
        ''' </summary>
        ''' <param name="sectorData">Sector byte array</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function GetSectorChecksum(sectorData() As Byte, ByVal indexStart As UInt32, bufferSize As UInt32) As System.UInt16
            Return _GetSectorChecksum(sectorData, GetBytes(), indexStart, bufferSize, IsDataSector())
        End Function

    End Class

End Module