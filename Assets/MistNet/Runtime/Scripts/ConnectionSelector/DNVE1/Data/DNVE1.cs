namespace MistNet
{
    public class DNVE1
    {
        public IDNVE1MessageSender Sender;
        public KademliaDataStore DataStore;
        public KademliaRoutingTable RoutingTable;
        public AreaTracker AreaTracker;
        public Kademlia Kademlia;
        public ConnectionBalancer ConnectionBalancer;
        public DNVE1VisibleNodesController VisibleNodesController;
        public RoutingBase RoutingBase;
        public IPeerRepository PeerRepository;
    }
}
