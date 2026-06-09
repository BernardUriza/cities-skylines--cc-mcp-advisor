using System;
using System.Diagnostics;
using UnityEngine;

namespace ClaudeAdvisor
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public static class Logger
    {
        private const string TAG = "[ClaudeAdvisor]";

        // Minimum level to emit — Debug in dev, Info in prod
        public static LogLevel MinLevel = LogLevel.Debug;

        // Thread-local correlation ID for request tracing
        [ThreadStatic]
        private static string _correlationId;

        public static string CorrelationId
        {
            get { return _correlationId ?? "-"; }
            set { _correlationId = value; }
        }

        public static string NewCorrelationId()
        {
            // Short 8-char ID: enough for local tracing, not a distributed system
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _correlationId = id;
            return id;
        }

        public static void ClearCorrelationId()
        {
            _correlationId = null;
        }

        // --- Core logging ---

        public static void Log(LogLevel level, string component, string message, string detail = null)
        {
            if (level < MinLevel) return;

            string line = string.Format("{0} [{1}] [{2}] [cid:{3}] {4}",
                TAG, level.ToString().ToUpper(), component, CorrelationId, message);

            if (detail != null)
                line += " | " + detail;

            switch (level)
            {
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(line);
                    break;
                case LogLevel.Warn:
                    UnityEngine.Debug.LogWarning(line);
                    break;
                default:
                    UnityEngine.Debug.Log(line);
                    break;
            }
        }

        // --- Convenience methods ---

        public static void Debug(string component, string message, string detail = null)
        {
            Log(LogLevel.Debug, component, message, detail);
        }

        public static void Info(string component, string message, string detail = null)
        {
            Log(LogLevel.Info, component, message, detail);
        }

        public static void Warn(string component, string message, string detail = null)
        {
            Log(LogLevel.Warn, component, message, detail);
        }

        public static void Error(string component, string message, Exception ex = null)
        {
            string detail = null;
            if (ex != null)
                detail = ex.GetType().Name + ": " + ex.Message;
            Log(LogLevel.Error, component, message, detail);
        }

        // --- Request lifecycle ---

        public static Stopwatch RequestStart(string method, string path)
        {
            Info("HTTP", method + " " + path + " started");
            return Stopwatch.StartNew();
        }

        public static void RequestEnd(string method, string path, int statusCode, Stopwatch sw)
        {
            sw.Stop();
            string detail = "status=" + statusCode + " duration=" + sw.ElapsedMilliseconds + "ms";
            if (statusCode >= 400)
                Warn("HTTP", method + " " + path + " completed", detail);
            else
                Info("HTTP", method + " " + path + " completed", detail);
        }

        // --- Action lifecycle ---

        public static void ActionQueued(string action, string detail = null)
        {
            Info("Action", action + " queued", detail);
        }

        public static void ActionExecuted(string action, string detail = null)
        {
            Info("Action", action + " executed", detail);
        }

        public static void ActionFailed(string action, Exception ex)
        {
            Error("Action", action + " failed", ex);
        }

        // --- Data collection ---

        public static void CollectorStart(string collector)
        {
            Debug("Data", collector + " collecting");
        }

        public static void CollectorEnd(string collector, int itemCount = -1)
        {
            string detail = itemCount >= 0 ? "items=" + itemCount : null;
            Debug("Data", collector + " done", detail);
        }

        public static void CollectorError(string collector, Exception ex)
        {
            Error("Data", collector + " failed", ex);
        }
    }
}
