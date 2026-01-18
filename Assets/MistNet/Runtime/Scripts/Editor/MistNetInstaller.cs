#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MistNet.Editor
{
    [InitializeOnLoad]
    public class MistNetInstaller
    {
        private static ListRequest _listRequest;
        private static Queue<(string name, string url)> _installQueue = new();
        private static AddRequest _addRequest;

        static MistNetInstaller()
        {
            // 起動直後に実行すると不安定なため、1フレーム遅らせる
            EditorApplication.delayCall += StartCheck;
        }

        private static void StartCheck()
        {
            _listRequest = Client.List(true);
            EditorApplication.update += ListProgress;
        }

        private static void ListProgress()
        {
            if (!_listRequest.IsCompleted) return;

            EditorApplication.update -= ListProgress;

            if (_listRequest.Status == StatusCode.Success)
            {
                var targetPackages = new[]
                {
                    ("com.cysharp.unitask", "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"),
                    ("com.cysharp.memorypack", "https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity")
                };

                foreach (var (name, url) in targetPackages)
                {
                    if (_listRequest.Result.All(p => p.name != name))
                    {
                        _installQueue.Enqueue((name, url));
                    }
                }

                ProcessQueue();
                CheckNuGetRequirement();
            }
        }

        private static void ProcessQueue()
        {
            if (_installQueue.Count == 0) return;
            if (_addRequest != null && !_addRequest.IsCompleted) return;

            var (name, url) = _installQueue.Dequeue();
            Debug.Log($"[MistNet] Installing dependency: {name} from {url}");
            _addRequest = Client.Add(url);
            EditorApplication.update += AddProgress;
        }

        private static void AddProgress()
        {
            if (!_addRequest.IsCompleted) return;

            EditorApplication.update -= AddProgress;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[MistNet] Successfully installed: {_addRequest.Result.name}");
            }
            else
            {
                Debug.LogError($"[MistNet] Failed to install package: {_addRequest.Error.message}");
            }

            ProcessQueue(); // 次のパッケージへ
        }

        private static void CheckNuGetRequirement()
        {
            // MemoryPackのCore DLL(NuGet)が存在するか簡易チェック
            // Assets/Packages は NuGetForUnity のデフォルトフォルダ
            if (!AssetDatabase.IsValidFolder("Assets/Packages/MemoryPack"))
            {
                Debug.LogWarning("[MistNet] MemoryPack (Core) is not found. Please install 'MemoryPack' via NuGetForUnity to avoid compilation errors.");
            }
        }
    }
}
#endif
