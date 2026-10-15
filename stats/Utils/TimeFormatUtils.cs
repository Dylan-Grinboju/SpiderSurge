using System;

namespace StatsMod
{
    public static class TimeFormatUtils
    {
        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }
    }
}
