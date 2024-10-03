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
                new(selfData.chunk.x - 1, selfData.chunk.y, selfData.chunk.z),
                new(selfData.chunk.x + 1, selfData.chunk.y, selfData.chunk.z),
                new(selfData.chunk.x, selfData.chunk.y - 1, selfData.chunk.z),
                new(selfData.chunk.x, selfData.chunk.y + 1, selfData.chunk.z),
                new(selfData.chunk.x, selfData.chunk.y, selfData.chunk.z - 1),
                new(selfData.chunk.x, selfData.chunk.y, selfData.chunk.z + 1),
            };
            var nearUsers = _users
                .Where(x => x.Key != MistPeerData.I.SelfId)
                .Where(x => nearChunkList.Contains(x.Value.chunk))
                .Select(x => x.Key);

            var sameChunkUsers = _users
                .Where(x => x.Key != MistPeerData.I.SelfId)
                .Where(x => x.Value.chunk.Equals(selfData.chunk))
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
            selfData.position = new Position(position.x, position.y, position.z);
            selfData.chunk = selfData.position.ToChunk;
            selfData.last_update = DateTime.Now.ToString("o");

            _users[MistPeerData.I.SelfId] = selfData;
        }
    }

    [Serializable]
    public struct Position
    {
        private const int ChunkSize = 16;
        private const float ChunkSizeDivide = 1f / ChunkSize;
        public float x;
        public float y;
        public float z;

        public Position(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Chunk ToChunk => new
        (
            Mathf.FloorToInt(x * ChunkSizeDivide),
            Mathf.FloorToInt(y * ChunkSizeDivide),
            Mathf.FloorToInt(z * ChunkSizeDivide)
        );
    }

    [Serializable]
    public struct Chunk
    {
        public int x;
        public int y;
        public int z;

        public Chunk(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (Chunk) obj;
            return x == other.x && y == other.y && z == other.z;
        }

        public bool Equals(Chunk other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z);
        }
    }

    [Serializable]
    public class Node
    {
        public Chunk chunk; // 1,2,-1 のような形式
        public Position position;
        public string last_update;
        public DateTime LastUpdate => DateTime.Parse(last_update);

        public Node(Chunk chunk, Position position)
        {
            this.chunk = chunk;
            this.position = position;
        }
    }

    [Serializable]
    public class CheckAllNodes
    {
        public string type;
        public string id;
        public Dictionary<string, Node> nodes;

        public CheckAllNodes(string type, string id, Dictionary<string, Node> nodes)
        {
            this.type = type;
            this.id = id;
            this.nodes = nodes;
        }
    }
}
