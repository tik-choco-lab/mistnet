using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace MistNet
{
    public class KBucket
    {
        private const float PingTimeoutSeconds = 5f;
        public static int K = 20;
        public IReadOnlyList<NodeInfo> Nodes => _nodes.AsReadOnly();
        private readonly List<NodeInfo> _nodes;
        private readonly Dictionary<NodeInfo, NodeInfo> _pendingNodeList = new();
        private readonly Kademlia _kademlia;

        public KBucket(Kademlia kademlia)
        {
            _nodes = new List<NodeInfo>();
            _kademlia = kademlia;
            K = OptConfig.Data.KademliaK;
        }

        public void AddNode(NodeInfo newNode)
        {
            // すでにいるか探す
            var existing = _nodes.FirstOrDefault(n => n.Id.Equals(newNode.Id));
            if (existing != null)
            {
                // LRU 更新: 先頭から削除して末尾に付け直す
                _nodes.Remove(existing);
                _nodes.Add(existing);
                existing.LastSeen = DateTime.UtcNow;

                if (_pendingNodeList.ContainsKey(existing))
                {
                    // 置き換え候補から削除
                    _pendingNodeList.Remove(existing);
                }
                return;
            }

            // 空きがあるなら追加
            if (_nodes.Count < K)
            {
                _nodes.Add(newNode);
                return;
            }

            // 満杯 → 先頭を PING して置き換え判定
            var oldest = _nodes.First();
            if (_pendingNodeList.ContainsKey(oldest))
            {
                // すでに PING 済みなら何もしない
                return;
            }
            _kademlia.Ping(oldest);
            _pendingNodeList[oldest] = newNode; // 置き換え候補を記録
            MistLogger.Debug($"[KBucket] PING sent to {oldest.Id} for replacement with {newNode.Id}");
            ReplaceByTimeout(oldest).Forget();  // タイムアウトで置き換えを実行
        }

        public void RemoveNode(NodeId nodeId)
        {
            var existing = _nodes.FirstOrDefault(n => n.Id.Equals(nodeId));
            if (existing != null)
            {
                _nodes.Remove(existing);
            }

            // 置き換え候補からも削除
            var pending = _pendingNodeList.Keys.FirstOrDefault(n => n.Id.Equals(nodeId));
            if (pending != null)
            {
                _pendingNodeList.Remove(pending);
            }
        }

        private async UniTask ReplaceByTimeout(NodeInfo node)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(PingTimeoutSeconds)); // PING 待ち時間
            if (_pendingNodeList.ContainsKey(node))
            {
                // PING 応答がなかった場合、置き換え
                _nodes.Remove(node);
                _nodes.Add(_pendingNodeList[node]);
                _pendingNodeList.Remove(node);
            }
        }
    }
}
