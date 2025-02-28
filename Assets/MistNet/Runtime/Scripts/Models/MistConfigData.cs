using Newtonsoft.Json;

namespace MistNet
{
    public class MistConfigData
    {
        [JsonProperty("bootstraps")] public string[] Bootstraps = { "ws://localhost:8080/ws" };
        [JsonProperty("roomId")] public string RoomId = "MistNet";
        [JsonProperty("stunUrls")] public string[] StunUrls = { "stun:stun.l.google.com:19302" };
        [JsonProperty("showLogLine")] public int ShowLogLine = 10;
        [JsonProperty("logFilter")] public string LogFilter = "[STATS]"; // ログフィルターの種類を指定する文字列
        [JsonProperty("logDisplay")] public bool LogDisplay;
        [JsonProperty("logWarningDisplay")] public bool LogWarningDisplay;
        [JsonProperty("logErrorDisplay")] public bool LogErrorDisplay = true;
    }
}
