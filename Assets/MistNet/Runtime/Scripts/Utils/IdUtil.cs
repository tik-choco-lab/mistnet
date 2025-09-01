using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace MistNet.Utils
{
    public static class IdUtil
    {
        public const int BitLength = 160;
        public static byte[] ToBytes(string id)
        {
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
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

}
