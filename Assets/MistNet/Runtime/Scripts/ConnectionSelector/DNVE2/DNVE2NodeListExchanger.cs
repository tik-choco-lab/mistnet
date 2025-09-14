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
        private readonly IDNVE2MessageSender _sender;
        private readonly IDNVE2NodeListStore _store;
        private readonly CancellationTokenSource _cts = new();
        private DNVE2Message _message;
        private NodeId _selfId;

        public DNVE2NodeListExchanger(IDNVE2MessageSender sender, IDNVE2NodeListStore store)
        {
            _sender = sender;
            _store = store;
            _sender.RegisterReceive(DNVE2MessageType.NodeList, OnNodeListReceived);
            LoopExchangeNodeList(_cts.Token).Forget();
        }

        private void OnNodeListReceived(DNVE2Message message)
        {
            MistLogger.Debug($"[DNVE2ConnectionBalancer] OnNodeListReceived: {message.Payload} from {message.Sender}");
            var nodes = JsonConvert.DeserializeObject<List<Node>>(message.Payload);
            foreach (var node in nodes)
            {
                _store.AddOrUpdate(node);
            }
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

                    _message ??= new DNVE2Message
                    {
                        Type = DNVE2MessageType.NodeList,
                        Receiver = connectedNode,
                        Payload = payload
                    };

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
