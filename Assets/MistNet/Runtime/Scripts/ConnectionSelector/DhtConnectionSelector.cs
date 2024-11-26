using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class DhtConnectionSelector : IConnectionSelector
    {
        private const int MaxVisibleNodes = 5;
        private const string NodeMessageType = "node";
        private const string NodesMessageType = "nodes";
        private const string PingMessageType = "ping";
        private const string PongMessageType = "pong";

        // timeout時間
        private const float PingTimeoutSeconds = 5f;
        private const int BucketBase = 4;
        private const int BucketSize = 20;
        private const int NodeUpdateIntervalSeconds = 5;
        private const int RequestObjectIntervalSeconds = 1;

        [SerializeField] private DhtRouting routing;

        private Dictionary<string, Action<string, string>> _onMessageReceived;
        private readonly Dictionary<string, bool> _pongWaitList = new();

        // Objectとして表示しているNodeのリスト
        private readonly HashSet<NodeId> _visibleNodes = new();

        [Serializable]
        private class ConnectionSelectorMessage
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
                { NodesMessageType, OnNodesReceived },
                { PingMessageType, OnPingReceived },
                { PongMessageType, OnPongReceived },
            };

            UpdateRequestObjectInfo(this.GetCancellationTokenOnDestroy()).Forget();
            UpdateNodeInfo(this.GetCancellationTokenOnDestroy()).Forget();
            UpdateDebugShowBucketInfo(this.GetCancellationTokenOnDestroy()).Forget();
            UpdateFindNextConnect(this.GetCancellationTokenOnDestroy()).Forget();
        }

        public override void OnConnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnConnected: {id}");
            routing.ConnectedNodes.Add(id);
        }

        public override void OnDisconnected(string id)
        {
            Debug.Log($"[ConnectionSelector] OnDisconnected: {id}");
            routing.ConnectedNodes.Remove(id);
        }

        protected override void OnMessage(string data, string senderId)
        {
            Debug.Log($"[ConnectionSelector] OnMessage: {data}");
            var message = JsonConvert.DeserializeObject<ConnectionSelectorMessage>(data);
            _onMessageReceived[message.type](message.data, senderId);
        }

        private void OnNodeReceived(string data, string senderId)
        {
            var node = JsonConvert.DeserializeObject<Node>(data);
            OnNodeReceived(node, senderId);
        }

        private void OnNodeReceived(Node node, string senderId)
        {
            var nodeId = node.Id;
            if (nodeId == MistPeerData.I.SelfId) return; // 自分のNodeは無視

            var position = node.Position.ToVector3();
            var index = GetBucketIndex(position);

            Debug.Log($"[ConnectionSelector] OnNodeReceived: {nodeId} {position} {index}");

            var oldIndex = routing.NodeIdToBucketIndex.GetValueOrDefault(nodeId, -1);
            var needUpdate = oldIndex != index;
            if (oldIndex != -1 && needUpdate)
            {
                // 前回と値が異なる場合
                var oldNode = routing.Buckets[oldIndex].First(n => n.Id == nodeId);
                routing.Buckets[oldIndex].Remove(oldNode);
            }

            if (index >= routing.Buckets.Count)
            {
                // 初期化
                while (routing.Buckets.Count <= index)
                {
                    routing.Buckets.Add(new HashSet<Node>());
                }

                // 新規追加
                AddNode(index, node);
                return;
            }

            if (routing.Buckets[index].Count >= BucketSize)
            {
                SendPingAndAddNode(index, node).Forget();
            }
            else
            {
                AddNode(index, node);
            }
        }

        private void OnNodesReceived(string data, string senderId)
        {
            var nodes = JsonConvert.DeserializeObject<List<Node>>(data);
            foreach (var node in nodes)
            {
                OnNodeReceived(node, senderId);
            }
        }

        private void AddNode(int index, Node node)
        {
            routing.Buckets[index] ??= new HashSet<Node>();
            routing.Buckets[index].Add(node);
            routing.NodeIdToBucketIndex[node.Id] = index;
        }

        private void DeleteNode(int index, Node oldNode)
        {
            routing.Buckets[index].Remove(oldNode);
            routing.NodeIdToBucketIndex.Remove(oldNode.Id);
        }

        private void OnPingReceived(string data, string senderId)
        {
            var message = new ConnectionSelectorMessage
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
            var oldNode = routing.Buckets[index].First(); // これがきちんと最初のNodeであるか確認が必要
            Debug.Log($"[ConnectionSelector] SendPingAndAddNode: {oldNode.Id} -> {newNode.Id}");

            var octreeMessage = new ConnectionSelectorMessage
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
                routing.Buckets[index].Add(newNode);
                DeleteNode(index, oldNode);
            }

            _pongWaitList.Remove(oldNode.Id);
        }

        private int GetBucketIndex(Vector3 position)
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var distance = Vector3.Distance(selfPosition, position);

            return CalculateBucketIndexUsingBaseN(distance);
        }

        #region CalculateBucketIndex base N
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
        #endregion

        private int CalculateBucketIndexUsingBaseN(float distance)
        {
            // 初期バケツインデックス
            var bucketIndex = 0;

            // 距離が1未満の場合、バケツ0に分類
            if (distance < 1)
            {
                return bucketIndex;
            }

            // 整数部分にキャスト
            var intDistance = Mathf.FloorToInt(distance);

            // 基数 baseN で割り続ける
            while (intDistance >= BucketBase)
            {
                intDistance /= BucketBase; // baseNで割る
                bucketIndex++;
            }

            return bucketIndex;
        }

        private static string CreateNodeInfo()
        {
            var node = CreateSelfNodeData();
            var octreeMessage = new ConnectionSelectorMessage
            {
                type = NodeMessageType,
                data = JsonConvert.SerializeObject(node)
            };
            return JsonConvert.SerializeObject(octreeMessage);
        }

        private static Node CreateSelfNodeData()
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var node = new Node(
                nodeId: new NodeId(MistPeerData.I.SelfId),
                position: new Position(selfPosition)
            );
            return node;
        }

        private string CreateNodesInfo()
        {
            var nodes = routing.Buckets.SelectMany(b => b).ToList();
            nodes.Add(CreateSelfNodeData());
            var octreeMessage = new ConnectionSelectorMessage
            {
                type = NodesMessageType,
                data = JsonConvert.SerializeObject(nodes)
            };
            return JsonConvert.SerializeObject(octreeMessage);
        }

        private async UniTask UpdateNodeInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(NodeUpdateIntervalSeconds), cancellationToken: token);

                var message = CreateNodesInfo();
                foreach (var id in routing.ConnectedNodes)
                {
                    if (string.IsNullOrEmpty(id)) Debug.LogError("[ConnectionSelector] Connected node id is empty");
                    Send(message, id);
                }
            }
        }

        private async UniTask UpdateRequestObjectInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(RequestObjectIntervalSeconds), cancellationToken: token);

                // 現在の位置を取得
                var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;

                // バケツのノードを距離順にソートして新規表示ノードを選定
                var visibleNodes = routing.Buckets
                    .Where(bucket => bucket.Count != 0)
                    .SelectMany(bucket => bucket)
                    .OrderBy(node => Vector3.Distance(selfPosition, node.Position.ToVector3()))
                    .Take(MaxVisibleNodes)
                    .Select(node => node.Id)
                    .ToHashSet();

                // 現在表示中のノードと比較し、表示する必要があるノードを追加
                var nodesToShow = visibleNodes.Except(_visibleNodes).ToList();
                foreach (var nodeId in nodesToShow)
                {
                    Debug.Log($"[ConnectionSelector] RequestObject: {nodeId}");
                    if (string.IsNullOrEmpty(nodeId)) Debug.LogError("[ConnectionSelector] Node id is empty");
                    RequestObject(nodeId); // Objectを表示するRequestを出す
                    _visibleNodes.Add(nodeId);
                }

                // 現在表示中のノードのうち、非表示にする必要があるノードを削除
                var nodesToHide = _visibleNodes.Except(visibleNodes).ToList();
                foreach (var nodeId in nodesToHide)
                {
                    Debug.Log($"[ConnectionSelector] RemoveObject: {nodeId}");
                    RemoveObject(nodeId); // Objectを非表示にする
                    _visibleNodes.Remove(nodeId);
                }

                // Debug用に表示
                var outputStr = "";
                foreach (var nodeId in _visibleNodes)
                {
                    outputStr += $"{nodeId}, ";
                }
                Debug.Log($"[ConnectionSelector] VisibleNodes: {_visibleNodes.Count} {outputStr}");
            }
        }

        private async UniTask UpdateDebugShowBucketInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
                for (var i = 0; i < routing.Buckets.Count; i++)
                {
                    var outputStr = "";
                    foreach (var node in routing.Buckets[i])
                    {
                        outputStr += $"{node.Id}, ";
                    }
                    Debug.Log($"[ConnectionSelector] Bucket {i}: {routing.Buckets[i].Count} {outputStr}");
                }
            }
        }

        private async UniTask UpdateFindNextConnect(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);

                foreach (var node in from bucket in routing.Buckets where bucket.Count != 0 select bucket.First())
                {
                    if (MistManager.I.CompareId(node.Id))
                    {
                        MistManager.I.Connect(node.Id).Forget();
                    }

                    break;
                }
            }
        }

        public override void OnSpawned(string id)
        {
            base.OnSpawned(id);
            _visibleNodes.Add(id);
        }

        public override void OnDestroyed(string id)
        {
            base.OnDestroyed(id);
            _visibleNodes.Remove(id);
        }
    }
}
