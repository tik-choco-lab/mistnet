using System;
using System.Diagnostics;

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

        public void Dispose()
        {
            var endTicks = Stopwatch.GetTimestamp();
            var elapsedMs = (endTicks - _startTicks) * 1000.0 / Stopwatch.Frequency;

            Console.WriteLine($"[{_key}] {elapsedMs}ms");
        }
    }
}
