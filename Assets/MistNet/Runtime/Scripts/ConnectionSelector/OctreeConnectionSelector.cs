using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class OctreeConnectionSelector : IConnectionSelector
    {
        private const int BucketSize = 20;
        private readonly HashSet<string> _connectedNodes = new();
        private readonly List<List<Node>> _buckets = new();
        private Dictionary<string, Action<string>> _onMessageReceived;

        protected override void Start()
        {
            base.Start();
            Debug.Log($"[ConnectionSelector] SelfId {MistPeerData.I.SelfId}");

            _onMessageReceived = new Dictionary<string, Action<string>>
            {
                { "node", OnNodeReceived }
            };
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

        protected override void OnMessage(string data, string id)
        {
            var message = JsonConvert.DeserializeObject<OctreeMessage>(data);
            _onMessageReceived[message.type](message.data);
        }

        private void OnNodeReceived(string data)
        {
            var node = JsonConvert.DeserializeObject<Node>(data);
            var nodeId = node.id;
            var position = node.position.ToVector3();
            var index = GetBucketIndex(nodeId, position);

            if (_buckets[index].Count >= BucketSize)
            {
                SendPing(index);
            }
            else
            {
                _buckets[index].Add(node);
            }
        }

        private void SendPing(int index)
        {
            var node = _buckets[index][0];
            Send(JsonConvert.SerializeObject(node), node.id);
        }

        private int GetBucketIndex(string nodeId, Vector3 position)
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var distance = Vector3.Distance(selfPosition, position);

            return CalculateBucketIndexUsingBitShift(distance);
        }

        private const int BucketPower = 2; // 初期値を2に設定 (2の累乗)

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

        private int CalculateBucketIndexUsingBaseN(float distance, int baseN)
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
            while (intDistance >= baseN)
            {
                intDistance /= baseN; // baseNで割る
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
    }
}
