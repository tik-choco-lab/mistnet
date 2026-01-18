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
        private IEvalMessageSender _evalSender;
        protected override void Start()
        {
            base.Start();
            MistLogger.Info($"[ConnectionSelector] SelfId {PeerRepository.SelfId}");
            _evalSender = evalClient;
            evalClient.RegisterReceive(EvalMessageType.NodeRequest, OnRequest);
            evalClient.RegisterReceive(EvalMessageType.NodeReset, OnReset);
        }

        private void OnReset(string payload)
        {
            MistLogger.Info("[ConnectionSelector] Reset command received");
            NodeStart().Forget();
        }

        private async UniTask NodeStart()
        {
            MistLogger.Info("[ConnectionSelector] Resetting connections...");
            MistEventLogger.I.LogEvent(EventType.ConnectionReset, $"全切断");
            Layer.Transport.DisconnectAll();
            routing.ClearNodes();

            await UniTask.Delay(TimeSpan.FromSeconds(EvalConfig.Data.NodeResetIntervalSeconds));
            await MistManager.I.MistSignalingWebSocket.ReconnectToSignalingServer();
            MistEventLogger.I.LogEvent(EventType.ConnectionReset, $"Signaling Server Reconnect");
        }

        private void OnRequest(string payload)
        {
            var nodeRequest = JsonConvert.DeserializeObject<NodeRequest>(payload);
            var nodeId = new NodeId(nodeRequest.TargetNodeId);

            MistEventLogger.I.LogEvent(EventType.Request, $"action: {nodeRequest.Action} targetId: {nodeId}");

            switch (nodeRequest.Action)
            {
                case RequestActionType.Join:
                    MistLogger.Info($"[Action] Join {nodeId}");
                    NodeStart().Forget();
                    break;
                case RequestActionType.Connect:
                    if (nodeId == PeerRepository.SelfId) return;
                    MistLogger.Info($"[Action] Connect {nodeId}");
                    Layer.Transport.Connect(nodeId);
                    break;
                case RequestActionType.Disconnect:
                    if (nodeId == PeerRepository.SelfId) return;
                    MistLogger.Info($"[Action] Disconnect {nodeId}");
                    Layer.Transport.Disconnect(nodeId);
                    break;
                case RequestActionType.SendNodeInfo:
                    MistLogger.Info($"[Action] SendNodeInfo {nodeId}");
                    SendRequestNodeList(nodeId);
                    break;
            }
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
            else if (message.Type == OptMessageType.RequestNodeList) OnRequestNodeInfoReceived(message);
        }

        private void SendRequestNodeList(NodeId nodeId)
        {
            var message = new OptMessage
            {
                Type = OptMessageType.RequestNodeList,
                Payload = PeerRepository.SelfId,
            };

            var json = JsonConvert.SerializeObject(message);
            Send(json, nodeId);
        }

        private void OnRequestNodeInfoReceived(OptMessage message)
        {
            var nodeId = new NodeId(message.Payload.ToString());
            SendNodeInfo(nodeId);
            MistEventLogger.I.LogEvent(EventType.Request, $"Send node list to {nodeId}");
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
