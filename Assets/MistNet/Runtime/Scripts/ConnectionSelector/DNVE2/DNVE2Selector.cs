using System.Collections.Generic;
using MemoryPack;

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
            _exchanger = new DNVE2NodeListExchanger(this, dataStore, RoutingBase, PeerRepository);
            _balancer = new DNVE2ConnectionBalancer(dataStore, RoutingBase, PeerRepository, Layer);
            _visibleController = new VisibleNodesController(dataStore, RoutingBase);
        }

        private void OnDestroy()
        {
            _exchanger.Dispose();
            _balancer.Dispose();
            _visibleController.Dispose();
        }

        protected override void OnMessage(byte[] data, NodeId id)
        {
            var message = MemoryPackSerializer.Deserialize<DNVEMessage>(data);
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
            message.Sender = PeerRepository.SelfId;
            var data = MemoryPackSerializer.Serialize(message);
            SendRaw(data, message.Receiver);
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
