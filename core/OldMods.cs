using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using Silk;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    internal static class OldMods
    {
        private const string OwnId = "SpiderSurge_Mod";
        private const string RunningKey = "SpiderSurge.RunningFrom";
        private static readonly Version FirstMergedVersion = new Version(1, 0, 0);

        private static readonly Dictionary<string, Part> Replaced = new Dictionary<string, Part>
        {
            { "Stats_Mod", Parts.Stats },
            { "SpiderTeams", Parts.Teams },
        };

        // Silk starts every copy of the DLL it finds; the first one to start runs and the others stay off
        public static bool Claim()
        {
            if (AppDomain.CurrentDomain.GetData(RunningKey) is string running)
            {
                Logger.LogWarning($"SpiderSurge already runs from {running}, so this copy stays off. Delete the extra copy from the Mods folder");
                return false;
            }
            AppDomain.CurrentDomain.SetData(RunningKey, Assembly.GetExecutingAssembly().Location);
            return true;
        }

        public static void Release()
        {
            AppDomain.CurrentDomain.SetData(RunningKey, null);
        }

        // Stats, SpiderTeams and SpiderSurge before 1.0.0 patch the game on their own, so the part they would double stays off
        public static void Check()
        {
            var found = new List<string>();
            var blocked = new List<string>();
            try
            {
                string own = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Silk", "Mods");
                if (!Directory.Exists(folder))
                {
                    folder = Path.GetDirectoryName(own);
                }

                foreach (string file in Directory.GetFiles(folder, "*.dll", SearchOption.AllDirectories))
                {
                    if (file.IndexOf("Disabled", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        string.Equals(Path.GetFullPath(file), own, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string id = ReadModId(file, out string version);
                    string name = Path.GetFileName(file);
                    Part part = DoubledPart(id, version);
                    if (part != null)
                    {
                        part.BlockedBy = name;
                        found.Add(name);
                        if (!blocked.Contains(part.Title))
                        {
                            blocked.Add(part.Title);
                        }
                    }
                    else if (id == OwnId)
                    {
                        found.Add($"{name} (a second copy of SpiderSurge)");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not look for old mod files: {ex.Message}");
            }

            if (found.Count == 0)
            {
                return;
            }

            string list = string.Join("\n", found);
            string off = blocked.Count > 0 ? $"\nUntil then, SpiderSurge's {string.Join(" and ", blocked)} {(blocked.Count == 1 ? "stays" : "stay")} off." : "";
            Logger.LogWarning($"Old mod files in the Mods folder, delete them and restart the game:\n{list}");
            MainThread.PopupLater(
                "SpiderSurge now includes Stats and Teams.\n" +
                "Delete these old files from Silk/Mods in the game folder, then restart the game:\n" +
                list + off);
        }

        private static Part DoubledPart(string id, string version)
        {
            if (id == null)
            {
                return null;
            }
            if (Replaced.TryGetValue(id, out Part part))
            {
                return part;
            }
            bool beforeMerge = !Version.TryParse(version?.Split('+', '-')[0], out Version parsed) || parsed < FirstMergedVersion;
            return id == OwnId && beforeMerge ? Parts.Surge : null;
        }

        private static string ReadModId(string file, out string version)
        {
            version = null;
            try
            {
                using (var assembly = AssemblyDefinition.ReadAssembly(file))
                {
                    foreach (TypeDefinition type in assembly.MainModule.Types)
                    {
                        foreach (CustomAttribute attribute in type.CustomAttributes)
                        {
                            if (attribute.AttributeType.FullName == typeof(SilkModAttribute).FullName && attribute.ConstructorArguments.Count > 4)
                            {
                                version = attribute.ConstructorArguments[2].Value as string;
                                return attribute.ConstructorArguments[4].Value as string;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
