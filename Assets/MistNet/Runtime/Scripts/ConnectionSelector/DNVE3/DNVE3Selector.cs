using System.Collections.Generic;
using Newtonsoft.Json;
using MemoryPack;
using MistNet;

namespace MistNet.DNVE3
{
    public class DNVE3Selector : SelectorBase, IMessageSender
    {
        private DNVE3ConnectionBalancer _balancer;
        private DNVE3Exchanger _exchanger;
        private VisibleNodesController _visibleController;
        private DNVE3DataStore _dnveDataStore;
        private readonly Dictionary<DNVEMessageType, DNVEMessageReceivedHandler> _receivers = new();
        private static readonly List<DNVEOnConnectedHandler> OnConnectedHandlers = new();

        protected override void Start()
        {
            OptConfig.ReadConfig();
            Utils.SphericalHistogramUtils.Initialize(OptConfig.Data.SphericalHistogramLevel);
            base.Start();

            var dataStore = new NodeListStore();
            _dnveDataStore = new DNVE3DataStore();
            _exchanger = new DNVE3Exchanger(this, dataStore, _dnveDataStore, RoutingBase);
            _balancer = new DNVE3ConnectionBalancer(this, dataStore, _dnveDataStore, Layer, RoutingBase, PeerRepository);
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
            _dnveDataStore.LastMessageTimes[id] = System.DateTime.UtcNow;
            
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
            message.Sender = PeerRepository.SelfId;
            if (message.Receiver != null)
            {
                _dnveDataStore.LastMessageTimes[message.Receiver] = System.DateTime.UtcNow;
            }
            var data = MemoryPackSerializer.Serialize(message);
            SendRaw(data, message.Receiver);
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
