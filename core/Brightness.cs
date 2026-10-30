using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpiderSurge.Common
{
    public sealed class Brightness : MonoBehaviour
    {
        public const string SettingKey = "display.brightness";
        public const int Min = 50;
        public const int Max = 200;
        public static readonly KeyValuePair<string, object>[] Defaults = { new KeyValuePair<string, object>(SettingKey, 100) };
        // Layer 23 is unused by the game; only the final gameplay camera reads this volume to avoid applying brightness twice.
        private const int VolumeLayer = 23;
        private const int VolumeMask = 1 << VolumeLayer;

        private UniversalAdditionalCameraData _camera;
        private bool _addedLayer;
        private Volume _volume;
        private VolumeProfile _profile;
        private ColorAdjustments _color;

        public static int Value => Mathf.Clamp(Settings.GetInt(SettingKey), Min, Max);

        public static void Attach(CameraEffects camera)
        {
            if (camera != null && camera.GetComponent<Brightness>() == null) camera.gameObject.AddComponent<Brightness>();
        }

        public static void Shutdown()
        {
            foreach (var brightness in Resources.FindObjectsOfTypeAll<Brightness>())
            {
                brightness.enabled = false;
                Destroy(brightness);
            }
        }

        private void Awake()
        {
            _camera = GetComponent<UniversalAdditionalCameraData>();
            var target = new GameObject("SpiderSurge brightness") { layer = VolumeLayer };
            target.transform.SetParent(transform, false);
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = _profile.Add<ColorAdjustments>(false);
            _volume = target.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 1000f;
            _volume.sharedProfile = _profile;
        }

        private void OnEnable()
        {
            Settings.Changed += Apply;
            Apply(null);
        }

        private void OnDisable()
        {
            Settings.Changed -= Apply;
            if (_volume != null) _volume.enabled = false;
            RestoreMask();
        }

        private void Apply(string key)
        {
            if (!Settings.Affects(key, SettingKey) || _volume == null || _camera == null) return;
            _color.postExposure.Override(Mathf.Log(Value / 100f, 2f));
            _volume.enabled = Value != 100;
            if (_volume.enabled)
            {
                if ((_camera.volumeLayerMask & VolumeMask) == 0) _addedLayer = true;
                _camera.volumeLayerMask |= VolumeMask;
            }
            else RestoreMask();
        }

        private void RestoreMask()
        {
            if (_addedLayer && _camera != null) _camera.volumeLayerMask &= ~VolumeMask;
            _addedLayer = false;
        }

        private void OnDestroy()
        {
            if (_volume != null) Destroy(_volume.gameObject);
            if (_color != null) Destroy(_color);
            if (_profile != null) Destroy(_profile);
        }
    }

    [HarmonyPatch(typeof(CameraEffects), "Awake")]
    public static class CameraEffects_Brightness_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CameraEffects __instance) => Brightness.Attach(__instance);
    }
}
