using Newtonsoft.Json;

namespace MistNet
{
    public class MistConfigData
    {
        [JsonProperty("nodeId")] public NodeId NodeId = new("");
        [JsonProperty("randomId")] public bool RandomId;
        [JsonProperty("bootstraps")] public string[] Bootstraps = { "ws://localhost:8080/signaling" };
        [JsonProperty("globalNode")] public GlobalNodeData GlobalNode = new();
        [JsonProperty("roomId")] public string RoomId = "MistNet";
        [JsonProperty("stunUrls")] public string[] StunUrls = { "stun:stun.l.google.com:19302" };
        [JsonProperty("showLogLine")] public int ShowLogLine = 10;
        [JsonProperty("logFilter")] public string LogFilter = "[STATS]"; // ログフィルターの種類を指定する文字列
        [JsonProperty("logDisplay")] public bool LogDisplay;
        [JsonProperty("logWarningDisplay")] public bool LogWarningDisplay;
        [JsonProperty("logErrorDisplay")] public bool LogErrorDisplay = true;
    }

    public class GlobalNodeData
    {
        [JsonProperty("enable")] public bool Enable = false;
        [JsonProperty("port")] public int Port = 8080;
    }
}
