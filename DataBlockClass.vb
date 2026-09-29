Module mdl_DataBlockClass

    ''' <summary>
    ''' Class describing data blocks
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class DataBlockClass
        Inherits BlockClass

        Private _sectSize As UInt32
        Private _sectPerBlock As UInt32
        Private _chunkSize As UInt32
        Private _fullBlockSize As UInt32

        Private _realData() As Byte
        Private _realSize As UInt32 = 0

        Private _blockIndex As UInt32 = 0


        ''' <summary>
        ''' Creates a new data block
        ''' </summary>
        ''' <param name="sectorSize"></param>
        ''' <param name="sectorsPerBlock"></param>
        ''' <remarks></remarks>
        Friend Sub New(ByVal sectorSize As UInt32, ByVal sectorsPerBlock As UInt32, ByVal chunkSize As UInt32, Optional ByVal data() As Byte = Nothing, Optional ByVal dataSize As UInt32 = 0, Optional ByVal blockIndex As UInt32 = 0)
            _sectSize = sectorSize
            _sectPerBlock = sectorsPerBlock
            _chunkSize = chunkSize

            _fullBlockSize = (_sectSize + chunkSize) * _sectPerBlock
            _blockIndex = blockIndex

            SetData(data, dataSize)

        End Sub

        ''' <summary>
        ''' Clears the block
        ''' </summary>
        ''' <remarks></remarks>
        Friend Overrides Sub Clear()
            _realData = Nothing
            _realSize = 0
        End Sub

        ''' <summary>
        ''' Returns the actual number of sectors
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function GetSectorCount() As UInt32
            Dim res As UInt32 = _realSize \ _sectSize
            If (_realSize Mod _sectSize) > 0 Then
                res += 1UI
            End If
            Return res
        End Function

        ''' <summary>
        ''' Returns a byte array containing this block's data
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawData() As Byte()
            Dim blockData(CInt(_fullBlockSize)) As Byte
            FillArray(blockData, &HFF)

            Dim extraData(CInt(_chunkSize)) As Byte
            FillArray(extraData, &HFF)

            Dim startIndex As UInt32 = _blockIndex * _sectPerBlock
            If _realData IsNot Nothing Then
                If _realSize > 0 Then
                    Dim curpos As UInteger = 0
                    Dim curIndex As UInt32 = 0
                    For x = 0UI To (_realSize - _sectSize) Step _sectSize
                        Dim size As UInt32 = _sectSize
                        If (_realSize - x) < _sectSize Then
                            size = _realSize - x
                        End If
                        Array.Copy(_realData, x, blockData, curpos, size)

                        Array.Copy(extraData, 0, blockData, curpos + _sectSize, _chunkSize)
                        Dim sInfo As New SectorInfoClass(_chunkSize)
                        sInfo.sectorFlag = &HFF
                        sInfo.logSectorNumber = startIndex + curIndex
                        If IsEmpty(blockData, curpos, _sectSize) = False Then
                            sInfo.sectorChecksum = sInfo.GetSectorChecksum(blockData, curpos, _sectSize)
                        Else
                            sInfo.logSectorNumber = &HFFFFFF
                            sInfo.sectorChecksum = &HFFFFUI
                        End If
                        sInfo.GetBytes.CopyTo(blockData, curpos + _sectSize)
                        curpos += _sectSize + _chunkSize
                        curIndex += 1UI
                    Next
                End If
            End If
            Return blockData
        End Function

        ''' <summary>
        ''' Returns the number of used sectors
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function UsedSectors() As UInt32
            If _realData Is Nothing Or _realSize = 0 Then
                Return 0
            End If
            Dim sectors As UInt32 = _realSize \ _sectSize '_fullBlockSize
            If CBool(_realSize Mod _fullBlockSize) Or _realSize < _fullBlockSize Then
                sectors += 1UI
            End If
            Dim realUsedSectorCount As UInt32 = 0
            For x As UInt32 = 0 To sectors - 1UI
                Dim sizeLeft As UInt32 = _sectSize
                If (_sectSize * x + _sectSize) > _realSize Then
                    sizeLeft = _realSize - _sectSize * x
                End If
                If IsEmpty(_realData, _sectSize * x, sizeLeft, &HFF) = False Then
                    realUsedSectorCount = x + 1UI
                End If
            Next
            Return realUsedSectorCount
        End Function

        ''' <summary>
        ''' Returns the block size
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function GetRawDataSize() As UInteger
            Return _fullBlockSize
        End Function

        ''' <summary>
        ''' Returns True if the block is empty, False if it contains data
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Overrides Function IsEmpty() As Boolean
            If _realData Is Nothing Or _realSize = 0 Then
                'block is empty from the start
                Return True
            Else
                'since the block is not empty, check its data
                For i = 0 To _realData.Length - 1
                    If _realData(i) <> &HFF Then Return False
                Next

                'empty after all (filled with 0xFF bytes)
                Return True
            End If
        End Function

        ''' <summary>
        ''' Updates the block's data
        ''' </summary>
        ''' <param name="data"></param>
        ''' <param name="dataSize"></param>
        ''' <remarks></remarks>
        Friend Sub SetData(ByVal data As Byte(), ByVal dataSize As UInt32)
            _realData = data
            _realSize = dataSize
        End Sub

    End Class
End Module