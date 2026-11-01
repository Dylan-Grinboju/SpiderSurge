using System.Collections.Generic;
using System.IO;
using SpiderSurge.Common;
using UnityEngine;
using Values = SpiderSurge.Consts.Values.Bayonet;
using Mods = SpiderSurge.Consts.ModifierNames;

namespace SpiderSurge.Weapons
{
    // Runs on every peer: the blade is lit while the Bayonet is held and has durability left, in the holder's color, and shortens
    // against walls and as it wears down like the Particle Blade. The host owns the durability and sends it to clients
    public class BayonetBlade : MonoBehaviour
    {
        private const string Channel = "surge.bayonet";

        public ParticleField field;
        public AudioSource beamSound;
        public AudioSource offSound;
        // The Particle Blade's durability before perks
        public float durability;

        // Messages for a Bayonet a client hasn't spawned yet, or one the host is about to show it again
        private static readonly Dictionary<ulong, (float left, float max, Vector2 size)> _pending = new Dictionary<ulong, (float, float, Vector2)>();

        private Shotgun _gun;
        private Collider2D _fieldCollider;
        private Vector2 _centerOfMass;
        private float _inertia;
        private WeaponManager _holder;
        private Collider2D _ignoredHolder;
        private Vector2 _baseSize;
        private Vector2 _size;
        private float _max;
        private float _left;
        private float _restored = -1f;
        private bool _started;
        private bool _lit;

        public static void Initialize() => Net.RegisterFromHost(Channel, OnState);

        public static void Shutdown()
        {
            Net.Unregister(Channel);
            _pending.Clear();
        }

        // Object ids start again with each host
        public static void Forget() => _pending.Clear();

        private void Start()
        {
            _gun = GetComponent<Shotgun>();
            _fieldCollider = field.GetComponent<Collider2D>();
            _centerOfMass = _gun.rb2D.centerOfMass;
            _inertia = _gun.rb2D.inertia;
            _baseSize = PerkSize();
            _max = PerkDurability(durability);
            _left = _restored >= 0f ? Mathf.Min(_restored, _max) : _max;
            _started = true;
            if (Net.IsRemoteClient && _gun.NetworkObject != null && _pending.TryGetValue(_gun.NetworkObjectId, out var state))
            {
                _pending.Remove(_gun.NetworkObjectId);
                Apply(state.left, state.max, state.size);
            }
            field.SetBaseForce(Values.BladeForce);
            Resize(new Vector2(_baseSize.x, MaxLength));
            field.gameObject.SetActive(false);
            Send();
        }

        // A Bayonet back from the Pocket Dimension is shown to clients again
        private void OnEnable()
        {
            if (_started) Send();
        }

        // Put away into the Pocket Dimension, it lights again for whoever holds it when it comes back
        private void OnDisable() => Douse(false);

        private void Update()
        {
            bool held = _left > 0f && _gun.equipped && _gun.owner != null && _gun.owner.healthSystem != null;
            // Another spider can catch it in the frame it was dropped; the blade then spares its new holder instead
            if (held && _lit && _holder != _gun.owner) Douse(false);
            if (held != _lit)
            {
                if (held) Light(); else Douse(true);
            }
            if (!_lit) return;

            // The shotgun drops its own collider for a moment as it fires; the blade does too, so the volley isn't stopped by it
            SetCollider(_gun.shotgunCollider.enabled);
            FitToWalls();
        }

        // Size Matters and Thick Blades grow it as they grow a Particle Blade
        private static Vector2 PerkSize()
        {
            Vector2 size = Values.BladeSize;
            ModifierManager mods = ModifierManager.instance;
            if (mods == null) return size;
            size.y += size.y * Values.SizeMattersLengthPerLevel * mods.GetModLevel(Mods.SizeMatters);
            int thick = mods.GetModLevel(Mods.ThickBlades);
            if (thick > 0) size.x += Mathf.Clamp(thick * size.x, Values.ThickBladesMinExtraWidth, Values.ThickBladesMaxExtraWidth);
            return size;
        }

        // The ammo perks change it as they change a Particle Blade's (Weapon.StartActions)
        private static float PerkDurability(float baseDurability)
        {
            ModifierManager mods = ModifierManager.instance;
            if (mods == null) return baseDurability;
            int efficiency = mods.GetModLevel(Mods.Efficiency);
            int ammoOverride = mods.GetModLevel(Mods.AmmoOverride);
            float max = ammoOverride > 0 ? ammoOverride * (efficiency + 1) : baseDurability + baseDurability * 0.5f * efficiency;
            if (mods.GetModLevel(Mods.HalvedAmmo) > 0) max /= 2f;
            return max;
        }

        // A worn Particle Blade is shorter: its length follows its durability
        private float MaxLength => _max > 0f ? Mathf.Min(_baseSize.y, _baseSize.y * (_left + 1f) / _max) : _baseSize.y;

        internal static bool KeepsWeapon(Weapon weapon)
        {
            var blade = weapon.GetComponent<BayonetBlade>();
            return blade != null && weapon.ammo <= 0f && (!blade._started || blade._left > 0f);
        }

        internal static bool HasBladeLeft(Weapon weapon)
        {
            var blade = Bayonet.Is(weapon) ? weapon.GetComponent<BayonetBlade>() : null;
            return blade != null && blade._left > 0f;
        }

