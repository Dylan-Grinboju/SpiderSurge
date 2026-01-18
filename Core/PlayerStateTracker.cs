using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class PlayerStateTracker : MonoBehaviour
    {
        private static PlayerStateTracker instance;
        public static PlayerStateTracker Instance => instance;

        private Dictionary<PlayerInput, PlayerStateData> playerStates = new Dictionary<PlayerInput, PlayerStateData>();

        private bool isPaused = false;
        private float lastLogTime = 0f;
        private Dictionary<PlayerInput, float> maxStillnessThisSecond = new Dictionary<PlayerInput, float>();

        private const float STILLNESS_SPEED_THRESHOLD = 2.5f;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            // Update max stillness for this second
            foreach (var kvp in playerStates)
            {
                float currentStillness = (float)kvp.Value.GetTotalTime("stillness").TotalSeconds;
                if (!maxStillnessThisSecond.ContainsKey(kvp.Key) || currentStillness > maxStillnessThisSecond[kvp.Key])
                {
                    maxStillnessThisSecond[kvp.Key] = currentStillness;
                }
            }

            if (Time.time - lastLogTime >= 1f)
            {
                lastLogTime = Time.time;
                foreach (var kvp in playerStates)
                {
                    PlayerStateData data = kvp.Value;
                    float maxStillness = maxStillnessThisSecond.ContainsKey(kvp.Key) ? maxStillnessThisSecond[kvp.Key] : 0f;
                    float airborneTime = (float)data.GetTotalTime("airborne").TotalSeconds;
                    float speed = data.Rigidbody != null ? data.Rigidbody.velocity.magnitude : 0f;
                    Logger.LogInfo($"Player {kvp.Key.playerIndex}: Speed={speed:F2}, Stillness={maxStillness:F1}s, Airborne={airborneTime:F1}s");
                }
                maxStillnessThisSecond.Clear();
            }
        }

        public class PlayerStateData
        {
            public PlayerInput PlayerInput { get; set; }
            public SpiderController SpiderController { get; set; }
            public Stabilizer Stabilizer { get; set; }
            public Rigidbody2D Rigidbody { get; set; }

            private Dictionary<string, TimeSpan> totalTimes = new Dictionary<string, TimeSpan>();
            private Dictionary<string, DateTime?> startTimes = new Dictionary<string, DateTime?>();
            private Dictionary<string, bool> wasRunningWhenPaused = new Dictionary<string, bool>();

            public PlayerStateData(PlayerInput playerInput)
            {
                PlayerInput = playerInput;
                SpiderController = playerInput.GetComponentInChildren<SpiderController>();
                if (SpiderController != null)
                {
                    Stabilizer = SpiderController.GetComponentInChildren<Stabilizer>();
                    Rigidbody = SpiderController.bodyRigidbody2D;
                }
            }

            public void StartTimer(string key)
            {
                if (!startTimes.ContainsKey(key))
                {
                    startTimes[key] = null;
                    totalTimes[key] = TimeSpan.Zero;
                }
                if (startTimes[key].HasValue) return;
                startTimes[key] = DateTime.Now;
            }

            public void StopTimer(string key)
            {
                if (!startTimes.ContainsKey(key) || !startTimes[key].HasValue) return;
                totalTimes[key] += DateTime.Now - startTimes[key].Value;
                startTimes[key] = null;
            }

            public TimeSpan GetTotalTime(string key)
            {
                TimeSpan total = totalTimes.ContainsKey(key) ? totalTimes[key] : TimeSpan.Zero;
                if (startTimes.ContainsKey(key) && startTimes[key].HasValue)
                {
                    total += DateTime.Now - startTimes[key].Value;
                }
                return total;
            }

            public bool IsStill()
            {
                return Rigidbody != null && Rigidbody.velocity.magnitude < STILLNESS_SPEED_THRESHOLD;
            }

            public void ResetTimer(string key)
            {
                totalTimes[key] = TimeSpan.Zero;
                startTimes[key] = null;
            }

            public void PauseTimer(string key)
            {
                if (startTimes.ContainsKey(key) && startTimes[key].HasValue)
                {
                    TimeSpan session = DateTime.Now - startTimes[key].Value;
                    totalTimes[key] += session;
                    startTimes[key] = null;
                    wasRunningWhenPaused[key] = true;
                }
                else
                {
                    wasRunningWhenPaused[key] = false;
                }
            }

            public void ResumeTimer(string key)
            {
                if (wasRunningWhenPaused.ContainsKey(key) && wasRunningWhenPaused[key])
                {
                    startTimes[key] = DateTime.Now;
                    wasRunningWhenPaused[key] = false;
                }
            }

            public bool IsAirborne()
            {
                return Stabilizer != null && !Stabilizer.grounded;
            }

            public void Reset()
            {
                totalTimes.Clear();
                startTimes.Clear();
                wasRunningWhenPaused.Clear();
            }
        }

        public void RegisterPlayer(PlayerInput playerInput)
        {
            if (!playerStates.ContainsKey(playerInput))
            {
                playerStates[playerInput] = new PlayerStateData(playerInput);
                Logger.LogInfo($"PlayerStateTracker registered player {playerInput.playerIndex}");
            }
        }

        public void UnregisterPlayer(PlayerInput playerInput)
        {
            if (playerStates.ContainsKey(playerInput))
            {
                playerStates.Remove(playerInput);
                Logger.LogInfo($"PlayerStateTracker unregistered player {playerInput.playerIndex}");
            }
        }

        private void FixedUpdate()
        {
            if (isPaused) return;

            foreach (var kvp in playerStates)
            {
                PlayerStateData data = kvp.Value;

                // Airborne tracking
                if (data.IsAirborne())
                {
                    data.StartTimer("airborne");
                }
                else
                {
                    data.ResetTimer("airborne");
                }

                // Stillness tracking
                if (data.IsStill())
                {
                    data.StartTimer("stillness");
                }
                else
                {
                    data.ResetTimer("stillness");
                }
            }
        }

        public PlayerStateData GetPlayerState(PlayerInput playerInput)
        {
            playerStates.TryGetValue(playerInput, out PlayerStateData data);
            return data;
        }

        public void ResetPlayerStates()
        {
            foreach (var data in playerStates.Values)
            {
                data.Reset();
            }
        }

        public void PauseTimers()
        {
            if (isPaused)
                return;

            isPaused = true;
            foreach (var data in playerStates.Values)
            {
                data.PauseTimer("airborne");
                data.PauseTimer("stillness");
            }
            Logger.LogInfo("PlayerStateTracker: Timers paused for airborne and stillness");
        }

        public void ResumeTimers()
        {
            if (!isPaused)
                return;

            isPaused = false;
            foreach (var data in playerStates.Values)
            {
                data.ResumeTimer("airborne");
                data.ResumeTimer("stillness");
            }
            Logger.LogInfo("PlayerStateTracker: Timers resumed for airborne and stillness");
        }

        // Methods to check if enough time has passed for charge gain
        public bool HasTime(PlayerInput playerInput, string key, float seconds)
        {
            var data = GetPlayerState(playerInput);
            if (data == null) return false;
            return data.GetTotalTime(key).TotalSeconds >= seconds;
        }

        // Reset timers after charge gain
        public void ResetTime(PlayerInput playerInput, string key)
        {
            var data = GetPlayerState(playerInput);
            if (data != null)
            {
                data.ResetTimer(key);
            }
        }
    }
}