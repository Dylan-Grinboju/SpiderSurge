using System;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;

namespace StatsMod
{
    internal static class AccuracyTracker
    {
        private sealed class Shot
        {
            public PlayerInput Player;
            public int PlayerId;
            public WeaponAccuracy Weapon;
            public WeaponAccuracy Total;
            public bool Hit;
        }

        private static ConditionalWeakTable<GameObject, Shot> shots = new ConditionalWeakTable<GameObject, Shot>();

        public static void Reset() => shots = new ConditionalWeakTable<GameObject, Shot>();

        public static void Fire(Weapon weapon, GameObject source)
        {
            try
            {
                if (weapon == null || source == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || !GameSessionManager.Instance.IsActive) return;
                PlayerInput player = HitLogic.WeaponPlayer(weapon);
                if (player == null || !PlayerTracker.Instance.TryGetPlayer(player, out var data)) return;

                string name = weapon.serializationWeaponName.ToString();
                if (!data.AccuracyByWeapon.TryGetValue(name, out WeaponAccuracy accuracy))
                {
                    accuracy = new WeaponAccuracy();
                    data.AccuracyByWeapon.Add(name, accuracy);
                }
                accuracy.Fire();
                data.ShootingAccuracy.Fire();
                shots.Remove(source);
                PlayerController controller = player.GetComponentInChildren<PlayerController>();
                shots.Add(source, new Shot { Player = player, PlayerId = controller != null ? controller.playerID.Value : -1, Weapon = accuracy, Total = data.ShootingAccuracy });
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

        public static void Hit(object source, Component target)
        {
            if (!GameSessionManager.Instance.IsActive || !(source is Component component)) return;
            Shot shot = Find(component);
            if (shot == null || shot.Hit || shot.Player == null) return;

            if (target is EnemyHealthSystem enemy)
            {
                if (enemy.serializationEnemyName == SerializationEnemyName.FriendlyWasp) return;
            }
            else if (target is SpiderHealthSystem spider)
            {
                PlayerInput victim = spider.rootObject?.GetComponentInParent<PlayerInput>();
                if (GameSessionManager.Instance.CurrentGameMode != GameMode.Versus || victim == null || victim == shot.Player) return;
                if (SpiderTeams.TeamManager.TeamMatchActive)
                {
                    var manager = SpiderTeams.TeamManager.Instance;
                    if (manager.TryGetTeamOfPlayerId(shot.PlayerId, out int attackerTeam) &&
                        attackerTeam == manager.GetTeam(spider.GetComponentInParent<PlayerController>())) return;
                }
            }
            else return;

            shot.Hit = true;
            shot.Weapon.Hit();
            shot.Total.Hit();
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
