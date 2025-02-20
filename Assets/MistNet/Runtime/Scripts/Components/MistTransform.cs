using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;

namespace MistNet
{
    [RequireComponent(typeof(MistSyncObject))]
    public class MistTransform : MonoBehaviour
    {
        [Tooltip("Note: This setting is applicable to all synchronized objects, excluding a player object")]
        [SerializeField] private float syncIntervalTimeSecond = 0.1f;
        
        private MistSyncObject _syncObject;
        private float _time;
        private P_Location _sendData;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation;
        private Vector3 _receivedPosition = Vector3.zero;
        private Quaternion _receivedRotation = Quaternion.identity;
        private float _elapsedTime;

        private async void Start()
        {
            await UniTask.Yield(); // MistSyncObjectの初期化を待つ

            _syncObject = GetComponent<MistSyncObject>();

            _sendData = new()
            {
                ObjId = _syncObject.Id,
                Time = syncIntervalTimeSecond
            };

            if (!_syncObject.IsOwner)
            {
                syncIntervalTimeSecond = 0; // まだ受信していないので、同期しない
            }

            MistDebug.Log($"[MistDebug] Start: {_syncObject.Id} {_syncObject.IsOwner}");
        }

        private void Update()
        {
            if (_sendData == null) return; // 初期化が終わっていない場合は、処理しない

            if (_syncObject.IsOwner)
            {
                UpdateAndSendLocation();
            }
            else
            {
                InterpolationLocation();
            }
        }

        private void UpdateAndSendLocation()
        {
            _time += Time.deltaTime;
            if (_time < syncIntervalTimeSecond) return;
            
            _time = 0;

            // 座標が変わっていない場合は、送信しない
            if (_previousPosition == transform.position &&
                _previousRotation == transform.rotation) return;

            // 座標が異なる場合、送信する
            _previousPosition = transform.position;
            _previousRotation = transform.rotation;

            _sendData.Position = transform.position;
            _sendData.Rotation = transform.rotation.eulerAngles;


            if (_syncObject.IsGlobalObject) MistDebug.Log($"[Transform][Send] {_sendData.ObjId}");
            if (syncIntervalTimeSecond == 0) syncIntervalTimeSecond = 0.1f;
            _sendData.Time = syncIntervalTimeSecond;
            var bytes = MemoryPackSerializer.Serialize(_sendData);
            MistManager.I.SendAll(MistNetMessageType.Location, bytes);
        }
        
        public void ReceiveLocation(P_Location location)
        {
            MistDebug.Log($"[MistDebug][Transform][Receive] {location.ObjId} {location.Position}");
            if (_syncObject == null) return;
            if (_syncObject.IsOwner) return;

            if (_syncObject.IsGlobalObject) MistDebug.Log($"[Transform][Receive] {location.ObjId} {location.Position}");
            MistDebug.Log($"[MistDebug][Transform][Receive] {location.ObjId} {location.Position}");
            _receivedPosition = location.Position;
            _receivedRotation = Quaternion.Euler(location.Rotation);
            syncIntervalTimeSecond = location.Time;

            _elapsedTime = 0f;
        }

        private void InterpolationLocation()
        {
            if (syncIntervalTimeSecond == 0) return;

            var timeRatio = Mathf.Clamp01(_elapsedTime / syncIntervalTimeSecond);
            _elapsedTime += Time.deltaTime;
            
            transform.position = Vector3.Lerp(transform.position, _receivedPosition, timeRatio);
            transform.rotation = Quaternion.Slerp(transform.rotation, _receivedRotation, timeRatio);
            
            if (_elapsedTime >= syncIntervalTimeSecond) _elapsedTime = 0f;
        }
    }
}
