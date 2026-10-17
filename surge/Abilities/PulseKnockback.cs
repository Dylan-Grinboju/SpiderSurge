using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SpiderSurge
{
    // Launches enemies hit by a pulse at a set speed, lifting the game's enemy speed cap and stunning their brain for a moment
    public static class PulseKnockback
    {
        private const float ContinuousCollisionMargin = 0.1f;

        private class Launch
        {
            public float UncappedUntil;
            public CollisionDetectionMode2D OriginalMode;
        }

        private static readonly Dictionary<Rigidbody2D, Launch> launches = new Dictionary<Rigidbody2D, Launch>();
        private static readonly Dictionary<EnemyBrain, float> pulseStuns = new Dictionary<EnemyBrain, float>();
        private static readonly Dictionary<Type, FieldInfo> stunFields = new Dictionary<Type, FieldInfo>();
        private static readonly FieldInfo immuneTimeField = AccessTools.Field(typeof(EnemyHealthSystem), "_immuneTime");

        public static bool CanLaunch(EnemyHealthSystem enemy)
        {
            if (enemy == null || enemy.dead || BodyOf(enemy) == null)
            {
                return false;
            }
            return immuneTimeField == null || Time.time >= (float)immuneTimeField.GetValue(enemy);
        }

        public static void LaunchEnemy(EnemyHealthSystem enemy, Vector2 origin, float speed)
        {
            Rigidbody2D body = BodyOf(enemy);
            Vector2 direction = body.position - origin;
            Vector2 velocity = direction.sqrMagnitude > 0.0001f ? direction.normalized * speed : Vector2.up * speed;

            // Stingers, blades and roller struts hang on joints and would drag the body back, so every part moves together
            foreach (Rigidbody2D part in enemy.GetComponentsInChildren<Rigidbody2D>())
            {
                part.velocity = velocity;
            }
            Stun(enemy, Consts.Values.Pulse.EnemyStunDuration);

            ForgetDestroyed();
            float uncappedUntil = Time.time + Consts.Values.Pulse.EnemyUncappedDuration;
            if (launches.TryGetValue(body, out Launch launch))
            {
                launch.UncappedUntil = uncappedUntil;
                return;
            }

            launch = new Launch { UncappedUntil = uncappedUntil, OriginalMode = body.collisionDetectionMode };
            launches[body] = launch;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemy.StartCoroutine(EndLaunch(body, launch));
        }

        public static bool IsUncapped(Rigidbody2D body)
        {
            return body != null && launches.TryGetValue(body, out Launch launch) && Time.time < launch.UncappedUntil;
        }

        public static bool IsPulseStunned(EnemyBrain brain)
        {
            if (!pulseStuns.TryGetValue(brain, out float until))
            {
                return false;
            }
            if (Time.time < until)
            {
                return true;
            }
            pulseStuns.Remove(brain);
            return false;
        }

        // Continuous collision stays on until the cap has slowed the enemy back down, so a fast enemy doesn't pass through thin platforms
        private static IEnumerator EndLaunch(Rigidbody2D body, Launch launch)
        {
            WaitForFixedUpdate tick = new WaitForFixedUpdate();
            while (body != null && Time.time < launch.UncappedUntil + ContinuousCollisionMargin)
            {
                yield return tick;
            }
            if (body != null)
            {
                body.collisionDetectionMode = launch.OriginalMode;
            }
            launches.Remove(body);
        }

        private static void ForgetDestroyed()
        {
            foreach (Rigidbody2D body in launches.Keys.Where(body => body == null).ToList())
            {
                launches.Remove(body);
            }
            foreach (EnemyBrain brain in pulseStuns.Keys.Where(brain => brain == null).ToList())
            {
                pulseStuns.Remove(brain);
            }
        }

        private static Rigidbody2D BodyOf(EnemyHealthSystem enemy)
        {
            return enemy.rb2D != null ? enemy.rb2D : enemy.GetComponent<Rigidbody2D>();
        }

        // Brains skip movement and obstacle braking until _stunTill; WhispBrain has the field but the game never sets it
        private static void Stun(EnemyHealthSystem enemy, float duration)
        {
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            if (brain == null)
            {
                return;
            }

            Type type = brain.GetType();
            if (!stunFields.TryGetValue(type, out FieldInfo field))
            {
                field = AccessTools.Field(type, "_stunTill");
                if (field != null && field.FieldType != typeof(float))
                {
                    field = null;
                }
                stunFields[type] = field;
            }

            float until = Time.time + duration;
            pulseStuns[brain] = until;
            if (field != null && (float)field.GetValue(brain) < until)
            {
                field.SetValue(brain, until);
            }
        }
    }
}
