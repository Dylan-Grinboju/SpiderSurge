using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    public abstract class Part
    {
        public abstract string Key { get; }
        public abstract string Title { get; }
        public abstract string Description { get; }
        public abstract string Namespace { get; }
        public abstract bool OnByDefault { get; }

        public string HarmonyId => "com.SpiderSurge." + Key;
        public string SettingKey => "parts." + Key;
        public virtual bool CanDisable => true;
        public bool Enabled => !CanDisable || Settings.GetBool(SettingKey);
        public bool Running { get; internal set; }
        public bool Failed { get; internal set; }
        public string BlockedBy { get; internal set; }
        public Harmony Harmony { get; internal set; }

        public virtual bool HasSettings => false;

        protected internal virtual void OnStart()
        {
        }

        protected internal virtual void OnStop()
        {
        }

        public virtual void BuildSettings(SettingsPage page)
        {
        }
    }

    public static class Parts
    {
        private const string CoreNamespace = "SpiderSurge.Common";
        private const string CoreHarmonyId = "com.SpiderSurge.core";

        public static readonly Part Fixes = new global::SpiderSurge.Fixes.FixesPart();
        public static readonly Part Surge = new global::SpiderSurge.SurgePart();
        public static readonly Part Teams = new global::SpiderTeams.TeamsPart();
        public static readonly Part Stats = new global::StatsMod.StatsPart();

        public static readonly Part[] All = { Fixes, Surge, Teams, Stats };

        private static Harmony _core;

        public static IEnumerable<KeyValuePair<string, object>> Defaults => All.Where(part => part.CanDisable).Select(part =>new KeyValuePair<string, object>(part.SettingKey, part.OnByDefault));

        public static void StartAll()
        {
            Type[] types = AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly());

            _core = new Harmony(CoreHarmonyId);
            Patch(_core, types.Where(type => IsIn(type, CoreNamespace)), "core");

            foreach (Part part in All)
            {
                Start(part, types);
            }
        }

        public static void StopAll()
        {
            for (int i = All.Length - 1; i >= 0; i--)
            {
                Stop(All[i]);
            }

            _core?.UnpatchSelf();
            _core = null;
        }

        private static void Start(Part part, Type[] types)
        {
            if (!part.Enabled)
            {
                Logger.LogInfo($"{part.Title} is turned off");
                return;
            }
            if (part.BlockedBy != null)
            {
                Logger.LogWarning($"{part.Title} stays off because the old {part.BlockedBy} is still in the Mods folder");
                return;
            }

            Logger.LogInfo($"Starting {part.Title}...");
            part.Harmony = new Harmony(part.HarmonyId);
            try
            {
                part.OnStart();
            }
            catch (Exception ex)
            {
                Logger.LogError($"{part.Title} failed to start, so it stays off: {ex}");
                part.Failed = true;
                Stop(part);
                return;
            }
            Patch(part.Harmony, types.Where(type => OwnerOf(type) == part), part.Title);
            part.Running = true;
        }

        private static void Stop(Part part)
        {
            if (part.Harmony == null)
            {
                return;
            }

            try
            {
                part.OnStop();
            }
            catch (Exception ex)
            {
                Logger.LogError($"{part.Title} failed to stop: {ex}");
            }
            part.Harmony?.UnpatchSelf();
            part.Harmony = null;
            part.Running = false;
        }

        // Class by class, so a class a game update broke doesn't stop the others
        private static void Patch(Harmony harmony, IEnumerable<Type> types, string owner)
        {
            foreach (Type type in types)
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"{owner} could not apply {type.Name}: {ex.GetBaseException().Message}");
                }
            }
            Logger.LogInfo($"{owner} patched {harmony.GetPatchedMethods().Count()} methods");
        }

        private static Part OwnerOf(Type type)
        {
            if (IsIn(type, CoreNamespace))
            {
                return null;
            }

            Part owner = null;
            foreach (Part part in All)
            {
                if (IsIn(type, part.Namespace) && (owner == null || part.Namespace.Length > owner.Namespace.Length))
                {
                    owner = part;
                }
            }
            return owner;
        }

        private static bool IsIn(Type type, string space)
        {
            string name = type.Namespace;
            return name != null && (name == space || name.StartsWith(space + ".", StringComparison.Ordinal));
        }
    }
}
