using System;
using System.Collections.Generic;
using MistNet.Utils;
using Newtonsoft.Json;

namespace MistNet
{
    public class NodeInfo
    {
        private NodeId _id;

        [JsonProperty("id")]
        public NodeId Id
        {
            get => _id;
            set
            {
                _id = value;
                IdBytes = _id != null ? IdUtil.ToBytes(_id.ToString()) : null;
            }
        }

        [JsonIgnore]
        public byte[] IdBytes { get; private set; }

        [JsonIgnore]
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;

        // デフォルトコンストラクタ
        public NodeInfo()
        {
            // 初期値依存を残す場合（テスト時などに注意）
            if (MistManager.I.PeerRepository != null)
            {
                Id = MistManager.I.PeerRepository.SelfId;
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NodeInfo);
        }

        public bool Equals(NodeInfo other)
        {
            return other != null && EqualityComparer<NodeId>.Default.Equals(Id, other.Id);
        }

        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }

        public static bool operator ==(NodeInfo left, NodeInfo right)
        {
            return EqualityComparer<NodeInfo>.Default.Equals(left, right);
        }

        public static bool operator !=(NodeInfo left, NodeInfo right)
        {
            return !(left == right);
        }
    }
}
