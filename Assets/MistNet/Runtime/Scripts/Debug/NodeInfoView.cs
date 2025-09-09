using TMPro;
using UnityEngine;

namespace MistNet
{
    [RequireComponent(typeof(Transform))]
    public class NodeInfoView : MonoBehaviour
    {
        [SerializeField] private MistSyncObject syncObject;
        [SerializeField] private TMP_Text idText;
        [SerializeField] private TMP_Text chunkText;

        private const float UpdateInterval = 1.0f;
        private float _count;

        private void Start()
        {
            idText.text = syncObject.OwnerId;
        }

#if UNITY_EDITOR
        private void Update()
        {
            _count += Time.deltaTime;
            if (_count < UpdateInterval) return;
            _count = 0;

            var chunkPos = Area.ToChunk(transform.position);
            chunkText.text = $"({chunkPos.x}, {chunkPos.y}, {chunkPos.z})";
        }
#endif
    }
}
