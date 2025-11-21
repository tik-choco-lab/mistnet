using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet.DNVE2
{
    public class DNVE2Selector : SelectorBase, IMessageSender
    {
        private static readonly Dictionary<DNVEMessageType, DNVEMessageReceivedHandler> Receivers = new();
        private static readonly List<DNVEOnConnectedHandler> OnConnectedHandlers = new();
        private DNVE2NodeListExchanger _exchanger;
        private DNVE2ConnectionBalancer _balancer;
        private VisibleNodesController _visibleController;

        protected override void Start()
        {
            base.Start();

            var dataStore = new NodeListStore();
            _exchanger = new DNVE2NodeListExchanger(this, dataStore);
            _balancer = new DNVE2ConnectionBalancer(dataStore);
            _visibleController = new VisibleNodesController(dataStore, RoutingBase);
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

        public void Send(DNVEMessage message)
        {
            message.Sender = MistManager.I.PeerRepository.SelfId;
            var json = JsonConvert.SerializeObject(message);
            MistLogger.Debug($"[DNVE2Selector] Send: {json} to {message.Receiver}");
            Send(json, message.Receiver);
        }

        public void RegisterReceive(DNVEMessageType type, DNVEMessageReceivedHandler receiver)
        {
            Receivers[type] = receiver;
        }

        public void RegisterOnConnected(DNVEOnConnectedHandler handler)
        {
            OnConnectedHandlers.Add(handler);
        }
    }
}
