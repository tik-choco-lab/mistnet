namespace MistNet.Utils
{
    public class NodeUtils
    {
        public static Node GetSelfNodeData()
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var node = new Node(
                nodeId: new NodeId(PeerRepository.I.SelfId),
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
            var nodes = MistManager.I.routing.Nodes;
            var connectedNodes = MistManager.I.routing.ConnectedNodes;
            var visibleNodes = MistSyncManager.I.ObjectIdsByOwnerId;

            var nodeArray = new Node[nodes.Count];
            var i = 0;
            foreach (var node in nodes.Values)
            {
                if (node.Id == PeerRepository.I.SelfId) continue; // Skip self node
                if (connectedNodes.Contains(node.Id))
                {
                    node.State = EvalNodeState.Connected;
                    if (visibleNodes.TryGetValue(node.Id, out var objectList) && objectList.Count > 0)
                    {
                        var firstObjectId = objectList[0];
                        var firstObject = MistSyncManager.I.GetSyncObject(firstObjectId);
                        if (firstObject != null)
                        {
                            node.Position = new Position(firstObject.transform.position);
                            node.State = EvalNodeState.Visible;
                        }
                    }
                }
                else node.State = EvalNodeState.Disconnected;

                nodeArray[i] = node;
                i++;
            }
            return nodeArray;
        }
    }
}
