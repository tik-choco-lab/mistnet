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
        private const string NodeMessageType = "node";
        private const string PingMessageType = "ping";
        private const string PongMessageType = "pong";

        // timeout時間
        private const float PingTimeoutSeconds = 5f;
        private const int BucketBase = 4;
        private const int BucketSize = 20;
        private const int NodeUpdateIntervalSeconds = 5;
        private const int RequestObjectIntervalSeconds = 1;

        private readonly HashSet<string> _connectedNodes = new();
        private readonly List<List<Node>> _buckets = new();
        private Dictionary<string, Action<string, string>> _onMessageReceived;

        private readonly Dictionary<string, bool> _pongWaitList = new();

        // Objectとして表示しているNodeのリスト
        private readonly HashSet<string> _visibleNodes = new();

        [Serializable]
        private class OctreeMessage
        {
            public string type;
            public string data;
        }

        protected override void Start()
        {
            base.Start();
            Debug.Log($"[ConnectionSelector] SelfId {MistPeerData.I.SelfId}");

            _onMessageReceived = new Dictionary<string, Action<string, string>>
            {
                { NodeMessageType, OnNodeReceived },
                { PingMessageType, OnPingReceived },
                { PongMessageType, OnPongReceived },
            };

            UpdateRequestObjectInfo(this.GetCancellationTokenOnDestroy()).Forget();
            UpdateNodeInfo(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            if (!_connectedNodes.Add(id)) return;
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            _connectedNodes.Remove(id);
        }

        protected override void OnMessage(string data, string senderId)
        {
            Debug.Log($"[ConnectionSelector] OnMessage: {data}");
            var message = JsonConvert.DeserializeObject<OctreeMessage>(data);
            _onMessageReceived[message.type](message.data, senderId);
        }

        private void OnNodeReceived(string data, string senderId)
        {
            var node = JsonConvert.DeserializeObject<Node>(data);
            var nodeId = node.Id;
            var position = node.Position.ToVector3();
            var index = GetBucketIndex(position);

            Debug.Log($"[ConnectionSelector] OnNodeReceived: {nodeId} {position} {index}");
            if (index >= _buckets.Count)
            {
                while (_buckets.Count <= index)
                {
                    _buckets.Add(new List<Node>());
                }

                _buckets[index] = new List<Node> { node };
                return;
            }

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
                type = PongMessageType,
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
        /// <param name= "newNode" ></param>
        private async UniTask SendPingAndAddNode(int index, Node newNode)
        {
            var oldNode = _buckets[index][0];

            var octreeMessage = new OctreeMessage
            {
                type = PingMessageType,
            };

            Send(JsonConvert.SerializeObject(octreeMessage), oldNode.Id);
            _pongWaitList.Add(oldNode.Id, false);

            // Timeoutになるか、pongが返ってくるまで待機
            var pongReceivedTask = UniTask.WaitUntil(() => _pongWaitList[oldNode.Id]);
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(PingTimeoutSeconds));
            await UniTask.WhenAny(pongReceivedTask, timeoutTask);

            if (!_pongWaitList[oldNode.Id])
            {
                _buckets[index].Add(newNode);
                _buckets[index].RemoveAt(0);
            }

            _pongWaitList.Remove(oldNode.Id);
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
            while (intDistance >= BucketBase)
            {
                intDistance /= BucketBase; // baseNで割る
                bucketIndex++;
            }

            return bucketIndex;
        }

        private string CreateNodeInfo()
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var node = new Node(
                nodeId: new NodeId(MistPeerData.I.SelfId),
                position: new Position(selfPosition)
            );
            var octreeMessage = new OctreeMessage
            {
                type = NodeMessageType,
                data = JsonConvert.SerializeObject(node)
            };

            return JsonConvert.SerializeObject(octreeMessage);
        }

        private async UniTask UpdateNodeInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(NodeUpdateIntervalSeconds), cancellationToken: token);

                var message = CreateNodeInfo();
                foreach (var id in _connectedNodes)
                {
                    Send(message, id);
                }
            }
        }

        private async UniTask UpdateRequestObjectInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(RequestObjectIntervalSeconds), cancellationToken: token);
                var index = 0;

                // 新規表示するノードの処理
                for (var i = _visibleNodes.Count; i < MaxVisibleNodes; i++)
                {
                    if (index >= _buckets.Count) break;

                    var nodes = _buckets[index++];
                    foreach (var node in nodes.Where(node => !_visibleNodes.Contains(node.Id)))
                    {
                        RequestObject(node.Id);
                        _visibleNodes.Add(node.Id); // ノードを表示リストに追加
                    }
                }

                // 非表示にするノードの処理
                var nodesToHide = _visibleNodes.Where(id => _buckets.SelectMany(b => b).All(n => n.Id != id)).ToList();
                foreach (var nodeId in nodesToHide)
                {
                    RemoveObject(nodeId); // 非表示処理
                    _visibleNodes.Remove(nodeId); // ノードを表示リストから削除
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
