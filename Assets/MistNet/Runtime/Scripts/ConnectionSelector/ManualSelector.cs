using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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
        [SerializeField] private MistSignalingWebSocket signalingWebSocket;
        private const int InitialNodeCount = 2;
        private readonly List<NodeId> _initialNodeIds = new(InitialNodeCount);

        protected override void Start()
        {
            base.Start();
            OptConfigLoader.ReadConfig();
            MistDebug.Log($"[ConnectionSelector] SelfId {PeerRepository.I.SelfId}");
            evalClient.RegisterMessageHandler(EvalMessageType.NodeRequest, OnRequest);
            evalClient.RegisterMessageHandler(EvalMessageType.NodeReset, OnReset);
        }

        private async void OnReset(string payload)
        {
            MistDebug.Log("[ConnectionSelector] Resetting connections...");
            MistManager.I.DisconnectAll();
            routing.ClearNodes();

            await UniTask.Delay(TimeSpan.FromSeconds(1.5f));
            signalingWebSocket.SendOffer(_initialNodeIds[0]);
            signalingWebSocket.SendOffer(_initialNodeIds[1]);
            _initialNodeIds.Clear();
        }

        private void OnRequest(string payload)
        {
            var nodeRequest = JsonConvert.DeserializeObject<NodeRequest>(payload);
            var nodeId = new NodeId(nodeRequest.TargetNodeId);

            switch (nodeRequest.Action)
            {
                case RequestActionType.Connect:
                    if (nodeId == PeerRepository.I.SelfId) return;
                    // if (!MistManager.I.CompareId(nodeId)) return; // idの大きさを比較
                    MistDebug.Log($"[Action] Connect {nodeId}");
                    MistManager.I.Connect(nodeId);
                    break;
                case RequestActionType.Disconnect:
                    if (nodeId == PeerRepository.I.SelfId) return;
                    // if (!MistManager.I.CompareId(nodeId)) return; // idの大きさを比較
                    MistDebug.Log($"[Action] Disconnect {nodeId}");
                    MistManager.I.Disconnect(nodeId);
                    break;
                case RequestActionType.SendNodeInfo:
                    SendNodeInfo(nodeId);
                    break;
            }
        }

        private void SendNodeInfo(NodeId nodeId)
        {
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
            var json = JsonConvert.SerializeObject(octreeMessage);
            MistDebug.Log($"[Action] SendNodeInfo to {nodeId}: {json}");
            Send(json, nodeId);
        }

        public override void OnConnected(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] OnConnected: {id}");

            if (_initialNodeIds.Count < InitialNodeCount)
            {
                _initialNodeIds.Add(id);
            }

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
            MistDebug.Log($"[Action][OnMessage] {message.Type} {data}");
            if (message.Type == OptMessageType.NodeState) OnNodeStateReceived(message);
        }

        private void OnNodeStateReceived(OptMessage message)
        {
            var nodeState = JsonConvert.DeserializeObject<NodeState>(message.Payload.ToString());

            foreach (var node in nodeState.Nodes)
            {
                routing.AddRouting(node.Id, nodeState.Node.Id);
                routing.UpdateNode(node.Id, node);
            }
        }
    }
}
