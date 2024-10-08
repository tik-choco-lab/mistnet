using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public class BasicConnectionSelector : IConnectionSelector
    {
        private readonly HashSet<string> _connectedNodes = new();

        protected override void Start()
        {
            base.Start();
            Debug.Log($"[ConnectionSelector] SelfId {MistPeerData.I.SelfId}");
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
            var dataStr = string.Join(",", _connectedNodes);
            SendAll(dataStr);
            RequestObject(id);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(string data, string id)
        {
            var nodes = data.Split(',');
            Debug.Log($"[ConnectionSelector] ({nodes.Length}) Nodes: {data}");

            foreach (var nodeId in nodes)
            {
                if (nodeId == MistPeerData.I.SelfId) continue;
                if (!_connectedNodes.Add(nodeId)) continue;

                Debug.Log($"[ConnectionSelector] Connect: {nodeId}");

                // idの大きさを比較
                if (MistManager.I.CompareId(nodeId))
                {
                    MistManager.I.Connect(nodeId).Forget();
                }
            }
        }
    }
}
