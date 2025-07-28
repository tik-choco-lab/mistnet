using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class Kademlia
    {
        private readonly Action<NodeInfo, KademliaMessage> _send;
        private readonly Dictionary<KademliaMessageType, Action<KademliaMessage>> _onMessageReceived = new();
        private readonly KademliaDataStore _dataStore;
        private readonly KademliaRoutingTable _routingTable;

        public Kademlia(Action<NodeInfo, KademliaMessage> send, KademliaDataStore dataStore, KademliaRoutingTable routingTable)
        {
            _routingTable = routingTable;
            routingTable.Init(this);
            _dataStore = dataStore;
            _send = send;

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

        public void Ping(NodeInfo id)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Ping,
            };

            _send?.Invoke(id, message);
        }

        public void Store(NodeInfo id, byte[] key, string value)
        {
            Debug.Log($"[Debug][Kademlia] Store: {value}");
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.Store,
                Payload = $"{Convert.ToBase64String(key)}:{value}"
            };

            _send?.Invoke(id, message);
        }

        public void FindNode(NodeInfo id, byte[] target)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindNode,
                Payload = Convert.ToBase64String(target)
            };

            _send?.Invoke(id, message);
        }

        public void FindValue(NodeInfo id, byte[] key)
        {
            var message = new KademliaMessage
            {
                Type = KademliaMessageType.FindValue,
                Payload = Convert.ToBase64String(key)
            };

            _send?.Invoke(id, message);
        }

        private void OnPing(KademliaMessage message)
        {
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.Pong,
                Payload = ""
            };
            _send?.Invoke(message.Sender, response);
        }

        private void OnStore(KademliaMessage message)
        {
            var parts = message.Payload.Split(':');
            if (parts.Length == 2)
            {
                Debug.Log($"[Debug][Kademlia] OnStore Received store request: {parts[1]} for key {parts[0]}");
                var key = Convert.FromBase64String(parts[0]);
                var value = parts[1];
                var newAreaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);

                // ここはKademliaと異なる
                // NOTE: 上書きしてデータが失われないようにするための処理
                if (_dataStore.TryGetValue(key, out var existingValue))
                {
                    var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(existingValue);
                    foreach (var node in newAreaInfo.Nodes)
                    {
                        areaInfo.Nodes.Add(node);
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
                Debug.Log($"[Debug][Kademlia] Found value for key {BitConverter.ToString(targetKey)}: {value}");
                SendValue(message.Sender, targetKey, value);
            }
            else
            {
                Debug.Log($"[Debug][Kademlia] Value not found for key {BitConverter.ToString(targetKey)}. Sending closest nodes.");
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
            _send?.Invoke(sender, response);
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
            _send?.Invoke(sender, response);
        }
    }
}
