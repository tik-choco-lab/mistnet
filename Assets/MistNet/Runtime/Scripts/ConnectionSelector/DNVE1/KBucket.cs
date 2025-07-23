using System.Collections.Generic;

namespace MistNet
{
    public class KBucket
    {
        private const int K = 20;
        public LinkedList<DNVE1Node> Nodes;

        public void AddNode(DNVE1Node node)
        {
            if (Nodes == null)
            {
                Nodes = new LinkedList<DNVE1Node>();
            }

            // If the node already exists, move it to the front
            foreach (var existingNode in Nodes)
            {
                if (existingNode.Id.Equals(node.Id))
                {
                    Nodes.Remove(existingNode);
                    break;
                }
            }

            // Add the new node to the front
            Nodes.AddFirst(node);

            // If we exceed K nodes, remove the last one
            if (Nodes.Count > K)
            {
                Nodes.RemoveLast();
            }
        }


    }
}
