using System;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace SpiderSurge.Common
{
    [HarmonyPatch(typeof(RebindActionUINew), "PerformInteractiveRebind")]
    internal static class ChordRebindPatch
    {
        private static void Postfix(RebindActionUINew __instance, InputAction action, int bindingIndex)
        {
            if (action.type != InputActionType.Button || action.bindings[bindingIndex].isPartOfComposite) return;
            var operation = __instance.ongoingRebind;
            if (operation == null) return;
            if (__instance.rebindPrompt != null) __instance.rebindPrompt.text += "\nPress one button, or two together.";
            operation.OnMatchWaitForAnother(0.2f).OnApplyBinding((rebind, path) =>
            {
                int modifier = 0;
                InputControl primary = rebind.selectedControl;
                for (int i = 0; i < rebind.candidates.Count; i++)
                {
                    var candidate = rebind.candidates[i];
                    if (InputChord.Code(primary) != 0 && candidate != primary && candidate.device == primary.device && rebind.magnitudes[i] >= 0.5f && InputChord.Code(candidate) != 0)
                    {
                        modifier = InputChord.Code(candidate);
                        break;
                    }
                }
                action.ApplyBindingOverride(bindingIndex, new InputBinding
                {
                    overridePath = path,
                    overrideInteractions = modifier == 0 ? null : InputChord.Interactions(modifier)
                });
            });
        }
    }

    [HarmonyPatch(typeof(RebindActionUINew), nameof(RebindActionUINew.UpdateBindingDisplay))]
    internal static class ChordDisplayPatch
    {
        private static readonly AccessTools.FieldRef<RebindActionUINew, TMPro.TextMeshProUGUI> Text =
            AccessTools.FieldRefAccess<RebindActionUINew, TMPro.TextMeshProUGUI>("m_BindingText");

        private static void Postfix(RebindActionUINew __instance)
        {
            if (!__instance.ResolveActionAndBinding(out var action, out int index)) return;
            var binding = action.bindings[index];
            int modifier = InputChord.Modifier(binding.effectiveInteractions);
            if (modifier != 0 && Text(__instance) != null)
                Text(__instance).text = InputChord.Label(binding.effectivePath, modifier);
        }
    }

    [HarmonyPatch(typeof(RebindActionUINew), "IsUsedButton")]
    internal static class ChordConflictPatch
    {
        private static bool Prefix(RebindActionUINew __instance, ref bool __result)
        {
            if (!__instance.ResolveActionAndBinding(out var action, out int index)) return true;
            var selected = action.bindings[index];
            int modifier = InputChord.Modifier(selected.effectiveInteractions);
            bool duplicate = false;
            foreach (var binding in action.actionMap.bindings)
            {
                if (binding.id == selected.id || (binding.action != "Move" && binding.action != "Jump" && binding.action != "Equip" && binding.action != "Fire")) continue;
                int other = InputChord.Modifier(binding.effectiveInteractions);
                bool samePath = string.Equals(binding.effectivePath, selected.effectivePath, StringComparison.OrdinalIgnoreCase);
                if (samePath && (other == 0 || modifier == 0 || other == modifier)) duplicate = true;
                if (modifier != 0 && other != 0 && InputChord.Code(binding.effectivePath) == modifier && InputChord.Code(selected.effectivePath) == other) duplicate = true;
            }
            __result = duplicate;
            return false;
        }
    }

    [HarmonyPatch(typeof(RebindActionUINew), nameof(RebindActionUINew.StartInteractiveRebind))]
    internal static class ChordPreviousPatch
    {
        internal static InputBinding Previous;
        internal static RebindActionUINew Owner;

        private static void Prefix(RebindActionUINew __instance)
        {
            if (!__instance.ResolveActionAndBinding(out var action, out int index)) return;
            Previous = action.bindings[index];
            Owner = __instance;
        }
    }

    [HarmonyPatch(typeof(RebindActionUINew), "RebindPreviousButton")]
    internal static class ChordRestorePatch
    {
        private static bool Prefix(RebindActionUINew __instance)
        {
            if (ChordPreviousPatch.Owner != __instance || !__instance.ResolveActionAndBinding(out var action, out int index)) return true;
            if (InputChord.Modifier(action.bindings[index].effectiveInteractions) == 0 && InputChord.Modifier(ChordPreviousPatch.Previous.effectiveInteractions) == 0) return true;
            action.RemoveBindingOverride(index);
            action.ApplyBindingOverride(index, ChordPreviousPatch.Previous);
            return false;
        }
    }
}
