using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class SpiderPortraits : MonoBehaviour
    {
        public const int Size = 192;
        private const float ViewRadius = 7.5f;
        private const float ShotDelay = 2.5f;
        private static readonly Color Backdrop = new Color(0.075f, 0.06f, 0.115f, 1f);

        public static SpiderPortraits Instance { get; private set; }

        private readonly Dictionary<int, RenderTexture> portraits = new Dictionary<int, RenderTexture>();
        private readonly Dictionary<int, float> dueAt = new Dictionary<int, float>();
        private readonly HashSet<int> shotThisLife = new HashSet<int>();
        private readonly HashSet<int> captured = new HashSet<int>();
        private readonly Dictionary<int, PlayerController> owners = new Dictionary<int, PlayerController>();
        private readonly Dictionary<int, SpiderHealthSystem> lives = new Dictionary<int, SpiderHealthSystem>();
        private readonly HashSet<int> present = new HashSet<int>();
        private readonly List<int> departed = new List<int>();
        private readonly List<Renderer> hidden = new List<Renderer>();
        private Camera cam;
        private int shooting = -1;
        private Transform subject;

        private static readonly Dictionary<PlayerController, RenderTexture> finished = new Dictionary<PlayerController, RenderTexture>();

        internal static void ClearFinished()
        {
            foreach (var texture in finished.Values) DisposeTexture(texture);
            finished.Clear();
        }

        internal static void CaptureFinished(IEnumerable<PlayerController> players)
        {
            ClearFinished();
            foreach (var player in players)
            {
                var copy = CopyOf(player);
                if (copy != null) finished[player] = copy;
            }
        }

        public static Texture ViewOf(PlayerController player)
        {
            if (!ReferenceEquals(player, null) && finished.TryGetValue(player, out var saved)) return saved;
            if (Instance == null || player == null) return null;
            int id = player.playerID.Value;
            return Instance.captured.Contains(id) && Instance.owners.TryGetValue(id, out var owner) && owner == player
                && Instance.portraits.TryGetValue(id, out var texture) && texture != null && texture.IsCreated() ? texture : null;
        }

        public static RenderTexture CopyOf(PlayerController player)
        {
            if (Instance == null || player == null) return null;
            int id = player.playerID.Value;
            if (!Instance.captured.Contains(id) || !Instance.owners.TryGetValue(id, out PlayerController owner) || owner != player ||
                !Instance.portraits.TryGetValue(id, out RenderTexture source) || source == null || !source.IsCreated()) return null;

            RenderTexture copy = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                copy = NewTexture($"TitlePortrait{id}");
                Graphics.Blit(source, copy);
                return copy;
            }
            catch (System.Exception ex)
            {
                DisposeTexture(copy);
                Logger.LogWarning($"Spider portraits: could not copy a title portrait: {ex.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static RenderTexture NewTexture(string name)
        {
            RenderTexture texture = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32) { name = name, filterMode = FilterMode.Bilinear };
            if (texture.Create()) return texture;
            DisposeTexture(texture);
            throw new System.InvalidOperationException("Could not create a spider portrait texture");
        }

        internal static void DisposeTexture(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
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
            RenderPipelineManager.beginCameraRendering += BeginPortrait;
            RenderPipelineManager.endCameraRendering += EndPortrait;
        }

        private void Update()
        {
            StopShooting();

            float now = Time.unscaledTime;
            List<PlayerController> players = Players.All;
            present.Clear();
            if (players != null) foreach (PlayerController player in players)
            {
                if (player == null) continue;
                int id = player.playerID.Value;
                present.Add(id);
                if (!owners.TryGetValue(id, out PlayerController owner) || owner != player)
                {
                    Forget(id);
                    owners[id] = player;
                }
                SpiderHealthSystem health = player.spiderHealthSystem;
                if (!lives.TryGetValue(id, out SpiderHealthSystem life) || life != health)
                {
                    dueAt.Remove(id);
                    shotThisLife.Remove(id);
                    lives[id] = health;
                }
                if (health == null || health.dead || health.astralDead || !health.gameObject.activeInHierarchy)
                {
                    dueAt.Remove(id);
                    shotThisLife.Remove(id);
                    continue;
                }
                if (shotThisLife.Contains(id)) continue;

                if (!dueAt.TryGetValue(id, out float due))
                {
                    due = now + ShotDelay;
                    dueAt[id] = due;
                }
                if (now < due || shooting >= 0) continue;

                Shoot(id, health);
            }
            departed.Clear();
            foreach (int id in owners.Keys)
                if (!present.Contains(id)) departed.Add(id);
            foreach (int id in departed) Forget(id);
        }

        // Capture one living spider after its spawn effect finishes.
        private void Shoot(int playerId, SpiderHealthSystem health)
        {
            if (!portraits.TryGetValue(playerId, out RenderTexture texture) || texture == null)
            {
                texture = NewTexture($"SpiderPortrait{playerId}");
                portraits[playerId] = texture;
            }

            Vector3 center = health.transform.position;
            cam.transform.position = new Vector3(center.x, center.y, 0f);
            cam.transform.rotation = Quaternion.identity;
            cam.targetTexture = texture;
            subject = health.rootObject.transform;
            cam.enabled = true;
            shooting = playerId;
        }

        // Hide other subjects only for this camera, then restore them before any other camera renders.
        private void BeginPortrait(ScriptableRenderContext context, Camera camera)
        {
            if (camera != cam || shooting < 0) return;
            if (subject == null)
            {
                StopShooting();
                return;
            }
            foreach (Renderer renderer in FindObjectsOfType<Renderer>())
            {
                if (renderer.forceRenderingOff || (cam.cullingMask & (1 << renderer.gameObject.layer)) == 0 || renderer.transform.IsChildOf(subject)) continue;
                hidden.Add(renderer);
                renderer.forceRenderingOff = true;
            }
        }

        private void EndPortrait(ScriptableRenderContext context, Camera camera)
        {
            if (camera != cam || shooting < 0) return;
            if (subject != null)
            {
                shotThisLife.Add(shooting);
                captured.Add(shooting);
            }
            StopShooting();
        }

        private void StopShooting()
        {
            foreach (Renderer renderer in hidden)
                if (renderer != null) renderer.forceRenderingOff = false;
            hidden.Clear();
            if (cam != null)
            {
                cam.enabled = false;
                cam.targetTexture = null;
            }
            subject = null;
            shooting = -1;
        }

        private void Forget(int id)
        {
            if (portraits.TryGetValue(id, out RenderTexture texture)) DisposeTexture(texture);
            portraits.Remove(id);
            owners.Remove(id);
            lives.Remove(id);
            dueAt.Remove(id);
            shotThisLife.Remove(id);
            captured.Remove(id);
        }

        private void OnDisable()
        {
            StopShooting();
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeginPortrait;
            RenderPipelineManager.endCameraRendering -= EndPortrait;
            StopShooting();
            foreach (RenderTexture texture in portraits.Values)
            {
                DisposeTexture(texture);
            }
            portraits.Clear();
            ClearFinished();
            if (Instance == this) Instance = null;
        }
    }
}
