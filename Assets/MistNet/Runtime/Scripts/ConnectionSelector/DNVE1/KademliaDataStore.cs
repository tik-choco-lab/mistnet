using System.Collections.Generic;

namespace MistNet
{
    public class KademliaDataStore
    {
        private readonly Dictionary<byte[], string> _dataStore = new();

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
}
