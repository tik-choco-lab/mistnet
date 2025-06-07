using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace MistNet
{
    public class MistSyncManager : MonoBehaviour
    {
        public static MistSyncManager I { get; private set; }
        public MistSyncObject SelfSyncObject { get; set; }                           // 自身のSyncObject

        private readonly Dictionary<ObjectId, MistSyncObject> _syncObjects = new();    // objId, MistSyncObject
        private readonly Dictionary<ObjectId, MistAnimator> _syncAnimators = new();    // objId, MistAnimator

        // ユーザーが退出した際のGameObjectの削除に使用している Instantiateで生成されたObjectに限る
        public readonly Dictionary<NodeId, List<ObjectId>> ObjectIdsByOwnerId = new();  // ownerId, objId　
        private readonly Dictionary<ObjectId, MistSyncObject> _mySyncObjects = new();    // 自身が生成したObject一覧

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
            MistManager.I.AddRPC(MistNetMessageType.PropertyRequest, (_, sourceId) => SendAllProperties(sourceId));
            MistManager.I.AddRPC(MistNetMessageType.ObjectInstantiateRequest, ReceiveObjectInstantiateInfoRequest);
        }

        private void OnDestroy()
        {
            _objectPool.Dispose();
        }

        private void SendObjectInstantiateInfo(NodeId id)
        {
            var sendData = new P_ObjectInstantiate();
            foreach (var obj in _mySyncObjects.Values)
            {
                if (!obj.IsPlayerObject) continue;
                sendData.ObjId = obj.Id;
                var objTransform = obj.transform;
                sendData.Position = objTransform.position;
                sendData.Rotation = objTransform.rotation.eulerAngles;
                sendData.PrefabAddress = obj.PrefabAddress;
                var data = MemoryPackSerializer.Serialize(sendData);
                MistManager.I.Send(MistNetMessageType.ObjectInstantiate, data, id);
            }
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
            syncObject.SetData(new ObjectId(instantiateData.ObjId), false, instantiateData.PrefabAddress, sourceId);
            syncObject.Init();
            
            RegisterSyncObject(syncObject);
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
            DestroyBySenderId(targetId);
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

            if (syncObject.IsOwner)
            {
                // 最初のGameObjectは、接続先最適化に使用するため、PlayerObjectであることを設定
                if(_mySyncObjects.Count == 0) syncObject.IsPlayerObject = true;
                
                _mySyncObjects.Add(syncObject.Id, syncObject);
            }
            else
            {
                // 自身以外のSyncObjectの登録
                var sendData = new P_PropertyRequest();
                var bytes = MemoryPackSerializer.Serialize(sendData);
                MistManager.I.Send(MistNetMessageType.PropertyRequest, bytes, syncObject.OwnerId);
            }

            // OwnerIdAndObjIdDictに登録 自動削除で使用する
            if (!ObjectIdsByOwnerId.ContainsKey(syncObject.OwnerId))
            {
                ObjectIdsByOwnerId[syncObject.OwnerId] = new List<ObjectId>();
            }
            ObjectIdsByOwnerId[syncObject.OwnerId].Add(syncObject.Id);

            RegisterSyncAnimator(syncObject);
        }

        private void SendAllProperties(NodeId id)
        {
            foreach (var obj in _mySyncObjects.Values)
            {
                obj.SendAllProperties(id);
            }
        }

        public void UnregisterSyncObject(MistSyncObject syncObject)
        {
            if (!_syncObjects.ContainsKey(syncObject.Id))
            {
                MistDebug.LogWarning($"Sync object with id {syncObject.Id} does not exist!");
                return;
            }

            _syncObjects.Remove(syncObject.Id);
            if (_mySyncObjects.ContainsKey(syncObject.Id))
            {
                _mySyncObjects.Remove(syncObject.Id);
            }

            ObjectIdsByOwnerId.Remove(syncObject.OwnerId);
            
            UnregisterSyncAnimator(syncObject);
            MistManager.I.OnDestroyed(syncObject.OwnerId);
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
    }
}
