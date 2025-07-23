using System;
using System.Collections.Generic;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class ConnectionSelector : IConnectionSelector
    {
        private readonly Kademlia _kademlia;
        private readonly Dictionary<KademliaMessageType, Action<string, NodeInfo>> _onMessageReceived = new();
        private readonly KademliaRoutingTable _routingTable;
        private readonly KademliaDataStore _dataStore;

        public ConnectionSelector()
        {
            _kademlia = new Kademlia(SendInternal);
            _routingTable = new KademliaRoutingTable(_kademlia);
            _dataStore = new KademliaDataStore();

            _onMessageReceived[KademliaMessageType.Ping] = OnPing;
            _onMessageReceived[KademliaMessageType.Store] = OnStore;
            _onMessageReceived[KademliaMessageType.FindNode] = OnFindNode;
            _onMessageReceived[KademliaMessageType.FindValue] = OnFindValue;
            _onMessageReceived[KademliaMessageType.ResponseNode] = OnFindNodeResponse;
            _onMessageReceived[KademliaMessageType.ResponseValue] = OnFindValueResponse;
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<KademliaMessage>(data);
            _routingTable.AddNode(message.Sender);
            _onMessageReceived[message.Type]?.Invoke(message.Payload, message.Sender);
        }

        private void SendInternal(NodeInfo node, KademliaMessage message)
        {
            message.Sender = _routingTable.SelfNode;
            Send(JsonConvert.SerializeObject(message), node.Id);
        }

        private void Init()
        {
            var position = MistSyncManager.I.SelfSyncObject.transform.position;
            FindMyAreaInfo(position);
            //StoreMyLocation(position);
        }

        private void FindMyAreaInfo(Vector3 position)
        {
            var chunk = new Area(position);
            var target = IdUtil.ToBytes(chunk.ToString());
            var closestNodes = _routingTable.FindClosestNodes(target);
            FindValue(closestNodes, target);
        }

        private void StoreMyLocation(Vector3 position)
        {
            var chunk = new Area(position);
            var target = IdUtil.ToBytes(chunk.ToString());
            var closestNodes = _routingTable.FindClosestNodes(target);
            if (closestNodes.Count < KBucket.K)
            {
                UpdateArea(target, chunk, closestNodes);
            }
            else
            {
                FindNode(closestNodes, target);
            }
        }


        private void FindValue(List<NodeInfo> closestNodes, byte[] target)
        {
            foreach (var node in closestNodes)
            {
                _kademlia.FindValue(node, target);
            }
        }

        private void FindNode(List<NodeInfo> closestNodes, byte[] target)
        {
            foreach (var node in closestNodes)
            {
                _kademlia.FindNode(node, target);
            }
        }

        private void UpdateArea(byte[] target, Area chunk, List<NodeInfo> closestNodes)
        {
            AreaInfo areaInfo;
            if (!_dataStore.TryGetValue(target, out var value))
            {
                areaInfo = new AreaInfo
                {
                    Chunk = chunk,
                };
            }
            else
            {
                areaInfo = JsonConvert.DeserializeObject<AreaInfo>(value);
            }

            areaInfo.Nodes.Add(_routingTable.SelfNode);
            _dataStore.Store(target, areaInfo.ToString());

            foreach (var node in closestNodes)
            {
                _kademlia.Store(node, target, areaInfo.ToString());
            }
        }

        private void OnPing(string payload, NodeInfo sender)
        {
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.Pong,
                Payload = ""
            };
            SendInternal(sender, response);
        }

        private void OnFindNode(string payload, NodeInfo sender)
        {
            var target = Convert.FromBase64String(payload);
            SendClosestNodes(sender, target);
        }

        private void OnFindNodeResponse(string payload, NodeInfo sender)
        {
            var closestNodes = JsonConvert.DeserializeObject<ResponseFindNode>(payload);
            var target = closestNodes.Target;
            FindNode(closestNodes.Nodes, target);
        }

        private void OnFindValueResponse(string payload, NodeInfo sender)
        {
            var response = JsonConvert.DeserializeObject<ResponseFindValue>(payload);
            if (response.Value != null)
            {
                _dataStore.Store(response.Target, response.Value);
            }
            else
            {
                FindValue(response.Nodes, response.Target);
            }
        }

        private void OnStore(string payload, NodeInfo sender)
        {
            var parts = payload.Split(':');
            if (parts.Length == 2)
            {
                var key = Convert.FromBase64String(parts[0]);
                var value = parts[1];
                _dataStore.Store(key, value);
                SendValue(sender, value);
            }
        }

        private void OnFindValue(string payload, NodeInfo sender)
        {
            var targetKey = Convert.FromBase64String(payload);
            if (_dataStore.TryGetValue(targetKey, out var value))
            {
                SendValue(sender, value);
            }
            else
            {
                SendClosestNodes(sender, targetKey);
            }
        }

        private void SendValue(NodeInfo sender, string value)
        {
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.ResponseValue,
                Payload = value
            };
            SendInternal(sender, response);
        }

        private void SendClosestNodes(NodeInfo sender, byte[] target)
        {
            var closestNodes = _routingTable.FindClosestNodes(target);

            var responseFindNode = new ResponseFindNode
            {
                Nodes = closestNodes,
                Target = target
            };
            var response = new KademliaMessage
            {
                Type = KademliaMessageType.ResponseNode,
                Payload = JsonConvert.SerializeObject(responseFindNode)
            };
            SendInternal(sender, response);
        }
    }
}
