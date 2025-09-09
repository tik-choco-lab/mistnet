using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace MistNet
{
    public static class HttpRequestHandler
    {
        public static async UniTask<string> Get(string url, CancellationToken token = default)
        {
            using var webRequest = UnityWebRequest.Get(url);
            return await SendRequest(webRequest, token);
        }

        public static async UniTask<T> Get<T>(string url, CancellationToken token = default)
        {
            var jsonText = await Get(url, token);
            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        public static async UniTask<string> Post(string url, string json, CancellationToken token = default)
        {
            using var webRequest = new UnityWebRequest(url, "POST");
            var jsonToSend = new UTF8Encoding().GetBytes(json);
            webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            return await SendRequest(webRequest, token);
        }

        public static async UniTask<T> Post<T, U>(string url, U json, CancellationToken token = default)
        {
            var jsonText = await Post(url, JsonConvert.SerializeObject(json), token);
            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        public static async UniTask<T> Post<T>(string url, string json, CancellationToken token = default)
        {
            var jsonText = await Post(url, json, token);
            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        public static async UniTask<string> Put(string url, string json = "", CancellationToken token = default)
        {
            using var webRequest = new UnityWebRequest(url, "PUT");
            var jsonToSend = new UTF8Encoding().GetBytes(json);
            webRequest.uploadHandler = string.IsNullOrEmpty(json) ? new UploadHandlerRaw(Array.Empty<byte>()) : new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            return await SendRequest(webRequest, token);
        }

        public static async UniTask<T> Put<T>(string url, CancellationToken token = default)
        {
            var jsonText = await Put(url, "", token);
            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        public static async UniTask<T> Put<T, U>(string url, U json, CancellationToken token = default)
        {
            var jsonText = await Put(url, JsonConvert.SerializeObject(json), token);
            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        public static async UniTask<string> Delete(string url, CancellationToken token = default)
        {
            using var webRequest = UnityWebRequest.Delete(url);
            return await SendRequest(webRequest, token);
        }

        public static async UniTask<T> Delete<T>(string url, CancellationToken token = default)
        {
            var jsonText = await Delete(url, token);
            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        public static async UniTask<string> Patch(string url, string json, CancellationToken token = default)
        {
            using var webRequest = new UnityWebRequest(url, "PATCH");
            var jsonToSend = new UTF8Encoding().GetBytes(json);
            webRequest.uploadHandler = new UploadHandlerRaw(jsonToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            return await SendRequest(webRequest, token);
        }

        public static async UniTask<T> Patch<T, U>(string url, U json, CancellationToken token = default)
        {
            var jsonText = await Patch(url, JsonConvert.SerializeObject(json), token);
            return JsonConvert.DeserializeObject<T>(jsonText);
        }

        public static async UniTask<T> Patch<T>(string url, CancellationToken token = default)
        {
            using var webRequest = new UnityWebRequest(url, "PATCH");
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            return JsonConvert.DeserializeObject<T>(await SendRequest(webRequest, token));
        }

        private static async UniTask<string> SendRequest(UnityWebRequest webRequest, CancellationToken cancellationToken)
        {
            MistLogger.Info($"[HttpRequest] {webRequest.url}");

            try
            {
                await webRequest.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                if (webRequest.result != UnityWebRequest.Result.ConnectionError &&
                    webRequest.result != UnityWebRequest.Result.ProtocolError)
                {
                    MistLogger.Info($"[HttpRequest] {webRequest.downloadHandler.text}");
                    return webRequest.downloadHandler.text;
                }

                MistLogger.Error($"{webRequest.error}: {webRequest.downloadHandler.text}");

                throw new Exception(webRequest.error);
            }
            catch (Exception e)
            {
                MistLogger.Error($"[Error][HttpRequest] {webRequest.url} {e.Message}");
                throw;
            }
        }
    }
}
