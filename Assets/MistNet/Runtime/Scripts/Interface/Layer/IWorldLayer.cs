using System;

namespace MistNet
{
    public interface IWorldLayer : IDisposable
    {
        void Send(MistNetMessageType type, byte[] data, NodeId targetId);
        void SendAll(MistNetMessageType type, byte[] data);
        void AddSendFailedCallback(Delegate callback);
        void RegisterReceive(MistNetMessageType type, MessageReceivedHandler receiver);
    }

    public delegate void MessageReceivedHandler(byte[] data, NodeId fromId);

    public delegate void WLRegisterReceive(MistNetMessageType type, MessageReceivedHandler receiver);
    public delegate void WLSend(MistNetMessageType type, byte[] data, NodeId targetId);
}
