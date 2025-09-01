using System.Collections.Generic;
using System.Linq;

namespace MistNet
{
    public class KademliaDataStore
    {
        private readonly Dictionary<byte[], string> _dataStore = new(new ByteArrayComparer());

        public void Store(byte[] key, string value)
        {
            _dataStore[key] = value;
        }

        public bool TryGetValue(byte[] key, out string value)
        {
            if (_dataStore.TryGetValue(key, out value))
            {
                return true;
            }
            value = null;
            return false;
        }
    }

    public class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] x, byte[] y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }
            // 配列の内容が等しいかチェック
            return x.SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            // 配列の内容に基づいてハッシュコードを計算
            unchecked
            {
                int hash = 17;
                foreach (byte b in obj)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }
    }
}
