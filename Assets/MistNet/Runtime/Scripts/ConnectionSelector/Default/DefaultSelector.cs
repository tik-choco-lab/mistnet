using System.Collections.Generic;
using MistNet.Utils;
using UnityEngine;

namespace MistNet
{
    public class DefaultSelector : SelectorBase
    {
        private readonly HashSet<string> _connectedNodes = new();
        [SerializeField] private RoutingBase routingBase;
        protected override void Start()
        {
            base.Start();
            MistLogger.Debug($"[ConnectionSelector] SelfId {MistManager.I.PeerRepository.SelfId}");
        }

        public override void OnConnected(NodeId id)
        {
            MistLogger.Debug($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
            routingBase.AddMessageNode(id);

            var dataStr = string.Join(",", _connectedNodes);
            SendAll(dataStr);
            RequestObject(id);
        }

        public override void OnDisconnected(NodeId id)
        {
            MistLogger.Debug($"[ConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
            routingBase.AddMessageNode(id);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var nodes = data.Split(',');
            MistLogger.Debug($"[ConnectionSelector] ({nodes.Length}) Nodes: {data}");

            foreach (var nodeIdStr in nodes)
            {
                var nodeId = new NodeId(nodeIdStr);
                if (nodeId == MistManager.I.PeerRepository.SelfId) continue;
                if (!_connectedNodes.Add(nodeId)) continue;

                MistLogger.Debug($"[ConnectionSelector] Connecting: {nodeId}");

                // idの大きさを比較
                if (IdUtil.CompareId(nodeId))
                {
                    MistManager.I.Transport.Connect(nodeId);
                }
            }
        }
    }
}
