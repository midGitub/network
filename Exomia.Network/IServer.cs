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

using Exomia.Network.Serialization;

namespace Exomia.Network
{
    /// <summary>
    ///     IServer{T} interface
    /// </summary>
    /// <typeparam name="T">Socket|Endpoint</typeparam>
    interface IServer<in T>
        where T : class
    {
        /// <summary>
        ///     runs the server and starts the listener
        /// </summary>
        /// <param name="port"></param>
        /// <returns><b>true</b> if successful; <b>false</b> otherwise</returns>
        bool Run(int port);

        /// <summary>
        ///     send data to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">data length</param>
        /// <param name="responseid"></param>
        /// <returns>SendError</returns>
        SendError SendTo(T arg0, uint commandid, byte[] data, int offset, int length, uint responseid);

        /// <summary>
        ///     send data to the client
        /// </summary>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="serializable">ISerializable</param>
        /// <param name="responseid"></param>
        /// <returns>SendError</returns>
        SendError SendTo(T arg0, uint commandid, ISerializable serializable, uint responseid);

        /// <summary>
        ///     send data to the client
        /// </summary>
        /// <typeparam name="T1">struct type</typeparam>
        /// <param name="arg0">Socket|EndPoint</param>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="responseid"></param>
        /// <returns>SendError</returns>
        SendError SendTo<T1>(T arg0, uint commandid, in T1 data, uint responseid) where T1 : unmanaged;

        /// <summary>
        ///     send data to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        /// <param name="offset">offset</param>
        /// <param name="length">data length</param>
        void SendToAll(uint commandid, byte[] data, int offset, int length);

        /// <summary>
        ///     send data to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="serializable">ISerializable</param>
        void SendToAll(uint commandid, ISerializable serializable);

        /// <summary>
        ///     send data to all clients
        /// </summary>
        /// <param name="commandid">command id</param>
        /// <param name="data">data</param>
        void SendToAll<T1>(uint commandid, in T1 data) where T1 : unmanaged;
    }
}