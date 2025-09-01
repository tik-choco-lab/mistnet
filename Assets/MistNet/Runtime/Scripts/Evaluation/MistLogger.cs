using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace MistNet
{
    public static class MistLogger
    {
        public static Level LogLevel = Level.Info;
        public static bool UseJsonFormat = false;

        // camelcase
        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public enum Level
        {
            Trace,
            Debug,
            Info,
            Warning,
            Error,
            Fatal,
            None,
        }

        [Serializable]
        private class LogFormat
        {
            public string timestamp;
            public string level;
            public string message;
            public string caller;

            public void Set(string timestamp, string level, string caller, string message)
            {
                this.timestamp = timestamp;
                this.level = level;
                this.caller = caller;
                this.message = message;
            }
        }

        private static readonly LogFormat LOGFormat = new();

        private static string Format(Level level, object message, string filePath, int lineNumber)
        {
            return UseJsonFormat
                ? FormatJson(level, message, filePath, lineNumber)
                : FormatString(level, message, filePath, lineNumber);
        }

        private static string FormatString(
            Level level,
            object message,
            string filePath,
            int lineNumber)
        {
            return $"{GetLevelText(level)} " +
                   $"{message} " +
                   $"({ShortenPath(filePath)}:{lineNumber}) ";
        }

        private static string FormatJson(
            Level level,
            object message,
            string filePath,
            int lineNumber)
        {
            LOGFormat.Set(
                timestamp:DateTime.Now.ToString("o"),
                level:GetLevelText(level),
                message:message?.ToString(),
                caller:$"{ShortenPath(filePath)}:{lineNumber}"
            );
            return JsonUtility.ToJson(LOGFormat);
        }

        private static string GetLevelText(Level level)
        {
            switch (level)
            {
                case Level.Trace:
                    return "<color=#00ff00>[TRACE]</color>";
                case Level.Debug:
                    return "<color=#00ff00>[DEBUG]</color>";
                case Level.Info:
                    return "<color=#00ffff>[INFO]</color>";
                case Level.Warning:
                    return "<color=#ffff00>[WARNING]</color>";
                case Level.Error:
                    return "<color=#ffb6b7>[ERROR]</color>";
                case Level.Fatal:
                    return "<color=#ff64ff>[FATAL]</color>";
                default:
                    return "";
            }
        }

        private static string ShortenPath(string path)
        {
            var parts = path.Replace("\\", "/").Split('/');
            if (parts.Length >= 2)
            {
                return parts[^2] + "/" + parts[^1];
            }
            return parts[^1];
        }

        private static bool ShouldLog(Level level)
        {
            return level >= LogLevel;
        }

        [HideInCallstack]
        public static void Info(object message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ShouldLog(Level.Info))
                UnityEngine.Debug.Log(Format(Level.Info, message, file, line));
        }

        [HideInCallstack]
        public static void Trace(object message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ShouldLog(Level.Trace))
                UnityEngine.Debug.Log(Format(Level.Trace, message, file, line));
        }

        [HideInCallstack]
        public static void Debug(object message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ShouldLog(Level.Debug))
                UnityEngine.Debug.Log(Format(Level.Debug, message, file, line));
        }

        [HideInCallstack]
        public static void Warning(object message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ShouldLog(Level.Warning))
                UnityEngine.Debug.LogWarning(Format(Level.Warning, message, file, line));
        }

        [HideInCallstack]
        public static void Error(object message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ShouldLog(Level.Error))
                UnityEngine.Debug.LogError(Format(Level.Error, message, file, line));
        }

        [HideInCallstack]
        public static void Fatal(object message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0)
        {
            if (ShouldLog(Level.Fatal))
            {
                UnityEngine.Debug.LogError(Format(Level.Fatal, message, file, line));
            }
        }

        // -----------------------

        [Obsolete]
        [HideInCallstack]
        public static void Log(object message)
        {
            if (!MistConfig.Data.LogDisplay) return;
            UnityEngine.Debug.Log(message);
        }

        [Obsolete]
        [HideInCallstack]
        public static void LogWarning(object message)
        {
            if (!MistConfig.Data.LogWarningDisplay) return;
            UnityEngine.Debug.LogWarning(message);
        }

        [Obsolete]
        [HideInCallstack]
        public static void LogError(object message)
        {
            if (!MistConfig.Data.LogErrorDisplay) return;
            UnityEngine.Debug.LogError(message);
        }
    }
}
