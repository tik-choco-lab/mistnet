using MemoryPack;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace MistNet
{
    /// <summary>
    /// Messageの管理を行う
    /// </summary>
    public class MistManager : MonoBehaviour
    {
        private static readonly float WaitConnectingTimeSec = 3f;

        public static MistManager I;
        public MistPeerData MistPeerData;
        public Action<string> ConnectAction;
        public Action<string> OnConnectedAction;
        public Action<string> OnDisconnectedAction;

        [SerializeField] private IConnectionSelector connectionSelector;
        [SerializeField] public IRouting Routing;

        private readonly MistConfig _config = new();
        private readonly Dictionary<MistNetMessageType, Action<byte[], string>> _onMessageDict = new();
        private readonly Dictionary<string, Delegate> _functionDict = new();
        private readonly Dictionary<string, int> _functionArgsLengthDict = new();
        private readonly Dictionary<string, Type[]> _functionArgsTypeDict = new();

        public void Awake()
        {
            _config.ReadConfig();
            MistPeerData = new();
            MistPeerData.Init();
            I = this;
        }

        private void Start()
        {
            AddRPC(MistNetMessageType.RPC, OnRPC);
        }

        public void OnDestroy()
        {
            MistPeerData.AllForceClose();
        }

        public void Send(MistNetMessageType type, byte[] data, string targetId)
        {
            MistDebug.Log($"[SEND][{type.ToString()}] -> {targetId}");

            var message = new MistMessage
            {
                Id = MistPeerData.SelfId,
                Data = data,
                TargetId = targetId,
                Type = type,
            };
            var sendData = MemoryPackSerializer.Serialize(message);

            if (!MistPeerData.IsConnected(targetId))
            {
                targetId = Routing.Get(targetId);
                if (targetId == null)
                {
                    Debug.LogError($"[Error] {targetId} is not connected");
                    return; // メッセージの破棄
                }

                MistDebug.Log($"[SEND][FORWARD] {targetId} -> {message.TargetId}");
            }

            if (type == MistNetMessageType.ObjectInstantiateRequest)
            {
                MistDebug.Log($"[SEND][{type.ToString()}] {targetId} -> {message.TargetId}");
            }

            if (MistPeerData.IsConnected(targetId))
            {
                var peerData = MistPeerData.GetAllPeer[targetId];
                peerData.Peer.Send(sendData).Forget();
                return;
            }

            Debug.LogError($"[Error] {targetId} is not connected");
        }

        public void SendAll(MistNetMessageType type, byte[] data)
        {
            var message = new MistMessage
            {
                Id = MistPeerData.SelfId,
                Data = data,
                Type = type,
            };

            foreach (var peerData in MistPeerData.GetConnectedPeer)
            {
                MistDebug.Log($"[SEND][{peerData.Id}] {type.ToString()}");
                message.TargetId = peerData.Id;
                var sendData = MemoryPackSerializer.Serialize(message);
                peerData.Peer.Send(sendData).Forget();
            }
        }

        public void AddRPC(MistNetMessageType messageType, Action<byte[], string> function)
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

        public void RPC(string targetId, string key, params object[] args)
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

        private void OnRPC(byte[] data, string sourceId)
        {
            var message = MemoryPackSerializer.Deserialize<P_RPC>(data);
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

        public void OnMessage(byte[] data, string senderId)
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

            var targetId = message.TargetId;
            if (!MistPeerData.IsConnected(message.TargetId))
            {
                targetId = Routing.Get(message.TargetId);
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                var peer = MistPeerData.GetPeer(targetId);
                if (peer == null) return;
                peer.Send(data).Forget();
                MistDebug.Log(
                    $"[RECV][SEND][FORWARD][{message.Type.ToString()}] {message.Id} -> {MistPeerData.I.SelfId} -> {message.TargetId}");
            }
        }

        private bool IsMessageForSelf(MistMessage message)
        {
            return message.TargetId == MistPeerData.SelfId;
        }

        private void ProcessMessageForSelf(MistMessage message, string senderId)
        {
            Routing.Add(message.Id, senderId);
            _onMessageDict[message.Type].DynamicInvoke(message.Data, message.Id);
        }

        public async UniTaskVoid Connect(string id)
        {
            if (id == MistPeerData.I.SelfId) return;

            ConnectAction.Invoke(id);
            MistPeerData.GetPeerData(id).State = MistPeerState.Connecting;

            // await UniTask.Delay(TimeSpan.FromSeconds(WaitConnectingTimeSec));

            if (MistPeerData.GetPeerData(id).State == MistPeerState.Connecting)
            {
                MistDebug.Log($"[Connect] {id} is not connected");
                MistPeerData.GetPeerData(id).State = MistPeerState.Disconnected;
            }
        }

        public void OnConnected(string id)
        {
            MistDebug.Log($"[Connected] {id}");

            // InstantiateしたObject情報の送信
            MistPeerData.I.GetPeerData(id).State = MistPeerState.Connected;
            // MistSyncManager.I.SendObjectInstantiateInfo(id);
            connectionSelector.OnConnected(id);
            OnConnectedAction?.Invoke(id);
        }

        public void OnDisconnected(string id)
        {
            MistDebug.Log($"[Disconnected] {id}");
            MistSyncManager.I.DestroyBySenderId(id);
            connectionSelector.OnDisconnected(id);
            MistPeerData.I.OnDisconnected(id);
            OnDisconnectedAction?.Invoke(id);
        }

        public void OnSpawned(string id)
        {
            MistDebug.Log($"[Spawned] {id}");
            connectionSelector.OnSpawned(id);
        }

        public void OnDespawned(string id)
        {
            MistDebug.Log($"[Despawned] {id}");
            connectionSelector.OnDestroyed(id);
        }

        public void Disconnect(string id)
        {
            var peer = MistPeerData.GetPeer(id);
            peer.Close();
            OnDisconnected(id);
        }

        public void AddJoinedCallback(Delegate callback)
        {
            OnConnectedAction += (Action<string>)callback;
        }

        public void AddLeftCallback(Delegate callback)
        {
            OnDisconnectedAction += (Action<string>)callback;
        }

        /// <summary>
        /// TODO: prefabAddress.PrimaryKeyがAddressを表しているかどうかの確認が必要
        /// </summary>
        /// <param name="prefabAddress"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <returns></returns>
        public async UniTask<GameObject> InstantiateAsync(IResourceLocation prefabAddress, Vector3 position,
            Quaternion rotation)
        {
            var obj = await Addressables.InstantiateAsync(prefabAddress, position, rotation);
            InstantiateObject(prefabAddress.PrimaryKey, position, rotation, obj);
            return obj;
        }

        public async UniTask<GameObject> InstantiateAsync(string prefabAddress, Vector3 position, Quaternion rotation)
        {
            var obj = await Addressables.InstantiateAsync(prefabAddress, position, rotation);
            InstantiateObject(prefabAddress, position, rotation, obj);
            return obj;
        }

        private void InstantiateObject(string prefabAddress, Vector3 position, Quaternion rotation, GameObject obj)
        {
            var syncObject = obj.GetComponent<MistSyncObject>();
            var objId = Guid.NewGuid().ToString("N");
            syncObject.SetData(objId, true, prefabAddress, MistPeerData.SelfId);

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
    }
}
