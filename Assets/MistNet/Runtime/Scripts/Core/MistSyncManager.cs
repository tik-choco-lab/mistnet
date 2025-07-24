using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MistNet
{
    public class MistSyncManager : MonoBehaviour
    {
        public static MistSyncManager I { get; private set; }
        public MistSyncObject SelfSyncObject { get; set; }
        public bool DestroyMyObjectsOnDisconnect { get; set; } // 自身のSyncObject

        private readonly Dictionary<ObjectId, MistSyncObject> _syncObjects = new();    // objId, MistSyncObject
        private readonly Dictionary<ObjectId, MistAnimator> _syncAnimators = new();    // objId, MistAnimator

        // ユーザーが退出した際のGameObjectの削除に使用している Instantiateで生成されたObjectに限る
        public readonly Dictionary<NodeId, List<ObjectId>> ObjectIdsByOwnerId = new();  // ownerId, objId　
        private MistSyncObject _myPlayerObject; // 自身のプレイヤーオブジェクト

        private readonly MistObjectPool _objectPool = new();

        private void Awake()
        {
            I = this;
        }

        private void Start()
        {
            MistManager.I.AddRPC(MistNetMessageType.ObjectInstantiate,
                (a, b) => ReceiveObjectInstantiateInfo(a, b).Forget());
            MistManager.I.AddRPC(MistNetMessageType.Location, ReceiveLocation);
            MistManager.I.AddRPC(MistNetMessageType.Animation, ReceiveAnimation);
            MistManager.I.AddRPC(MistNetMessageType.PropertyRequest, ReceiveRequestProperty);
            MistManager.I.AddRPC(MistNetMessageType.ObjectInstantiateRequest, ReceiveObjectInstantiateInfoRequest);
        }

        private void OnDestroy()
        {
            _objectPool.Dispose();
        }

        private void SendObjectInstantiateInfo(NodeId id)
        {
            var objTransform = _myPlayerObject.transform;
            var sendData = new P_ObjectInstantiate
            {
                ObjId = _myPlayerObject.Id,
                Position = objTransform.position,
                Rotation = objTransform.rotation.eulerAngles,
                PrefabAddress = _myPlayerObject.PrefabAddress
            };

            var data = MemoryPackSerializer.Serialize(sendData);
            MistManager.I.Send(MistNetMessageType.ObjectInstantiate, data, id);
            MistDebug.Log($"[Debug] SendObjectInstantiateInfo: {id}");
        }

        private async UniTaskVoid ReceiveObjectInstantiateInfo(byte[] data, NodeId sourceId)
        {
            var instantiateData = MemoryPackSerializer.Deserialize<P_ObjectInstantiate>(data);
            if (_syncObjects.ContainsKey(new ObjectId(instantiateData.ObjId))) return;

            // -----------------
            // NOTE: これを入れないと高確率で生成に失敗する　おそらくIDの取得が間に合わないためであると考えられる
            await UniTask.Yield();
            var objId = new ObjectId(instantiateData.ObjId);
            if (!_objectPool.TryGetObject(objId, out var obj))
            {
                obj = await Addressables.InstantiateAsync(instantiateData.PrefabAddress);
                _objectPool.AddObject(objId, obj);
            }

            obj.transform.position = instantiateData.Position;
            obj.transform.rotation = Quaternion.Euler(instantiateData.Rotation);

            // -----------------
            var syncObject = obj.GetComponent<MistSyncObject>();
            syncObject.Init(new ObjectId(instantiateData.ObjId), true, instantiateData.PrefabAddress, sourceId);

            MistManager.I.OnSpawned(sourceId);
            MistDebug.Log($"[Debug] ReceiveObjectInstantiateInfo {sourceId}");
        }

        /// <summary>
        /// 相手はRequestを出して，自身は出さないことがある
        /// </summary>
        /// <param name="id"></param>
        public void RequestObjectInstantiateInfo(NodeId id)
        {
            var sendData = new P_ObjectInstantiateRequest();
            var bytes = MemoryPackSerializer.Serialize(sendData);
            MistManager.I.Send(MistNetMessageType.ObjectInstantiateRequest, bytes, id);
            MistDebug.Log($"[Debug] RequestObjectInstantiateInfo: {id}");
        }

        /// <summary>
        /// TODO: ここが呼ばれないことがある
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sourceId"></param>
        private void ReceiveObjectInstantiateInfoRequest(byte[] data, NodeId sourceId)
        {
            MistDebug.Log($"[Debug] ReceiveObjectInstantiateInfoRequest {sourceId}");
            SendObjectInstantiateInfo(sourceId);
        }

        public void RemoveObject(NodeId targetId)
        {
            MistDebug.Log($"[Debug] RemoveObject: {targetId}");
            if (DestroyMyObjectsOnDisconnect) DestroyBySenderId(targetId);
            else DestroyPlayerObject(targetId);
        }

        private void DestroyPlayerObject(NodeId targetId)
        {
            if (!ObjectIdsByOwnerId.ContainsKey(targetId))
            {
                MistDebug.LogWarning($"No objects found for ownerId: {targetId}");
                return;
            }

            var playerObjectId = ObjectIdsByOwnerId[targetId].FirstOrDefault(
                id => _syncObjects.ContainsKey(id) && _syncObjects[id].IsPlayerObject);

            if (playerObjectId == null)
            {
                MistDebug.LogWarning($"No player object found for ownerId: {targetId}");
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
                MistDebug.LogError($"Sync object with id {syncObject.Id} already exists!");
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
                    MistManager.I.Send(MistNetMessageType.PropertyRequest, bytes, syncObject.OwnerId);
                    break;
                }
            }

            // OwnerIdAndObjIdDictに登録 自動削除で使用する
            if (!ObjectIdsByOwnerId.ContainsKey(syncObject.OwnerId))
            {
                ObjectIdsByOwnerId[syncObject.OwnerId] = new List<ObjectId>();
            }
            ObjectIdsByOwnerId[syncObject.OwnerId].Add(syncObject.Id);

            RegisterSyncAnimator(syncObject);
        }

        public void UnregisterSyncObject(MistSyncObject syncObject)
        {
            if (!_syncObjects.ContainsKey(syncObject.Id))
            {
                MistDebug.LogWarning($"Sync object with id {syncObject.Id} does not exist!");
                return;
            }

            _syncObjects.Remove(syncObject.Id);
            if (!ObjectIdsByOwnerId.ContainsKey(syncObject.OwnerId))
            {
                MistDebug.LogWarning($"No objects found for ownerId: {syncObject.OwnerId}");
                return;
            }
            ObjectIdsByOwnerId[syncObject.OwnerId].Remove(syncObject.Id);
            
            UnregisterSyncAnimator(syncObject);
            MistManager.I.OnDestroyed(syncObject.OwnerId);
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
                MistDebug.LogWarning($"Sync object with id {id} does not exist!");
                return null;
            }

            return _syncObjects[id];
        }

        public void DestroyBySenderId(NodeId senderId)
        {
            if (!ObjectIdsByOwnerId.ContainsKey(senderId))
            {
                MistDebug.LogWarning("Already destroyed");
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

        private void RegisterSyncAnimator(MistSyncObject syncObject)
        {
            if (_syncAnimators.ContainsKey(syncObject.Id))
            {
                MistDebug.LogError($"Sync animator with id {syncObject.Id} already exists!");
                return;
            }

            if (!syncObject.TryGetComponent(out MistAnimator syncAnimator)) return;
            _syncAnimators.Add(syncObject.Id, syncAnimator);
        }

        private void UnregisterSyncAnimator(MistSyncObject syncObject)
        {
            if (!_syncAnimators.ContainsKey(syncObject.Id))
            {
                MistDebug.LogWarning($"Sync animator with id {syncObject.Id} does not exist!");
                return;
            }

            _syncAnimators.Remove(syncObject.Id);
        }
        
        private void ReceiveAnimation(byte[] data, NodeId sourceId)
        {
            var receiveData = MemoryPackSerializer.Deserialize<P_Animation>(data);
            if (!_syncAnimators.TryGetValue(new ObjectId(receiveData.ObjId), out var syncAnimator)) return;
            syncAnimator.ReceiveAnimState(receiveData);
        }

        private void ReceiveRequestProperty(byte[] data, NodeId sourceId)
        {
            var requestData = MemoryPackSerializer.Deserialize<P_PropertyRequest>(data);
            if (!_syncObjects.TryGetValue(new ObjectId(requestData.ObjId), out var syncObject)) return;
            syncObject.SendAllProperties(sourceId);
        }
    }
}
