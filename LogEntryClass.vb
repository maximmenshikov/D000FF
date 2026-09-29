Module mdl_LogEntryClass

    ''' <summary>
    ''' Log sector types
    ''' </summary>
    ''' <remarks></remarks>
    Friend Enum LogEntryType As UInt32
        LOG_TYPE_CHECKPOINT = &H474F4C01UI
        LOG_TYPE_BLOCK_WRITES = &H474F4C02UI
        LOG_TYPE_BLOCK_ERASES = &H474F4C03UI
        LOG_TYPE_BLOCK_WRITE_RECOVERIES = &H474F4C04UI
    End Enum


    ''' <summary>
    ''' Class describing LogEntry records that are stored in log sectors
    ''' </summary>
    ''' <remarks></remarks>
    Friend Class LogEntryClass

        Friend EntryType As System.UInt32
        Friend PrevBlock As System.UInt32
        Friend NextBlock As System.UInt32
        Friend ChainFirstPhysSector As System.UInt32
        Friend Reserved0 As System.UInt32
        Friend Reserved1 As System.UInt32
        Friend MappingTableFlag As System.UInt32
        Friend BlocksPerEntry As System.UInt32
        Friend PtrBlocksArray As New List(Of UInt32) 'DWORD*

        ''' <summary>
        ''' Reads data from an array of at least 36 bytes and returns a new LogEntryClass instance
        ''' </summary>
        ''' <param name="dataArray">Data array</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function Read(dataArray() As Byte) As LogEntryClass
            Dim lEnt As New LogEntryClass
            lEnt.EntryType = BitConverter.ToUInt32(dataArray, 0)
            lEnt.PrevBlock = BitConverter.ToUInt32(dataArray, 4)
            lEnt.NextBlock = BitConverter.ToUInt32(dataArray, 8)
            lEnt.ChainFirstPhysSector = BitConverter.ToUInt32(dataArray, 12)
            lEnt.Reserved0 = BitConverter.ToUInt32(dataArray, 16)
            lEnt.Reserved1 = BitConverter.ToUInt32(dataArray, 20)
            lEnt.MappingTableFlag = BitConverter.ToUInt32(dataArray, 24)
            lEnt.BlocksPerEntry = BitConverter.ToUInt32(dataArray, 28)
            If lEnt.BlocksPerEntry < 500 Then
                For x = 0 To lEnt.BlocksPerEntry - 1
                    lEnt.PtrBlocksArray.Add(BitConverter.ToUInt32(dataArray, 32 + x * 4))
                Next
            End If
            'lEnt.PtrBlocksArray = BitConverter.ToUInt32(dataArray, 32)
            Return lEnt
        End Function

        ''' <summary>
        ''' Returns the LogEntry as a byte array
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function GetBytes(Optional sectorSize As Integer = 0) As Byte()
            Dim eArray(36 + BlocksPerEntry * 4) As Byte
            If sectorSize > 0 Then
                ReDim eArray(sectorSize)
            End If
            wrDwordArray(EntryType, 0, eArray)
            wrDwordArray(PrevBlock, 4, eArray)
            wrDwordArray(NextBlock, 8, eArray)
            wrDwordArray(ChainFirstPhysSector, 12, eArray)
            wrDwordArray(Reserved0, 16, eArray)
            wrDwordArray(Reserved1, 20, eArray)
            wrDwordArray(MappingTableFlag, 24, eArray)
            wrDwordArray(BlocksPerEntry, 28, eArray)
            For x = 0 To BlocksPerEntry - 1
                wrDwordArray(PtrBlocksArray(x), 32 + x * 4, eArray)
            Next
            Return eArray
        End Function

        Friend Function ToText() As String
            Dim resStr As String = ""

            resStr = resStr & "EntryType            = " & HexSTR(EntryType) & vbCrLf
            resStr = resStr & "PrevBlock            = " & HexSTR(PrevBlock) & " (" & PrevBlock & ")" & vbCrLf
            resStr = resStr & "NextBlock            = " & HexSTR(NextBlock) & " (" & NextBlock & ")" & vbCrLf
            resStr = resStr & "ChainFirstPhysSector = " & HexSTR(ChainFirstPhysSector) & " (" & ChainFirstPhysSector & ")" & vbCrLf
            resStr = resStr & "Reserved0            = " & HexSTR(Reserved0) & vbCrLf
            resStr = resStr & "Reserved1            = " & HexSTR(Reserved1) & vbCrLf
            resStr = resStr & "MappingTableFlag     = " & HexSTR(MappingTableFlag) & vbCrLf
            resStr = resStr & "BlocksPerEntry       = " & HexSTR(BlocksPerEntry) & " (" & BlocksPerEntry & ")" & vbCrLf
            If BlocksPerEntry > 0 Then
                resStr = resStr & "PtrBlocksArray       = " & HexSTR(PtrBlocksArray(0)) & " (" & PtrBlocksArray(0) & ")" & vbCrLf
                For x = 1 To PtrBlocksArray.Count - 1
                    resStr = resStr & "                       " & HexSTR(PtrBlocksArray(x)) & " (" & PtrBlocksArray(x) & ")" & vbCrLf
                Next
            End If
            Return resStr

        End Function
    End Class
End Module