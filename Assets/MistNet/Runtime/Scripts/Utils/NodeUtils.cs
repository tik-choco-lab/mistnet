namespace MistNet.Utils
{
    public class NodeUtils
    {
        public static Node GetSelfNodeData()
        {
            var selfPosition = MistSyncManager.I.SelfSyncObject.transform.position;
            var node = new Node(
                nodeId: new NodeId(MistManager.I.PeerRepository.SelfId),
                position: new Position(selfPosition)
            );
            return node;
        }

        public static Node[] GetOtherNodeData()
        {
            var nodes = MistManager.I.Routing.Nodes;
            var connectedNodes = MistManager.I.Routing.ConnectedNodes;
            var visibleNodes = MistSyncManager.I.ObjectIdsByOwnerId;

            var nodeArray = new Node[nodes.Count];
            var i = 0;
            foreach (var node in nodes.Values)
            {
                if (node.Id == MistManager.I.PeerRepository.SelfId) continue; // Skip self node
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
