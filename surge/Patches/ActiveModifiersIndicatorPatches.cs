using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SpiderSurge.Common;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(ActiveModifiersIndicator), "TryUpdateIcons")]
    internal static class ActiveModifiersIndicator_PersonalLoadouts_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(ref List<Modifier> activeWavesMods)
        {
            var perks = PerksManager.Instance;
            var manager = ModifierManager.instance;
            if (!SurgeGameModeManager.IsSurgeRunActive || perks == null || manager == null || !perks.HasPersonalLoadouts) return;

            var actions = new HashSet<string>(perks.GetAbilityPerkNames().Concat(perks.GetAbilityUltimatePerkNames()));
            var selected = new HashSet<string>();
            foreach (var player in Players.All ?? new List<PlayerController>())
            {
                if (player == null) continue;
                int id = player.playerID.Value;
                selected.Add(perks.Loadouts.Get(id, false));
                selected.Add(perks.Loadouts.Get(id, true));
            }

            activeWavesMods = activeWavesMods.Where(modifier => !actions.Contains(modifier.data.key)).ToList();
            foreach (string key in actions)
            {
                if (!selected.Contains(key)) continue;
                int id = manager.GetModId(key);
                if (id >= 0) activeWavesMods.Add(manager.GetModById(id));
            }
        }
    }
}
