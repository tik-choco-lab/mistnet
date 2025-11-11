using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet.Minimal
{
    public class Node : MonoBehaviour
    {
        private MistSignalingWebSocket _mistSignalingWebSocket;
        private PeerRepository _peerRepository;

        private void Start()
        {
            _peerRepository = new PeerRepository();
            _mistSignalingWebSocket = new MistSignalingWebSocket(_peerRepository);
        }

        public void Signaling()
        {
            _mistSignalingWebSocket.Init().Forget();
        }

        public void Send()
        {
        }
    }
}
