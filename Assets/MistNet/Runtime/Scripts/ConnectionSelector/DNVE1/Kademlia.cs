using System;
using System.Collections.Generic;
using MistNet.DNVE2;
using Newtonsoft.Json;

namespace MistNet
{
    public class Kademlia
    {
        private const char SplitChar = '|';
        private readonly IDNVE1MessageSender _sender;
        private readonly Dictionary<KademliaMessageType, Action<KademliaMessage>> _onMessageReceived = new();
        private readonly KademliaDataStore _dataStore;
        private readonly KademliaRoutingTable _routingTable;
        private readonly RoutingBase _routingBase;

        public Kademlia(IDNVE1MessageSender sender, KademliaDataStore dataStore, KademliaRoutingTable routingTable)
        {
            _routingTable = routingTable;
            routingTable.Init(this);
            _dataStore = dataStore;
            sender = sender;
            _routingBase = MistManager.I.Routing;

            _onMessageReceived[KademliaMessageType.Ping] = OnPing;
            _onMessageReceived[KademliaMessageType.Store] = OnStore;
            _onMessageReceived[KademliaMessageType.FindNode] = OnFindNode;
            _onMessageReceived[KademliaMessageType.FindValue] = OnFindValue;
        }

        public void OnMessage(KademliaMessage message)
        {
            if (_onMessageReceived.TryGetValue(message.Type, out var handler))
            {
                handler(message);
            }
        }

        public void Ping(NodeInfo node)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Ping,
            };

            _sender?.Send(node.Id, message);
        }

        public void Store(NodeInfo node, byte[] key, string value)
        {
            MistLogger.Debug($"[Debug][Kademlia] Store: {value}");
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Store,
                Payload = $"{Convert.ToBase64String(key)}{SplitChar}{value}"
            };

            _sender?.Send(node.Id, message);
        }

        public void FindNode(NodeInfo node, byte[] target)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindNode,
                Payload = Convert.ToBase64String(target)
            };

            _sender?.Send(node.Id, message);
        }

        public void FindValue(NodeInfo node, byte[] key)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindValue,
                Payload = Convert.ToBase64String(key)
            };

            _sender?.Send(node.Id, message);
        }

        private void OnPing(KademliaMessage message)
        {
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.Pong,
                Payload = ""
            };
            _sender?.Send(message.Sender.Id, response);
        }

        private void OnStore(KademliaMessage message)
        {
            var parts = message.Payload.Split(SplitChar);
            if (parts.Length != 2)
            {
                MistLogger.Error($"[Error][Kademlia] Invalid store message format: {message.Payload}");
                return;
            }

            MistLogger.Info($"[Kademlia] OnStore Received store request: {parts[1]} for key {parts[0]}");
            var key = Convert.FromBase64String(parts[0]);
            var value = parts[1];

            // ここはKademliaと異なる
            // NOTE: 上書きしてデータが失われないようにするための処理
            if (_dataStore.TryGetValue(key, out var existingValue))
            {
                var newAreaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);
                var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(existingValue);
                // MergeList
                foreach (var node in newAreaInfo.Nodes)
                {
                    areaInfo.Nodes.Add(node);
                    _routingBase.AddRouting(node, message.Sender.Id);
                }

                // 削除されている場合は、Senderに限り削除を行う
                if (!newAreaInfo.Nodes.Contains(message.Sender.Id))
                {
                    areaInfo.Nodes.Remove(message.Sender.Id);
                }

                var areaInfoStr = JsonConvert.SerializeObject(areaInfo);
                _dataStore.Store(key, areaInfoStr);
            }
            else _dataStore.Store(key, value);
        }

        private void OnFindNode(KademliaMessage message)
        {
            var target = Convert.FromBase64String(message.Payload);
            SendClosestNodes(message.Sender, target);
        }

        private void OnFindValue(KademliaMessage message)
        {
            var targetKey = Convert.FromBase64String(message.Payload);
            if (_dataStore.TryGetValue(targetKey, out var value))
            {
                MistLogger.Debug($"[Debug][Kademlia] Found value for key {BitConverter.ToString(targetKey)}: {value}");
                SendValue(message.Sender, targetKey, value);
            }
            else
            {
                MistLogger.Debug($"[Debug][Kademlia] Value not found for key {BitConverter.ToString(targetKey)}. Sending closest nodes.");
                SendClosestNodes(message.Sender, targetKey);
            }
        }

        private void SendValue(NodeInfo sender, byte[] key, string value)
        {
            var responseFindValue = new ResponseFindValue
            {
                Key = key,
                Value = value,
            };

            var json = JsonConvert.SerializeObject(responseFindValue);

            var response = new KademliaMessage
            {
                Type = KademliaMessageType.ResponseValue,
                Payload = json
            };
            _sender?.Send(sender.Id, response);
        }

        private void SendClosestNodes(NodeInfo sender, byte[] target)
        {
            var closestNodes = _routingTable.FindClosestNodes(target);

            var responseFindNode = new ResponseFindNode
            {
                Key = target,
                Nodes = closestNodes,
            };
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.ResponseNode,
                Payload = JsonConvert.SerializeObject(responseFindNode)
            };
            _sender?.Send(sender.Id, response);
        }
    }
}
