using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet.DNVE2
{
    public class DNVE2Selector : SelectorBase, IDNVE2MessageSender
    {
        private static readonly Dictionary<DNVE2MessageType, DNVE2MessageReceivedHandler> Receivers = new();
        private static readonly List<DNVE2OnConnectedHandler> OnConnectedHandlers = new();
        private DNVE2NodeListExchanger _exchanger;
        private DNVE2ConnectionBalancer _balancer;
        private DNVE2VisibleNodesController _visibleController;

        protected override void Start()
        {
            OptConfig.ReadConfig();
            base.Start();

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
            var message = JsonConvert.DeserializeObject<DNVE2Message>(data);
            MistLogger.Debug($"[DNVE2Selector] OnMessage: sender {message.Sender} from {id}");

            if (!Receivers.TryGetValue(message.Type, out var handler))
            {
                MistLogger.Error($"[DNVE2Selector] Unknown message type: {message.Type}");
                return;
            }
            handler(message);
        }

        public override void OnConnected(NodeId id)
        {
            foreach (var handler in OnConnectedHandlers)
            {
                handler(id);
            }
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

        public void RegisterOnConnected(DNVE2OnConnectedHandler handler)
        {
            OnConnectedHandlers.Add(handler);
        }
    }
}
