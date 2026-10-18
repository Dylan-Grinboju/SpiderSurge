using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    public static class MainThread
    {
        private const int PopupDelayMs = 10000;

        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private static int _mainThreadId = -1;

        // Called from the main thread at startup
        public static void Claim()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        // Runs now on the main thread, or on its next frame from any other
        public static void Run(Action action)
        {
            if (IsMainThread) action?.Invoke();
            else Enqueue(action);
        }

        public static void Enqueue(Action action)
        {
            if (action != null)
            {
                _queue.Enqueue(action);
            }
        }

        // The game's popups aren't ready while mods load, so a startup message waits
        public static void PopupLater(string text)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(PopupDelayMs);
                Enqueue(() => Announcer.InformationPopup(text));
            });
        }

        public static void Drain()
        {
            Action action;
            while (_queue.TryDequeue(out action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Main-thread task failed: {ex}");
                }
            }
        }
    }

    // Silk's logger appends to its file without a lock, so a line logged from another thread at the same moment as one
    // from the main thread throws there; background tasks log through the main thread instead
    public static class ThreadSafeLog
    {
        public static void LogInfo(string message) => MainThread.Run(() => Silk.Logger.LogInfo(message));

        public static void LogWarning(string message) => MainThread.Run(() => Silk.Logger.LogWarning(message));

        public static void LogError(string message) => MainThread.Run(() => Silk.Logger.LogError(message));
    }
}
