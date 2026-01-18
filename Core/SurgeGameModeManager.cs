using Silk;
using Logger = Silk.Logger;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace SpiderSurge
{
    public class SurgeGameModeManager : MonoBehaviour
    {
        public static SurgeGameModeManager Instance { get; private set; }

        public bool IsActive { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Logger.LogInfo("SurgeGameModeManager initialized and set to persist across scenes");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetActive(bool active)
        {
            IsActive = active;
        }

    }
}