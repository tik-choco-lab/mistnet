using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace MistNet.DNVE2
{
    public class DNVE2NodeListExchanger : IDisposable
    {
        private readonly IMessageSender _sender;
        private readonly INodeListStore _store;
        private readonly CancellationTokenSource _cts = new();
        private DNVEMessage _message;
        private NodeId _selfId;
        private readonly RoutingBase _routingBase;

        public DNVE2NodeListExchanger(IMessageSender sender, INodeListStore store)
        {
            _sender = sender;
            _store = store;
            _sender.RegisterReceive(DNVEMessageType.NodeList, OnNodeListReceived);
            _sender.RegisterOnConnected(OnConnected);
            _routingBase = MistManager.I.Routing;
            LoopExchangeNodeList(_cts.Token).Forget();
        }

        private void OnNodeListReceived(DNVEMessage message)
        {
            MistLogger.Info($"[DNVE2ConnectionBalancer] OnNodeListReceived: {message.Payload} from {message.Sender}");
            var nodes = JsonConvert.DeserializeObject<List<Node>>(message.Payload);
            foreach (var node in nodes)
            {
                _routingBase.AddRouting(node.Id, message.Sender);
                _store.AddOrUpdate(node);
            }
        }

        private void OnConnected(NodeId id)
        {
            MistLogger.Info($"[DNVE2ConnectionBalancer] OnConnected: {id}");
            UpdateSelfNode();

            _store.TryGet(_selfId, out var selfNode);
            var payload = JsonConvert.SerializeObject(new List<Node> { selfNode });

            _message ??= new DNVEMessage
            {
                Type = DNVEMessageType.NodeList,
            };
            _message.Receiver = id;
            _message.Payload = payload;

            _sender.Send(_message);
        }

        private async UniTask LoopExchangeNodeList(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(OptConfig.Data.NodeListExchangeIntervalSeconds),
                    cancellationToken: token);

                UpdateSelfNode();

                var allNodes = _store.GetAllNodes().ToHashSet();
                foreach (var connectedNode in MistManager.I.Routing.ConnectedNodes)
                {
                    if (!_store.TryGet(connectedNode, out var node))
                        continue;

                    var nodeList = DNVE2Util.GetNodeList(allNodes, node, OptConfig.Data.NodeListExchangeMaxCount);
                    var payload = JsonConvert.SerializeObject(nodeList);

                    _message ??= new DNVEMessage
                    {
                        Type = DNVEMessageType.NodeList,
                    };
                    _message.Receiver = connectedNode;
                    _message.Payload = payload;

                    _sender.Send(_message);
                }
            }
        }

        private void UpdateSelfNode()
        {
            _selfId ??= PeerRepository.I.SelfId;
            var position = MistSyncManager.I.SelfSyncObject.transform.position;

            if (!_store.TryGet(_selfId, out var node))
            {
                node = new Node
                {
                    Id = _selfId,
                };
                _store.AddOrUpdate(node);
            }

            node.Position = new Position(position);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts?.Dispose();
        }
    }
}
