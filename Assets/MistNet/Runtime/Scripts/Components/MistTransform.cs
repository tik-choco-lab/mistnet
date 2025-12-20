using MemoryPack;
using UnityEngine;

namespace MistNet
{
    /// <summary>
    /// 位置同期
    /// - SmoothDamp補間: 滑らかな追従
    /// - 推測航法 (Extrapolation): パケット間の動きを予測
    /// - 距離ベースLOD: 近い人は高頻度、遠い人は低頻度で同期
    /// </summary>
    [RequireComponent(typeof(MistSyncObject))]
    public class MistTransform : MonoBehaviour
    {
        [Header("同期設定")]
        [Tooltip("基本の同期間隔（秒）。近距離ではこの値が使用されます")]
        [SerializeField] private float baseSyncInterval = 0.1f; // 10Hz
        
        [Header("距離ベースLOD設定")]
        [Tooltip("高頻度同期の最大距離（この距離以内は基本間隔で同期）")]
        [SerializeField] private float nearDistance = 5f;
        
        [Tooltip("低頻度同期になる距離（この距離以遠は最大間隔で同期）")]
        [SerializeField] private float farDistance = 30f;
        
        [Tooltip("遠距離での同期間隔（秒）")]
        [SerializeField] private float farSyncInterval = 0.5f; // 2Hz
        
        [Header("補間設定")]
        [Tooltip("位置補間の滑らかさ（秒）。0.1〜0.2程度が推奨")]
        [SerializeField] private float smoothTime = 0.12f;
        
        [Tooltip("回転補間の速度")]
        [SerializeField] private float rotationLerpSpeed = 12f;
        
        [Tooltip("推測航法の減衰率（0〜1）。1で完全に予測、0で予測なし")]
        [SerializeField] private float extrapolationDecay = 0.8f;
        
        private MistSyncObject _syncObject;
        private float _time;
        private P_Location _sendData;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation;
        
        // 受信データ補間用
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Vector3 _receivedVelocity;
        private Vector3 _currentVelocity; // SmoothDamp用の参照変数
        private Transform _cachedTransform;
        private float _currentSyncInterval;
        private float _timeSinceLastReceive;
        
        // 距離計算用
        private static Transform _selfPlayerTransform;

        public void Init()
        {
            _syncObject = GetComponent<MistSyncObject>();
            _cachedTransform = transform;

            _sendData = new P_Location
            {
                ObjId = _syncObject.Id,
                Time = baseSyncInterval
            };
            
            // 初期状態の設定
            _targetPosition = _cachedTransform.position;
            _targetRotation = _cachedTransform.rotation;
            _previousPosition = _cachedTransform.position;
            _previousRotation = _cachedTransform.rotation;
            _currentSyncInterval = baseSyncInterval;

            if (_syncObject.IsOwner && _syncObject.IsPlayerObject)
            {
                _selfPlayerTransform = _cachedTransform;
            }

            if (!_syncObject.IsOwner)
            {
                _currentSyncInterval = 0; // まだ受信していないので、同期しない
            }
        }

        private void Update()
        {
            if (_sendData == null) return;

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
            
            // 距離ベースのLOD: 自分のプレイヤーがいない場合は基本間隔を使用
            var syncInterval = CalculateSyncInterval();
            
            if (_time < syncInterval) return;
            
            var deltaTime = _time;
            _time = 0;

            var currentPosition = _cachedTransform.position;
            var currentRotation = _cachedTransform.rotation;

            // 座標が変わっていない場合は、送信しない
            if (_previousPosition == currentPosition &&
                _previousRotation == currentRotation) return;

            // 速度計算（移動距離 / 経過時間）
            var velocity = (currentPosition - _previousPosition) / deltaTime;

            _previousPosition = currentPosition;
            _previousRotation = currentRotation;

            _sendData.Position = currentPosition;
            _sendData.Rotation = currentRotation.eulerAngles;
            _sendData.Velocity = velocity;
            _sendData.Time = syncInterval;
            
            var data = MemoryPackSerializer.Serialize(_sendData);
            
            MistManager.I.AOI.SendAllLocation(data);
        }
        
        /// <summary>
        /// 距離に基づいて同期間隔を計算する（LOD）
        /// </summary>
        private float CalculateSyncInterval()
        {
            // 自身のPlayerObjectの場合は基本間隔を使用
            if (_syncObject.IsPlayerObject) return baseSyncInterval;
            
            // 自分のプレイヤーがまだいない場合は基本間隔
            if (_selfPlayerTransform == null) return baseSyncInterval;
            
            var distance = Vector3.Distance(_cachedTransform.position, _selfPlayerTransform.position);
            
            if (distance <= nearDistance)
            {
                return baseSyncInterval;
            }

            if (distance >= farDistance)
            {
                return farSyncInterval;
            }

            // 距離に応じて線形補間
            var t = (distance - nearDistance) / (farDistance - nearDistance);
            return Mathf.Lerp(baseSyncInterval, farSyncInterval, t);
        }
        
        public void ReceiveLocation(P_Location location)
        {
            if (_syncObject == null) return;
            if (_syncObject.IsOwner) return;

            // ターゲット位置・回転・速度を更新
            _targetPosition = location.Position;
            _targetRotation = Quaternion.Euler(location.Rotation);
            _receivedVelocity = location.Velocity;
            _currentSyncInterval = location.Time;
            _timeSinceLastReceive = 0f;
        }

        private void InterpolationLocation()
        {
            if (_currentSyncInterval <= 0) return;
            
            _timeSinceLastReceive += Time.deltaTime;

            // 推測航法 (Extrapolation):
            // パケット間隔の間も、相手は移動し続けていると仮定してターゲット位置を進める
            // 時間経過とともに減衰させ、次のパケットとのズレを軽減
            var extrapolationFactor = Mathf.Clamp01(1f - (_timeSinceLastReceive / _currentSyncInterval));
            extrapolationFactor *= extrapolationDecay;
            _targetPosition += _receivedVelocity * Time.deltaTime * extrapolationFactor;

            // 位置の補間: SmoothDampを使用して滑らかに追従
            _cachedTransform.position = Vector3.SmoothDamp(
                _cachedTransform.position, 
                _targetPosition, 
                ref _currentVelocity, 
                smoothTime
            );

            // 回転の補間: Slerpで滑らかに回転
            _cachedTransform.rotation = Quaternion.Slerp(
                _cachedTransform.rotation, 
                _targetRotation, 
                Time.deltaTime * rotationLerpSpeed
            );
        }
    }
}
