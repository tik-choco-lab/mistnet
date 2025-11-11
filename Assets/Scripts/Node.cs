using System;
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
            var selfId = Guid.NewGuid().ToString("N");
            _peerRepository = new PeerRepository();
            _peerRepository.Init(new NodeId(selfId));
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
