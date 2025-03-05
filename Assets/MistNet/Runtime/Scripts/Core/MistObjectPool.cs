using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public class MistObjectPool: IDisposable
    {
        private readonly Dictionary<ObjectId, GameObject> _objectPool = new();

        public bool TryGetObject(ObjectId objId, out GameObject obj)
        {
            var result = _objectPool.TryGetValue(objId, out obj);
            if (result)
            {
                obj.SetActive(true);
            }

            return result;
        }

        public void AddObject(ObjectId objId, GameObject prefab)
        {
            _objectPool.Add(objId, prefab);
        }

        public void Destroy(GameObject obj)
        {
            obj.SetActive(false);
        }

        public void Dispose()
        {
            foreach (var obj in _objectPool.Values)
            {
                Destroy(obj);
            }

            _objectPool.Clear();
        }
    }
}
