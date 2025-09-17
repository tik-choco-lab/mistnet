using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MistNet.Utils
{
    public static class IdUtil
    {
        public const int BitLength = 160;
        private static readonly Dictionary<string, byte[]> IDCache = new();

        public static byte[] ToBytes(string id)
        {
            if (IDCache.TryGetValue(id, out var cachedBytes))
            {
                return cachedBytes;
            }
            using var sha1 = SHA1.Create();
            var idBytes =sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
            IDCache[id] = idBytes;
            return idBytes;
        }

        public static int LeadingBitIndex(byte[] distance)
        {
            for (var i = 0; i < distance.Length; i++)
            {
                var b = distance[i];
                if (b == 0) continue;
                for (var bit = 0; bit < 8; bit++)
                {
                    if ((b & (0x80 >> bit)) != 0)
                    {
                        return i * 8 + bit;
                    }
                }
            }
            return -1;
        }

        public static byte[] Xor(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Byte arrays must be of the same length");

            var distance = new byte[a.Length];
            for (var i = 0; i < a.Length; i++)
            {
                distance[i] = (byte)(a[i] ^ b[i]);
            }
            return distance;
        }
    }

    public class ByteArrayDistanceComparer : IComparer<byte[]>
    {
        public int Compare(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                if (a[i] < b[i]) return -1;
                if (a[i] > b[i]) return 1;
            }
            return 0;
        }
    }

    public class PriorityQueue<TElement, TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _heap = new List<(TElement, TPriority)>();
        private readonly IComparer<TPriority> _comparer;
        private readonly int _direction; // 1 = min-heap, -1 = max-heap

        public PriorityQueue(IComparer<TPriority> comparer = null, bool isMinHeap = true)
        {
            _comparer = comparer ?? Comparer<TPriority>.Default;
            _direction = isMinHeap ? 1 : -1;
        }

        public int Count => _heap.Count;

        // 外部からルート優先度を読む（例: 最大ヒープで「現状の最大（worst）」を知るため）
        public TPriority PeekPriority()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("PriorityQueue is empty");
            return _heap[0].Priority;
        }

        public TElement Peek()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("PriorityQueue is empty");
            return _heap[0].Element;
        }

        public void Enqueue(TElement item, TPriority priority)
        {
            _heap.Add((item, priority));
            HeapifyUp(_heap.Count - 1);
        }

        public TElement Dequeue()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("PriorityQueue is empty");
            var root = _heap[0].Element;
            var last = _heap[_heap.Count - 1];
            _heap[0] = last;
            _heap.RemoveAt(_heap.Count - 1);
            if (_heap.Count > 0) HeapifyDown(0);
            return root;
        }

        // スナップショット取得（順序はヒープ順で未整列）
        public List<(TElement Element, TPriority Priority)> UnorderedItems()
        {
            return new List<(TElement, TPriority)>(_heap);
        }

        // 内部比較ユーティリティ（direction を考慮）
        private int ComparePriority(TPriority a, TPriority b)
        {
            return _direction * _comparer.Compare(a, b);
        }

        private void HeapifyUp(int idx)
        {
            while (idx > 0)
            {
                int parent = (idx - 1) / 2;
                if (ComparePriority(_heap[idx].Priority, _heap[parent].Priority) >= 0) break;
                (_heap[idx], _heap[parent]) = (_heap[parent], _heap[idx]);
                idx = parent;
            }
        }

        private void HeapifyDown(int idx)
        {
            int last = _heap.Count - 1;
            while (true)
            {
                int left = idx * 2 + 1;
                if (left > last) break;
                int right = left + 1;
                int best = left;
                if (right <= last && ComparePriority(_heap[right].Priority, _heap[left].Priority) < 0) best = right;
                if (ComparePriority(_heap[best].Priority, _heap[idx].Priority) >= 0) break;
                (_heap[idx], _heap[best]) = (_heap[best], _heap[idx]);
                idx = best;
            }
        }
    }
}
