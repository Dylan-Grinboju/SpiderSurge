using UnityEngine;
using Silk;
using Logger = Silk.Logger;
using System.Collections.Generic;
using HarmonyLib;
using System;
using Interfaces;
using UnityEngine.InputSystem;


namespace StatsMod
{

    public class EnemiesTracker
    {
        private static EnemiesTracker _instance;
        public static EnemiesTracker Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EnemiesTracker();
                    Logger.LogInfo("Enemies tracker created via singleton access");
                }
                return _instance;
            }
            private set => _instance = value;
        }

        public int EnemiesKilled { get; private set; }

        public EnemiesTracker()
        {
            EnemiesKilled = 0;

            Instance = this;
            Logger.LogInfo("Enemies tracker initialized");
        }

        public void IncrementEnemiesKilled()
        {
            EnemiesKilled++;
        }

        public void ResetEnemiesKilled()
        {
            EnemiesKilled = 0;
        }

        public int GetEnemiesKilled()
        {
            return EnemiesKilled;
        }

    }

}
