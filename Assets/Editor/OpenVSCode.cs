using System.Diagnostics;
using UnityEditor;

public class OpenVSCode
{
    // Start is called before the first frame update
    [MenuItem("Assets/Open Visual Studio Code", priority = 19)]
    static void Execute()
    {
        // get select GO full path
        int instanceID = Selection.activeInstanceID;
        string path = AssetDatabase.GetAssetPath(instanceID);
        string fullPath = System.IO.Path.GetFullPath(path);

        var psInfo = new ProcessStartInfo();
        psInfo.FileName = "code";
        psInfo.Arguments = fullPath;
        Process.Start(psInfo);
    }

    [MenuItem("Tools/Open Visual Studio Code")]
    static void Execute2()
    {
        Process.Start("code", ".");
    }
}
