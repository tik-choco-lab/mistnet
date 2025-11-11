using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace MistNet.Minimal
{
    public class TestController : MonoBehaviour
    {
        [SerializeField] private Node nodePrefab;
        [SerializeField] private int nodeCount = 10;

        private readonly List<Node> _nodes = new();

        private void Start()
        {
            MistConfig.ReadConfig();
        }

        [Button]
        private void SpawnNodes()
        {
            DestroyAllNodes();
            for (int i = 0; i < nodeCount; i++)
            {
                var position = new Vector3(
                    Random.Range(-50f, 50f),
                    Random.Range(-50f, 50f),
                    Random.Range(-50f, 50f)
                );

                var obj = Instantiate(nodePrefab, position, Quaternion.identity);
                obj.name = $"Node_{i}";
                _nodes.Add(obj);
            }
        }

        private void DestroyAllNodes()
        {
            foreach (var node in _nodes)
            {
                if (node != null)
                {
                    Destroy(node.gameObject);
                }
            }
            _nodes.Clear();
        }

        [Button]
        private void Signaling()
        {

        }
    }
}
