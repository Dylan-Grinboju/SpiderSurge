using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace StatsMod
{
    // Keeps a small picture of every player's live spider, re-shot while it is alive, so a title card can show the spider itself
    public class SpiderPortraits : MonoBehaviour
    {
        public const int Size = 192;
        private const float ViewRadius = 7.5f;
        private const float RefreshSeconds = 1f;
        private const float FirstShotDelay = 0.6f;
        private static readonly Color Backdrop = new Color(0.075f, 0.06f, 0.115f, 1f);

        public static SpiderPortraits Instance { get; private set; }

        private readonly Dictionary<int, RenderTexture> portraits = new Dictionary<int, RenderTexture>();
        private readonly Dictionary<int, float> nextShot = new Dictionary<int, float>();
        private readonly Dictionary<int, float> seenAlive = new Dictionary<int, float>();
        private Camera cam;
        private int shooting = -1;

        public static Texture Of(int playerId)
        {
            if (Instance == null || playerId < 0) return null;
            return Instance.portraits.TryGetValue(playerId, out RenderTexture texture) && texture != null && texture.IsCreated() ? texture : null;
        }

        private void Awake()
        {
            Instance = this;
            GameObject holder = new GameObject("SpiderPortraitCamera");
            holder.transform.SetParent(transform, false);
            cam = holder.AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = ViewRadius;
            cam.nearClipPlane = -50f;
            cam.farClipPlane = 50f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Backdrop;
            cam.cullingMask = (1 << LayerMask.NameToLayer("Player")) | (1 << LayerMask.NameToLayer("SpiderLegs"));
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.depth = -50f;
            cam.useOcclusionCulling = false;
            try
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"Spider portraits: could not configure the URP camera: {ex.Message}");
            }
        }

        private void Update()
        {
            if (shooting >= 0)
            {
                cam.enabled = false;
                cam.targetTexture = null;
                shooting = -1;
            }

            float now = Time.unscaledTime;
            List<PlayerController> players = Players.All;
            if (players == null) return;
            foreach (PlayerController player in players)
            {
                if (player == null) continue;
                int id = player.playerID.Value;
                SpiderHealthSystem health = player.spiderHealthSystem;
                if (health == null || health.dead || health.astralDead || !health.gameObject.activeInHierarchy)
                {
                    seenAlive.Remove(id);
                    continue;
                }

                if (!seenAlive.TryGetValue(id, out float since))
                {
                    since = now;
                    seenAlive[id] = now;
                    nextShot[id] = portraits.ContainsKey(id) ? now + FirstShotDelay : now + FirstShotDelay * 0.5f;
                }
                if (now < since + FirstShotDelay * 0.5f) continue;
                if (nextShot.TryGetValue(id, out float due) && now < due) continue;

                Shoot(id, health);
                nextShot[id] = now + RefreshSeconds;
                break;
            }
        }

        // Renders one spider into its texture this frame; the camera is switched off again on the next update
        private void Shoot(int playerId, SpiderHealthSystem health)
        {
            if (!portraits.TryGetValue(playerId, out RenderTexture texture) || texture == null)
            {
                texture = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32) { name = $"SpiderPortrait{playerId}", filterMode = FilterMode.Bilinear };
                texture.Create();
                portraits[playerId] = texture;
            }

            Vector3 center = health.transform.position;
            cam.transform.position = new Vector3(center.x, center.y, 0f);
            cam.transform.rotation = Quaternion.identity;
            cam.targetTexture = texture;
            cam.enabled = true;
            shooting = playerId;
        }

        private void OnDestroy()
        {
            foreach (RenderTexture texture in portraits.Values)
            {
                if (texture != null) texture.Release();
            }
            portraits.Clear();
            if (Instance == this) Instance = null;
        }
    }
}
