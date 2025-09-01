using UnityEngine;

namespace MistNet
{
    public class ChunkVisualizer : MonoBehaviour
    {
        private const int ChunkCountX = 50;
        private const int ChunkCountZ = 50;
        private static float ChunkSize => Area.ChunkSize;

        private void Start()
        {
#if UNITY_EDITOR
            for (int x = -ChunkCountX; x <= ChunkCountX; x++)
            {
                for (int z = -ChunkCountX; z <= ChunkCountZ; z++)
                {
                    var center = new Vector3(x * ChunkSize, 0, z * ChunkSize);
                    CreateChunkLines(center, ChunkSize);
                }
            }
#endif
        }

        private void CreateChunkLines(Vector3 center, float size)
        {
            var lineObj = new GameObject($"ChunkLine_{center.x}_{center.z}");
            lineObj.transform.parent = this.transform;

            var lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 5;
            lr.loop = true;
            lr.widthMultiplier = 10f;

            // 線が見えるようにマテリアルを設定
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lr.startColor = lr.endColor = Color.yellow;
            lr.material.color = Color.yellow;
            lr.useWorldSpace = true;

            var half = size / 2f;
            var corners = new Vector3[]
            {
                center + new Vector3(-half, 0, -half),
                center + new Vector3(-half, 0, half),
                center + new Vector3(half, 0, half),
                center + new Vector3(half, 0, -half),
                center + new Vector3(-half, 0, -half),
            };
            lr.SetPositions(corners);
        }
    }
}
