using System.Linq;

namespace MistNet.Utils
{
    public class NodeUtils
    {
        public static Node GetSelfNodeData()
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var node = new Node(
                nodeId: new NodeId(MistPeerData.I.SelfId),
                position: new Position(selfPosition)
            );
            return node;
        }

        public static Node[] GetAllNodeData()
        {
            var objects = MistSyncManager.I.ObjectIdsByOwnerId;
            var nodes = new Node[objects.Count];

            var i = 0;
            foreach (var (nodeId, objectList) in objects)
            {
                var firstObjectId = objectList[0];
                var firstObject = MistSyncManager.I.GetSyncObject(firstObjectId);
                nodes[i] = new Node(
                    nodeId: nodeId,
                    position: new Position(firstObject.transform.position)
                );
                i++;
            }

            return nodes;
        }

        public static Node[] GetOtherNodeData()
        {
            var nodeInfoDict = MistManager.I.routing.Nodes;
            var objects = MistSyncManager.I.ObjectIdsByOwnerId;

            foreach (var (nodeId, node) in nodeInfoDict)
            {
                if (node == null) continue;
                if (nodeId == MistPeerData.I.SelfId) continue;
                var state = EvalNodeState.Connected;
                if (objects.TryGetValue(nodeId, out var obj))
                {
                    var firstObjectId = obj[0];
                    var firstObject = MistSyncManager.I.GetSyncObject(firstObjectId);
                    if (firstObject != null)
                    {
                        state = EvalNodeState.Visible;
                    }
                }
                node.State = state;
            }

            return nodeInfoDict.Values.ToArray();
        }
    }
}
