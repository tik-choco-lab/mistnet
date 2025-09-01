using System.Collections.Generic;
using UnityEngine;

namespace MistNet
{
    public class BasicConnectionSelector : IConnectionSelector
    {
        private readonly HashSet<string> _connectedNodes = new();
        [SerializeField] private IRouting routing;
        protected override void Start()
        {
            base.Start();
            MistLogger.Debug($"[ConnectionSelector] SelfId {PeerRepository.I.SelfId}");
        }

        public override void OnConnected(NodeId id)
        {
            MistLogger.Debug($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
            routing.AddMessageNode(id);

            var dataStr = string.Join(",", _connectedNodes);
            SendAll(dataStr);
            RequestObject(id);
        }

        public override void OnDisconnected(NodeId id)
        {
            MistLogger.Debug($"[ConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
            routing.AddMessageNode(id);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var nodes = data.Split(',');
            MistLogger.Debug($"[ConnectionSelector] ({nodes.Length}) Nodes: {data}");

            foreach (var nodeIdStr in nodes)
            {
                var nodeId = new NodeId(nodeIdStr);
                if (nodeId == PeerRepository.I.SelfId) continue;
                if (!_connectedNodes.Add(nodeId)) continue;

                MistLogger.Debug($"[ConnectionSelector] Connecting: {nodeId}");

                // idの大きさを比較
                if (MistManager.I.CompareId(nodeId))
                {
                    MistManager.I.Connect(nodeId);
                }
            }
        }
    }
}
