using UnityEngine;
using System.Linq;

namespace SpiderSurge.Enemies
{
    public static class CustomEnemies
    {
        public static GameObject TwinBladeMeleeWhispPrefab;
        public static GameObject TwinBladePowerMeleeWhispPrefab;

        public static void CreateTwinBladeMeleeWhisp(GameObject original)
        {
            if (TwinBladeMeleeWhispPrefab != null) return;

            GameObject newEnemyObj = UnityEngine.Object.Instantiate(original);
            newEnemyObj.name = "TwinBladeMeleeWhisp";
            UnityEngine.Object.DontDestroyOnLoad(newEnemyObj);
            newEnemyObj.SetActive(false);
            TwinBladeMeleeWhispPrefab = newEnemyObj;

            var brain = newEnemyObj.GetComponent<WhispBrain>();
            Transform meleeWeaponTr = null;
            Transform[] allChildren = newEnemyObj.GetComponentsInChildren<Transform>(true);
            meleeWeaponTr = System.Linq.Enumerable.FirstOrDefault(allChildren, t => t.name == "MeleeWeapon");

            if (meleeWeaponTr == null && brain != null) meleeWeaponTr = brain.rotatingBase;

            if (meleeWeaponTr != null)
            {
                Transform bladehandle = null;
                foreach (Transform child in meleeWeaponTr)
                {
                    if (child.name.ToLower().Contains("handle") || child.name.ToLower().Contains("blade"))
                    {
                        bladehandle = child;
                        break;
                    }
                }

                if (bladehandle != null)
                {
                    Transform blade2 = UnityEngine.Object.Instantiate(bladehandle, meleeWeaponTr);
                    blade2.name = "SecondBladeHandle";
                    blade2.localPosition = -bladehandle.localPosition;
                    blade2.localRotation = bladehandle.localRotation * Quaternion.Euler(0, 0, 180);
                }
            }

            // Register for Cheats
            RegisterEnemyForCheats(newEnemyObj);
        }

        public static void CreateTwinBladePowerMeleeWhisp(GameObject original)
        {
            if (TwinBladePowerMeleeWhispPrefab != null) return;

            GameObject newEnemyObj = UnityEngine.Object.Instantiate(original);
            newEnemyObj.name = "TwinBladePowerMeleeWhisp";
            UnityEngine.Object.DontDestroyOnLoad(newEnemyObj);
            newEnemyObj.SetActive(false);
            TwinBladePowerMeleeWhispPrefab = newEnemyObj;

            var brain = newEnemyObj.GetComponent<WhispBrain>();
            Transform meleeWeaponTr = null;
            Transform[] allChildren = newEnemyObj.GetComponentsInChildren<Transform>(true);
            meleeWeaponTr = System.Linq.Enumerable.FirstOrDefault(allChildren, t => t.name == "MeleeWeapon");

            if (meleeWeaponTr == null && brain != null) meleeWeaponTr = brain.rotatingBase;

            if (meleeWeaponTr != null)
            {
                Transform bladehandle = null;
                foreach (Transform child in meleeWeaponTr)
                {
                    if (child.name.ToLower().Contains("handle") || child.name.ToLower().Contains("blade"))
                    {
                        bladehandle = child;
                        break;
                    }
                }

                if (bladehandle != null)
                {
                    Transform blade2 = UnityEngine.Object.Instantiate(bladehandle, meleeWeaponTr);
                    blade2.name = "SecondBladeHandle";
                    blade2.localPosition = -bladehandle.localPosition;
                    blade2.localRotation = bladehandle.localRotation * Quaternion.Euler(0, 0, 180);
                }
            }

            // Register for Cheats
            RegisterEnemyForCheats(newEnemyObj);
        }

        private static void RegisterEnemyForCheats(GameObject enemyObj)
        {
            if (CustomTiersScreen.instance != null && CustomTiersScreen.instance.allElements != null)
            {
                var healthSystem = enemyObj.GetComponent<EnemyHealthSystem>();
                if (healthSystem != null)
                {
                    bool alreadyExists = false;
                    foreach (var enemy in CustomTiersScreen.instance.allElements.allEnemies)
                    {
                        if (enemy.name == enemyObj.name)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        CustomTiersScreen.instance.allElements.allEnemies.Add(healthSystem);
                        Debug.Log($"[SpiderSurge] Registered {enemyObj.name} for cheats.");
                    }
                }
            }
        }
    }
}
