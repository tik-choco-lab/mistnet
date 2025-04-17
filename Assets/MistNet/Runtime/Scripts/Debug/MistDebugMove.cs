using MistNet.Evaluation;
using UnityEngine;

namespace MistNet
{
    public class MistDebugMove : MonoBehaviour
    {
        [SerializeField] MistSyncObject syncObject;
        [SerializeField] private bool yFixed;
        private Vector3 _moveVector = Vector3.zero;
        private int _areaSize;

        private void Start()
        {
            _areaSize = EvalConfig.Data.MaxAreaSize;
            var maxMoveSpeed = EvalConfig.Data.MaxMoveSpeed;
            var x = Random.Range(-maxMoveSpeed, maxMoveSpeed);
            var y = yFixed ? 0 : Random.Range(-maxMoveSpeed, maxMoveSpeed);
            var z = Random.Range(-maxMoveSpeed, maxMoveSpeed);
            _moveVector = new Vector3(x, y, z);
        }

        private void Update()
        {
            if (!syncObject.IsOwner) return;
            transform.position += _moveVector;

            if (transform.position.x > _areaSize || transform.position.x < -_areaSize) _moveVector.x = -_moveVector.x;
            if (transform.position.y > _areaSize || transform.position.y < -_areaSize) _moveVector.y = -_moveVector.y;
            if (transform.position.z > _areaSize || transform.position.z < -_areaSize) _moveVector.z = -_moveVector.z;
        }
    }
}
