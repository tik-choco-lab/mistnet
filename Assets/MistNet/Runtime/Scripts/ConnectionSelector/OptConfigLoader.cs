using UnityEngine;

namespace MistNet
{
    public static class OptConfigLoader
    {
        private static readonly string ConfigPath = $"{Application.dataPath}/../mistnet_opt_config.json";
        public static MistOptConfig Data { get; private set; }

        public static void ReadConfig()
        {
            if (System.IO.File.Exists(ConfigPath))
            {
                var json = System.IO.File.ReadAllText(ConfigPath);
                Data = Newtonsoft.Json.JsonConvert.DeserializeObject<MistOptConfig>(json);
                if (Data != null) return;
            }

            Data = new MistOptConfig();
            WriteConfig();
        }

        public static void WriteConfig()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(Data, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(ConfigPath, json);
        }
    }
}
