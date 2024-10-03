using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    public class BasicConnectionSelector : IConnectionSelector
    {
        private readonly HashSet<string> _connectedNodes = new();

        protected override void Start()
        {
            base.Start();
            Debug.Log($"[BasicConnectionSelector] SelfId {MistPeerData.I.SelfId}");
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnConnected: {id}");
            // _connectedNodes.Add(id);
            if (!_connectedNodes.Add(id)) return;
            var dataStr = string.Join(",", _connectedNodes);
            SendAll(dataStr);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(string data, string id)
        {
            var nodes = data.Split(',');
            Debug.Log($"[BasicConnectionSelector] ({nodes.Length}) Nodes: {data}");

            foreach (var nodeId in nodes)
            {
                if (nodeId == MistPeerData.I.SelfId) continue;
                if (!_connectedNodes.Add(nodeId)) continue;
                // _connectedNodes.Add(id);
                Debug.Log($"[BasicConnectionSelector] Connect: {nodeId}");

                // idの大きさを比較
                if (MistManager.I.CompareId(nodeId))
                {
                    MistManager.I.Connect(nodeId).Forget();
                }
            }
        }
    }
}
