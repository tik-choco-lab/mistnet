using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class DhtConnectionSelector : IConnectionSelector
    {
        private const string NodeMessageType = "node";
        private const string NodesMessageType = "nodes";
        private const string PingMessageType = "ping";
        private const string PongMessageType = "pong";

        // timeout時間
        private MistOptConfig Data => OptConfigLoader.Data;

        [SerializeField] private DhtRouting routing;

        private Dictionary<string, Action<string, NodeId>> _onMessageReceived;
        private readonly Dictionary<string, bool> _pongWaitList = new();

        [Serializable]
        [Obsolete("Please use OptMessage instead.")]
        private class ConnectionSelectorMessage
        {
            public string type;
            public string data;
        }

        protected override void Start()
        {
            OptConfigLoader.ReadConfig();
            base.Start();
            MistDebug.Log($"[ConnectionSelector] SelfId {MistPeerData.I.SelfId}");

            _onMessageReceived = new Dictionary<string, Action<string, NodeId>>
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

        private void OnDestroy()
        {
            OptConfigLoader.WriteConfig();
        }

        protected override void OnMessage(string data, NodeId senderId)
        {
            MistDebug.Log($"[ConnectionSelector] OnMessage: {data}");
            var message = JsonConvert.DeserializeObject<ConnectionSelectorMessage>(data);
            _onMessageReceived[message.type](message.data, senderId);
        }

        private void OnNodeReceived(string data, NodeId senderId)
        {
            var node = JsonConvert.DeserializeObject<Node>(data);
            OnNodeReceived(node, senderId);
            routing.AddNode(node.Id, node);
        }

        private void OnNodeReceived(Node node, NodeId senderId)
        {
            var nodeId = node.Id;
            routing.Add(nodeId, senderId);

            if (nodeId == MistPeerData.I.SelfId) return; // 自分のNodeは無視

            var position = node.Position.ToVector3();
            var index = GetBucketIndex(position);
            MistDebug.Log($"[ConnectionSelector] OnNodeReceived: {nodeId} {position} {index}");

            var currentIndex = routing.GetBucketIndex(nodeId);
            if (currentIndex == -1)
            {
                var result = routing.AddBucket(index, node);
                if (result == DhtRouting.Result.Fail)
                {
                    // bucketがいっぱい
                    SendPingAndAddNode(index, node).Forget();
                }

                return;
            }

            if (index != currentIndex)
            {
                routing.ReplaceBucket(node, index);
            }
        }

        private void OnNodesReceived(string data, NodeId senderId)
        {
            var nodes = JsonConvert.DeserializeObject<List<Node>>(data);
            foreach (var node in nodes)
            {
                OnNodeReceived(node, senderId);
                routing.AddNode(node.Id, node);
            }
        }

        private void OnPingReceived(string data, NodeId senderId)
        {
            var message = new ConnectionSelectorMessage
            {
                type = PongMessageType,
                data = MistPeerData.I.SelfId,
            };
            Send(JsonConvert.SerializeObject(message), senderId);
        }

        private void OnPongReceived(string data, NodeId senderId)
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
            MistDebug.Log($"[ConnectionSelector] SendPingAndAddNode: {oldNode.Id} -> {newNode.Id}");

            var octreeMessage = new ConnectionSelectorMessage
            {
                type = PingMessageType,
            };

            Send(JsonConvert.SerializeObject(octreeMessage), oldNode.Id);
            _pongWaitList.TryAdd(oldNode.Id, false);

            // Timeoutになるか、pongが返ってくるまで待機
            var pongReceivedTask = UniTask.WaitUntil(() => _pongWaitList[oldNode.Id]);
            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(Data.PingTimeoutSeconds));
            await UniTask.WhenAny(pongReceivedTask, timeoutTask);

            if (!_pongWaitList[oldNode.Id])
            {
                routing.RemoveBucket(index, oldNode);
                routing.AddBucket(index, newNode);
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
            while (intDistance >= Data.BucketBase)
            {
                intDistance /= Data.BucketBase; // baseNで割る
                bucketIndex++;
            }

            return bucketIndex;
        }

        private static string CreateNodeInfo()
        {
            var node = NodeUtils.GetSelfNodeData();
            var octreeMessage = new ConnectionSelectorMessage
            {
                type = NodeMessageType,
                data = JsonConvert.SerializeObject(node)
            };
            return JsonConvert.SerializeObject(octreeMessage);
        }

        private string CreateNodesInfo()
        {
            var nodes = routing.Buckets.SelectMany(b => b).ToList();
            nodes.Add(NodeUtils.GetSelfNodeData());
            var octreeMessage = new ConnectionSelectorMessage
            {
                type = NodesMessageType,
                data = JsonConvert.SerializeObject(nodes)
            };
            return JsonConvert.SerializeObject(octreeMessage);
        }

        /// <summary>
        /// Node情報を接続中のNodeに送信する
        /// </summary>
        /// <param name="token"></param>
        private async UniTask UpdateNodeInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Data.SendInfoIntervalSeconds), cancellationToken: token);

                var message = CreateNodesInfo();
                foreach (var id in routing.ConnectedNodes)
                {
                    if (string.IsNullOrEmpty(id)) MistDebug.LogError("[ConnectionSelector] Connected node id is empty");
                    Send(message, id);
                }
            }
        }

        /// <summary>
        /// 定期的に実行する
        /// 表示するObjectの情報を更新する
        /// </summary>
        /// <param name="token"></param>
        private async UniTask UpdateRequestObjectInfo(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Data.RequestObjectIntervalSeconds), cancellationToken: token);

                // 現在の位置を取得
                var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;

                // バケツのノードを距離順にソートして新規表示ノードを選定
                var visibleNodes = routing.Buckets
                    .Where(bucket => bucket.Count != 0)
                    .SelectMany(bucket => bucket)
                    .Where(node => routing.ConnectedNodes.Contains(node.Id))
                    .OrderBy(node => Vector3.Distance(selfPosition, node.Position.ToVector3()))
                    .Take(Data.VisibleCount)
                    .Select(node => node.Id)
                    .ToHashSet();

                // 現在表示中のノードと比較し、表示する必要があるノードを追加
                var nodesToShow = visibleNodes.Except(routing.AoiNodes).ToList();
                foreach (var nodeId in nodesToShow)
                {
                    MistDebug.Log($"[ConnectionSelector] RequestObject: {nodeId}");
                    if (string.IsNullOrEmpty(nodeId)) MistDebug.LogError("[ConnectionSelector] Node id is empty");
                    RequestObject(nodeId); // Objectを表示するRequestを出す
                    routing.AddAoiNode(nodeId);
                }

                // 現在表示中のノードのうち、非表示にする必要があるノードを削除
                var nodesToHide = routing.AoiNodes.Except(visibleNodes).ToList();
                foreach (var nodeId in nodesToHide)
                {
                    MistDebug.Log($"[ConnectionSelector] RemoveObject: {nodeId}");
                    RemoveObject(nodeId); // Objectを非表示にする
                    routing.RemoveAoiNode(nodeId);
                }

                // Debug用に表示
                var outputStr = "";
                foreach (var nodeId in routing.AoiNodes)
                {
                    outputStr += $"{nodeId}, ";
                }

                MistDebug.Log($"[ConnectionSelector] VisibleNodes: {routing.AoiNodes.Count} {outputStr}");
            }
        }

        /// <summary>
        /// [Debug] バケツ情報を表示
        /// </summary>
        /// <param name="token"></param>
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

                    MistDebug.Log($"[ConnectionSelector] Bucket {i}: {routing.Buckets[i].Count} {outputStr}");
                }
            }
        }

        /// <summary>
        /// 次に接続するNodeを探す
        /// </summary>
        /// <param name="token"></param>
        private async UniTask UpdateFindNextConnect(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                MistDebug.Log("[ConnectionSelector] UpdateFindNextConnect");
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);

                foreach (var node in from bucket in routing.Buckets where bucket.Count != 0 select bucket.First())
                {
                    if (MistPeerData.I.IsConnectingOrConnected(node.Id)) continue;

                    MistDebug.Log($"[ConnectionSelector] Connecting: {node.Id}");
                    if (MistManager.I.CompareId(node.Id))
                    {
                        MistManager.I.Connect(node.Id);
                    }

                    break;
                }
            }
        }
    }
}
