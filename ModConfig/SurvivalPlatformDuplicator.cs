using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using TMPro;

namespace SpiderSurge
{
    /// <summary>
    /// This class duplicates survival start platforms in the lobby, positioned 100 units above the original.
    /// Only activates when the lobby scene loads.
    /// </summary>
    public class SurvivalPlatformDuplicator
    {
        private static bool hasAttemptedDuplication = false;
        private static readonly string platformName = "SurvivalStartPlatform";


        public static void Initialize()
        {
            Logger.LogInfo("SurvivalPlatformDuplicator initialized");
        }

        public static void TryDuplicateSurvivalPlatforms()
        {
            if (hasAttemptedDuplication)
            {
                Logger.LogError("Platform duplication already attempted this session");
                return;
            }

            try
            {
                GameObject survivalPlatform = FindSurvivalPlatform();

                if (survivalPlatform != null)
                {
                    CreateDuplicatePlatform(survivalPlatform);
                    hasAttemptedDuplication = true;
                }
                else
                {
                    Logger.LogWarning("No survival platform found to duplicate");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error duplicating survival platforms: {ex.Message}");
            }
        }

        private static GameObject FindSurvivalPlatform()
        {
            GameObject platform = GameObject.Find(platformName);
            if (platform != null)
            {
                return platform;
            }
            return null;
        }

        private static void CreateDuplicatePlatform(GameObject originalPlatform)
        {
            try
            {
                // Check if duplicate already exists
                GameObject existingDuplicate = GameObject.Find(originalPlatform.name + "_Duplicate_SpiderSurge");
                if (existingDuplicate != null)
                {
                    Logger.LogError("Duplicate platform already exists");
                    return;
                }

                // Create the duplicate
                GameObject duplicatePlatform = GameObject.Instantiate(originalPlatform);

                Vector3 newPosition = originalPlatform.transform.position;
                newPosition.y += 70f;
                newPosition.x += 50f;
                duplicatePlatform.transform.position = newPosition;
                duplicatePlatform.name = originalPlatform.name + "_Duplicate_SpiderSurge";
                duplicatePlatform.SetActive(true);

                // Handle networking if needed
                NetworkObject networkObject = duplicatePlatform.GetComponent<NetworkObject>();
                if (networkObject != null && NetworkManager.Singleton != null &&
                    (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                {
                    try
                    {
                        networkObject.Spawn();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not spawn NetworkObject: {ex.Message}");
                    }
                }

                // Update text on the duplicated platform
                UpdatePlatformText(duplicatePlatform);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error creating duplicate platform: {ex.Message}");
            }
        }

        private static void UpdatePlatformText(GameObject duplicatePlatform)
        {
            try
            {
                // Find all TextMeshPro components (both UI and 3D variants)
                var tmpTexts = duplicatePlatform.GetComponentsInChildren<TMP_Text>(true);
                var tmp3DTexts = duplicatePlatform.GetComponentsInChildren<TextMeshPro>(true);

                // Update TextMeshPro UI components
                foreach (var tmpText in tmpTexts)
                {
                    if (tmpText.text.Contains("WAVE SURVIVAL") || tmpText.text.Contains("WAVE SURVIVAL"))
                    {
                        tmpText.text = tmpText.text.Replace("WAVE SURVIVAL", "Surge Mode").Replace("WAVE SURVIVAL", "Surge Mode");
                    }
                }

                Logger.LogInfo("Platform text update completed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error updating platform text: {ex.Message}");
            }
        }

        public static void ResetDuplicationFlag()
        {
            hasAttemptedDuplication = false;
        }
    }

    /// <summary>
    /// Patch that triggers when ANY scene loads - only activates for lobby scenes
    /// </summary>
    [HarmonyPatch(typeof(UnityEngine.SceneManagement.SceneManager), "Internal_SceneLoaded")]
    public static class SceneLoadedPatch
    {
        [HarmonyPostfix]
        static void Internal_SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                Logger.LogInfo($"Scene loaded: {scene.name} (mode: {mode})");

                // Only activate for lobby/main menu scenes
                if (scene.name.ToLower().Contains("lobby") || scene.name.ToLower().Contains("main") || scene.name.ToLower().Contains("menu"))
                {
                    var monoBehaviour = UnityEngine.Object.FindObjectOfType<MonoBehaviour>();
                    if (monoBehaviour != null)
                    {
                        monoBehaviour.StartCoroutine(DelayedDuplication());
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SceneLoadedPatch: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator DelayedDuplication()
        {
            yield return new UnityEngine.WaitForSeconds(2f);
            SurvivalPlatformDuplicator.TryDuplicateSurvivalPlatforms();
        }
    }
}
