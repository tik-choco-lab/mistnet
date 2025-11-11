using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet.Minimal
{
    public class Node : MonoBehaviour
    {
        public MistSignalingWebSocket MistSignalingWebSocket;
        private void Start()
        {
            // MistSignalingWebSocket = new MistSignalingWebSocket();
        }

        public void Signaling()
        {
            MistSignalingWebSocket.Init().Forget();
        }

        public void Send()
        {

        }
    }
}
