using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Mono.Cecil;
using Silk;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // Stats and SpiderTeams used to be separate mods; a leftover DLL would patch the game twice, so its part stays off
    internal static class OldMods
    {
        private const string OwnId = "SpiderSurge_Mod";
        private const int PopupDelayMs = 10000;

        private static readonly Dictionary<string, Part> Replaced = new Dictionary<string, Part>
        {
            { "Stats_Mod", Parts.Stats },
            { "SpiderTeams", Parts.Teams },
        };

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

                    string id = ReadModId(file);
                    string name = Path.GetFileName(file);
                    if (id != null && Replaced.TryGetValue(id, out Part part))
                    {
                        part.BlockedBy = name;
                        found.Add(name);
                        blocked.Add(part.Title);
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
            _ = Task.Run(async () =>
            {
                await Task.Delay(PopupDelayMs);
                MainThread.Enqueue(() => Announcer.InformationPopup(
                    "SpiderSurge now includes Stats and Teams.\n" +
                    "Delete these old files from Silk/Mods in the game folder, then restart the game:\n" +
                    list + off));
            });
        }

        private static string ReadModId(string file)
        {
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
