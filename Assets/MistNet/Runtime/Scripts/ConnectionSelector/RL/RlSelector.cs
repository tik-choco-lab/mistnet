using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MistNet.Evaluation;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class RlSelector : SelectorBase
    {
        private readonly HashSet<string> _connectedNodes = new();
        [SerializeField] private RlRouting routing;
        [SerializeField] private EvalClient evalClient;

        protected override void Start()
        {
            base.Start();
            OptConfig.ReadConfig();
            MistLogger.Info($"[ConnectionSelector] SelfId {PeerRepository.I.SelfId}");
            evalClient.RegisterMessageHandler(EvalMessageType.NodeRequest, OnRequest);
            evalClient.RegisterMessageHandler(EvalMessageType.NodeReset, OnReset);
        }

        private async void OnReset(string payload)
        {
            MistLogger.Info("[ConnectionSelector] Resetting connections...");
            MistEventLogger.I.LogEvent(EventType.ConnectionReset, $"全切断");
            MistManager.I.DisconnectAll();
            routing.ClearNodes();

            await UniTask.Delay(TimeSpan.FromSeconds(0.75f));
            await MistManager.I.MistSignalingWebSocket.ReconnectToSignalingServer();
            MistEventLogger.I.LogEvent(EventType.ConnectionReset, $"Signaling Server Reconnect");
        }

        private void OnRequest(string payload)
        {
            var nodeRequest = JsonConvert.DeserializeObject<NodeRequest>(payload);
            var nodeId = new NodeId(nodeRequest.TargetNodeId);

            switch (nodeRequest.Action)
            {
                case RequestActionType.Connect:
                    if (nodeId == PeerRepository.I.SelfId) return;
                    MistLogger.Info($"[Action] Connect {nodeId}");
                    MistManager.I.Connect(nodeId);
                    break;
                case RequestActionType.Disconnect:
                    if (nodeId == PeerRepository.I.SelfId) return;
                    MistLogger.Info($"[Action] Disconnect {nodeId}");
                    MistManager.I.Disconnect(nodeId);
                    break;
                case RequestActionType.SendNodeInfo:
                    MistLogger.Info($"[Action] SendNodeInfo {nodeId}");
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
            MistLogger.Info($"[Action] SendNodeInfo to {nodeId}: {json}");
            Send(json, nodeId);
        }

        public override void OnConnected(NodeId id)
        {
            MistLogger.Info($"[ConnectionSelector] OnConnected: {id}");

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
            MistLogger.Info($"[Action][OnMessage] {message.Type} {data}");
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
