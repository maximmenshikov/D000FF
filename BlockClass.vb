Module mdl_BlockClass

    ''' <summary>
    ''' Class describing blocks of any type
    ''' </summary>
    ''' <remarks></remarks>
    Friend MustInherit Class BlockClass

        Private _BlockAddress As UInt32 = 0
        Private _BlockAddressOffset As Int32 = 0
        Private Shared FillBlock() As Byte = Nothing
        Private Const FillBlockSize As UInt32 = 4096

        ''' <summary>
        ''' Fill the array with the given value
        ''' </summary>
        ''' <param name="bArray"></param>
        ''' <param name="value"></param>
        ''' <remarks></remarks>
        Private Sub _FillArray(ByRef bArray() As Byte, ByVal value As Byte)
            For i = 0 To bArray.Length - 1
                bArray(i) = value
            Next
        End Sub

        ''' <summary>
        ''' Fast fill of the array with the given value (with block caching)
        ''' </summary>
        ''' <param name="bArray"></param>
        ''' <param name="value"></param>
        ''' <param name="index"></param>
        ''' <param name="bufSize"></param>
        ''' <remarks></remarks>
        Protected Sub FillArray(ByRef bArray() As Byte, ByVal value As Byte, Optional index As UInteger = 0, Optional bufSize As Integer = -1)

            If FillBlock Is Nothing Then
                ReDim FillBlock(FillBlockSize)
                _FillArray(FillBlock, value)
            ElseIf FillBlock(0) <> value Then
                _FillArray(FillBlock, value)
            End If

            Dim i As UInt32 = 0
            Dim length As UInt32 = CUInt(bArray.Length)

            If bufSize <> -1 Then
                length = CUInt(bufSize)
                i = index
            End If

            While True
                If i >= (index + length) Then
                    Exit While
                Else
                    Dim size As UInt32 = index + length - i
                    If size = 0 Then
                        Exit While
                    End If
                    If size > FillBlockSize Then
                        size = FillBlockSize
                    End If
                    Array.Copy(FillBlock, 0, bArray, i, size)
                    i += size
                End If
            End While
        End Sub

        ''' <summary>
        ''' Block address.
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks>Be careful with BlockAddressOffset! It does not change when BlockAddress changes!</remarks>
        Friend Property BlockAddress As UInt32
            Get
                Return _BlockAddress
            End Get
            Set(value As UInt32)
                _BlockAddress = value
            End Set
        End Property

        ''' <summary>
        ''' Offset from the block address
        ''' </summary>
        ''' <value></value>
        ''' <returns></returns>
        ''' <remarks>Used when obtaining the real block address</remarks>
        Friend Property BlockAddressOffset As Int32
            Get
                Return _BlockAddressOffset
            End Get
            Set(value As Int32)
                _BlockAddressOffset = value
            End Set
        End Property

        ''' <summary>
        ''' Real block address
        ''' </summary>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Function GetRealOffset() As UInt32
            'Console.WriteLine("Real offset: " + Hex(_BlockAddress + _BlockAddressOffset).ToString + " (addr = " + Hex(_BlockAddress).ToString + ", offset = " + Hex(_BlockAddressOffset).ToString)
            Return CUInt(_BlockAddress + _BlockAddressOffset)
        End Function


        ''' <summary>
        ''' Checks whether the array is filled with 0xFF bytes or a custom value (returns TRUE if it is)
        ''' </summary>
        ''' <param name="sectBytes">Byte array (passed by reference)</param>
        ''' <param name="size">Number of bytes to check. If 0, the entire array is checked</param>
        ''' <param name="eValue">Value to check against. Defaults to FF</param>
        ''' <returns></returns>
        ''' <remarks></remarks>
        Friend Shared Function IsEmpty(ByRef sectBytes() As Byte, ByVal index As UInt32, ByVal size As UInteger, Optional ByVal eValue As Byte = &HFF) As Boolean
            Dim sSize As UInteger = size
            For i = index To index + sSize - 1
                If sectBytes(CInt(i)) <> eValue Then Return False '<> &HFF
            Next
            Return True
        End Function

        Friend MustOverride Sub Clear()
        Friend MustOverride Function IsEmpty() As Boolean
        Friend MustOverride Function GetRawData() As Byte()
        Friend MustOverride Function GetRawDataSize() As UInt32

    End Class

End Module
