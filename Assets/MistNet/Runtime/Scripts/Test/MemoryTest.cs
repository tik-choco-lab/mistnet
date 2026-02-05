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
            SphericalHistogramUtils.Initialize(HistogramLevel);
            var directions = SphericalHistogramUtils.Directions;
            Debug.Log($"[MemoryTest] Level: {HistogramLevel}, Directions: {directions.Length}, Bins: {BinCount}");

            var selfId = System.Guid.NewGuid().ToString();
            var targetId = System.Guid.NewGuid().ToString();

            var dummyNodes = new Vector3[] {
                new Vector3(10, 0, 0),
                new Vector3(0, 10, 0),
                new Vector3(0, 0, 10),
                new Vector3(-10, 5, 2),
                new Vector3(3, -8, 1)
            };
            var selfPos = Vector3.zero;

            using var timer = new CodeTimer("MemoryTest");

            var hists = SphericalHistogramUtils.CreateSphericalHistogram(selfPos, dummyNodes, BinCount);
            var histsBytes = new byte[directions.Length * BinCount];
            var spatialData = new SpatialHistogramData
            {
                Hists = hists,
                Position = new Position(selfPos)
            };

            SerializeHistogramOptimal(hists, histsBytes);
            var spatialDataByte = new SpatialHistogramDataByte
            {
                Position = spatialData.Position.ToVector3(),
                ByteHists = histsBytes
            };
            var byteDataMemoryPack = MemoryPackSerializer.Serialize(spatialDataByte);

            var payloadJson = JsonConvert.SerializeObject(spatialData);
            var payloadBytes = Encoding.UTF8.GetByteCount(payloadJson);

            var message = new DNVEMessage
            {
                Sender = new NodeId(selfId),
                Receiver = new NodeId(targetId),
                Type = DNVEMessageType.Heartbeat,
                Payload = payloadJson
            };

            var fullJson = JsonConvert.SerializeObject(message);
            var fullBytes = Encoding.UTF8.GetByteCount(fullJson);
            var connectionSelectorMessage = new P_ConnectionSelector
            {
                Data = fullJson
            };
            var memoryPackBytes = MemoryPackSerializer.Serialize(connectionSelectorMessage);
            var mistMessage = new MistMessage
            {
                Type = MistNetMessageType.ConnectionSelector,
                Payload = memoryPackBytes,
                Id =  selfId,
                TargetId = targetId,
                HopCount = 3,
            };
            var mistMessageBytes = MemoryPackSerializer.Serialize(mistMessage);
            timer.Stop();

            Debug.Log($"[MemoryTest] Payload JSON Length: {payloadJson.Length} chars, {payloadBytes} bytes");
            Debug.Log($"[MemoryTest] Payload MemoryPack (ByteHist) Length: {byteDataMemoryPack.Length} bytes");
            Debug.Log($"[MemoryTest] Full Message JSON Length: {fullJson.Length} chars, {fullBytes} bytes");
            Debug.Log($"[MemoryTest] Full Message MemoryPack Length: {memoryPackBytes.Length} bytes");
            Debug.Log($"[MemoryTest] MistMessage MemoryPack Length: {mistMessageBytes.Length} bytes");
            
            var rawFloatCount = directions.Length * BinCount;
            var rawBytes = rawFloatCount * sizeof(float) + 3 * sizeof(float); // Hists + Position(3 floats)
            Debug.Log($"[MemoryTest] Raw data (floats): {rawFloatCount} floats, approx {rawBytes} bytes");

            MeasureNodeList(5); // 5 nodes
            MeasureNodeList(20); // 20 nodes
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
                Payload = payloadJson
            };

            var fullJson = JsonConvert.SerializeObject(message);
            var fullBytes = Encoding.UTF8.GetByteCount(fullJson);

            Debug.Log($"[MemoryTest] NodeList ({nodeCount} nodes) JSON Length: {fullJson.Length} chars, {fullBytes} bytes");
        }

        private static void SerializeHistogramOptimal(float[,] hist, byte[] result)
        {
            var histSpan = MemoryMarshal.CreateSpan(ref hist[0, 0], hist.Length);
            var resSpan = result.AsSpan();

            var totalElements = histSpan.Length;
            if (totalElements == 0) return;

            // 最大値探索
            var maxVal = 0.00001f;
            foreach (float val in histSpan)
            {
                if (val > maxVal) maxVal = val;
            }

            var invMax = 255.0f / maxVal;

            // 正規化
            for (var i = 0; i < totalElements; i++)
            {
                var norm = histSpan[i] * invMax;
                resSpan[i] = (byte)norm;
            }
        }
    }

    [MemoryPackable]
    public partial class SpatialHistogramDataByte
    {
        public Vector3 Position { get; set; }
        public byte[] ByteHists { get; set; }
    }
}
