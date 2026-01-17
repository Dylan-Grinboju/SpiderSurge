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

        public class PlayerStateData
        {
            public PlayerInput PlayerInput { get; set; }
            public SpiderController SpiderController { get; set; }
            public Stabilizer Stabilizer { get; set; }
            public Rigidbody2D Rigidbody { get; set; }

            private Dictionary<string, TimeSpan> totalTimes = new Dictionary<string, TimeSpan>();
            private Dictionary<string, DateTime?> startTimes = new Dictionary<string, DateTime?>();

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
                return Rigidbody != null && Rigidbody.velocity.magnitude < 0.1f;
            }

            public void ResetTimer(string key)
            {
                totalTimes[key] = TimeSpan.Zero;
                startTimes[key] = null;
            }

            public bool IsAirborne()
            {
                return Stabilizer != null && !Stabilizer.grounded;
            }

            public void Reset()
            {
                totalTimes.Clear();
                startTimes.Clear();
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
                    data.StopTimer("airborne");
                }

                // Stillness tracking
                if (data.IsStill())
                {
                    data.StartTimer("stillness");
                }
                else
                {
                    data.StopTimer("stillness");
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