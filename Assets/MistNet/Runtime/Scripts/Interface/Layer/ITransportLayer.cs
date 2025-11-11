using System;

namespace MistNet
{
    public interface ITransportLayer : IDisposable
    {
        void Init();

        void Connect(NodeId id);
        void Disconnect(NodeId id);
        void DisconnectAll();
        void Send(NodeId targetId, MistMessage data, bool isForward = false);

        void AddConnectCallback(Delegate callback);
        void AddDisconnectCallback(Delegate callback);
        void RegisterReceive(Action<MistMessage, NodeId> callback);

        void OnConnected(NodeId id);
        void OnDisconnected(NodeId id);
        void OnMessage(byte[] data, NodeId senderId);

        bool IsConnectingOrConnected(NodeId id);
        bool IsConnected(NodeId id);
    }
}
