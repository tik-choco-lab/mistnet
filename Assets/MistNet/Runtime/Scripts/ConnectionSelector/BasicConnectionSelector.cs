using System.Collections.Generic;
using MemoryPack;
using UnityEngine;

namespace MistNet
{
    public class BasicConnectionSelector : IConnectionSelector
    {
        private readonly HashSet<string> _connectedNodes = new();

        private void Start()
        {
            MistManager.I.AddRPC(MistNetMessageType.ConnectionSelector, OnMessage);
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnConnected: {id}");
            _connectedNodes.Add(id);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(byte[] data, string id)
        {
            var message = MemoryPackSerializer.Deserialize<P_ConnectionSelector>(data);
        }
    }
}
