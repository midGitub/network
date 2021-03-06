﻿#region MIT License

// Copyright (c) 2019 exomia - Daniel Bätz
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

#endregion

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Exomia.Network.Native
{
    unsafe class CircularBuffer : IDisposable
    {
        private readonly IntPtr _mPtr;
        private readonly byte* _ptr;
        private readonly int _capacity;
        private readonly int _mask;

        private int _head;
        private int _tail;
        private int _count;

        private SpinLock _lock;

        /// <summary>
        ///     Maximum capacity of the buffer.
        ///     Elements pushed into the buffer after maximum capacity is reached will remove an element.
        /// </summary>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _capacity; }
        }

        /// <summary>
        ///     <c>true</c> if the circular buffer is empty; <c>false</c> otherwise.
        /// </summary>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _count == 0; }
        }

        /// <summary>
        ///     current used bytes
        /// </summary>
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _count; }
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="CircularBuffer" /> class.
        /// </summary>
        /// <param name="capacity">capacity (pow2)</param>
        public CircularBuffer(int capacity = 1024)
        {
            _lock = new SpinLock(Debugger.IsAttached);

            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            uint value = (uint)capacity;
            if (value > 0x80000000)
            {
                throw new ArgumentOutOfRangeException();
            }
            value--;
            value     |= value >> 1;
            value     |= value >> 2;
            value     |= value >> 4;
            value     |= value >> 8;
            value     |= value >> 16;
            _capacity =  (int)(value + 1);

            if (_capacity <= 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            _mask = _capacity - 1;

            _mPtr = Marshal.AllocHGlobal(_capacity);
            _ptr  = (byte*)_mPtr;

            Clear();
        }

        /// <summary>
        ///     clear the circular buffer
        /// </summary>
        public void Clear()
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                Mem.Set(_ptr, 0, _capacity);
                _head  = 0;
                _tail  = 0;
                _count = 0;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     read a piece from the buffer
        /// </summary>
        /// <param name="dest">destination array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length</param>
        /// <param name="skip">skip bytes</param>
        /// <returns>a byte array</returns>
        /// <exception cref="InvalidOperationException">if the buffer is empty</exception>
        public int Read(byte[] dest, int offset, int length, int skip)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0)
                {
                    return 0;
                }

                if (skip + length > _count)
                {
                    length = _count - skip;
                }

                fixed (byte* d = dest)
                {
                    if (_tail + skip + length < _capacity)
                    {
                        Mem.Cpy(d + offset, _ptr + _tail + skip, length);
                    }
                    else if (_tail + skip < _capacity)
                    {
                        int l1 = _capacity - (_tail + skip);
                        Mem.Cpy(d + offset, _ptr + _tail + skip, l1);
                        Mem.Cpy(d + offset + l1, _ptr, length - l1);
                    }
                    else
                    {
                        Mem.Cpy(d + offset, _ptr + ((_tail + skip) & _mask), length);
                    }
                }

                _tail  =  (_tail + length) & _mask;
                _count -= skip + length;

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     read a piece from the buffer
        /// </summary>
        /// <param name="dest">destination array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length</param>
        /// <param name="skip">skip bytes</param>
        /// <exception cref="InvalidOperationException">if the buffer is empty</exception>
        public int Read(byte* dest, int offset, int length, int skip)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0)
                {
                    return 0;
                }

                if (skip + length > _count)
                {
                    length = _count - skip;
                }

                if (_tail + skip + length < _capacity)
                {
                    Mem.Cpy(dest + offset, _ptr + _tail + skip, length);
                }
                else if (_tail + skip < _capacity)
                {
                    int l1 = _capacity - (_tail + skip);
                    Mem.Cpy(dest + offset, _ptr + _tail + skip, l1);
                    Mem.Cpy(dest + offset + l1, _ptr, length - l1);
                }
                else
                {
                    Mem.Cpy(dest + offset, _ptr + ((_tail + skip) & _mask), length);
                }

                _tail  =  (_tail + skip + length) & _mask;
                _count -= skip + length;

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     peek a piece from the buffer
        /// </summary>
        /// <param name="dest">destination array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length you want to read from the buffer</param>
        /// <param name="skip">skip bytes</param>
        /// <returns>a byte array</returns>
        /// <exception cref="InvalidOperationException">if the buffer is empty</exception>
        public int Peek(byte[] dest, int offset, int length, int skip)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0)
                {
                    return 0;
                }

                if (skip + length > _count)
                {
                    length = _count - skip;
                }

                fixed (byte* d = dest)
                {
                    if (_tail + skip + length < _capacity)
                    {
                        Mem.Cpy(d + offset, _ptr + _tail + skip, length);
                    }
                    else if (_tail + skip < _capacity)
                    {
                        int l1 = _capacity - (_tail + skip);
                        Mem.Cpy(d + offset, _ptr + _tail + skip, l1);
                        Mem.Cpy(d + offset + l1, _ptr, length - l1);
                    }
                    else
                    {
                        Mem.Cpy(d + offset, _ptr + ((_tail + skip) & _mask), length);
                    }
                }

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     peek a piece from the buffer
        /// </summary>
        /// <param name="dest">destination array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length you want to read from the buffer</param>
        /// <param name="skip">skip bytes</param>
        /// <returns>a byte array</returns>
        /// <exception cref="InvalidOperationException">if the buffer is empty</exception>
        public int Peek(byte* dest, int offset, int length, int skip)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0)
                {
                    return 0;
                }

                if (skip + length > _count)
                {
                    length = _count - skip;
                }

                if (_tail + skip + length < _capacity)
                {
                    Mem.Cpy(dest + offset, _ptr + _tail + skip, length);
                }
                else if (_tail + skip < _capacity)
                {
                    int l1 = _capacity - (_tail + skip);
                    Mem.Cpy(dest + offset, _ptr + _tail + skip, l1);
                    Mem.Cpy(dest + offset + l1, _ptr, length - l1);
                }
                else
                {
                    Mem.Cpy(dest + offset, _ptr + ((_tail + skip) & _mask), length);
                }

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     peek a single byte from the buffer
        /// </summary>
        /// <param name="offset">offset</param>
        /// <param name="b">out byte</param>
        /// <returns><c>true</c> if the peek was successful; <c>false</c> otherwise</returns>
        /// <exception cref="InvalidOperationException">if the buffer is empty</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool PeekByte(int offset, out byte b)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0 || _count <= offset)
                {
                    b = 0;
                    return false;
                }
                b = *(_ptr + ((_tail + offset) & _mask));
                return true;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     peek a single byte from the buffer
        /// </summary>
        /// <param name="skip">skip bytes</param>
        /// <param name="packetHeader">packetHeader</param>
        /// <param name="commandID">command id</param>
        /// <param name="dataLength">data length</param>
        /// <param name="checksum"></param>
        /// <returns>a byte array</returns>
        /// <exception cref="InvalidOperationException">if the buffer is empty</exception>
        public bool PeekHeader(int skip, out byte packetHeader, out uint commandID, out int dataLength,
            out ushort checksum)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count == 0 || _count < skip + Constants.TCP_HEADER_SIZE)
                {
                    commandID    = 0;
                    dataLength   = 0;
                    packetHeader = 0;
                    checksum     = 0;
                    return false;
                }

                // 8bit
                // 
                // | UNUSED BIT   | RESPONSE BIT | COMPRESSED MODE | ENCRYPT MODE |
                // | 7            | 6            | 5  4  3         | 2  1  0      |
                // | VR: 0/1      | VR: 0/1      | VR: 0-8         | VR: 0-8      | VR = VALUE RANGE
                // ---------------------------------------------------------------------------------------------------------------------
                // | 0            | 0            | 0  0  0         | 1  1  1      | ENCRYPT_MODE_MASK    0b00000111
                // | 0            | 0            | 1  1  1         | 0  0  0      | COMPRESSED_MODE_MASK 0b00111000
                // | 0            | 1            | 0  0  0         | 0  0  0      | RESPONSE_BIT_MASK    0b01000000
                // | 1            | 0            | 0  0  0         | 0  0  0      | UNUSED_BIT_MASK      0b10000000

                // 32bit
                // 
                // | COMMANDID 31-16 (16)bit                          | DATA LENGTH 15-0 (16)bit                        |
                // | 31 30 29 28 27 26 25 24 23 22 21 20 19 18 17  16 | 15 14 13 12 11 10  9  8  7  6  5  4  3  2  1  0 |
                // | VR: 0-65535                                      | VR: 0-65535                                     | VR = VALUE RANGE
                // --------------------------------------------------------------------------------------------------------------------------------
                // |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1 | DATA_LENGTH_MASK 0xFFFF
                // |  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  1  |  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0  0 | COMMANDID_MASK 0xFFFF0000

                // 16bit   -    CHECKSUM

                if (_tail + skip + Constants.TCP_HEADER_SIZE < _capacity)
                {
                    packetHeader = *(_ptr + _tail + skip);
                    uint h2 = *(uint*)(_ptr + _tail + skip + 1);
                    commandID  = h2 >> Serialization.Serialization.COMMAND_ID_SHIFT;
                    dataLength = (int)(h2 & Serialization.Serialization.DATA_LENGTH_MASK);
                    checksum   = *(ushort*)(_ptr + _tail + skip + 5);
                }
                else if (_tail + skip < _capacity)
                {
                    packetHeader = *(_ptr + ((_tail + skip) & _mask));
                    uint h2 = (uint)((*(_ptr + ((_tail + skip + 4) & _mask)) << 24)
                                     | (*(_ptr + ((_tail + skip + 3) & _mask)) << 16)
                                     | (*(_ptr + ((_tail + skip + 2) & _mask)) << 8)
                                     | *(_ptr + ((_tail + skip + 1) & _mask)));
                    commandID  = h2 >> Serialization.Serialization.COMMAND_ID_SHIFT;
                    dataLength = (int)(h2 & Serialization.Serialization.DATA_LENGTH_MASK);
                    checksum = (ushort)(
                        (*(_ptr + ((_tail + skip + 6) & _mask)) << 8)
                        | *(_ptr + ((_tail + skip + 5) & _mask)));
                }
                else
                {
                    packetHeader = *(_ptr + ((_tail + skip) & _mask));
                    uint h2 = *(uint*)(_ptr + ((_tail + skip + 1) & _mask));
                    commandID  = h2 >> Serialization.Serialization.COMMAND_ID_SHIFT;
                    dataLength = (int)(h2 & Serialization.Serialization.DATA_LENGTH_MASK);
                    checksum   = *(ushort*)(_ptr + ((_tail + skip + 5) & _mask));
                }

                return true;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     skips until a specified byte is found
        /// </summary>
        /// <param name="offset">offset</param>
        /// <param name="value">the value to compare with</param>
        /// <returns><c>true</c> if the value was found; <c>false otherwise</c></returns>
        public bool SkipUntil(int offset, byte value)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (_count > offset)
                {
                    int i = offset;
                    while (i < _count)
                    {
                        if (*(_ptr + ((_tail + i++) & _mask)) == value)
                        {
                            _tail  =  (_tail + i) & _mask;
                            _count -= i;
                            return true;
                        }
                    }
                    _tail  = (_tail + _count) & _mask;
                    _count = 0;
                }
                return false;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     skips a specified count
        /// </summary>
        /// <param name="count">count to skip</param>
        public void Skip(int count)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);
                if (count > _count)
                {
                    count = _count;
                }
                _tail  =  (_tail + count) & _mask;
                _count -= count;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     write a piece of data into the buffer
        ///     attention: if you write to much unread data will be overridden
        /// </summary>
        /// <param name="value">source array</param>
        /// <param name="offset">offset</param>
        /// <param name="length">length</param>
        /// <returns>the bytes written to the buffer</returns>
        public int Write(byte[] value, int offset, int length)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_count + length > _capacity)
                {
                    length = _capacity - _count;
                }

                fixed (byte* src = value)
                {
                    if (_head + length < _capacity)
                    {
                        Mem.Cpy(_ptr + _head, src + offset, length);
                    }
                    else
                    {
                        int l1 = _capacity - _head;
                        Mem.Cpy(_ptr + _head, src + offset, l1);
                        Mem.Cpy(_ptr, src + offset + l1, length - l1);
                    }
                }

                _head  =  (_head + length) & _mask;
                _count += length;

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        /// <summary>
        ///     write a piece of data into the buffer
        ///     attention: if you write to much unread data will be overridden
        /// </summary>
        /// <param name="src">source array</param>
        /// <param name="offset">source offset</param>
        /// <param name="length">length</param>
        /// <returns>the bytes written to the buffer</returns>
        public int Write(byte* src, int offset, int length)
        {
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                if (_count + length > _capacity)
                {
                    length = _capacity - _count;
                }

                if (_head + length < _capacity)
                {
                    Mem.Cpy(_ptr + _head, src + offset, length);
                }
                else
                {
                    int l1 = _capacity - _head;
                    Mem.Cpy(_ptr + _head, src + offset, l1);
                    Mem.Cpy(_ptr, src + offset + l1, length - l1);
                }

                _head  =  (_head + length) & _mask;
                _count += length;

                return length;
            }
            finally
            {
                if (lockTaken) { _lock.Exit(false); }
            }
        }

        #region IDisposable Support

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                Marshal.FreeHGlobal(_mPtr);
                _disposedValue = true;
            }
        }

        /// <inheritdoc />
        ~CircularBuffer()
        {
            Dispose(false);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}