using System;
using System.Collections.Concurrent;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class MainThread
    {
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        public static void Enqueue(Action action)
        {
            if (action != null)
            {
                _queue.Enqueue(action);
            }
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
                    Logger.LogError($"SpiderTeams main-thread task failed: {ex}");
                }
            }
        }
    }
}