        // For the Pocket Dimension, which keeps a weapon across maps as its name and ammo; -1 for any other weapon
        internal static float LeftOn(Weapon weapon)
        {
            var blade = Bayonet.Is(weapon) ? weapon.GetComponent<BayonetBlade>() : null;
            return blade == null ? -1f : blade._started ? blade._left : blade._restored;
        }

        internal static void Restore(Weapon weapon, float left)
        {
            var blade = Bayonet.Is(weapon) ? weapon.GetComponent<BayonetBlade>() : null;
            if (blade == null || left < 0f) return;
            blade._restored = left;
            if (blade._started) blade.SetLeft(Mathf.Min(left, blade._max));
        }

        // Overcharge's salvage keeps the same share of a Particle Blade's durability
        internal static void Salvage(Weapon weapon, float fraction)
        {
            var blade = Bayonet.Is(weapon) ? weapon.GetComponent<BayonetBlade>() : null;
            if (blade != null && blade._started) blade.SetLeft(Mathf.Min(blade._left, blade._max * fraction));
        }

        internal void Wear()
        {
            // Overcharge spends no ammo, and no blade
            if (!_started || _left <= 0f || AmmoAbility.AmmoFloorFor(_gun) >= 0f) return;
            SetLeft(_left - 1f);
        }

        private void SetLeft(float left)
        {
            _left = Mathf.Max(0f, left);
            Send();
        }

        private void Send()
        {
            if (!Net.HasRemotePlayers || _gun == null || _gun.NetworkObject == null || !_gun.NetworkObject.IsSpawned) return;
            ulong id = _gun.NetworkObjectId;
            Net.SendToAll(Channel, writer => Write(writer, id));
        }

        private void SendTo(ulong clientId)
        {
            if (!_started || _gun.NetworkObject == null || !_gun.NetworkObject.IsSpawned) return;
            ulong id = _gun.NetworkObjectId;
            Net.SendTo(clientId, Channel, writer => Write(writer, id));
        }

        public static void SendAllTo(ulong clientId)
        {
            foreach (BayonetBlade blade in FindObjectsOfType<BayonetBlade>()) blade.SendTo(clientId);
        }

        private void Write(BinaryWriter writer, ulong id)
        {
            writer.Write(id);
            writer.Write(_left);
            writer.Write(_max);
            writer.Write(_baseSize.x);
            writer.Write(_baseSize.y);
        }

        private static void OnState(BinaryReader reader)
        {
            ulong id = reader.ReadUInt64();
            float left = reader.ReadSingle();
            float max = reader.ReadSingle();
            var size = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            if (Net.Manager.SpawnManager.SpawnedObjects.TryGetValue(id, out var target) && target != null)
            {
                var blade = target.GetComponent<BayonetBlade>();
                if (blade != null && blade._started)
                {
                    blade.Apply(left, max, size);
                    return;
                }
            }
            _pending[id] = (left, max, size);
        }

        private void Apply(float left, float max, Vector2 size)
        {
            _left = left;
            _max = max;
            _baseSize = size;
        }

        private void Light()
        {
            _lit = true;
            _holder = _gun.owner;
            _ignoredHolder = _gun.owner.healthSystem.GetComponent<Collider2D>();
            if (_ignoredHolder != null) Physics2D.IgnoreCollision(_fieldCollider, _ignoredHolder, true);
            field.ChangeColor(BladeColor(_gun.owner));
            field.gameObject.SetActive(true);
            SetCollider(true);
            beamSound.Play();
        }

        private void Douse(bool sound)
        {
            if (!_lit) return;
            _lit = false;
            if (_ignoredHolder != null && _fieldCollider != null) Physics2D.IgnoreCollision(_fieldCollider, _ignoredHolder, false);
            _ignoredHolder = null;
            _holder = null;
            if (_fieldCollider != null) SetCollider(false);
            if (field != null) field.gameObject.SetActive(false);
            if (beamSound != null) beamSound.Pause();
            if (sound && offSound != null) offSound.Play();
        }

        private void SetCollider(bool enabled)
        {
            if (_fieldCollider.enabled == enabled) return;
            _fieldCollider.enabled = enabled;
            KeepBalance();
        }

        // A collider change makes Unity recompute the body's mass spread from its colliders; the blade would pull the balance up the barrel
        private void KeepBalance()
        {
            _gun.rb2D.centerOfMass = _centerOfMass;
            _gun.rb2D.inertia = _inertia;
        }

        // The Particle Blade's own coloring: the spider's primary color, brightened
        private static Color BladeColor(WeaponManager owner)
        {
            var customizer = owner.GetComponentInParent<SpiderCustomizer>();
            if (customizer == null) return owner.healthSystem.bloodColor;
            Color.RGBToHSV(customizer.GetPrimaryColor(), out float h, out float s, out _);
            return Color.HSVToRGB(h, s * 2f, 1f, false);
        }

        private void FitToWalls()
        {
            Transform origin = field.transform;
            float maxLength = MaxLength;
            RaycastHit2D hit = Physics2D.Raycast(origin.position, origin.up, maxLength * origin.lossyScale.y, GameController.instance.worldLayers);
            float length = hit.collider != null ? Mathf.Clamp(hit.distance / origin.lossyScale.y, 1f, maxLength) : maxLength;
            Resize(new Vector2(_baseSize.x, length));
        }

        private void Resize(Vector2 size)
        {
            if (_size != Vector2.zero && Vector2.Distance(size, _size) <= 0.5f) return;
            field.Resize(size);
            _size = size;
            KeepBalance();
        }
    }
}
