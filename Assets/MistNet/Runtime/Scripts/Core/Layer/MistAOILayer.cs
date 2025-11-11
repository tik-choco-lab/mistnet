using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MistNet
{
    public class MistAOILayer : IAOILayer
    {
        private readonly Dictionary<string, Delegate> _methods = new();
        private readonly Dictionary<string, Type[]> _argTypes = new();
        private readonly IWorldLayer _worldLayer;
        private readonly Selector _selector;
        private readonly IPeerRepository _peerRepository;

        public MistAOILayer(IWorldLayer worldLayer, Selector selector, IPeerRepository peerRepository)
        {
            _selector = selector;
            _worldLayer = worldLayer;
            _worldLayer.RegisterReceive(MistNetMessageType.RPC, OnRPC);
            _peerRepository = peerRepository;
        }

        public void AddRPC(MistNetMessageType messageType, MessageReceivedHandler function)
        {
            _worldLayer.RegisterReceive(messageType, function);
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
            _worldLayer.Send(MistNetMessageType.RPC, bytes, targetId);
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
            _worldLayer.SendAll(MistNetMessageType.RPC, bytes);
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

        public void OnRPC(byte[] data, NodeId sourceId)
        {
            var rpc = MemoryPackSerializer.Deserialize<P_RPC>(data);
            Invoke(rpc);
        }

        public void SendAll(MistNetMessageType type, byte[] data)
        {
            var messageNode = _selector.RoutingBase.MessageNodes;
            foreach (var nodeId in messageNode)
            {
                _worldLayer.Send(type, data, nodeId);
            }
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
            objId ??= new ObjectId(_peerRepository.SelfId);
            syncObject.Init(objId, true, prefabAddress, _peerRepository.SelfId);

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
            _worldLayer.SendAll(MistNetMessageType.ObjectInstantiate, bytes);
        }

        public void OnSpawned(NodeId id)
        {
            MistLogger.Info($"[Spawned] {id}");
        }

        public void OnDestroyed(NodeId id)
        {
            MistLogger.Info($"[Destroyed] {id}");
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

        public void Dispose()
        {
            _methods.Clear();
            _argTypes.Clear();
        }
    }
}
