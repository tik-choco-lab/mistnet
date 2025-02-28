using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class MistConfig
    {
        private static readonly string ConfigPath = $"{Application.dataPath}/../mistnet_config.json";
        private static MistConfigData _config;

        public static string SignalingServerAddress => _config.signalingServerAddress;
        public static string[] StunUrls => _config.stunUrls;
        public static bool DebugLog => _config.debugLog;
        public static string LogFilter => _config.logFilter;
        public static int ShowLogLine => _config.showLogLine;

        [Serializable]
        private class MistConfigData
        {
            public string signalingServerAddress = "ws://localhost:8080/ws";
            public string[] stunUrls = { "stun:stun.l.google.com:19302" };

            public bool debugLog;
            public string logFilter = "[STATS]"; // ログフィルターの種類を指定する文字列
            public int showLogLine = 10;
        }

        public void ReadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _config = JsonConvert.DeserializeObject<MistConfigData>(json);
            }
            else
            {
                _config = new MistConfigData();
                WriteConfig();
            }
        }

        private static void WriteConfig()
        {
            var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
