using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Logger = Silk.Logger;

namespace StatsMod
{
    internal static class AccuracyTracker
    {
        private sealed class Shot
        {
            public PlayerController Player;
            public WeaponAccuracy Weapon;
            public WeaponAccuracy Total;
            public bool Hit;
        }

        private static ConditionalWeakTable<GameObject, Shot> shots = new ConditionalWeakTable<GameObject, Shot>();

        public static void Reset() => shots = new ConditionalWeakTable<GameObject, Shot>();

        // Mono's Enum.ToString uses reflection and allocates, and it would run for every shot
        private static readonly Dictionary<SerializationWeaponName, string> weaponNames = new Dictionary<SerializationWeaponName, string>();

        private static string WeaponName(SerializationWeaponName weapon)
        {
            if (!weaponNames.TryGetValue(weapon, out string name))
            {
                name = weapon.ToString();
                weaponNames[weapon] = name;
            }
            return name;
        }

        public static void Fire(Weapon weapon, GameObject source)
        {
            try
            {
                if (weapon == null || source == null || !GameSessionManager.IsRecording) return;
                PlayerController player = HitLogic.WeaponPlayer(weapon);
                if (player == null || !PlayerTracker.Instance.TryGetPlayer(player, out var data)) return;

                string name = WeaponName(weapon.serializationWeaponName);
                if (!data.AccuracyByWeapon.TryGetValue(name, out WeaponAccuracy accuracy))
                {
                    accuracy = new WeaponAccuracy();
                    data.AccuracyByWeapon.Add(name, accuracy);
                }
                accuracy.Fire();
                data.ShootingAccuracy.Fire();
                shots.Remove(source);
                shots.Add(source, new Shot { Player = player, Weapon = accuracy, Total = data.ShootingAccuracy });
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking shot: {ex.Message}");
            }
        }

        public static GameObject Spawned(GameObject projectile, Weapon launcher)
        {
            Fire(launcher, projectile);
            return projectile;
        }

        public static GameObject EffectSpawned(GameObject effect, Component source)
        {
            Transfer(source, effect.transform);
            return effect;
        }

        public static void Transfer(Component source, Component destination)
        {
            if (source == null || destination == null) return;
            Shot shot = Find(source);
            if (shot == null) return;
            shots.Remove(destination.gameObject);
            shots.Add(destination.gameObject, shot);
        }

        public static void Forget(Component projectile)
        {
            if (projectile != null) shots.Remove(projectile.gameObject);
        }

        public static void EnemyHit(object source) => Hit(source, null);

        public static void PlayerHit(object source, PlayerController victim)
        {
            if (victim != null) Hit(source, victim);
        }

        // Outside versus every player counts as a teammate, so only enemies and opponents make a shot hit.
        private static void Hit(object source, PlayerController victim)
        {
            try
            {
                if (!(source is Component component) || component == null) return;
                Shot shot = Find(component);
                if (shot == null || shot.Hit || shot.Player == null) return;
                if (victim != null && (victim == shot.Player || PlayerRelations.AreTeammates(shot.Player, victim))) return;

                shot.Hit = true;
                shot.Weapon.Hit();
                shot.Total.Hit();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking hit: {ex.Message}");
            }
        }

        private static Shot Find(Component source)
        {
            for (Transform node = source.transform; node != null; node = node.parent)
            {
                if (shots.TryGetValue(node.gameObject, out Shot shot)) return shot;
            }
            return null;
        }
    }
}
