using System.Collections.Generic;
using MistNet.Utils;
using UnityEngine;

namespace MistNet
{
    public class DefaultSelector : SelectorBase
    {
        private readonly HashSet<string> _connectingOrConnectedNodes = new();
        [SerializeField] private RoutingBase routingBase;

        protected override void Start()
        {
            base.Start();
            MistLogger.Debug($"[ConnectionSelector] SelfId {PeerRepository.SelfId}");
        }

        public override void OnConnected(NodeId id)
        {
            MistLogger.Debug($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectingOrConnectedNodes.Add(id)) return;
            routingBase.AddMessageNode(id);

            var dataStr = string.Join(",", _connectingOrConnectedNodes);
            SendAll(dataStr);
            RequestObject(id);
        }

        public override void OnDisconnected(NodeId id)
        {
            MistLogger.Debug($"[ConnectionSelector] OnDisconnected: {id}");
            _connectingOrConnectedNodes.Remove(id);
            routingBase.AddMessageNode(id);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var nodes = data.Split(',');
            MistLogger.Debug($"[ConnectionSelector] ({nodes.Length}) Nodes: {data}");

            foreach (var nodeIdStr in nodes)
            {
                var nodeId = new NodeId(nodeIdStr);
                if (nodeId == PeerRepository.SelfId) continue;
                if (!_connectingOrConnectedNodes.Add(nodeId)) continue;

                MistLogger.Debug($"[ConnectionSelector] Connecting: {nodeId}");

                // idの大きさを比較
                if (IdUtil.CompareId(PeerRepository.SelfId, nodeId))
                {
                    Layer.Transport.Connect(nodeId);
                }
            }
        }
    }
}
