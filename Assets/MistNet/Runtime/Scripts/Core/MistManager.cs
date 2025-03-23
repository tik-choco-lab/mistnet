using MemoryPack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MistNet
{
    /// <summary>
    /// Messageの管理を行う
    /// </summary>
    public class MistManager : MonoBehaviour
    {
        public static MistManager I;
        public MistPeerData MistPeerData;
        public Action<NodeId> ConnectAction;
        private Action<NodeId> _onConnectedAction;
        private Action<NodeId> _onDisconnectedAction;

        [SerializeField] private IConnectionSelector connectionSelector;
        [SerializeField] public IRouting routing;

        private readonly MistConfig _config = new();
        private readonly Dictionary<MistNetMessageType, Action<byte[], NodeId>> _onMessageDict = new();
        private readonly Dictionary<string, Delegate> _functionDict = new();
        private readonly Dictionary<string, int> _functionArgsLengthDict = new();
        private readonly Dictionary<string, Type[]> _functionArgsTypeDict = new();
        private JsonSerializerSettings _jsonSettings;

        public void Awake()
        {
            MistConfig.ReadConfig();
            MistPeerData = new();
            MistPeerData.Init();
            I = this;
            
            // JsonSerializerSettingsの初期化
            _jsonSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter>
                {
                    new NodeIdConverter()
                }
            };
        }

        private void Start()
        {
            AddRPC(MistNetMessageType.RPC, OnRPC);
        }

        public void OnDestroy()
        {
            MistPeerData.AllForceClose();
            MistConfig.WriteConfig();
        }

        public void Send(MistNetMessageType type, byte[] data, NodeId targetId)
        {
            var message = new MistMessage
            {
                Id = MistPeerData.SelfId,
                Payload = data,
                TargetId = targetId,
                Type = type,
            };
            var sendData = MemoryPackSerializer.Serialize(message);

            if (!MistPeerData.IsConnected(targetId))
            {
                targetId = routing.Get(targetId);
                if (targetId == null) return; // メッセージの破棄
                MistDebug.Log($"[FORWARD] {targetId} {type} {message.TargetId}");
            }
            if (MistPeerData.IsConnected(targetId))
            {
                MistDebug.Log($"[SEND][{type.ToString()}] {type} {targetId}");
                var peerData = MistPeerData.GetAllPeer[targetId];
                peerData.Peer.Send(sendData);
            }
        }

        public void SendAll(MistNetMessageType type, byte[] data)
        {
            var message = new MistMessage
            {
                Id = MistPeerData.SelfId,
                Payload = data,
                Type = type,
            };

            foreach (var peerId in routing.MessageNodes)
            {
                MistDebug.Log($"[SEND][{peerId}] {type.ToString()}");
                message.TargetId = peerId;
                var sendData = MemoryPackSerializer.Serialize(message);
                var peerData = MistPeerData.GetPeer(peerId);
                peerData.Send(sendData);
            }
        }

        public void AddRPC(MistNetMessageType messageType, Action<byte[], NodeId> function)
        {
            _onMessageDict.Add(messageType, function);
        }

        public void AddRPC(string key, Delegate function, Type[] types)
        {
            _functionDict.Add(key, function);
            var parameters = function.GetMethodInfo().GetParameters();
            _functionArgsLengthDict.Add(key, parameters.Length);
            _functionArgsTypeDict.Add(key, types);
        }

        public void RemoveRPC(string key)
        {
            _functionDict.Remove(key);
            _functionArgsLengthDict.Remove(key);
            _functionArgsTypeDict.Remove(key);
        }

        public void RPC(NodeId targetId, string key, params object[] args)
        {
            var argsString = string.Join(",", args);
            var sendData = new P_RPC
            {
                Method = key,
                Args = argsString,
            };
            var bytes = MemoryPackSerializer.Serialize(sendData);
            Send(MistNetMessageType.RPC, bytes, targetId);
        }

        public void RPCOther(string key, params object[] args)
        {
            var argsString = string.Join(",", args);
            var sendData = new P_RPC
            {
                Method = key,
                Args = argsString,
            };
            var bytes = MemoryPackSerializer.Serialize(sendData);
            SendAll(MistNetMessageType.RPC, bytes);
        }

        public void RPCAll(string key, params object[] args)
        {
            RPCOther(key, args);
            _functionDict[key].DynamicInvoke(args);
        }

        private void OnRPC(byte[] data, NodeId sourceId)
        {
            var message = MemoryPackSerializer.Deserialize<P_RPC>(data);
            if (!_functionDict.ContainsKey(message.Method))
            {
                MistDebug.LogError($"[Error][RPC] {message.Method} is not found");
                return;
            }

            var args = ConvertStringToObjects(message.Method, message.Args);
            var argsLength = _functionArgsLengthDict[message.Method];

            if (args.Count != argsLength)
            {
                args.Add(new MessageInfo
                {
                    SourceId = sourceId,
                });
            }

            _functionDict[message.Method].DynamicInvoke(args.ToArray());
        }

        private List<object> ConvertStringToObjects(string key, string input)
        {
            var types = _functionArgsTypeDict[key];
            var objects = new List<object>(types.Length);
            var parts = input.Split(',');

            // typesを使って、partsを変換する
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                var converter = TypeDescriptor.GetConverter(type);
                var obj = converter.ConvertFromString(parts[i]);
                objects.Add(obj);
            }

            return objects;
        }

        public void OnMessage(byte[] data, NodeId senderId)
        {
            var message = MemoryPackSerializer.Deserialize<MistMessage>(data);
            MistDebug.Log($"[RECV][{message.Type.ToString()}] {message.Id} -> {message.TargetId}");

            if (IsMessageForSelf(message))
            {
                // 自身宛のメッセージの場合
                ProcessMessageForSelf(message, senderId);
                return;
            }

            // 他のPeer宛のメッセージの場合
            var targetId = new NodeId(message.TargetId);
            if (!MistPeerData.IsConnected(targetId))
            {
                targetId = routing.Get(targetId);
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                var peer = MistPeerData.GetPeer(targetId);
                if (peer == null
                    || peer.Connection.ConnectionState != RTCPeerConnectionState.Connected
                    || peer.Id == MistPeerData.I.SelfId
                    || peer.Id == senderId)
                {
                    MistDebug.LogWarning($"[Error] Peer is null {targetId}");
                    return;
                }

                peer.Send(data);
                MistDebug.Log(
                    $"[RECV][SEND][FORWARD][{message.Type.ToString()}] {message.Id} -> {MistPeerData.I.SelfId} -> {peer.Id}");
            }
        }

        private bool IsMessageForSelf(MistMessage message)
        {
            return message.TargetId == MistPeerData.SelfId;
        }

        private void ProcessMessageForSelf(MistMessage message, NodeId senderId)
        {
            routing.Add(new NodeId(message.Id), senderId);
            _onMessageDict[message.Type](message.Payload, new NodeId(message.Id));
        }

        public void Connect(NodeId id)
        {
            if (id == MistPeerData.I.SelfId) return;

            ConnectAction.Invoke(id);
        }

        public void OnConnected(NodeId id)
        {
            MistDebug.Log($"[Connected] {id}");
            connectionSelector.OnConnected(id);
            _onConnectedAction?.Invoke(id);
            routing.OnConnected(id);
        }

        public void OnDisconnected(NodeId id)
        {
            MistDebug.Log($"[Disconnected] {id}");
            MistSyncManager.I.DestroyBySenderId(id);
            connectionSelector.OnDisconnected(id);
            MistPeerData.I.OnDisconnected(id);
            _onDisconnectedAction?.Invoke(id);
            routing.OnDisconnected(id);
        }

        public void OnSpawned(NodeId id)
        {
            MistDebug.Log($"[Spawned] {id}");
        }

        public void OnDestroyed(NodeId id)
        {
            MistDebug.Log($"[Destroyed] {id}");
        }

        public void AddJoinedCallback(Delegate callback)
        {
            _onConnectedAction += (Action<NodeId>)callback;
        }

        public void AddLeftCallback(Delegate callback)
        {
            _onDisconnectedAction += (Action<NodeId>)callback;
        }

        public async UniTask<GameObject> InstantiateAsync(string prefabAddress, Vector3 position, Quaternion rotation, ObjectId objId = null)
        {
            var obj = await Addressables.InstantiateAsync(prefabAddress, position, rotation);
            InstantiateObject(prefabAddress, position, rotation, obj, objId);
            return obj;
        }

        private void InstantiateObject(string prefabAddress, Vector3 position, Quaternion rotation, GameObject obj, ObjectId objId)
        {
            var syncObject = obj.GetComponent<MistSyncObject>();
            objId ??= new ObjectId(Guid.NewGuid().ToString("N"));
            syncObject.SetData(new ObjectId(objId), true, prefabAddress, MistPeerData.SelfId);

            MistSyncManager.I.RegisterSyncObject(syncObject);

            var sendData = new P_ObjectInstantiate()
            {
                ObjId = objId,
                Position = position,
                Rotation = rotation.eulerAngles,
                PrefabAddress = prefabAddress,
            };

            var bytes = MemoryPackSerializer.Serialize(sendData);
            SendAll(MistNetMessageType.ObjectInstantiate, bytes);
        }

        /// <summary>
        /// IDを比較する
        /// </summary>
        public bool CompareId(string sourceId)
        {
            var selfId = MistManager.I.MistPeerData.SelfId;
            return string.CompareOrdinal(selfId, sourceId) < 0;
        }

        // JsonSerializerSettingsを使用するヘルパーメソッド
        private string SerializeJson<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, _jsonSettings);
        }

        private T DeserializeJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
        }
    }
}
