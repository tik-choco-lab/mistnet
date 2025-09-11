using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MistNet.Evaluation;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace MistNet
{
    public class MistEventLogger : IDisposable
    {
        private const float SendIntervalSeconds = 10f;
        public static MistEventLogger I { get; private set; }
        private const string Key = "6TdW05^r4F*bvAre$hv^&E8Z";
        private readonly List<EventData> _logQueue = new();
        private EventData _templateEventData;
        private Stack<EventData> _pool;

        private readonly bool _enableLogging;
        private readonly CancellationTokenSource _cts = new();

        public MistEventLogger(bool enableLogging)
        {
            _enableLogging = enableLogging;
            if (I != null)
            {
                MistLogger.Error("EventLogger is already initialized.");
                I = null;
            }

            I = this;
            if (!_enableLogging) return;

            _pool = new Stack<EventData>();
            Application.logMessageReceived += HandleLog;
            Update(_cts.Token).Forget();
        }

        private async UniTask Update(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(SendIntervalSeconds), cancellationToken: token);
                if (!_enableLogging) continue;
                SendLogs();
            }
        }

        private void SendLogs()
        {
            if (_logQueue.Count == 0) return;
            var json = JsonConvert.SerializeObject(_logQueue);

            foreach (var eventData in _logQueue)
            {
                ReturnEventData(eventData);
            }
            _logQueue.Clear();
            HttpRequestHandler.Post(EvalConfig.Data.EventLogUrl, json).Forget();
        }

        public void LogEvent(EventType type, string data = "")
        {
            if (!_enableLogging) return;

            var eventData = RentEventData();

            eventData.EventType = type;
            eventData.Data = data;
            eventData.Timestamp = DateTime.UtcNow;

            _logQueue.Add(eventData);
        }

        private EventData RentEventData()
        {
            _pool ??= new Stack<EventData>();

            return _pool.Count > 0 ? _pool.Pop() : new EventData
            {
                Id = PeerRepository.I.SelfId,
                Service = Application.productName,
                Version = Application.version,
                Key = Key,
            };
        }

        private void ReturnEventData(EventData data)
        {
            data.Data = "";
            _pool.Push(data);
        }

        public void Dispose()
        {
            LogEvent(EventType.GameEnded);
            SendLogs();
            I = null;
            Application.logMessageReceived -= HandleLog;
            _cts.Cancel();
            _pool.Clear();
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception)
            {
                if (PeerRepository.I == null) return;
                LogEvent(EventType.Error, $"{logString}\n{stackTrace}");
            }
        }
    }

    public class EventData
    {
        [JsonProperty("id")] public NodeId Id;
        [JsonProperty("key")] public string Key;
        [JsonProperty("service")] public string Service;
        [JsonProperty("version")] public string Version;
        [JsonProperty("eventType")] public EventType EventType;
        [JsonProperty("data")] public string Data;
        [JsonProperty("timestamp")] public DateTime Timestamp;
    }

    [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
    public enum EventType
    {
        GameStarted,
        GameEnded,
        Error,
        ConnectionReset,
        Request,
    }
}
