using System.Collections.Generic;
using Interfaces;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge.Enemies
{
    // Lives on a laser enemy's shield object, so breaking the shield (or killing the enemy) disables the ring
    public class LaserRing : MonoBehaviour
    {
        private enum Phase { Idle, Warning, Active }

        [SerializeField] private EnemyHealthSystem health;
        [SerializeField] private LineRenderer line;
        [SerializeField] private Material laserMaterial;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private AudioSource chargeSound;
        [SerializeField] private AudioSource beamSound;
        [SerializeField] private AudioSource beamEndSound;
        [SerializeField] private AudioSource killSound;
        [SerializeField] private float chargeSoundStart;

        private bool hintQueued;
        private static Collider2D[] hits = new Collider2D[32];
        private readonly HashSet<Rigidbody2D> hitThisPulse = new HashSet<Rigidbody2D>();
        private Vector3[] points;
        private Phase phase;
        private float startTime;
        private int playerMask;
        private int damageMask;
        private bool synced;
        private bool held;
        private bool remoteHold;

        internal bool IsFiring => isActiveAndEnabled && phase == Phase.Active && line != null && line.enabled;

        public void Initialize(EnemyHealthSystem enemyHealth, LaserCannon laserCannon, DeathRay deathRay)
        {
            health = enemyHealth;
            laserMaterial = laserCannon.laserLine.sharedMaterial;
            warningMaterial = deathRay.lasers.Length > 0 ? deathRay.lasers[0].sharedMaterial : laserMaterial;

            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Consts.Values.LaserRing.Segments;
            line.numCornerVertices = 2;
            line.sortingLayerID = laserCannon.laserLine.sortingLayerID;
            line.sortingOrder = laserCannon.laserLine.sortingOrder;
            line.sharedMaterial = laserMaterial;
            line.enabled = false;

            chargeSound = CopySound(deathRay.chargeSound, "LaserRingCharge");
            beamSound = CopySound(laserCannon.beamSound, "LaserRingBeam");
            beamEndSound = CopySound(laserCannon.beamEnd, "LaserRingBeamEnd");
            killSound = CopySound(laserCannon.killSound, "LaserRingKill");

            // The Death Ray's charge ends when it fires, so the warning plays the last second of that charge
            if (chargeSound != null && chargeSound.clip != null)
            {
                float warning = Consts.Values.LaserRing.WarningDuration;
                chargeSoundStart = Mathf.Clamp(deathRay.chargeTime - warning, 0f, Mathf.Max(0f, chargeSound.clip.length - warning));
            }
        }

        private AudioSource CopySound(AudioSource source, string name)
        {
            if (source == null) return null;
            GameObject copy = Instantiate(source.gameObject, transform, false);
            copy.name = name;
            copy.transform.localPosition = Vector3.zero;
            AudioSource audio = copy.GetComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.Stop();
            return audio;
        }

        private void OnEnable()
        {
            startTime = Time.time;
            phase = Phase.Idle;
            synced = Net.IsAuthority;
            held = false;
            remoteHold = false;
            playerMask = LayerMask.GetMask("Player");
            damageMask = playerMask | LayerMask.GetMask("Item", "Weapon");
            if (points == null) points = new Vector3[Consts.Values.LaserRing.Segments];
            if (line != null) line.enabled = false;
        }

        private void OnDisable()
        {
            GoOut();
        }

        private void GoOut()
        {
            if (line != null) line.enabled = false;
            if (chargeSound != null) chargeSound.Stop();
            if (beamSound != null) beamSound.Stop();
            phase = Phase.Idle;
        }

        private void FixedUpdate()
        {
            if (health == null) return;
            if (health.IsDead())
            {
                if (phase != Phase.Idle) GoOut();
                return;
            }
            if (!hintQueued) hintQueued = EncounterHints.Encounter("laser-shield", "Break the red shield to stop this enemy's laser ring.", health);
            if (!synced) return;

            // Freeze Time and the Black Hole make the enemy harmless, so the ring goes out and its cycle waits until the hold ends; a cut warning or beam resumes from the warning
            if (remoteHold || FrozenEnemy.IsHeld(health) || SwallowedTarget.IsSwallowed(health.gameObject))
            {
                // Clients don't see the Black Hole's hold, so the host tells them when a hold starts; its next warning sync ends it
                if (!held) SurgeNet.SendEnemyFx(EnemyFx.LaserHold, health.GetComponent<NetworkObject>(), 0f);
                held = true;
                startTime += Time.fixedDeltaTime;
                if (phase != Phase.Idle)
                {
                    if (phase == Phase.Active) StopBeam();
                    else line.enabled = false;
                    if (chargeSound != null) chargeSound.Stop();
                    phase = Phase.Idle;
                    startTime = Time.time - WarningStart();
                }
                return;
            }
            held = false;

            Phase next = CurrentPhase();
            if (next != phase)
            {
                phase = next;
                if (phase == Phase.Warning)
                {
                    SurgeNet.SendEnemyFx(EnemyFx.LaserPhase, health.GetComponent<NetworkObject>(), (Time.time - startTime) % Consts.Values.LaserRing.Period);
                    StartWarning();
                }
                else if (phase == Phase.Active) StartBeam();
                else StopBeam();
            }

            if (phase == Phase.Active && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                DamageTargets();
            }
        }

        // A client starts its cycle from the host's each time the warning starts, so the beam it draws is the one that hurts
        public void Sync(float timeIntoPeriod)
        {
            startTime = Time.time - timeIntoPeriod;
            synced = true;
            remoteHold = false;
        }

        public void Hold()
        {
            remoteHold = true;
        }

        private Phase CurrentPhase()
        {
            float period = Consts.Values.LaserRing.Period;
            float t = (Time.time - startTime) % period;
            float activeStart = period - Consts.Values.LaserRing.ActiveDuration;
            if (t >= activeStart) return Phase.Active;
            if (t >= WarningStart()) return Phase.Warning;
            return Phase.Idle;
        }

        private static float WarningStart() => Consts.Values.LaserRing.Period - Consts.Values.LaserRing.ActiveDuration - Consts.Values.LaserRing.WarningDuration;

        private void StartWarning()
        {
            line.sharedMaterial = warningMaterial;
            line.enabled = true;
            if (chargeSound != null)
            {
                chargeSound.pitch = Random.Range(0.9f, 1.1f);
                chargeSound.Play();
                chargeSound.time = chargeSoundStart;
            }
        }

        private void StartBeam()
        {
            hitThisPulse.Clear();
            if (chargeSound != null) chargeSound.Stop();
            line.sharedMaterial = laserMaterial;
            line.enabled = true;
            float width = Consts.Values.LaserRing.Width;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = Consts.Values.LaserRing.Color;
            line.endColor = Consts.Values.LaserRing.Color;
            if (beamSound != null) beamSound.Play();
        }

        private void StopBeam()
        {
            line.enabled = false;
            if (beamSound != null) beamSound.Stop();
            if (beamEndSound != null) beamEndSound.Play();
        }

        // Hits spiders and grenades touching the active band through the Laser Cannon's damage path.
        private void DamageTargets()
        {
            Vector2 center = health.rb2D.position;
            float radius = Consts.Values.LaserRing.Radius;
            float halfWidth = Consts.Values.LaserRing.Width / 2f;
            int count;
            while ((count = Physics2D.OverlapCircleNonAlloc(center, radius + halfWidth, hits, damageMask)) == hits.Length)
            {
                System.Array.Resize(ref hits, hits.Length * 2);
            }
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                Bounds bounds = hit.bounds;
                Vector2 offset = (Vector2)bounds.center - center;
                float bodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
                if (offset.magnitude + bodyRadius < radius - halfWidth) continue;

                IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.attachedRigidbody?.GetComponent<IDamageable>();
                if (damageable == null || (damageable is not Grenade && (playerMask & (1 << hit.gameObject.layer)) == 0)) continue;

                Vector2 direction = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.up;
                damageable.Damage(direction * Consts.Values.LaserRing.Power, hit.ClosestPoint(center), stun: true);

                if (killSound != null && hit.CompareTag("PlayerRigidbody") && hitThisPulse.Add(hit.attachedRigidbody))
                {
                    killSound.pitch = Random.Range(0.8f, 1.2f);
                    killSound.Play();
                }
            }
        }

        private void LateUpdate()
        {
            if (line == null || !line.enabled || health == null || health.rb2D == null) return;

            if (phase == Phase.Warning)
            {
                float warningStart = WarningStart();
                float progress = Mathf.Clamp01(((Time.time - startTime) % Consts.Values.LaserRing.Period - warningStart) / Consts.Values.LaserRing.WarningDuration);
                Color color = Consts.Values.LaserRing.Color;
                color.a = Mathf.Lerp(Consts.Values.LaserRing.WarningMinAlpha, Consts.Values.LaserRing.WarningMaxAlpha, progress);
                line.startColor = color;
                line.endColor = color;
                line.startWidth = Consts.Values.LaserRing.WarningWidth;
                line.endWidth = Consts.Values.LaserRing.WarningWidth;
            }

            Vector2 center = health.rb2D.position;
            float radius = Consts.Values.LaserRing.Radius;
            float step = 2f * Mathf.PI / points.Length;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3(center.x + Mathf.Cos(i * step) * radius, center.y + Mathf.Sin(i * step) * radius, 0f);
            }
            line.SetPositions(points);
        }
    }
}
