using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MistNet.DNVE2
{
    public static class DNVE2Util
    {
        public static IEnumerable<Node> GetNodeList(IEnumerable<Node> allNodes, Node node, int takeCount)
        {
            // nodeに近いノードを最大n件取得する
            var nodeList = allNodes
                .OrderBy(kvp => Vector3.Distance(node.Position.ToVector3(), kvp.Position.ToVector3()))
                .Take(takeCount);

            return nodeList;
        }
    }
}
