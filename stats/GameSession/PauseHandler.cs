using UnityEngine;
using Silk;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class PauseHandler : MonoBehaviour
    {
        private bool wasPaused = false;

        void Update()
        {
            StatsNet.Tick();
            bool isPaused = Mathf.Approximately(Time.timeScale, 0f);
            if (isPaused && !wasPaused)
            {
                GameSessionManager.Instance.PauseTimers();
            }
            else if (!isPaused && wasPaused)
            {
                GameSessionManager.Instance.ResumeTimers();
            }

            wasPaused = isPaused;
        }
    }
}
