using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet.VC
{
    [RequireComponent(typeof(AudioSource))]
    public class MistVCOutput : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        private MistSyncObject _syncObject;

        private async void Start()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            _syncObject = transform.root.gameObject.GetComponent<MistSyncObject>();

            if (_syncObject == null)
            {
                MistDebug.LogError("[Error] MistSyncObject is not found");
                return;
            }

            if (_syncObject.IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }

            MistPeerDataElement peerData = null;
            while (peerData == null)
            {
                await UniTask.Yield();
                peerData = MistPeerData.I.GetPeerData(_syncObject.OwnerId);
            }

            MistDebug.Log($"[Debug][MistVC] {peerData.Peer.Id} add output audio source");
            peerData.Peer.AddOutputAudioSource(audioSource);
        }
    }
}
