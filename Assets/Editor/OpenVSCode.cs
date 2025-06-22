using System.Diagnostics;
using UnityEditor;

public class OpenVSCode
{
    [MenuItem("Assets/Open Visual Studio Code", priority = 19)]
    private static void Execute()
    {
        // get select GO full path
        var instanceID = Selection.activeInstanceID;
        var path = AssetDatabase.GetAssetPath(instanceID);
        var fullPath = System.IO.Path.GetFullPath(path);

        Open(fullPath);
    }

    [MenuItem("Tools/Open Visual Studio Code")]
    private static void Execute2()
    {
        Open(".");
    }

    [MenuItem("Tools/MistNet/Open Config")]
    private static void OpenConfig()
    {
        Open("mistnet_config.json");
    }

    [MenuItem("Tools/MistNet/Open OPT Config")]
    private static void OpenOptConfig()
    {
        Open("mistnet_opt_config.json");
    }

    [MenuItem("Tools/MistNet/Open Eval Config")]
    private static void OpenEvalConfig()
    {
        Open("mistnet_eval_config.json");
    }

    private static void Open(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "code",
            Arguments = arguments,
            CreateNoWindow = true,
        };

        Process.Start(startInfo);
    }
}
