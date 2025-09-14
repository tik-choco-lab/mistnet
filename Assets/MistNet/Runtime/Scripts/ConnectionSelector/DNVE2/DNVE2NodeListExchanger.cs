using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet.DNVE2
{
    public class DNVE2NodeListExchanger : IDisposable
    {
        private readonly IDNVE2MessageSender _sender;
        private readonly IDNVE2NodeListStore _store;
        private CancellationTokenSource _cts = new();
        private DNVE2Message _message;

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

                var allNodes = _store.GetAllNodes().ToHashSet();
                foreach (var connectedNode in MistManager.I.Routing.ConnectedNodes)
                {
                    if (!_store.TryGet(connectedNode, out var node))
                        continue;

                    var nodeList = GetNodeList(allNodes, node);
                    var payload = JsonConvert.SerializeObject(nodeList);

                    _message ??= new DNVE2Message
                    {
                        Receiver = connectedNode,
                        Payload = payload
                    };

                }
            }
        }

        private IEnumerable<Node> GetNodeList(IEnumerable<Node> allNodes, Node node)
        {
            // nodeに近いノードを最大n件取得する
            var nodeList = allNodes
                .OrderBy(kvp => Vector3.Distance(node.Position.ToVector3(), kvp.Position.ToVector3()))
                .Take(OptConfig.Data.NodeListExchangeMaxCount);

            return nodeList;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts?.Dispose();
        }
    }
}
