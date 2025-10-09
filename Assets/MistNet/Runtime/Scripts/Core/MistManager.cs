using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    /// <summary>
    /// Messageの管理を行う
    /// </summary>
    public class MistManager : MonoBehaviour
    {
        public static MistManager I;
        public PeerRepository PeerRepository;

        [field: SerializeField] public Selector Selector { get; private set; }
        public MistSignalingWebSocket MistSignalingWebSocket { get; private set; }

        private MistSyncManager _mistSyncManager;
        public RoutingBase Routing => Selector.RoutingBase;

        public IAOILayer AOI { get; private set; }
        public IWorldLayer World { get; private set; }
        public ITransportLayer Transport { get; private set; }

        public void Awake()
        {
            MistConfig.ReadConfig();
            PeerRepository = new();
            _mistSyncManager = new MistSyncManager();
            PeerRepository.Init();
            I = this;

            Transport = new MistTransportLayer(Selector);
            World = new MistWorldLayer(Transport, Selector);
            AOI = new MistAOILayer(World);

            Transport.Init();
        }

        private void Start()
        {
            MistSignalingWebSocket = new MistSignalingWebSocket();
            MistSignalingWebSocket.Init().Forget();
            _mistSyncManager.Start();
        }

        public void OnDestroy()
        {
            PeerRepository.Dispose();
            _mistSyncManager.Dispose();
            MistSignalingWebSocket.Dispose();
            Transport.Dispose();
            World.Dispose();
            AOI.Dispose();
        }

        [Obsolete]
        public void AddRPC(MistNetMessageType messageType, MessageReceivedHandler function)
        {
            AOI.AddRPC(messageType, function);
        }

        [Obsolete]
        public void AddObjectRPC(string key, Delegate function, Type[] types)
        {
            AOI.AddObjectRPC(key, function, types);
        }

        [Obsolete]
        public void RemoveRPC(string key)
        {
            AOI.RemoveRPC(key);
        }

        [Obsolete]
        public void RPC(NodeId targetId, string key, params object[] args)
        {
            AOI.RPC(targetId, key, args);
        }

        [Obsolete]
        public void RPCOther(string key, params object[] args)
        {
            AOI.RPCOther(key, args);
        }

        [Obsolete]
        public void RPCAll(string key, params object[] args)
        {
            AOI.RPCAll(key, args);
        }

        [Obsolete]
        public void OnRPC(byte[] data, NodeId sourceId)
        {
            AOI.OnRPC(data, sourceId);
        }

        [Obsolete]
        public void OnSpawned(NodeId id)
        {
            AOI.OnSpawned(id);
        }

        [Obsolete]
        public void OnDestroyed(NodeId id)
        {
            AOI.OnDestroyed(id);
        }

        [Obsolete]
        public void Send(MistNetMessageType type, byte[] data, NodeId targetId)
        {
            World.Send(type, data, targetId);
        }

        [Obsolete]
        public void SendAll(MistNetMessageType type, byte[] data)
        {
            World.SendAll(type, data);
        }

        [Obsolete]
        public void AddSendFailedCallback(Delegate callback)
        {
            World.AddSendFailedCallback(callback);
        }

        [Obsolete]
        public void Connect(NodeId id)
        {
            Transport.Connect(id);
        }

        [Obsolete]
        public void Disconnect(NodeId id)
        {
            Transport.Disconnect(id);
        }

        [Obsolete]
        public void DisconnectAll()
        {
            Transport.DisconnectAll();
        }

        [Obsolete]
        public void OnConnected(NodeId id)
        {
            Transport.OnConnected(id);
        }

        [Obsolete]
        public void OnDisconnected(NodeId id)
        {
            Transport.OnDisconnected(id);
        }

        [Obsolete]
        public void AddJoinedCallback(Delegate callback)
        {
            Transport.AddConnectCallback(callback);
        }

        [Obsolete]
        public void AddLeftCallback(Delegate callback)
        {
            Transport.AddDisconnectCallback(callback);
        }

        [Obsolete("Use InstantiatePlayerAsync instead. InstantiateAsync will be removed in future versions.")]
        public async UniTask<GameObject> InstantiateAsync(string prefabAddress, Vector3 position,
            Quaternion rotation, ObjectId objId = null)
        {
            return await AOI.InstantiatePlayerAsync(prefabAddress, position, rotation, objId);
        }

        [Obsolete]
        public async UniTask<GameObject> InstantiatePlayerAsync(string prefabAddress, Vector3 position,
            Quaternion rotation, ObjectId objId = null)
        {
            return await AOI.InstantiatePlayerAsync(prefabAddress, position, rotation, objId);
        }

        /// <summary>
        /// IDを比較する
        /// </summary>
        public bool CompareId(string sourceId)
        {
            var selfId = MistManager.I.PeerRepository.SelfId;
            return string.CompareOrdinal(selfId, sourceId) < 0;
        }

        private void Update()
        {
            _mistSyncManager.UpdateSyncObjects();
        }
    }
}
