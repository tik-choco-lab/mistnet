using System.Collections.Generic;
using MistNet.Evaluation;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class ManualConnectionSelector : IConnectionSelector
    {
        private readonly HashSet<string> _connectedNodes = new();
        [SerializeField] private IRouting routing;
        [SerializeField] private EvalClient evalClient;

        protected override void Start()
        {
            base.Start();
            MistDebug.Log($"[ConnectionSelector] SelfId {MistPeerData.I.SelfId}");
            evalClient.RegisterMessageHandler(EvalMessageType.NodeRequest, OnRequest);
        }

        private void OnRequest(string payload)
        {
            var nodeRequest = JsonConvert.DeserializeObject<NodeRequest>(payload);
            var nodeId = new NodeId(nodeRequest.NodeId);

            switch (nodeRequest.Request)
            {
                case RequestType.Connect:
                    if (nodeId == MistPeerData.I.SelfId) return;
                    if (!MistManager.I.CompareId(nodeId)) return; // idの大きさを比較
                    MistManager.I.Connect(nodeId);
                    break;
                case RequestType.Disconnect:
                    if (nodeId == MistPeerData.I.SelfId) return;
                    if (!MistManager.I.CompareId(nodeId)) return; // idの大きさを比較
                    MistManager.I.Disconnect(nodeId);
                    break;
                case RequestType.SendNodeInfo:
                    var selfNode = NodeUtils.GetSelfNodeData();
                    var allNodes = NodeUtils.GetOtherNodeData();
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
            }
        }

        public override void OnConnected(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
            routing.AddMessageNode(id);
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
            }
        }
    }
}
