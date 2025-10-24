using System.Collections.Generic;

namespace MistNet
{
    public interface INodeListStore
    {
        IReadOnlyDictionary<NodeId, Node> NodeList { get; }
        void AddOrUpdate(Node node);
        void Remove(NodeId id);
        bool TryGet(NodeId id, out Node node);
        IEnumerable<Node> GetAllNodes();
    }
}
