using UnityEngine;
using Silk;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class PauseHandler : MonoBehaviour
    {
        private bool wasPaused = false;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            bool isPaused = Mathf.Approximately(Time.timeScale, 0f);
            if (isPaused && !wasPaused)
            {
                PlayerStateTracker.Instance.PauseTimers();
            }
            else if (!isPaused && wasPaused)
            {
                PlayerStateTracker.Instance.ResumeTimers();
            }

            wasPaused = isPaused;
        }
    }
}