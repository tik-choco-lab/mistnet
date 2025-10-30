using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet.DNVE3
{
    public class DNVE3Selector : SelectorBase, IMessageSender
    {
        private DNVE3ConnectionBalancer _balancer;
        private DNVE3Exchanger _exchanger;
        private VisibleNodesController _visibleController;
        private readonly Dictionary<DNVEMessageType, DNVEMessageReceivedHandler> _receivers = new();
        private static readonly List<DNVEOnConnectedHandler> OnConnectedHandlers = new();

        protected override void Start()
        {
            OptConfig.ReadConfig();
            base.Start();

            var dataStore = new NodeListStore();
            var dnveDataStore = new DNVE3DataStore();
            _exchanger = new DNVE3Exchanger(this, dataStore, dnveDataStore);
            _balancer = new DNVE3ConnectionBalancer(dnveDataStore);
            _visibleController = new VisibleNodesController(dataStore);
        }

        private void OnDestroy()
        {
            _exchanger.Dispose();
            _balancer.Dispose();
            _visibleController.Dispose();
        }

        protected override void OnMessage(string data, NodeId id)
        {
            var message = JsonConvert.DeserializeObject<DNVEMessage>(data);
            if (!_receivers.TryGetValue(message.Type, out var handler))
            {
                MistLogger.Error($"[DNVE3Selector] Unknown message type: {message.Type}");
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

        public void Send(DNVEMessage message)
        {
            message.Sender = PeerRepository.I.SelfId;
            var json = JsonConvert.SerializeObject(message);
            MistLogger.Debug($"[DNVE3Selector] Send: {json} to {message.Receiver}");
            Send(json, message.Receiver);
        }

        public void RegisterReceive(DNVEMessageType type, DNVEMessageReceivedHandler receiver)
        {
            _receivers[type] = receiver;
        }

        public void RegisterOnConnected(DNVEOnConnectedHandler handler)
        {
            OnConnectedHandlers.Add(handler);
        }
    }
}
