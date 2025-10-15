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
        private Transform _cachedTransform;

        public void Init()
        {
            _syncObject = GetComponent<MistSyncObject>();
            _cachedTransform = transform; // Transformをキャッシュ

            _sendData = new()
            {
                ObjId = _syncObject.Id,
                Time = syncIntervalTimeSecond
            };

            if (!_syncObject.IsOwner)
            {
                syncIntervalTimeSecond = 0; // まだ受信していないので、同期しない
            }
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
            if (_previousPosition == _cachedTransform.position &&
                _previousRotation == _cachedTransform.rotation) return;

            // 座標が異なる場合、送信する
            _previousPosition = _cachedTransform.position;
            _previousRotation = _cachedTransform.rotation;

            _sendData.Position = _previousPosition;
            _sendData.Rotation = _previousRotation.eulerAngles;

            if (syncIntervalTimeSecond == 0) syncIntervalTimeSecond = 0.1f;
            _sendData.Time = syncIntervalTimeSecond;
            
            var data = MemoryPackSerializer.Serialize(_sendData);
            MistManager.I.AOI.SendAll(MistNetMessageType.Location, data);
        }
        
        public void ReceiveLocation(P_Location location)
        {
            if (_syncObject == null) return;
            if (_syncObject.IsOwner) return;

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
            
            _cachedTransform.position = Vector3.Lerp(_cachedTransform.position, _receivedPosition, timeRatio);
            _cachedTransform.rotation = Quaternion.Slerp(_cachedTransform.rotation, _receivedRotation, timeRatio);
            
            if (_elapsedTime >= syncIntervalTimeSecond) _elapsedTime = 0f;
        }
    }
}
