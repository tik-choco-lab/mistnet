using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet.VC
{
    [RequireComponent(typeof(AudioSource))]
    public class MistVC : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        private MistSyncObject _syncObject;

        private async void Start()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();

            _syncObject = transform.root.gameObject.GetComponent<MistSyncObject>();

            MistPeerDataElement peerData = null;
            while (peerData == null)
            {
                await UniTask.Yield();
                Debug.Log($"[MistVC] id: {_syncObject.OwnerId}");
                peerData = MistPeerData.I.GetPeerData(_syncObject.OwnerId);
            }

            Debug.Log($"[MistVC] {peerData.Peer.Id} set audio source");
            if (_syncObject.IsOwner)
            {
                peerData.Peer.AddInputAudioSource(_audioSource);
            }
            else
            {
                peerData.Peer.AddOutputAudioSource(_audioSource);
            }
        }
    }
}
