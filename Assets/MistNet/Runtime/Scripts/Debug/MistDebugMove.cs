using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Evaluation;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MistNet
{
    public class MistDebugMove : MonoBehaviour
    {
        private const float LoopDelaySeconds = 1.0f;
        [SerializeField] private MistSyncObject syncObject;
        [SerializeField] private bool yFixed;
        private Vector3 _moveVector = Vector3.zero;
        private int _areaSize;

        private void Start()
        {
            if (!syncObject.IsOwner) return;

            _areaSize = EvalConfig.Data.MaxAreaSize;
            var maxMoveSpeed = EvalConfig.Data.MaxMoveSpeed;
            var x = Random.Range(-maxMoveSpeed, maxMoveSpeed);
            var y = yFixed ? 0 : Random.Range(-maxMoveSpeed, maxMoveSpeed);
            var z = Random.Range(-maxMoveSpeed, maxMoveSpeed);
            _moveVector = new Vector3(x, y, z);
            LoopMove(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask LoopMove(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                transform.position += _moveVector;
                if (transform.position.x > _areaSize || transform.position.x < -_areaSize) _moveVector.x = -_moveVector.x;
                if (transform.position.y > _areaSize || transform.position.y < -_areaSize) _moveVector.y = -_moveVector.y;
                if (transform.position.z > _areaSize || transform.position.z < -_areaSize) _moveVector.z = -_moveVector.z;
                await UniTask.Delay(TimeSpan.FromSeconds(LoopDelaySeconds), cancellationToken: token);
            }
        }
    }
}
