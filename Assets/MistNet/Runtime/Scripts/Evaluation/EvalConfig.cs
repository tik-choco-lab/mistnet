using UnityEngine;

namespace MistNet.Evaluation
{
    public class EvalConfig
    {
        private static readonly string ConfigPath = $"{Application.dataPath}/../mistnet_eval_config.json";
        public static MistEvalConfigData Data { get; private set; }

        public static void ReadConfig()
        {
            if (System.IO.File.Exists(ConfigPath))
            {
                var json = System.IO.File.ReadAllText(ConfigPath);
                Data = Newtonsoft.Json.JsonConvert.DeserializeObject<MistEvalConfigData>(json);
                if (Data != null) return;
            }

            Data = new MistEvalConfigData();
            WriteConfig();
        }

        public static void WriteConfig()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(Data, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(ConfigPath, json);
        }
    }
}
