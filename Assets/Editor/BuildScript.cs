using UnityEditor;
using UnityEngine;
using System.Linq;

public class BuildScript
{
    public static void Build()
    {
        var buildTargetArg = GetCommandLineArg("-buildTarget");
        if (string.IsNullOrEmpty(buildTargetArg))
        {
            Debug.LogError("[Error] No build target specified. Use -buildTarget to specify a target platform.");
            return;
        }

        if (!System.Enum.TryParse(buildTargetArg, out BuildTarget buildTarget))
        {
            Debug.LogError($"[Error] Invalid build target: {buildTargetArg}");
            return;
        }

        string[] scenes = null;
        var sceneOptions = GetCommandLineArg("-scenes");
        scenes = !string.IsNullOrEmpty(sceneOptions)
            ? sceneOptions.Split(',')
            : GetEnabledScenes();


        var developmentBuild = GetCommandLineArg("-buildOptions") == "Development";
        BuildWithOptions(scenes, buildTarget, developmentBuild);
    }

    private static void BuildWithOptions(string[] scenes, BuildTarget buildTarget, bool developmentBuild)
    {
        var buildPath = GetBuildPath(buildTarget, developmentBuild);
        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = buildTarget,
            options = developmentBuild
                ? BuildOptions.Development
                : BuildOptions.None
        };

        var buildReport = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (buildReport.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[Success] Build {buildTarget}");
        }
        else
        {
            Debug.LogError($"[Error] Build {buildTarget} failed. Reason: {buildReport.summary.result}");
        }
    }

    /// <summary>
    /// 有効なシーンを取得するメソッド
    /// </summary>
    /// <returns></returns>
    private static string[] GetEnabledScenes()
    {
        var customScenes = GetCommandLineArg("-scenes");
        if (!string.IsNullOrEmpty(customScenes))
        {
            return customScenes.Split(',').Select(scene => scene.Trim()).ToArray();
        }

        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
    }

    /// <summary>
    /// ビルドターゲットに応じた出力先パスを生成するメソッド
    /// </summary>
    /// <param name="buildTarget"></param>
    /// <param name="developmentBuild"></param>
    /// <returns></returns>
    private static string GetBuildPath(BuildTarget buildTarget, bool developmentBuild)
    {
        var buildDirectory = GetCommandLineArg("-buildDirectory") ?? "Builds/";
        // debug or release
        var buildOptions = developmentBuild ? "debug" : "release";
        buildDirectory = $"{buildDirectory}{buildOptions}/";
        var productName = PlayerSettings.productName;

        switch (buildTarget)
        {
            case BuildTarget.StandaloneOSX:
                return $"{buildDirectory}macOS/{productName}.app";
            case BuildTarget.StandaloneWindows:
                return $"{buildDirectory}Windows/{productName}_32.exe";
            case BuildTarget.StandaloneWindows64:
                return $"{buildDirectory}Windows/{productName}.exe";
            case BuildTarget.StandaloneLinux64:
                return $"{buildDirectory}Linux/{productName}";
            case BuildTarget.iOS:
                return $"{buildDirectory}iOS/{productName}";
            case BuildTarget.Android:
                return $"{buildDirectory}Android/{productName}.apk";
            default:
                Debug.LogError("Unsupported build target.");
                return $"{buildDirectory}Unknown/{productName}";
        }
    }

    /// <summary>
    /// Command Line引数を取得するメソッド
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static string GetCommandLineArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == name && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
