using UnityEngine;

namespace MistNet
{
    public class ChunkVisualizer : MonoBehaviour
    {
        private const int ChunkCountX = 50;
        private const int ChunkCountZ = 50;
        private static float _chunkSize;
        private static readonly Color LineColor = new Color(0.4f, 0.4f, 0.4f, 1f);

        private void Start()
        {
            _chunkSize = OptConfig.Data.ChunkSize;
#if UNITY_EDITOR
            for (int x = -ChunkCountX; x <= ChunkCountX; x++)
            {
                for (int z = -ChunkCountX; z <= ChunkCountZ; z++)
                {
                    var origin = new Vector3(x * _chunkSize, 0, z * _chunkSize);
                    CreateChunkLines(origin, _chunkSize);
                }
            }
#endif
        }

        private void CreateChunkLines(Vector3 origin, float size)
        {
            var lineObj = new GameObject($"ChunkLine_{origin.x}_{origin.z}")
            {
                transform =
                {
                    parent = this.transform
                }
            };

            var lr = lineObj.AddComponent<LineRenderer>();
            lr.positionCount = 5;
            lr.loop = true;
            lr.widthMultiplier = 10f;

            // 線が見えるようにマテリアルを設定
            lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lr.startColor = lr.endColor = LineColor;
            lr.material.color = LineColor;
            lr.useWorldSpace = true;

            var corners = new Vector3[]
            {
                origin,
                origin + new Vector3(0, 0, size),
                origin + new Vector3(size, 0, size),
                origin + new Vector3(size, 0, 0),
                origin
            };
            lr.SetPositions(corners);
        }
    }
}
