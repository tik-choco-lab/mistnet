using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class GossipConnectionSelector : IConnectionSelector
    {
        private const float AttemptConnectIntervalTimeSeconds = 5f;
        private readonly HashSet<string> _connectedNodes = new();
        private readonly HashSet<string> _receivedMessageIds = new();
        private readonly Dictionary<string, Node> _users = new(); // key: userId, value: User

        protected override void Start()
        {
            base.Start();
            Debug.Log($"[ConnectionSelector] SelfId {MistPeerData.I.SelfId}");
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            // _connectedNodes.Add(id);
            if (!_connectedNodes.Add(id)) return;
            var dataStr = string.Join(",", _connectedNodes);
            SendAll(dataStr);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(string data, string id)
        {
            var message = JsonConvert.DeserializeObject<CheckAllNodes>(data);
            Debug.Log($"[ConnectionSelector] OnMessage: {message.id}");

            if (!_receivedMessageIds.Add(message.id)) return; // 既に受信済みは破棄

            UpdateNodeData(message);
            UpdateSelfData(); // 自分のデータを更新
            SendAllNodeData();
            ConnectNearNodes();
        }

        private void UpdateNodeData(CheckAllNodes message)
        {
            foreach (var (key, value) in message.nodes)
            {
                _users.TryAdd(key, value);
                if (_users[key].LastUpdate >= value.LastUpdate) continue;
                _users[key] = value;
            }
        }

        private void SendAllNodeData()
        {
            var messageId = Guid.NewGuid().ToString();
            var message = new CheckAllNodes("check_all_nodes", id:messageId, _users);
            var data = JsonConvert.SerializeObject(message);
            Debug.Log($"[ConnectionSelector] SendAllUserData: {data}");
            SendAll(data);
        }

        private void ConnectNearNodes()
        {
            var selfData = _users.GetValueOrDefault(MistPeerData.I.SelfId);
            var nearChunkList = new List<Chunk>
            {
                new(selfData.Chunk.x - 1, selfData.Chunk.y, selfData.Chunk.z),
                new(selfData.Chunk.x + 1, selfData.Chunk.y, selfData.Chunk.z),
                new(selfData.Chunk.x, selfData.Chunk.y - 1, selfData.Chunk.z),
                new(selfData.Chunk.x, selfData.Chunk.y + 1, selfData.Chunk.z),
                new(selfData.Chunk.x, selfData.Chunk.y, selfData.Chunk.z - 1),
                new(selfData.Chunk.x, selfData.Chunk.y, selfData.Chunk.z + 1),
            };
            var nearUsers = _users
                .Where(x => x.Key != MistPeerData.I.SelfId)
                .Where(x => nearChunkList.Contains(x.Value.Chunk))
                .Select(x => x.Key);

            var sameChunkUsers = _users
                .Where(x => x.Key != MistPeerData.I.SelfId)
                .Where(x => x.Value.Chunk.Equals(selfData.Chunk))
                .Select(x => x.Key);

            foreach (var userId in sameChunkUsers)
            {
                MistManager.I.Connect(userId).Forget();
            }
        }

        private async UniTask UpdateAttemptConnectToFailedNode(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(AttemptConnectIntervalTimeSeconds), cancellationToken: token);
                var failedNodes = _connectedNodes
                    .Where(x => !MistPeerData.I.IsConnected(x) && MistManager.I.CompareId(x));
                foreach (var nodeId in failedNodes)
                {
                    MistManager.I.Connect(nodeId).Forget();
                }
            }
        }

        private void UpdateSelfData()
        {
            var objectData = MistSyncManager.I.SelfSyncObject;
            var selfData = _users.GetValueOrDefault(MistPeerData.I.SelfId);

            var position = objectData.transform.position;
            selfData.Position = new Position(position.x, position.y, position.z);
            selfData.Chunk = selfData.Position.ToChunk;
            selfData.LastUpdate = DateTime.Now;

            _users[MistPeerData.I.SelfId] = selfData;
        }
    }
}
