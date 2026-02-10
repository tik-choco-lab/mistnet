using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Newtonsoft.Json;
using MistNet.DNVE3;
using MistNet.Utils;
using System.Text;
using MemoryPack;
using Vector3 = UnityEngine.Vector3;

namespace MistNet.Test
{
    public class MemoryTest : MonoBehaviour
    {
        [Header("Parameters")]
        public int HistogramLevel = 3;
        public int BinCount = 4;

        [ContextMenu("Test")]
        public void Test()
        {
            SpatialDensityUtils.Initialize(HistogramLevel);
            var directions = SpatialDensityUtils.Directions;
            var selfId = Guid.NewGuid().ToString();
            var targetId = Guid.NewGuid().ToString();
            var selfPos = Vector3.zero;
            
            // Create a realistic spread of 100 nodes
            var nodeList = new List<Vector3>();
            var random = new System.Random(42); // Seed for reproducibility

            // 1. Close-by cluster (Densely populated area)
            for (int i = 0; i < 40; i++) {
                nodeList.Add(new Vector3(
                    (float)random.NextDouble() * 10 - 5,
                    (float)random.NextDouble() * 5,
                    (float)random.NextDouble() * 10 - 5
                ));
            }

            // 2. Middle-range spread
            for (int i = 0; i < 50; i++) {
                var angle = random.NextDouble() * Math.PI * 2;
                var dist = 20 + random.NextDouble() * 50;
                nodeList.Add(new Vector3(
                    (float)Math.Cos(angle) * (float)dist,
                    (float)random.NextDouble() * 10,
                    (float)Math.Sin(angle) * (float)dist
                ));
            }

            // 3. Distant outliers (Extremely far nodes to test normalization scale)
            nodeList.Add(new Vector3(500, 0, 500));
            nodeList.Add(new Vector3(-400, 100, 0));

            var dummyNodes = nodeList.ToArray();

            // A. Prepare Original Data
            var hists = SpatialDensityUtils.CreateSpatialDensity(selfPos, dummyNodes, BinCount);
            var spatialData = new SpatialDensityData { DensityMap = hists, Position = new Position(selfPos) };

            // --- 1. CURRENT APPROACH ---
            // SpatialData(float[,]) -> JSON -> DNVEMessage.Payload(string) -> JSON -> P_ConnectionSelector.Data(string) -> MistMessage.Payload(byte[])
            var payloadJson = JsonConvert.SerializeObject(spatialData);
            var dnveMsg = new DNVEMessage { Sender = new NodeId(selfId), Receiver = new NodeId(targetId), Type = DNVEMessageType.Heartbeat, Payload = Encoding.UTF8.GetBytes(payloadJson) };
            var dnveMsgJson = JsonConvert.SerializeObject(dnveMsg);
            var pSelector = new P_ConnectionSelector { Data = dnveMsgJson };
            var currentMistMsg = new MistMessage { Type = MistNetMessageType.ConnectionSelector, Id = selfId, TargetId = targetId, HopCount = 3, Payload = MemoryPackSerializer.Serialize(pSelector) };
            var currentBytes = MemoryPackSerializer.Serialize(currentMistMsg);

            // --- 2. OPTIMIZED (Binary float) ---
            // SpatialData(float[,]) -> MemoryPack -> DNVEMessageBinary.Payload(byte[]) -> MistMessage.Payload(byte[])
            var spatialBinary = MemoryPackSerializer.Serialize(spatialData);
            var dnveMsgBin = new DNVEMessageBinary { Sender = new NodeId(selfId), Receiver = new NodeId(targetId), Type = DNVEMessageType.Heartbeat, Payload = spatialBinary };
            var optMistMsg = new MistMessage { Type = MistNetMessageType.ConnectionSelector, Id = selfId, TargetId = targetId, HopCount = 3, Payload = MemoryPackSerializer.Serialize(dnveMsgBin) };
            var optBytes = MemoryPackSerializer.Serialize(optMistMsg);

            // --- 3. EXTREME (Binary byte - Lossy) ---
            // SpatialData -> byte[] -> MemoryPack -> DNVEMessageBinary.Payload(byte[])
            var histsBytes = new byte[directions.Length * BinCount];
            SerializeHistogramOptimal(hists, histsBytes);
            var spatialByteData = new SpatialHistogramDataByte { Position = selfPos, ByteHists = histsBytes };
            var spatialByteBinary = MemoryPackSerializer.Serialize(spatialByteData);
            var extremeDnveMsg = new DNVEMessageBinary { Sender = new NodeId(selfId), Receiver = new NodeId(targetId), Type = DNVEMessageType.Heartbeat, Payload = spatialByteBinary };
            var extremeMistMsg = new MistMessage { Type = MistNetMessageType.ConnectionSelector, Id = selfId, TargetId = targetId, HopCount = 3, Payload = MemoryPackSerializer.Serialize(extremeDnveMsg) };
            var extremeBytes = MemoryPackSerializer.Serialize(extremeMistMsg);

            // --- 4. SUPER RAW (Theoretical Minimum) ---
            // 直接 byte[] に [Position(12b) + Histogram(104b)] を書き込むだけ
            var superRawPayload = new byte[12 + directions.Length * BinCount];
            Buffer.BlockCopy(BitConverter.GetBytes(selfPos.x), 0, superRawPayload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(selfPos.y), 0, superRawPayload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(selfPos.z), 0, superRawPayload, 8, 4);
            Buffer.BlockCopy(histsBytes, 0, superRawPayload, 12, histsBytes.Length);
            
            // MistMessage の Id も string ではなくバイナリ化（16バイト）すればさらに削れるが、
            // まずは Payload だけ Raw 化したサイズを計測
            var superRawMistMsg = new MistMessage { Type = MistNetMessageType.ConnectionSelector, Id = selfId, TargetId = targetId, HopCount = 3, Payload = superRawPayload };
            var superRawBytes = MemoryPackSerializer.Serialize(superRawMistMsg);

            // --- Visualization (Error check for Extreme) ---
            var maxVal = 0.00001f;
            foreach (var v in hists) if (v > maxVal) maxVal = v;
            var decoded = MemoryPackSerializer.Deserialize<SpatialHistogramDataByte>(spatialByteBinary);
            var restored = (decoded.ByteHists[0] / 255.0f) * maxVal;
            var error = Mathf.Abs(hists[0, 0] - restored);

            // --- REPORT ---
            var sb = new StringBuilder();
            sb.AppendLine("<b>[DNVE3 Serialization Comparison]</b>");
            sb.AppendLine($"Dirs: {directions.Length}, Bins: {BinCount}, Max Value: {maxVal:F4}");
            sb.AppendLine("--------------------------------------------------");
            
            sb.AppendLine("<b>1. Current (JSON Path):</b>");
            sb.AppendLine($"  Size: <color=red>{currentBytes.Length}</color> bytes (100%)");
            
            sb.AppendLine("<b>2. Optimized (Pure Binary float):</b>");
            var optReduc = (1.0f - (float)optBytes.Length / currentBytes.Length) * 100f;
            sb.AppendLine($"  Size: <color=green>{optBytes.Length}</color> bytes (-{optReduc:F1}%)");
            
            sb.AppendLine("<b>3. Extreme (Binary byte - Lossy):</b>");
            var extReduc = (1.0f - (float)extremeBytes.Length / currentBytes.Length) * 100f;
            sb.AppendLine($"  Size: <color=yellow>{extremeBytes.Length}</color> bytes (-{extReduc:F1}%)");

            sb.AppendLine("<b>4. Super Raw (Minimum Headers + Payload):</b>");
            var rawReduc = (1.0f - (float)superRawBytes.Length / currentBytes.Length) * 100f;
            sb.AppendLine($"  Size: <color=cyan>{superRawBytes.Length}</color> bytes (<b>-{rawReduc:F1}%</b>)");
            
            // Comprehensive Error Analysis
            float maxError = 0;
            float sumError = 0;
            int nonZeroSamples = 0;
            var errorDetail = new StringBuilder();
            
            for (int i = 0; i < hists.GetLength(0); i++)
            {
                for (int j = 0; j < hists.GetLength(1); j++)
                {
                    float ori = hists[i, j];
                    float res = (decoded.ByteHists[i * BinCount + j] / 255.0f) * maxVal;
                    float err = Mathf.Abs(ori - res);
                    if (err > maxError) maxError = err;
                    sumError += err;
                    
                    if (ori > 0.01f && nonZeroSamples < 3)
                    {
                        errorDetail.AppendLine($"    - Original: {ori:F4} -> Restored: {res:F4} (Gap: {err:F4})");
                        nonZeroSamples++;
                    }
                }
            }
            
            sb.AppendLine($"  Max Error: <color=orange>{maxError:F6}</color>");
            sb.AppendLine($"  Avg Error: {sumError / hists.Length:F6}");
            sb.AppendLine("  <b>Restoration Samples:</b>");
            sb.Append(errorDetail);
            
            sb.AppendLine("--------------------------------------------------");
            sb.AppendLine("<i>*Super Raw strips all DNVEMessage metadata.</i>");

            Debug.Log(sb.ToString());
        }

