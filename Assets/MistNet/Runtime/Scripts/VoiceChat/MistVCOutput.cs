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
                MistLogger.Error("[Error] MistSyncObject is not found");
                return;
            }

            if (_syncObject.IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }

            PeerEntity peerData = null;
            while (peerData == null)
            {
                await UniTask.Yield();
                peerData = MistManager.I.PeerRepository.GetPeer(_syncObject.OwnerId);
            }

            MistLogger.Info($"[Debug][MistVC] {peerData.Id} add output audio source");
            peerData.AddOutputAudioSource(audioSource);
        }
    }
}
