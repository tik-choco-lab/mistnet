using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MistNet
{
    public class MistSyncManager : IDisposable
    {
        private const float SyncIntervalSeconds = 0.5f; // 同期間隔
        public static MistSyncManager I { get; private set; }
        public MistSyncObject SelfSyncObject { get; set; }
        public bool DestroyMyObjectsOnDisconnect { get; set; } // 自身のSyncObject

        private readonly Dictionary<ObjectId, MistSyncObject> _syncObjects = new();    // objId, MistSyncObject

        // ユーザーが退出した際のGameObjectの削除に使用している Instantiateで生成されたObjectに限る
        public readonly Dictionary<NodeId, List<ObjectId>> ObjectIdsByOwnerId = new();  // ownerId, objId　
        private MistSyncObject _myPlayerObject; // 自身のプレイヤーオブジェクト

        private readonly MistObjectPool _objectPool = new();
        private readonly CancellationTokenSource _cts;
        private float _timeSync;
        private readonly ILayer _layer;

        public MistSyncManager(ILayer layer)
        {
            I = this;
            _cts = new();
            _layer = layer;
        }

        public void Start()
        {
            _layer.AOI.AddRPC(MistNetMessageType.ObjectInstantiate,
                (a, b) => ReceiveObjectInstantiateInfo(a, b).Forget());
            _layer.AOI.AddRPC(MistNetMessageType.Location, ReceiveLocation);
            _layer.AOI.AddRPC(MistNetMessageType.PropertyRequest, ReceiveRequestProperty);
            _layer.AOI.AddRPC(MistNetMessageType.ObjectInstantiateRequest, ReceiveObjectInstantiateInfoRequest);
        }

        public void Dispose()
        {
            _objectPool?.Dispose();
            _cts.Cancel();
        }

        private async UniTask SendObjectInstantiateInfo(NodeId id)
        {
            // _myPlayerObjectが確実に入るまで待機
            await UniTask.WaitUntil(() => _myPlayerObject != null);

            var objTransform = _myPlayerObject.transform;
            var sendData = new P_ObjectInstantiate
            {
                ObjId = _myPlayerObject.Id,
                Position = objTransform.position,
                Rotation = objTransform.rotation.eulerAngles,
                PrefabAddress = _myPlayerObject.PrefabAddress
            };

            var data = MemoryPackSerializer.Serialize(sendData);
            _layer.World.Send(MistNetMessageType.ObjectInstantiate, data, id);
            MistLogger.Debug($"[Sync] SendObjectInstantiateInfo: {id}");
        }

        private async UniTaskVoid ReceiveObjectInstantiateInfo(byte[] data, NodeId sourceId)
        {
            var instantiateData = MemoryPackSerializer.Deserialize<P_ObjectInstantiate>(data);
            if (_syncObjects.ContainsKey(new ObjectId(instantiateData.ObjId)))
            {
                MistLogger.Warning($"[Sync] Object with id {instantiateData.ObjId} already exists!");
                return;
            }

            // -----------------
            // NOTE: これを入れないと高確率で生成に失敗する　おそらくIDの取得が間に合わないためであると考えられる
            await UniTask.Yield();
            var objId = new ObjectId(instantiateData.ObjId);
            if (!_objectPool.TryGetObject(objId, out var obj))
            {
                obj = await Addressables.InstantiateAsync(instantiateData.PrefabAddress);
                if (_objectPool.TryGetObject(objId, out _))
                {
                    MistLogger.Warning($"[Sync] Object with id {instantiateData.ObjId} already exists!");
                    return;
                }

                _objectPool.AddObject(objId, obj);
            }

            obj.transform.position = instantiateData.Position;
            obj.transform.rotation = Quaternion.Euler(instantiateData.Rotation);

            // -----------------
            var syncObject = obj.GetComponent<MistSyncObject>();
            syncObject.Init(new ObjectId(instantiateData.ObjId), true, instantiateData.PrefabAddress, sourceId);

            _layer.AOI.OnSpawned(sourceId);
            MistLogger.Debug($"[Sync] ReceiveObjectInstantiateInfo {sourceId}");
        }

        /// <summary>
        /// 相手はRequestを出して，自身は出さないことがある
        /// </summary>
        /// <param name="id"></param>
        public void RequestObjectInstantiateInfo(NodeId id)
        {
            var sendData = new P_ObjectInstantiateRequest();
            var bytes = MemoryPackSerializer.Serialize(sendData);
            _layer.World.Send(MistNetMessageType.ObjectInstantiateRequest, bytes, id);
            MistLogger.Debug($"[Sync] RequestObjectInstantiateInfo: {id}");
        }

        /// <summary>
        /// TODO: ここが呼ばれないことがある
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sourceId"></param>
        private void ReceiveObjectInstantiateInfoRequest(byte[] data, NodeId sourceId)
        {
            MistLogger.Debug($"[Sync] ReceiveObjectInstantiateInfoRequest {sourceId}");
            SendObjectInstantiateInfo(sourceId).Forget();
        }

        public void RemoveObject(NodeId targetId)
        {
            MistLogger.Debug($"[Sync] RemoveObject: {targetId}");
            if (DestroyMyObjectsOnDisconnect) DestroyBySenderId(targetId);
            else DestroyPlayerObject(targetId);
        }

        private void DestroyPlayerObject(NodeId targetId)
        {
            if (!ObjectIdsByOwnerId.ContainsKey(targetId))
            {
                MistLogger.Warning($"No objects found for ownerId: {targetId}");
                return;
            }

            var playerObjectId = ObjectIdsByOwnerId[targetId].FirstOrDefault(
                id => _syncObjects.ContainsKey(id) && _syncObjects[id].IsPlayerObject);

            if (playerObjectId == null)
            {
                MistLogger.Warning($"No player object found for ownerId: {targetId}");
                return;
            }

            var playerObject = _syncObjects[playerObjectId];
            _objectPool.Destroy(playerObject.gameObject);
            _syncObjects.Remove(playerObjectId);
        }

        private void ReceiveLocation(byte[] data, NodeId sourceId)
        {
            var location = MemoryPackSerializer.Deserialize<P_Location>(data);
            var syncObject = GetSyncObject(new ObjectId(location.ObjId));
            if (syncObject == null) return;
            syncObject.MistTransform.ReceiveLocation(location);
        }

        public void RegisterSyncObject(MistSyncObject syncObject)
        {
            if (!_syncObjects.TryAdd(syncObject.Id, syncObject))
            {
                MistLogger.Warning($"Sync object with id {syncObject.Id} already exists!");
                return;
            }

            switch (syncObject.IsOwner)
            {
                case true when syncObject.IsPlayerObject:
                    _myPlayerObject = syncObject;
                    break;
                case false:
                {
                    // 自身以外のSyncObjectの登録
                    var sendData = new P_PropertyRequest
                    {
                        ObjId = syncObject.Id,
                    };
                    var bytes = MemoryPackSerializer.Serialize(sendData);
                    _layer.World.Send(MistNetMessageType.PropertyRequest, bytes, syncObject.OwnerId);
                    break;
                }
            }

            // OwnerIdAndObjIdDictに登録 自動削除で使用する
            if (!ObjectIdsByOwnerId.ContainsKey(syncObject.OwnerId))
            {
                ObjectIdsByOwnerId[syncObject.OwnerId] = new List<ObjectId>();
            }
            ObjectIdsByOwnerId[syncObject.OwnerId].Add(syncObject.Id);
        }

        public void UnregisterSyncObject(MistSyncObject syncObject)
        {
            if (!_syncObjects.ContainsKey(syncObject.Id))
            {
                MistLogger.Warning($"Sync object with id {syncObject.Id} does not exist!");
                return;
            }

            _syncObjects.Remove(syncObject.Id);
            if (!ObjectIdsByOwnerId.ContainsKey(syncObject.OwnerId))
            {
                MistLogger.Warning($"No objects found for ownerId: {syncObject.OwnerId}");
                return;
            }
            ObjectIdsByOwnerId[syncObject.OwnerId].Remove(syncObject.Id);
            
            _layer.AOI.OnDestroyed(syncObject.OwnerId);
        }

        public MistSyncObject GetSyncObject(NodeId id)
        {
            var objectId = ObjectIdsByOwnerId
                .FirstOrDefault(pair => pair.Key == id).Value?.FirstOrDefault();
            if (objectId == null || !_syncObjects.ContainsKey(objectId))
            {
                return null;
            }

            return _syncObjects[objectId];
        }

        public MistSyncObject GetSyncObject(ObjectId id)
        {
            if (!_syncObjects.ContainsKey(id))
            {
                MistLogger.Warning($"Sync object with id {id} does not exist!");
                return null;
            }

            return _syncObjects[id];
        }

        private void DestroyBySenderId(NodeId senderId)
        {
            if (!ObjectIdsByOwnerId.ContainsKey(senderId))
            {
                MistLogger.Warning("Already destroyed");
                return;
            }

            var objIds = ObjectIdsByOwnerId[senderId];
            foreach (var id in objIds)
            {
                _objectPool.Destroy(_syncObjects[id].gameObject);
                _syncObjects.Remove(id);
            }

            ObjectIdsByOwnerId.Remove(senderId);
        }

        private void ReceiveRequestProperty(byte[] data, NodeId sourceId)
        {
            var requestData = MemoryPackSerializer.Deserialize<P_PropertyRequest>(data);
            if (!_syncObjects.TryGetValue(new ObjectId(requestData.ObjId), out var syncObject)) return;
            syncObject.SendAllProperties(sourceId);
        }

        public void UpdateSyncObjects()
        {
            _timeSync += Time.deltaTime;
            if (_timeSync < SyncIntervalSeconds) return;
            _timeSync = 0f;
            foreach (var syncObject in _syncObjects.Values)
            {
                if (!syncObject.IsOwner) continue;
                syncObject.WatchPropertiesAsync();
            }
        }
    }
}
