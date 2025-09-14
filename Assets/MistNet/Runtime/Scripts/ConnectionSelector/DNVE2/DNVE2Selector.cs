using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet.DNVE2
{
    public class DNVE2Selector : SelectorBase, IDNVE2MessageSender
    {
        private static readonly Dictionary<DNVE2MessageType, DNVE2MessageReceivedHandler> Receivers = new();
        private RoutingBase _routingBase;
        private DNVE2NodeListExchanger _exchanger;
        private DNVE2ConnectionBalancer _balancer;
        private DNVE2VisibleNodesController _visibleController;

        protected override void Start()
        {
            OptConfig.ReadConfig();
            base.Start();
            _routingBase = MistManager.I.Routing;
            var dataStore = new DNVE2NodeListStore();
            _exchanger = new DNVE2NodeListExchanger(this, dataStore);
            _balancer = new DNVE2ConnectionBalancer(this, dataStore);
            _visibleController = new DNVE2VisibleNodesController(dataStore);
        }

        private void OnDestroy()
        {
            _exchanger.Dispose();
            _balancer.Dispose();
            _visibleController.Dispose();
        }

        protected override void OnMessage(string data, NodeId id)
        {
            MistLogger.Debug($"[DNVE2Selector] OnMessage: {data} from {id}");
            var message = JsonConvert.DeserializeObject<DNVE2Message>(data);
            _routingBase.Add(message.Sender, id);

            if (!Receivers.TryGetValue(message.Type, out var handler))
            {
                MistLogger.Error($"[DNVE2Selector] Unknown message type: {message.Type}");
                return;
            }
            handler(message);
        }

        public void Send(DNVE2Message message)
        {
            message.Sender = PeerRepository.I.SelfId;
            var json = JsonConvert.SerializeObject(message);
            MistLogger.Debug($"[DNVE2Selector] Send: {json} to {message.Receiver}");
            Send(json, message.Receiver);
        }

        public void RegisterReceive(DNVE2MessageType type, DNVE2MessageReceivedHandler receiver)
        {
            Receivers[type] = receiver;
        }
    }
}
