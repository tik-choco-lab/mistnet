using UnityEngine;

namespace MistNet
{
    public abstract class IConnectionSelector : MonoBehaviour
    {
        public virtual void OnConnected(string id)
        {
        }

        public virtual void OnDisconnected(string id)
        {
        }

        protected virtual void OnMessage(byte[] data, string id)
        {
        }
    }
}
