using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet.Minimal
{
    public class Node : MonoBehaviour
    {
        private MistSignalingWebSocket _mistSignalingWebSocket;
        private PeerRepository _peerRepository;
        private ITransportLayer Transport { get; set; }

        private void Start()
        {
            var selfId = Guid.NewGuid().ToString("N");
            _peerRepository = new PeerRepository();
            Transport = new TransportLayerTest(_peerRepository);
            _peerRepository.Init(Transport, new NodeId(selfId));
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
