using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class OctreeConnectionSelector : IConnectionSelector
    {
        private const int MaxVisibleNodes = 20;
        // timeout時間
        private const float PingTimeoutSeconds = 5f;
        private const int BucketPower = 4; // 初期値を2に設定 (2の累乗)
        private const int BucketSize = 20;
        private readonly HashSet<string> _connectedNodes = new();
        private readonly List<List<Node>> _buckets = new();
        private Dictionary<string, Action<string, string>> _onMessageReceived;
        private readonly Dictionary<string, bool> _pongWaitList = new();
        // Objectとして表示しているNodeのリスト
        private readonly HashSet<string> _visibleNodes = new();

        protected override void Start()
        {
            base.Start();
            Debug.Log($"[ConnectionSelector] SelfId {MistPeerData.I.SelfId}");

            _onMessageReceived = new Dictionary<string, Action<string, string>>
            {
                { "node", OnNodeReceived },
                { "ping", OnPingReceived },
                { "pong", OnPongReceived },
            };

            UpdateRequestObjectInfo(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
            var dataStr = string.Join(",", _connectedNodes);
            SendAll(dataStr);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(string data, string senderId)
        {
            var message = JsonConvert.DeserializeObject<OctreeMessage>(data);
            _onMessageReceived[message.type](message.data, senderId);
        }

        private void OnNodeReceived(string data, string senderId)
        {
            var node = JsonConvert.DeserializeObject<Node>(data);
            var nodeId = node.id;
            var position = node.position.ToVector3();
            var index = GetBucketIndex(position);

            if (_buckets[index].Count >= BucketSize)
            {
                SendPingAndAddNode(index, node).Forget();
            }
            else
            {
                _buckets[index].Add(node);
            }
        }

        private void OnPingReceived(string data, string senderId)
        {
            var message = new OctreeMessage
            {
                type = "pong",
                data = MistPeerData.I.SelfId,
            };
            Send(JsonConvert.SerializeObject(message), senderId);
        }

        private void OnPongReceived(string data, string senderId)
        {
            if (_pongWaitList.ContainsKey(senderId))
            {
                // keyがない場合はtimeoutしているということ
                _pongWaitList[senderId] = true;
            }
        }

        /// <summary>
        /// Churn耐性を持たせるために、バケツがいっぱいになった場合にPingを送信する
        /// </summary>
        /// <param name="index"></param>
        private async UniTask SendPingAndAddNode(int index, Node newNode)
        {
            var oldNode = _buckets[index][0];

            var octreeMessage = new OctreeMessage
            {
                type = "ping",
            };

            Send(JsonConvert.SerializeObject(octreeMessage), oldNode.id);
            _pongWaitList.Add(oldNode.id, false);

            // Timeoutになるか、pongが返ってくるまで待機
            var pongReceivedTask = UniTask.WaitUntil(() => _pongWaitList[oldNode.id]);
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(PingTimeoutSeconds));
            await UniTask.WhenAny(pongReceivedTask, timeoutTask);

            if (!_pongWaitList[oldNode.id])
            {
                _buckets[index].Add(newNode);
                _buckets[index].RemoveAt(0);
            }

            _pongWaitList.Remove(oldNode.id);
        }

        private int GetBucketIndex(Vector3 position)
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var distance = Vector3.Distance(selfPosition, position);

            return CalculateBucketIndexUsingBaseN(distance);
        }

        private int CalculateBucketIndexUsingBitShift(float distance)
        {
            // 初期バケツインデックス
            int bucketIndex = 0;

            // 距離が1未満の場合、バケツ0に分類
            if (distance < 1)
            {
                return bucketIndex;
            }

            // 整数部分にキャスト
            int intDistance = Mathf.FloorToInt(distance);

            // シフト演算で2の累乗のどの範囲に収まるかを決定
            while (intDistance > 1)
            {
                intDistance >>= 1; // 右シフトで割り算
                bucketIndex++;
            }

            return bucketIndex;
        }

        private int CalculateBucketIndexUsingBaseN(float distance)
        {
            // 初期バケツインデックス
            int bucketIndex = 0;

            // 距離が1未満の場合、バケツ0に分類
            if (distance < 1)
            {
                return bucketIndex;
            }

            // 整数部分にキャスト
            int intDistance = Mathf.FloorToInt(distance);

            // 基数 baseN で割り続ける
            while (intDistance >= BucketPower)
            {
                intDistance /= BucketPower; // baseNで割る
                bucketIndex++;
            }

            return bucketIndex;
        }

        [Serializable]
        private class OctreeMessage
        {
            public string type;
            public string data;
        }

        private async UniTask UpdateRequestObjectInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
                var index = 0;

                for (var i = _visibleNodes.Count; i < MaxVisibleNodes; i++)
                {
                    if (index >= _buckets.Count) break;

                    var nodes = _buckets[index++];
                    foreach (var node in nodes.Where(node => !_visibleNodes.Contains(node.id)))
                    {
                        RequestObject(node.id);
                    }
                }
            }
        }

        public override void OnSpawned(string id)
        {
            base.OnSpawned(id);
            _visibleNodes.Add(id);
        }

        public override void OnDespawned(string id)
        {
            base.OnDespawned(id);
            _visibleNodes.Remove(id);
        }
    }
}
