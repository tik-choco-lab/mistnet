using System.Collections.Generic;

namespace MistNet
{
    public class ManualRouting : IRouting
    {
        public IReadOnlyDictionary<NodeId, Node> Nodes => _nodes;
        private readonly Dictionary<NodeId, Node> _nodes = new(); // ノードのリスト 接続しているかどうかに関わらず持つ

        public void AddNode(NodeId id, Node node)
        {
            MistDebug.Log($"[ConnectionSelector] AddNode: {id}");
            _nodes[id] = node;
        }

        public void RemoveNode(NodeId id)
        {
            MistDebug.Log($"[ConnectionSelector] RemoveNode: {id}");
            _nodes.Remove(id);
        }

        public void ClearNodes()
        {
            MistDebug.Log("[ConnectionSelector] ClearNodes");
            _nodes.Clear();
        }
    }
}
