using System.IO;
using System;
using System.Collections.Generic;

namespace UnityWebSocket
{
    public class PooledBuffer : EventArgs, IDisposable
    {
        public Opcode Opcode { get; internal set; }
        public bool IsBinary => Opcode == Opcode.Binary;
        public bool IsText => Opcode == Opcode.Text;

        private bool m_Disposed = false;
        private byte[] m_bytes;
        private int m_Count;

        public int Length => m_Count;
        public byte[] Bytes => m_bytes;
        public int Capacity => m_bytes.Length;

        public string Data
        {
            get
            {
                if (m_bytes == null || m_Count == 0) return string.Empty;
                return System.Text.Encoding.UTF8.GetString(m_bytes, 0, m_Count);
            }
        }

        public PooledBuffer()
        {
            m_bytes = null;
            m_Count = 0;
        }

        public void SetBytesFromMemoryStream(MemoryStream ms)
        {
            if (ms == null) return;
            if (ms.Length == 0) return;
            EnsureFixedCapacity((int)ms.Length);
            ms.Read(m_bytes, 0, (int)ms.Length);
            m_Count = (int)ms.Length;
        }

        public void Write(byte[] data, int dataOffset, int dataLength, int index)
        {
            var totalLength = index + dataLength;
            EnsureFixedCapacity(totalLength);
            Buffer.BlockCopy(data, dataOffset, m_bytes, index, dataLength);
            m_Count = totalLength;
        }

        public void Write(IntPtr data, int dataOffset, int dataLength)
        {
            var totalLength = dataLength;
            EnsureFixedCapacity(totalLength);
            System.Runtime.InteropServices.Marshal.Copy(data, m_bytes, dataOffset, dataLength);
            m_Count = totalLength;
        }

        public void Write(string str, int index)
        {
            var dataLength = index + str.Length;
            EnsureFixedCapacity(dataLength);
            // fill utf-8 bytes
            System.Text.Encoding.UTF8.GetBytes(str, 0, str.Length, m_bytes, index);
            m_Count = dataLength;
        }

        public void Write(byte b, int index)
        {
            var dataLength = index + 1;
            EnsureFixedCapacity(dataLength);
            m_bytes[index] = b;
            if (m_Count < dataLength) m_Count = dataLength;
        }

        public void EnsureFixedCapacity(int count)
        {
            if (m_bytes == null || m_bytes.Length < count)
            {
                // 从池中取出新的数组
                var newBytes = ByteArrayPool.Shared.Get(count);
                if (m_bytes != null)
                {
                    // 将旧数组的数据拷贝到新数组
                    Buffer.BlockCopy(m_bytes, 0, newBytes, 0, m_Count);
                    // 将旧数组归还到池中
                    ByteArrayPool.Shared.Return(ref m_bytes);
                }
                m_bytes = newBytes;
            }
        }

        public void Resize(int count)
        {
            EnsureFixedCapacity(count);
            m_Count = count;
        }

        public void Clear()
        {
            ByteArrayPool.Shared.Return(ref m_bytes);

            m_bytes = null;
            m_Count = 0;
        }

        public static readonly Queue<PooledBuffer> Pool = new Queue<PooledBuffer>();

        public static PooledBuffer Create()
        {
            PooledBuffer buffer;
            if (Pool.Count > 0)
            {
#if UNITY_WEB_SOCKET_LOG
                UnityEngine.Debug.Log($"PooledBuffer Get Reuse");
#endif
                buffer = Pool.Dequeue();
            }
            else
            {
#if UNITY_WEB_SOCKET_LOG
                UnityEngine.Debug.Log($"PooledBuffer Get Alloc");
#endif
                buffer = new PooledBuffer();
            }
            buffer.m_Disposed = false;

            return buffer;
        }

        public static PooledBuffer Create(Opcode opcode, byte[] data)
        {
            var buffer = Create();
            buffer.Opcode = opcode;
            buffer.Write(data, 0, data.Length, 0);
            return buffer;
        }

        public static PooledBuffer Create(Opcode opcode)
        {
            var buffer = Create();
            buffer.Opcode = opcode;
            return buffer;
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            Clear();
            Pool.Enqueue(this);
        }

        public static void ClearPool()
        {
            while (Pool.Count > 0)
            {
                var buffer = Pool.Dequeue();
                buffer.Clear();
            }
        }
    }
}