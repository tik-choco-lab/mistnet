using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using MistNet.DNVE3;
using MistNet.Utils;
using System.Text;
using MemoryPack;

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
            var spatialData = new SpatialHistogramData
            {
                Hists = hists,
                Position = new Position(selfPos)
            };

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

        private DNVEMessage CreateMessage()
        {
            var msg = new DNVEMessage
            {
                Type = DNVEMessageType.Heartbeat,
                Payload = ""
            };
            return msg;
        }
    }
}
