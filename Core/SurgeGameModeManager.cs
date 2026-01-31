using UnityEngine;

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