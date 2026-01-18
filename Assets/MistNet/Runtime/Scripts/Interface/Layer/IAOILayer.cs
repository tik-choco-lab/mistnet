using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    public interface IAOILayer : IDisposable
    {
        void AddRPC(MistNetMessageType messageType, MessageReceivedHandler function);
        void AddObjectRPC(string key, Delegate function, Type[] types);
        void RemoveRPC(string key);
        void RPC(NodeId targetId, string key, params object[] args);
        void SendAll(MistNetMessageType type, byte[] data);
        
        /// <summary>
        /// 位置同期専用の高速チャンネルで全ノードに送信
        /// </summary>
        void SendAllLocation(byte[] data);
        
        /// <summary>
        /// 位置同期受信コールバックを登録
        /// </summary>
        void RegisterLocationReceive(Action<byte[], NodeId> callback);
        
        void RPCOther(string key, params object[] args);
        void RPCAll(string key, params object[] args);
        void OnRPC(byte[] data, NodeId sourceId);
        void OnSpawned(NodeId id);
        void OnDestroyed(NodeId id);

        UniTask<GameObject> InstantiatePlayerAsync(string prefabAddress, Vector3 position,
            Quaternion rotation, ObjectId objId = null);
    }
}
