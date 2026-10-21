using System.Collections.Generic;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StatsMod
{
    public partial class BigUI
    {
        internal static readonly string[] PresetKeys = { "default", "combat", "enemies", "weapons", "accuracy", "movement" };
        internal static readonly string[] PresetNames = { "Default", "Combat", "Enemy kills", "Weapon kills", "Accuracy", "Movement" };
        private static readonly HashSet<string> CombatColumns = new HashSet<string> { "Kills", "Deaths", "Max Kill Streak", "Enemy Shields", "Friendly Kills", "Shields Lost" };
        private static readonly HashSet<string> MovementColumns = new HashSet<string> { "Web Swings", "Web Swing Time", "Airborne Time", "Airborne Kills", "Lava Deaths", "Astral Returns" };
        private readonly List<Column> presetColumns = new List<Column>();
        private readonly ControlPoller previousKey = new ControlPoller();
        private readonly ControlPoller nextKey = new ControlPoller();
        private readonly AnyPadPoller previousPad = new AnyPadPoller();
        private readonly AnyPadPoller nextPad = new AnyPadPoller();
        private readonly BindingCache keyboardBindings = new BindingCache();
        private readonly BindingCache padBindings = new BindingCache();
        private sealed class BindingCache
        {
            public string Left, Right, Ultimate, EffectiveLeft, EffectiveRight;
            public void Update(string left, string right, string ultimate)
            {
                if (left == Left && right == Right && ultimate == Ultimate) return;
                Left = left; Right = right; Ultimate = ultimate;
                EffectiveLeft = ultimate != null && Controls.Overlap(left, ultimate) ? Controls.None : left;
                EffectiveRight = ultimate != null && Controls.Overlap(right, ultimate) ? Controls.None : right;
            }
        }
        private int preset;
        private int presetPage;
        private Rect panelRect, previousRect, nextRect;
        private bool PresetEnabled(int index) => index == 0 || Settings.GetBool("stats.display.presets." + PresetKeys[index]);

        private void SelectColumns()
        {
            if (!PresetEnabled(preset)) { preset = 0; presetPage = 0; }
            presetColumns.Clear();
            foreach (Column column in Columns)
            {
                bool selected = preset == 0 ? column.Shown() : preset == 1 ? CombatColumns.Contains(column.Label)
                    : preset == 4 ? column.Accuracy != null : preset == 5 ? MovementColumns.Contains(column.Label)
                    : column.Group == PresetKeys[preset];
                if (selected) presetColumns.Add(column);
            }
            presetPage = Mathf.Clamp(presetPage, 0, Mathf.Max(0, (presetColumns.Count - 1) / 6));
        }

        public void Navigate(int direction)
        {
            SelectColumns();
            int pages = Mathf.Max(1, (presetColumns.Count + 5) / 6);
            if (presetPage + direction >= 0 && presetPage + direction < pages) { presetPage += direction; return; }
            do { preset = (preset + direction + PresetKeys.Length) % PresetKeys.Length; } while (!PresetEnabled(preset));
            SelectColumns();
            presetPage = direction > 0 ? 0 : Mathf.Max(0, (presetColumns.Count - 1) / 6);
        }

        public bool HandlePresetInput(Keyboard keyboard)
        {
            if (!IsVisible() || GameMenus.Open || Controls.Ignored(keyboard)) return false;
            keyboardBindings.Update(Controls.Get(Controls.PreviousPresetKeyboard), Controls.Get(Controls.NextPresetKeyboard), ModConfig.SurgeUltimateTaken ? Controls.Get(Controls.UltimateKeyboard) : null);
            return Page(previousKey.WasPressed(keyboardBindings.EffectiveLeft, keyboard), nextKey.WasPressed(keyboardBindings.EffectiveRight, keyboard));
        }

        public bool HandlePresetPads()
        {
            if (!IsVisible() || GameMenus.Open) return false;
            padBindings.Update(Controls.Get(Controls.PreviousPresetController), Controls.Get(Controls.NextPresetController), ModConfig.SurgeUltimateTaken ? Controls.Get(Controls.UltimateController) : null);
            return Page(previousPad.WasPressed(padBindings.EffectiveLeft), nextPad.WasPressed(padBindings.EffectiveRight));
        }

        private bool Page(bool previous, bool next)
        {
            if (previous || next) Navigate(previous ? -1 : 1);
            return previous || next;
        }

        private void HandlePresetMouse()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && (previousRect.Contains(e.mousePosition) || nextRect.Contains(e.mousePosition)))
            { Navigate(previousRect.Contains(e.mousePosition) ? -1 : 1); e.Use(); }
            if (e.type == EventType.ScrollWheel && panelRect.Contains(e.mousePosition))
            { Navigate(e.delta.y > 0 ? 1 : -1); e.Use(); }
        }

        private void DrawPresetNavigation(Rect content, float scale)
        {
            previousRect = new Rect(content.x, content.yMax - 30f * scale, 36f * scale, 30f * scale);
            nextRect = new Rect(content.xMax - 36f * scale, previousRect.y, 36f * scale, previousRect.height);
            Hud.Label(previousRect, "<", nameStyle, Hud.Pink);
            Hud.Label(nextRect, ">", nameStyle, Hud.Pink);
            int pages = Mathf.Max(1, (presetColumns.Count + 5) / 6);
            Hud.Label(new Rect(previousRect.xMax + 8f * scale, previousRect.y, content.width - 88f * scale, previousRect.height),
                PresetNames[preset] + (pages > 1 ? $"  {presetPage + 1}/{pages}" : ""), nameStyle, Hud.Text);
        }
    }
}
