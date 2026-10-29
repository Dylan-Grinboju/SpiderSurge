using System;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // Runs each listener on its own, so one that throws is logged and the rest still run
    public static class Listeners
    {
        public static void Raise(Action handler, string failure)
        {
            if (handler == null)
            {
                return;
            }
            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{failure}: {ex}");
                }
            }
        }

        public static void Raise<T>(Action<T> handler, T value, string failure)
        {
            if (handler == null)
            {
                return;
            }
            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action<T>)listener)(value);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{failure}: {ex}");
                }
            }
        }

        public static void Raise<T1, T2>(Action<T1, T2> handler, T1 first, T2 second, string failure)
        {
            if (handler == null)
            {
                return;
            }
            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action<T1, T2>)listener)(first, second);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{failure}: {ex}");
                }
            }
        }
    }
}
