using Newtonsoft.Json;

namespace MistNet.DNVE2
{
    public class DNVE2Selector : SelectorBase, IDNVE2MessageSender
    {
        protected override void Start()
        {
            OptConfig.ReadConfig();
            base.Start();
        }

        protected override void OnMessage(string data, NodeId id)
        {
            MistLogger.Debug($"[DNVE2Selector] OnMessage: {data} from {id}");
        }

        public void Send(NodeId targetId, DNVE2Message message)
        {
            var json = JsonConvert.SerializeObject(message);
            MistLogger.Debug($"[DNVE2Selector] Send: {json} to {targetId}");
            Send(json, targetId);
        }
    }
}
