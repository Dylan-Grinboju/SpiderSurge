using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Team-colored chevron over each spider, drawn on every machine whose player turned it on
    public class TeamMarkers : MonoBehaviour
    {
        public static TeamMarkers Instance { get; private set; }

        private sealed class Marker
        {
            public PlayerController Player;
            public SpriteRenderer Renderer;
        }

        private static Sprite _sprite;

        private readonly Dictionary<int, Marker> _markers = new Dictionary<int, Marker>();
        private readonly List<int> _stale = new List<int>();
        private float _nextSync;
        private bool _shown;
        private bool _loggedError;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            foreach (var marker in _markers.Values)
            {
                if (marker.Renderer != null)
                {
                    Destroy(marker.Renderer.gameObject);
                }
            }
            _markers.Clear();
        }

        private void LateUpdate()
        {
            try
            {
                bool show = ShouldShow();
                if (show != _shown || Time.unscaledTime >= _nextSync)
                {
                    _shown = show;
                    _nextSync = Time.unscaledTime + Consts.Timing.MarkerSyncInterval;
                    Sync(show);
                }
                if (show)
                {
                    Follow();
                }
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Logger.LogError($"SpiderTeams marker update failed: {ex}");
                }
            }
        }

        public void Forget(PlayerController player)
        {
            Marker marker;
            if (player != null && _markers.TryGetValue(player.playerID.Value, out marker))
            {
                if (marker.Renderer != null)
                {
                    Destroy(marker.Renderer.gameObject);
                }
                _markers.Remove(player.playerID.Value);
            }
        }

        private static bool ShouldShow()
        {
            if (!ModConfig.ShowMarker || !ModConfig.TeamsEnabled)
            {
                return false;
            }
            return LobbyController.inLobby || LobbyController.inWaitingRoom || TeamManager.TeamMatchActive;
        }

        private void Sync(bool show)
        {
            var manager = TeamManager.Instance;
            var players = TeamManager.Players;
            if (!show || manager == null || players == null)
            {
                foreach (var marker in _markers.Values)
                {
                    if (marker.Renderer != null)
                    {
                        marker.Renderer.enabled = false;
                    }
                }
                return;
            }

            _stale.Clear();
            _stale.AddRange(_markers.Keys);
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                int id = player.playerID.Value;
                _stale.Remove(id);
                Marker marker;
                if (!_markers.TryGetValue(id, out marker) || marker.Renderer == null)
                {
                    marker = new Marker { Renderer = CreateRenderer(id) };
                    _markers[id] = marker;
                }
                marker.Player = player;
                marker.Renderer.color = TeamPalette.TeamColor(manager.GetTeam(player));
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                Marker marker = _markers[_stale[i]];
                if (marker.Renderer != null)
                {
                    Destroy(marker.Renderer.gameObject);
                }
                _markers.Remove(_stale[i]);
            }
        }

        private void Follow()
        {
            var offset = new Vector3(0f, Consts.Marker.HeightOffset, 0f);
            foreach (var marker in _markers.Values)
            {
                if (marker.Renderer == null)
                {
                    continue;
                }

                var player = marker.Player;
                var health = player != null ? player.spiderHealthSystem : null;
                bool visible = health != null && player.isAlive && !health.dead;
                marker.Renderer.enabled = visible;
                if (visible)
                {
                    marker.Renderer.transform.position = health.transform.position + offset;
                }
            }
        }

        private SpriteRenderer CreateRenderer(int playerId)
        {
            var go = new GameObject("SpiderTeamsMarker_P" + (playerId + 1));
            go.transform.SetParent(transform, false);
            float scale = Consts.Marker.Width / (Consts.Marker.TextureSize / Consts.Marker.PixelsPerUnit);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetSprite();
            spriteRenderer.sortingLayerName = Consts.Marker.SortingLayer;
            spriteRenderer.sortingOrder = Consts.Marker.SortingOrder;
            spriteRenderer.enabled = false;
            return spriteRenderer;
        }

        // White downward triangle with a dark outline, tinted per team through SpriteRenderer.color.
        private static Sprite GetSprite()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            int size = Consts.Marker.TextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var a = new Vector2(4f, size - 6f);
            var c = new Vector2((size - 1) / 2f, 6f);
            var b = new Vector2(size - 5f, size - 6f);
            var outline = new Color(0.08f, 0.08f, 0.08f, 1f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x, y);
                    float inside = Mathf.Min(EdgeDistance(a, c, p), Mathf.Min(EdgeDistance(c, b, p), EdgeDistance(b, a, p)));
                    float alpha = Mathf.Clamp01(inside + 0.5f);
                    Color color = inside < Consts.Marker.OutlinePixels ? outline : Color.white;
                    color.a = alpha;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            _sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), Consts.Marker.PixelsPerUnit);
            return _sprite;
        }

        private static float EdgeDistance(Vector2 from, Vector2 to, Vector2 point)
        {
            Vector2 edge = to - from;
            return (edge.x * (point.y - from.y) - edge.y * (point.x - from.x)) / edge.magnitude;
        }
    }
}
