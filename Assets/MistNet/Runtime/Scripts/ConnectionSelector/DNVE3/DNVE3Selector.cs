using Newtonsoft.Json;

namespace MistNet.DNVE3
{
    public class DNVE3Selector : SelectorBase, IMessageSender
    {
        private RoutingBase _routingBase;
        private DNVE3ConnectionBalancer _balancer;
        private DNVE3Exchanger _exchanger;
        private VisibleNodesController _visibleController;

        protected override void Start()
        {
            OptConfig.ReadConfig();
            base.Start();

            _routingBase = MistManager.I.Routing;
            var dataStore = new NodeListStore();
            _exchanger = new DNVE3Exchanger();
            _balancer = new DNVE3ConnectionBalancer();
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
        }

        public void Send(DNVEMessage message)
        {

        }

        public void RegisterReceive(DNVEMessageType type, DNVEMessageReceivedHandler receiver)
        {
        }

        public void RegisterOnConnected(DNVEOnConnectedHandler handler)
        {
        }
    }
}
