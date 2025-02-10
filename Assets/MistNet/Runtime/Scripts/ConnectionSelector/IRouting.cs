using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public abstract class IRouting : MonoBehaviour
    {
        public readonly HashSet<string> ConnectedNodes = new();

        public virtual void OnConnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            ConnectedNodes.Add(id);
        }

        public virtual void OnDisconnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            ConnectedNodes.Remove(id);
        }

        public virtual void Add(string sourceId, string fromId)
        {
        }

        public virtual string Get(string targetId)
        {
            return null;
        }
    }
}
