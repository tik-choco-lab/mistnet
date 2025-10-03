using UnityEngine;

namespace MistNet
{
    [RequireComponent(typeof(MistSyncObject))]
    public class MistDebugColorChange : MonoBehaviour
    {
        [SerializeField] private Material changeMaterial;
        private MistSyncObject _syncObject;
        
        private void Start()
        {
            _syncObject = GetComponent<MistSyncObject>();

            if (!_syncObject.IsOwner) return;
            // Materialを変更
            var ren = transform.GetChild(0).GetComponent<Renderer>();
            ren.material = changeMaterial;
        }
    }
}
