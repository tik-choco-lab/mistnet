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

        public virtual void OnConnected(NodeId id)
        {
        }

        public virtual void OnDisconnected(NodeId id)
        {
        }

        private void OnMessageReceived(byte[] data, NodeId id)
        {
            var message = MemoryPackSerializer.Deserialize<P_ConnectionSelector>(data);
            OnMessage(message.Data, id);
        }

        protected virtual void OnMessage(string data, NodeId id)
        {
        }

        protected virtual void RequestObject(NodeId targetId)
        {
            MistSyncManager.I.RequestObjectInstantiateInfo(targetId);
        }

        protected virtual void RemoveObject(NodeId targetId)
        {
            MistSyncManager.I.RemoveObject(targetId);
        }

        protected void SendAll(string data)
        {
            MistManager.I.SendAll(MistNetMessageType.ConnectionSelector, CreateData(data));
        }

        protected void Send(string data, NodeId targetId)
        {
            MistDebug.Log($"[Debug][Send] {data}");
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
