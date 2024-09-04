namespace MistNet
{
    interface IConnectionSelector
    {
        void OnConnected(string id);
        void OnDisconnected(string id);
    }
}
