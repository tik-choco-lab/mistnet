namespace MistNet
{
    public class GossipConnectionSelector : IConnectionSelector
    {
        public override void OnConnected(string id)
        {
            MistDebug.Log($"[GossipConnectionSelector] OnConnected {id}");
        }

        public override void OnDisconnected(string id)
        {
            MistDebug.Log($"[GossipConnectionSelector] OnDisconnected {id}");
        }

        protected override void OnMessage(string data, string id)
        {
            MistDebug.Log($"[GossipConnectionSelector] OnMessage {id}");
        }
    }
}
