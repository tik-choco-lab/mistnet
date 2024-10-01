using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class BasicConnectionSelector : IConnectionSelector
    {
        private const float AttemptConnectIntervalTimeSeconds = 5f;
        private readonly HashSet<string> _connectedNodes = new();
        private readonly HashSet<string> _receivedMessageIds = new();
        private readonly Dictionary<string, User> _users = new(); // key: userId, value: User

        protected override void Start()
        {
            base.Start();
            Debug.Log($"[BasicConnectionSelector] SelfId {MistPeerData.I.SelfId}");
            // UpdateAttemptConnectToFailedNode(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnConnected: {id}");
            // _connectedNodes.Add(id);
            if (!_connectedNodes.Add(id)) return;
            var dataStr = string.Join(",", _connectedNodes);
            SendAll(dataStr);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[BasicConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(string data, string id)
        {
            // data -. Dictionary<string, object>
            var message = JsonConvert.DeserializeObject<CheckAllNodes>(data);
            Debug.Log($"[BasicConnectionSelector] OnMessage: {message.id}");

            if (!_receivedMessageIds.Add(message.id)) return; // 既に受信済みは破棄

            UpdateUserData(message);
            UpdateSelfData(); // 自分のデータを更新
            SendAllUserData();
        }

        private void UpdateUserData(CheckAllNodes message)
        {
            foreach (var (key, value) in message.users)
            {
                _users.TryAdd(key, value);
                if (_users[key].LastUpdate >= value.LastUpdate) continue;
                _users[key] = value;
            }
        }

        private void SendAllUserData()
        {
            var messageId = Guid.NewGuid().ToString();
            var message = new CheckAllNodes("check_all_nodes", id:messageId, _users);
            var data = JsonConvert.SerializeObject(message);
            Debug.Log($"[BasicConnectionSelector] SendAllUserData: {data}");
            SendAll(data);
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
        private static readonly float ChunkSizeDivide = 1f / ChunkSize;
        public float x;
        public float y;
        public float z;

        public Position(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public Chunk ToChunk => new(
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
    }

    [Serializable]
    public class User
    {
        public Chunk chunk; // 1,2,-1 のような形式
        public Position position;
        public string last_update;
        public DateTime LastUpdate => DateTime.Parse(last_update);

        public User(Chunk chunk, Position position)
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
        public Dictionary<string, User> users;

        public CheckAllNodes(string type, string id, Dictionary<string, User> users)
        {
            this.type = type;
            this.id = id;
            this.users = users;
        }
    }
}
