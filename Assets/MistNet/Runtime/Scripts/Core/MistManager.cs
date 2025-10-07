using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MistNet
{
    /// <summary>
    /// Messageの管理を行う
    /// </summary>
    public class MistManager : MonoBehaviour
    {
        public static MistManager I;
        public PeerRepository PeerRepository;
        private Action<NodeId> _onConnectedAction;
        private Action<NodeId> _onDisconnectedAction;
        private Action<NodeId> _sendFailed;

        [field: SerializeField] public Selector Selector { get; private set; }
        public MistSignalingWebSocket MistSignalingWebSocket { get; private set; }
        private MistSignalingWebRTC _mistSignalingWebRtc;
        private MistSyncManager _mistSyncManager;
        public RoutingBase Routing => Selector.RoutingBase;

        private readonly Dictionary<MistNetMessageType, Action<byte[], NodeId>> _onMessageDict = new();
        private readonly Dictionary<string, Delegate> _methods = new();
        private readonly Dictionary<string, Type[]> _argTypes = new();

        public void Awake()
        {
            MistConfig.ReadConfig();
            PeerRepository = new();
            _mistSyncManager = new MistSyncManager();
            PeerRepository.Init();
            I = this;
        }

        private void Start()
        {
            AddRPC(MistNetMessageType.RPC, OnRPC);
            _mistSignalingWebRtc = new MistSignalingWebRTC();
            MistSignalingWebSocket = new MistSignalingWebSocket();
            _mistSyncManager.Start();
        }

        public void OnDestroy()
        {
            _onMessageDict.Clear();
            PeerRepository.Dispose();
            _mistSignalingWebRtc.Dispose();
            _mistSyncManager.Dispose();
            MistSignalingWebSocket.Dispose();
        }

        public void Send(MistNetMessageType type, byte[] data, NodeId targetId)
        {
            var message = new MistMessage
            {
                Id = PeerRepository.SelfId,
                Payload = data,
                TargetId = targetId,
                Type = type,
            };
            var sendData = MemoryPackSerializer.Serialize(message);

            if (!PeerRepository.IsConnected(targetId))
            {
                targetId = Routing.Get(targetId);
                if (targetId == null)
                {
                    MistLogger.Warning($"[Error] No route to {message.TargetId}");
                    _sendFailed?.Invoke(new NodeId(message.TargetId));
                    return; // メッセージの破棄
                }

                MistLogger.Trace($"[FORWARD] {targetId} {type} {message.TargetId}");
            }

            if (PeerRepository.IsConnected(targetId))
            {
                MistLogger.Trace($"[SEND][{type.ToString()}] {type} {targetId}");
                var peerData = PeerRepository.GetAllPeer[targetId];
                peerData.PeerEntity.Send(sendData);
            }
        }

        public void SendAll(MistNetMessageType type, byte[] data)
        {
            var message = new MistMessage
            {
                Id = PeerRepository.SelfId,
                Payload = data,
                Type = type,
            };

            foreach (var peerId in Routing.MessageNodes)
            {
                MistLogger.Trace($"[SEND][{peerId}] {type.ToString()}");
                message.TargetId = peerId;
                var sendData = MemoryPackSerializer.Serialize(message);
                var peerEntity = PeerRepository.GetPeer(peerId);
                peerEntity?.Send(sendData);
            }
        }

        public void AddRPC(MistNetMessageType messageType, Action<byte[], NodeId> function)
        {
            _onMessageDict.Add(messageType, function);
        }

        public void AddObjectRPC(string key, Delegate function, Type[] types)
        {
            _methods[key] = function;
            _argTypes[key] = types;
        }

        public void RemoveRPC(string key)
        {
            _methods.Remove(key);
            _argTypes.Remove(key);
        }

        public void RPC(NodeId targetId, string key, params object[] args)
        {
            var rpcArgs = WrapArgs(args);
            var sendData = new P_RPC
            {
                Method = key,
                Args = rpcArgs,
            };
            var bytes = MemoryPackSerializer.Serialize(sendData);
            Send(MistNetMessageType.RPC, bytes, targetId);
        }

        public void RPCOther(string key, params object[] args)
        {
            var rpcArgs = WrapArgs(args);
            var sendData = new P_RPC
            {
                Method = key,
                Args = rpcArgs,
            };
            var bytes = MemoryPackSerializer.Serialize(sendData);
            SendAll(MistNetMessageType.RPC, bytes);
        }

        public void RPCAll(string key, params object[] args)
        {
            RPCOther(key, args);

            if (!_methods.TryGetValue(key, out var del))
            {
                MistLogger.Warning($"Unknown RPC method: {key}");
                return;
            }

            del.DynamicInvoke(args);
        }

        private void OnRPC(byte[] data, NodeId sourceId)
        {
            var rpc = MemoryPackSerializer.Deserialize<P_RPC>(data);
            Invoke(rpc);
        }

        public void OnMessage(byte[] data, NodeId senderId)
        {
            var message = MemoryPackSerializer.Deserialize<MistMessage>(data);
            MistLogger.Trace($"[RECV][{message.Type.ToString()}] {message.Id} -> {message.TargetId}");

            if (IsMessageForSelf(message))
            {
                // 自身宛のメッセージの場合
                ProcessMessageForSelf(message, senderId);
                return;
            }

            // 他のPeer宛のメッセージの場合
            var targetId = new NodeId(message.TargetId);
            if (!PeerRepository.IsConnected(targetId))
            {
                targetId = Routing.Get(targetId);
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                var peer = PeerRepository.GetPeer(targetId);
                if (peer == null
                    || peer.RtcPeer == null
                    || peer.RtcPeer.ConnectionState != RTCPeerConnectionState.Connected
                    || peer.Id == PeerRepository.I.SelfId
                    || peer.Id == senderId)
                {
                    MistLogger.Warning($"[Error] Peer is null {targetId}");
                    return;
                }

                peer.Send(data);
                MistLogger.Trace(
                    $"[RECV][SEND][FORWARD][{message.Type.ToString()}] {message.Id} -> {PeerRepository.I.SelfId} -> {peer.Id}");
            }
        }

        private bool IsMessageForSelf(MistMessage message)
        {
            return message.TargetId == PeerRepository.SelfId;
        }

        private void ProcessMessageForSelf(MistMessage message, NodeId senderId)
        {
            Routing.AddRouting(new NodeId(message.Id), senderId);
            _onMessageDict[message.Type](message.Payload, new NodeId(message.Id));
        }

        public void Connect(NodeId id)
        {
            if (id == PeerRepository.I.SelfId) return;

            _mistSignalingWebRtc.Connect(id);
        }

        public void Disconnect(NodeId id)
        {
            if (id == PeerRepository.I.SelfId) return;

            Routing.RemoveMessageNode(id);
            Routing.Remove(id);
            OnDisconnected(id);
        }

        public void DisconnectAll()
        {
            MistLogger.Info("[DisconnectAll] All peers will be disconnected.");
            var peerIds = Routing.ConnectedNodes.ToArray();
            foreach (var peerId in peerIds)
            {
                Disconnect(peerId);
            }
        }

        public void OnConnected(NodeId id)
        {
            MistLogger.Info($"[Connected] {id}");
            Selector.SelectorBase.OnConnected(id);
            _onConnectedAction?.Invoke(id);
            Routing.OnConnected(id);
        }

        public void OnDisconnected(NodeId id)
        {
            MistLogger.Info($"[Disconnected] {id}");
            MistSyncManager.I.RemoveObject(id);
            Selector.SelectorBase.OnDisconnected(id);
            PeerRepository.I.OnDisconnected(id);
            _onDisconnectedAction?.Invoke(id);
            Routing.OnDisconnected(id);
        }

        public void OnSpawned(NodeId id)
        {
            MistLogger.Info($"[Spawned] {id}");
        }

        public void OnDestroyed(NodeId id)
        {
            MistLogger.Info($"[Destroyed] {id}");
        }

        public void AddJoinedCallback(Delegate callback)
        {
            _onConnectedAction += (Action<NodeId>)callback;
        }

        public void AddLeftCallback(Delegate callback)
        {
            _onDisconnectedAction += (Action<NodeId>)callback;
        }

        public void AddSendFailedCallback(Delegate callback)
        {
            _sendFailed += (Action<NodeId>)callback;
        }

        [Obsolete("Use InstantiatePlayerAsync instead. InstantiateAsync will be removed in future versions.")]
        public async UniTask<GameObject> InstantiateAsync(string prefabAddress, Vector3 position,
            Quaternion rotation, ObjectId objId = null)
        {
            return await InstantiatePlayerAsync(prefabAddress, position, rotation, objId);
        }

        public async UniTask<GameObject> InstantiatePlayerAsync(string prefabAddress, Vector3 position,
            Quaternion rotation, ObjectId objId = null)
        {
            var obj = await Addressables.InstantiateAsync(prefabAddress, position, rotation);
            InstantiatePlayerObject(prefabAddress, position, rotation, obj, objId);
            return obj;
        }

        private void InstantiatePlayerObject(string prefabAddress, Vector3 position, Quaternion rotation,
            GameObject obj, ObjectId objId)
        {
            var syncObject = obj.GetComponent<MistSyncObject>();
            objId ??= new ObjectId(PeerRepository.SelfId);
            syncObject.Init(objId, true, prefabAddress, PeerRepository.SelfId);

            // 接続先最適化に使用するため、PlayerObjectであることを設定
            MistSyncManager.I.SelfSyncObject = syncObject;

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
            var selfId = MistManager.I.PeerRepository.SelfId;
            return string.CompareOrdinal(selfId, sourceId) < 0;
        }

        private void Invoke(P_RPC rpc)
        {
            if (!_methods.TryGetValue(rpc.Method, out var del))
            {
                MistLogger.Warning($"Unknown RPC method: {rpc.Method}");
                return;
            }

            var argTypes = _argTypes[rpc.Method];
            var args = MemoryPackDeserializeArgs(rpc.Args, argTypes);

            del.DynamicInvoke(args);
        }

        private static object[] MemoryPackDeserializeArgs(RpcArgBase[] data, Type[] types)
        {
            var result = new object[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                var arg = data[i];

                object value = arg switch
                {
                    RpcArgInt a => a.Value,
                    RpcArgFloat a => a.Value,
                    RpcArgString a => a.Value,
                    RpcArgBool a => a.Value,
                    RpcArgByteArray a => a.Value,
                    _ => throw new InvalidOperationException($"Unsupported argument type: {arg?.GetType()}")
                };

                result[i] = value.GetType() == types[i]
                    ? value
                    : Convert.ChangeType(value, types[i]);
            }

            return result;
        }

        private static RpcArgBase[] WrapArgs(params object[] args)
        {
            if (args == null || args.Length == 0)
                return Array.Empty<RpcArgBase>();

            var result = new RpcArgBase[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                result[i] = arg switch
                {
                    int iVal => new RpcArgInt(iVal),
                    float fVal => new RpcArgFloat(fVal),
                    string sVal => new RpcArgString(sVal),
                    bool bVal => new RpcArgBool(bVal),
                    byte[] bytes => new RpcArgByteArray(bytes),
                    _ => throw new InvalidOperationException(
                        $"Unsupported RPC argument type: {arg?.GetType().FullName}")
                };
            }

            return result;
        }
    }
}
