using System.Collections.Generic;
using Newtonsoft.Json;

namespace MistNet
{
    public class EvalStatData
    {
        // 1秒間当たりの値
        [JsonProperty("messageCount")] public int MessageCount { get; set; }
        [JsonProperty("sendBits")] public int SendBits { get; set; }
        [JsonProperty("receiveBits")] public int ReceiveBits { get; set; }
        [JsonProperty("rttMillis")] public Dictionary<NodeId, float> RttMillis { get; set; } = new();
    }
}
