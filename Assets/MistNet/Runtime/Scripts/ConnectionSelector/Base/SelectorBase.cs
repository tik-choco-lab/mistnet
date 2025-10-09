using MemoryPack;
using UnityEngine;

namespace MistNet
{
    public abstract class SelectorBase : MonoBehaviour
    {
        protected virtual void Start()
        {
            MistManager.I.World.RegisterReceive(MistNetMessageType.ConnectionSelector, OnMessageReceived);
        }

        public virtual void OnConnected(NodeId id)
        {
        }

        public virtual void OnDisconnected(NodeId id)
        {
        }

        private void OnMessageReceived(byte[] data, NodeId id)
        {
            if (MistStats.I != null)
            {
                MistStats.I.TotalEvalReceiveBytes += data.Length;
            }

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
            MistManager.I.World.SendAll(MistNetMessageType.ConnectionSelector, CreateData(data));
        }

        protected void Send(string data, NodeId targetId)
        {
            MistLogger.Debug($"[Debug][Send] {data}");
            var bytes = CreateData(data);
            MistManager.I.World.Send(MistNetMessageType.ConnectionSelector, bytes, targetId);

            if (MistStats.I == null) return;
            MistStats.I.TotalEvalSendBytes += bytes.Length;
            MistStats.I.TotalEvalMessageCount++;
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
