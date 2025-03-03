using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MistNet
{
    public class MistConfig
    {
        private static readonly string ConfigPath = $"{Application.dataPath}/../mistnet_config.json";
        public static MistConfigData Data { get; private set; }

        public static void ReadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Data = JsonConvert.DeserializeObject<MistConfigData>(json);
            }
            else
            {
                Data = new MistConfigData();
                WriteConfig();
            }
        }

        public static void WriteConfig()
        {
            var json = JsonConvert.SerializeObject(Data, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
