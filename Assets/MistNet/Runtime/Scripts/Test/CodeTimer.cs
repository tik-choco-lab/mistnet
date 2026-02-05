using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace MistNet.Test
{
    /// <summary>
    /// C#の処理時間を計測する機能
    /// </summary>
    public readonly struct CodeTimer : IDisposable
    {
        private readonly string _key;
        private readonly long _startTicks;

        public CodeTimer(string key)
        {
            _key = key;
            _startTicks = Stopwatch.GetTimestamp();
        }

        public void Stop()
        {
            Dispose();
        }

        public void Dispose()
        {
            var endTicks = Stopwatch.GetTimestamp();
            var elapsedMs = (endTicks - _startTicks) * 1000.0 / Stopwatch.Frequency;

            Debug.Log($"[{_key}] {elapsedMs}ms");
        }
    }
}
