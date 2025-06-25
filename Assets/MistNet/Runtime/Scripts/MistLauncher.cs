using Cysharp.Threading.Tasks;
using MistNet.Evaluation;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MistNet
{
    public class MistLauncher : MonoBehaviour
    {
        [SerializeField] private string prefabAddress = "Assets/Prefab/MistNet/MistPlayerTest.prefab";
        [SerializeField] private bool randomSpawn;
        [SerializeField] private bool yFixed;
        
        private void Start()
        {
            // 座標をランダムで取得する
            var position = Vector3.zero;
            var maxRange = EvalConfig.Data.MaxAreaSize;
            if (randomSpawn)
            {
                var x = Random.Range(-maxRange, maxRange);
                var y = yFixed ? 0 : Random.Range(-maxRange, maxRange);
                var z = Random.Range(-maxRange, maxRange);
                position = new Vector3(x, y, z);
            }

            var selfId = MistConfig.Data.NodeId;
            var objId = new ObjectId(selfId);
            MistManager.I.InstantiatePlayerAsync(prefabAddress, position, Quaternion.identity, objId).Forget();
        }
    }
}
