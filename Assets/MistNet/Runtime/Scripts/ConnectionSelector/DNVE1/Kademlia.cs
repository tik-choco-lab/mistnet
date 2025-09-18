using System;
using System.Linq;
using Newtonsoft.Json;

namespace MistNet
{
    public class Kademlia
    {
        public const char SplitChar = '|';
        private readonly IDNVE1MessageSender _sender;
        private readonly KademliaDataStore _dataStore;
        private readonly KademliaRoutingTable _routingTable;
        private readonly RoutingBase _routingBase;

        public Kademlia(IDNVE1MessageSender sender, KademliaDataStore dataStore, KademliaRoutingTable routingTable)
        {
            _routingTable = routingTable;
            routingTable.Init(this);
            _dataStore = dataStore;
            _sender = sender;
            _routingBase = MistManager.I.Routing;

            _sender.RegisterReceive(KademliaMessageType.Ping, OnPing);
            _sender.RegisterReceive(KademliaMessageType.Store, OnStore);
            _sender.RegisterReceive(KademliaMessageType.FindNode, OnFindNode);
            _sender.RegisterReceive(KademliaMessageType.FindValue, OnFindValue);
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
            MistLogger.Debug($"[Store][SND] {node.Id}");
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
            MistLogger.Debug($"[FindValue][REQ] to {node.Id}");
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
            MistLogger.Debug($"[Store][RCV] {message.Payload}");
            var parts = message.Payload.Split(SplitChar);
            if (parts.Length != 3)
            {
                MistLogger.Error($"[Error][Kademlia] Invalid store message format: {message.Payload}");
                return;
            }

            var key = Convert.FromBase64String(parts[0]);
            var action = parts[1];
            var nodeId = new NodeId(parts[2]);

            // ここはKademliaと異なる
            // NOTE: 上書きしてデータが失われないようにするための処理
            if (!_dataStore.TryGetValue(key, out var existingValue))
            {
                var newAreaInfo = new AreaInfo();
                existingValue = JsonConvert.SerializeObject(newAreaInfo);
            }

            var areaInfo = JsonConvert.DeserializeObject<AreaInfo>(existingValue);
            switch (action)
            {
                case "add":
                    areaInfo.Nodes.Add(nodeId);
                    break;
                case "remove":
                    areaInfo.Nodes.Remove(nodeId);
                    break;
            }

            var areaInfoStr = JsonConvert.SerializeObject(areaInfo);
            _dataStore.Store(key, areaInfoStr);
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
                MistLogger.Debug($"[FindValue][RCV] found {value}");
                SendValue(message.Sender, targetKey, value);
            }
            else
            {
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
            MistLogger.Debug($"[FindValue][RCV] not found, closest nodes: {string.Join(", ", closestNodes.Select(n => n.Id))}");

            var responseFindNode = new ResponseFindValue()
            {
                Key = target,
                Nodes = closestNodes,
            };
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.ResponseValue,
                Payload = JsonConvert.SerializeObject(responseFindNode)
            };
            _sender?.Send(sender.Id, response);
        }
    }
}
