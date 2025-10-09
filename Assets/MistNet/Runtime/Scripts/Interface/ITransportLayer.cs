using System;

namespace MistNet
{
    public interface ITransportLayer : IDisposable
    {
        void Connect(NodeId id);
        void Disconnect(NodeId id);
        void DisconnectAll();
        void OnConnected(NodeId id);
        void OnDisconnected(NodeId id);
        void AddConnectCallback(Delegate callback);
        void AddDisconnectCallback(Delegate callback);
    }
}
