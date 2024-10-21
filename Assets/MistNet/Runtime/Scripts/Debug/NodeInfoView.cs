using TMPro;
using UnityEngine;

namespace MistNet
{
    [RequireComponent(typeof(Transform))]
    public class NodeInfoView : MonoBehaviour
    {
        [SerializeField] private MistSyncObject syncObject;
        [SerializeField] private TMP_Text idText;

        private void Start()
        {
            idText.text = syncObject.OwnerId;
        }
    }
}
