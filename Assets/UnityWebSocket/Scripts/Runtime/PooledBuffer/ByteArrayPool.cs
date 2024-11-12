using System;
using System.Collections.Generic;

namespace UnityWebSocket
{
    public class ByteArrayPool
    {
        public static readonly ByteArrayPool Shared = new ByteArrayPool();

        private static readonly string TName = typeof(ByteArrayPool).Name;

        // The pool is divided into 18 buckets, each bucket is a queue.
        private readonly Queue<byte[]>[] m_Pool;
        // The stack Max Length of each bucket.
        const int kQueueSize = 32;

        #region 
        // Only Support Kinds Of byte[] Lengths.
        const int kByteLenKindCount = 17;
        const int kMinByteLen = 16;
        const int kMaxByteLen = 1048576;
        static readonly Dictionary<int, int> ByteLenToKind = new Dictionary<int, int>()
        {
            {16, 0}, {32, 1}, {64, 2}, {128, 3}, {256, 4}, {512, 5}, {1024, 6},
            {2048, 7}, {4096, 8}, {8192, 9}, {16384, 10}, {32768, 11}, {65536, 12},
            {131072, 13}, {262144, 14}, {524288, 15}, {1048576, 16},
        };
        #endregion

        static int GetPoolIndex(int length)
        {
            if (length < kMinByteLen || length > kMaxByteLen)
            {
                return -1;
            }

            return ByteLenToKind[length];
        }

        static int CalcByteLen(int size)
        {
            size--;
            size |= size >> 1;
            size |= size >> 2;
            size |= size >> 4;
            size |= size >> 8;
            size |= size >> 16;
            size += 1;

            if (size < kMinByteLen) size = kMinByteLen;
            return size;
        }

        public ByteArrayPool()
        {
            m_Pool = new Queue<byte[]>[kByteLenKindCount];
        }

        /// <summary>
        /// The array length is not always accurate.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public byte[] Get(int len)
        {
            if (len < 0) len = 0;

            if (len == 0)
            {
                return Array.Empty<byte>();
            }

            int size = CalcByteLen(len);
            int poolIndex = GetPoolIndex(size);

            if (poolIndex != -1)
            {
                if (m_Pool[poolIndex] == null)
                {
                    m_Pool[poolIndex] = new Queue<byte[]>(kQueueSize);
                }

                var pool = m_Pool[poolIndex];

                if (pool.Count > 0)
                {
#if UNITY_WEB_SOCKET_LOG
                    UnityEngine.Debug.Log($"{TName} Get byte[{size}] Reuse");
#endif
                    var array = pool.Dequeue();
                    return array;
                }
            }

            // #if UNITY_WEB_SOCKET_LOG
            UnityEngine.Debug.Log($"{TName} Get byte[{size}] Alloc");
            // #endif

            var allocArr = new byte[size];
            return allocArr;
        }

        /// <summary>
        /// <para> Return the array to the pool. </para>
        /// <para> The length of the array must be greater than or equal to 8 and a power of 2. </para>
        /// </summary>
        /// <param name="array"> The length of the array must be greater than or equal to 8 and a power of 2. </param>
        public void Return(byte[] array)
        {
            if ((array == null) || (array.Length == 0))
            {
                UnityEngine.Debug.LogWarning($"ByteArrayPool Return byte[] Null Or Empty");
                return;
            }

            int poolIndex = GetPoolIndex(array.Length);
            if (poolIndex == -1)
            {
                UnityEngine.Debug.LogWarning($"ByteArrayPool Return byte[{array.Length}] Invalid Length");
                return;
            }

            if (m_Pool[poolIndex] == null)
            {
                m_Pool[poolIndex] = new Queue<byte[]>();
            }

            var pool = m_Pool[poolIndex];

            if (pool.Count >= kQueueSize)
            {
                // If the pool is full, we will not return the array to the pool.
                UnityEngine.Debug.LogWarning($"ByteArrayPool Return byte[{array.Length}] Too Many {pool.Count} Max {kQueueSize}");
                array = null;
            }
            else
            {
#if UNITY_WEB_SOCKET_LOG
                UnityEngine.Debug.Log($"{TName} Return byte[{array.Length}] Enqueue {pool.Count + 1}");
#endif
                pool.Enqueue(array);
            }
        }

        /// <summary>
        /// <para> Return the array to the pool and set array reference to null. </para>
        /// <para> The length of the array must be greater than or equal to 8 and a power of 2. </para>
        /// </summary>
        /// <param name="array"> The length of the array must be greater than or equal to 8 and a power of 2. </param>
        public void Return(ref byte[] array)
        {
            Return(array);
            array = null;
        }

        public void Cleanup()
        {
            // Release instances from each buckets.
            for (int i = 0; i < m_Pool.Length; i++)
            {
                var bucket = m_Pool[i];
                bucket.Clear();
            }
        }
    }
}