        private void MeasureNodeList(int nodeCount)
        {
            var nodes = new List<Node>();
            for (int i = 0; i < nodeCount; i++)
            {
                nodes.Add(new Node
                {
                    Id = new NodeId($"node-{i}"),
                    Position = new Position(new Vector3(i, 0, 0))
                });
            }

            var payloadJson = JsonConvert.SerializeObject(nodes);
            var payloadBytes = Encoding.UTF8.GetByteCount(payloadJson);

            var message = new DNVEMessage
            {
                Sender = new NodeId("self-id"),
                Receiver = new NodeId("target-id"),
                Type = DNVEMessageType.NodeList,
                Payload = Encoding.UTF8.GetBytes(payloadJson)
            };

            var fullJson = JsonConvert.SerializeObject(message);
            var fullBytes = Encoding.UTF8.GetByteCount(fullJson);

            Debug.Log($"[MemoryTest] NodeList ({nodeCount} nodes) JSON Length: {fullJson.Length} chars, {fullBytes} bytes");
        }

        private static void SerializeHistogramOptimal(float[,] hist, byte[] result)
        {
            var histSpan = MemoryMarshal.CreateSpan(ref hist[0, 0], hist.Length);
            var maxVal = 0.00001f;
            foreach (var val in histSpan) if (val > maxVal) maxVal = val;
            var invMax = 255.0f / maxVal;
            for (var i = 0; i < histSpan.Length; i++) result[i] = (byte)(histSpan[i] * invMax);
        }
    }

    [MemoryPackable]
    public partial class DNVEMessageBinary
    {
        public NodeId Sender;
        public NodeId Receiver;
        public DNVEMessageType Type;
        public byte[] Payload;
    }

    [MemoryPackable]
    public partial class SpatialHistogramDataByte
    {
        public Vector3 Position;
        public byte[] ByteHists;
    }
}
