using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    public static class MainThread
    {
        private const int PopupDelayMs = 10000;

        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

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
}
