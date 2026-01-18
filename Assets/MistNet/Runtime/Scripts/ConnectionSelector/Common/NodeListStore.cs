using System.Collections.Generic;

namespace MistNet
{
    public class NodeListStore : INodeListStore
    {
        public IReadOnlyDictionary<NodeId, Node> NodeList => _nodeList;
        private readonly Dictionary<NodeId, Node> _nodeList = new();

        public void AddOrUpdate(Node node)
        {
            _nodeList[node.Id] = node;
        }

        public void Remove(NodeId id)
        {
            _nodeList.Remove(id);
        }

        public bool TryGet(NodeId id, out Node node)
        {
            return _nodeList.TryGetValue(id, out node);
        }

        public IEnumerable<Node> GetAllNodes()
        {
            return _nodeList.Values;
        }
    }
}
