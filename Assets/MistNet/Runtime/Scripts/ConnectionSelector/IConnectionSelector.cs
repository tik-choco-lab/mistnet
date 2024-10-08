using MemoryPack;
using UnityEngine;

namespace MistNet
{
    public abstract class IConnectionSelector : MonoBehaviour
    {
        protected virtual void Start()
        {
            MistManager.I.AddRPC(MistNetMessageType.ConnectionSelector, OnMessageReceived);
        }

        public virtual void OnConnected(string id)
        {
        }

        public virtual void OnDisconnected(string id)
        {
        }

        private void OnMessageReceived(byte[] data, string id)
        {
            var message = MemoryPackSerializer.Deserialize<P_ConnectionSelector>(data);
            OnMessage(message.Data, id);
        }

        protected virtual void OnMessage(string data, string id)
        {
        }

        protected virtual void RequestObject(string targetId)
        {
            MistSyncManager.I.RequestObjectInstantiateInfo(targetId);
        }

        protected virtual void RemoveObject(string targetId)
        {
            MistSyncManager.I.RemoveObject(targetId);
        }

        public virtual void OnSpawned(string id)
        {
        }

        public virtual void OnDespawned(string id)
        {
        }

        protected void SendAll(string data)
        {
            MistManager.I.SendAll(MistNetMessageType.ConnectionSelector, CreateData(data));
        }

        protected void Send(string data, string targetId)
        {
            MistManager.I.Send(MistNetMessageType.ConnectionSelector, CreateData(data), targetId);
        }

        private static byte[] CreateData(string data)
        {
            var message = new P_ConnectionSelector
            {
                Data = data
            };

            return MemoryPackSerializer.Serialize(message);
        }
    }
}
