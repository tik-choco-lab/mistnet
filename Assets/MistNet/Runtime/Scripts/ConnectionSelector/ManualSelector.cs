using System.Collections.Generic;
using System.Linq;
using MistNet.Evaluation;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class ManualSelector : IConnectionSelector
    {
        private readonly HashSet<string> _connectedNodes = new();
        [SerializeField] private ManualRouting routing;
        [SerializeField] private EvalClient evalClient;

        protected override void Start()
        {
            base.Start();
            OptConfigLoader.ReadConfig();
            MistDebug.Log($"[ConnectionSelector] SelfId {PeerRepository.I.SelfId}");
            evalClient.RegisterMessageHandler(EvalMessageType.NodeRequest, OnRequest);
        }

        private void OnRequest(string payload)
        {
            var nodeRequest = JsonConvert.DeserializeObject<NodeRequest>(payload);
            var nodeId = new NodeId(nodeRequest.TargetNodeId);

            switch (nodeRequest.Action)
            {
                case RequestActionType.Connect:
                    if (nodeId == PeerRepository.I.SelfId) return;
                    if (!MistManager.I.CompareId(nodeId)) return; // idの大きさを比較
                    MistManager.I.Connect(nodeId);
                    break;
                case RequestActionType.Disconnect:
                    if (nodeId == PeerRepository.I.SelfId) return;
                    if (!MistManager.I.CompareId(nodeId)) return; // idの大きさを比較
                    MistManager.I.Disconnect(nodeId);
                    break;
                case RequestActionType.SendNodeInfo:
                    var selfNode = NodeUtils.GetSelfNodeData();
                    var allNodes = routing.Nodes.Values.ToArray();
                    var nodeState = new NodeState
                    {
                        Node = selfNode,
                        Nodes = allNodes
                    };
                    var octreeMessage = new OptMessage
                    {
                        Type = OptMessageType.NodeState,
                        Payload = nodeState
                    };
                    Send(JsonConvert.SerializeObject(octreeMessage), nodeId);
                    break;
                case RequestActionType.Reset:
                    // 全clientと切断
                    MistManager.I.DisconnectAll();
                    routing.ClearNodes();
                    break;
            }
        }

        public override void OnConnected(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
            routing.AddMessageNode(id);
            RequestObject(id);
        }

        public override void OnDisconnected(NodeId id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
            routing.RemoveMessageNode(id);
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<OptMessage>(data);
            MistDebug.Log($"[ConnectionSelector] OnMessage: {message.Type}");
            if (message.Type == OptMessageType.NodeState) OnNodeStateReceived(message);
        }

        private void OnNodeStateReceived(OptMessage message)
        {
            var nodeState = JsonConvert.DeserializeObject<NodeState>(message.Payload.ToString());

            foreach (var node in nodeState.Nodes)
            {
                routing.Add(node.Id, nodeState.Node.Id);
                routing.AddNode(node.Id, node);
            }
        }
    }
}
