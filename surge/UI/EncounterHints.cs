using System.Collections.Generic;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    public sealed class EncounterHints : MonoBehaviour
    {
        private sealed class Hint
        {
            public string Key;
            public string Text;
            public EnemyHealthSystem Enemy;
        }

        private static readonly ProgressSection<bool> Seen = new ProgressSection<bool>("encounter.hints", Parse, (key, value) => key, (a, b) => true);
        private static EncounterHints instance;
        private readonly Queue<Hint> waiting = new Queue<Hint>();
        private readonly HashSet<string> queued = new HashSet<string>();
        private Hint current;
        private float remaining;
        private GUIStyle style;
        private int fontVersion = -1;

        private static bool Parse(string line, out string key, out bool value)
        {
            key = line.Trim();
            value = true;
            return key.Length > 0;
        }

        private void Awake() => instance = this;
        private void OnDestroy() { if (instance == this) instance = null; }

        public static bool Encounter(string key, string text, EnemyHealthSystem enemy)
        {
            if (Seen.Contains(key)) return true;
            if (instance == null || !SurgeGameModeManager.IsSurgeRunActive || enemy == null || enemy.IsDead()) return false;
            var network = enemy.GetComponent<NetworkObject>();
            if (network == null || !network.IsSpawned) return false;
            var camera = Camera.main;
            if (camera == null) return false;
            Vector3 point = camera.WorldToViewportPoint(enemy.transform.position);
            if (point.z < 0f || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f) return false;
            if (instance.queued.Add(key)) instance.waiting.Enqueue(new Hint { Key = key, Text = text, Enemy = enemy });
            return false;
        }

        private static bool Visible => ChaosWallet.Enabled && !GameMenus.Open && !ChallengeController.Pending &&
            SurvivalModeHud.instance != null && SurvivalModeHud.instance.survivalHud.IsVisible;

        private void Update()
        {
            if (!SurgeGameModeManager.IsSurgeRunActive)
            {
                current = null;
                waiting.Clear();
                queued.Clear();
                return;
            }
            if (!Visible) return;
            if (current == null && waiting.Count > 0)
            {
                current = waiting.Dequeue();
                if (current.Enemy == null || current.Enemy.IsDead())
                {
                    queued.Remove(current.Key);
                    current = null;
                    return;
                }
                remaining = 6f;
            }
            if (current == null) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                Seen.Add(current.Key, true);
                current = null;
            }
        }

        private void OnGUI()
        {
            if (current == null || !Visible || Event.current.type != EventType.Repaint) return;
            Hud.EnsureFonts();
            if (style == null || fontVersion != Hud.FontVersion)
            {
                style = Hud.Style(18, false, TextAnchor.MiddleCenter, true);
                fontVersion = Hud.FontVersion;
            }
            float scale = Mathf.Min(Screen.width / 1066f, Screen.height / 600f);
            style.fontSize = Mathf.RoundToInt(14f * scale);
            var rect = new Rect((Screen.width - 550f * scale) / 2f, Screen.height * .14f, 550f * scale, 48f * scale);
            Hud.Fill(rect, Hud.Ink);
            Hud.Fill(new Rect(rect.x, rect.y, 3f * scale, rect.height), Hud.Pink);
            rect.x += 14f * scale;
            rect.width -= 28f * scale;
            Hud.Label(rect, current.Text, style, Hud.Text);
        }
    }
}
