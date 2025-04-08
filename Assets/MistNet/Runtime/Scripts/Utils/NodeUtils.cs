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
    }
}
