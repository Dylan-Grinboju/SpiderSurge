using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using SpiderSurge;
using SpiderSurge.Common;

internal static class UpdaterSettingsTests
{
    private static object Field(string name) => typeof(ModUpdater).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    private static void SetField(string name, object value) => typeof(ModUpdater).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);

    internal static void Run(Action<bool, string> check)
    {
        Settings.Define(ModUpdater.Defaults);
        Settings.Set(ModUpdater.CheckForUpdatesKey, false);
        SpiderSurgeMod.Version = "0.4.0";
        ModUpdater.ScheduleCheck();
        check(!ModUpdater.SubscribeToBetas, "stable installs start unsubscribed");

        ModUpdater.Shutdown();
        SpiderSurgeMod.Version = "1.0.0-beta.1+local";
        ModUpdater.ScheduleCheck();
        check(ModUpdater.SubscribeToBetas && Field("checkCancellation") == null, "first beta launch subscribes even with startup checks disabled");
        Settings.Set(ModUpdater.SubscribeToBetasKey, false);
        ModUpdater.Shutdown();
        SpiderSurgeMod.Version = "1.0.0-beta.2";
        ModUpdater.ScheduleCheck();
        check(!ModUpdater.SubscribeToBetas, "beta opt-out survives restart and a newer beta install");

        Settings.Set(ModUpdater.SubscribeToBetasKey, true);
        ModUpdater.Shutdown();
        SpiderSurgeMod.Version = "1.0.0";
        ModUpdater.ScheduleCheck();
        check(ModUpdater.SubscribeToBetas, "subscription survives installing a stable release");

        var beta = new UpdateRelease { Tag = "v1.1.0-beta.1", Prerelease = true };
        typeof(ModUpdater).GetMethod("Checked", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { beta, null, true });
        var page = new SettingsPage();
        ModUpdater.BuildPage(page);
        check(page.Notes.Contains(ModUpdater.BetaSubscriptionNotice), "update page explains subscription and where to unsubscribe");
        check(Field("notice") == beta && Field("release") == beta, "beta result queues its lobby offer");
        Settings.Set(ModUpdater.SubscribeToBetasKey, false);
        check(Field("notice") == null && Field("release") == null, "unsubscribing clears pending offers and install actions");

        Settings.Set(ModUpdater.CheckForUpdatesKey, true);
        var oldCheck = (CancellationTokenSource)Field("checkCancellation");
        var oldToken = oldCheck.Token;
        Settings.Set(ModUpdater.SubscribeToBetasKey, true);
        check(oldToken.IsCancellationRequested && Field("checkCancellation") != oldCheck, "channel change cancels the old check and schedules a fresh check");
        ModUpdater.Shutdown();

        Settings.Define(ModUpdater.Defaults);
        Settings.Set(ModUpdater.CheckForUpdatesKey, false);
        SpiderSurgeMod.Version = "0.4.1-dev.12";
        ModUpdater.ScheduleCheck();
        check(!ModUpdater.SubscribeToBetas, "development builds do not automatically subscribe");
        Settings.Set(ModUpdater.SubscribeToBetasKey, true);
        Settings.Set(ModUpdater.SubscribeToBetasKey, false);
        ModUpdater.Shutdown();
        SpiderSurgeMod.Version = "1.0.0-beta.1";
        ModUpdater.ScheduleCheck();
        check(!ModUpdater.SubscribeToBetas, "explicit stable-build opt-out survives a later beta install");
        ModUpdater.Shutdown();
        SetField("status", "Check for a published SpiderSurge release.");
    }
}

// Engine boundaries are stubbed; subscription, cancellation and page logic use the production updater.
namespace SpiderSurge
{
    internal static class SpiderSurgeMod { internal static string Version = "0.4.0"; }
}

internal static class LobbyController { internal static bool inLobby = true; }

namespace SpiderSurge.Common
{
    internal static class Settings
    {
        private static readonly Dictionary<string, bool> Values = new Dictionary<string, bool>();
        internal static event Action<string> Changed;
        internal static void Define(IEnumerable<KeyValuePair<string, object>> defaults)
        {
            foreach (var pair in defaults) Values[pair.Key] = (bool)pair.Value;
        }
        internal static bool GetBool(string key) => Values[key];
        internal static void Set(string key, object value) { Values[key] = (bool)value; Changed?.Invoke(key); }
        internal static bool Affects(string changed, string key) => changed == null || changed == key || key.StartsWith(changed + ".", StringComparison.Ordinal);
    }

    public sealed class SettingsPage
    {
        internal readonly List<string> Notes = new List<string>();
        public static bool IsShowing(string page) => false;
        public static void RefreshOpen() { }
        public void Title(string text) { }
        public void Line(string text) { }
        public void Paragraph(string text) { }
        public void Note(string text) => Notes.Add(text);
        public void Button(string text, Action action) { }
    }

    internal static class MainThread { internal static void Enqueue(Action action) { } }
    internal static class Announcer { internal static void InformationPopup(string text) { } }
    internal static class ThreadSafeLog
    {
        internal static void LogInfo(string text) { }
        internal static void LogWarning(string text) { }
    }
    public static partial class ModUpdater
    {
        private static string downloadProblem;
        private static void ShowOffer(UpdateRelease found) { }
        private static void ClosePopup() { }
        private static void RefreshPopup() { }
    }
}
