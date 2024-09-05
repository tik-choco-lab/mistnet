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
            Debug.Log($"[BasicConnectionSelector] Start {MistPeerData.I.SelfId}");
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
            var dataStr = string.Join(",", _connectedNodes);
            SendMessage(dataStr);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(byte[] data, string id)
        {
            var message = MemoryPackSerializer.Deserialize<P_ConnectionSelector>(data);
            var dataStr = message.Data;
            var nodes = dataStr.Split(',');

            foreach (var nodeId in nodes)
            {
                if (nodeId == MistPeerData.I.SelfId) continue;
                if (!_connectedNodes.Add(nodeId)) continue;
                Debug.Log($"[BasicConnectionSelector] Connect: {nodeId}");

                // idの大きさを比較
                if (MistManager.I.CompareId(nodeId))
                {
                    MistManager.I.Connect(nodeId).Forget();
                }
            }
        }

        private void SendMessage(string data)
        {
            var message = new P_ConnectionSelector
            {
                Data = data
            };

            var serialized = MemoryPackSerializer.Serialize(message);
            MistManager.I.SendAll(MistNetMessageType.ConnectionSelector, serialized);
        }
    }
}
