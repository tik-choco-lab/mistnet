using System.Collections.Generic;

namespace MistNet.DNVE2
{
    public interface IDNVE2NodeListStore
    {
        IReadOnlyDictionary<NodeId, Node> NodeList { get; }
        void AddOrUpdate(Node node);
        void Remove(NodeId id);
        bool TryGet(NodeId id, out Node node);
        IEnumerable<Node> GetAllNodes();
    }
}
