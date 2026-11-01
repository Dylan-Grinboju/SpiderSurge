using UnityEngine;
using Values = SpiderSurge.Consts.Values.Bayonet;

namespace SpiderSurge.Weapons
{
    // Runs on every peer: the blade is lit while the Bayonet is held, in the holder's color, and shortens against walls like the Particle Blade
    public class BayonetBlade : MonoBehaviour
    {
        public ParticleField field;
        public AudioSource beamSound;
        public AudioSource offSound;

        private Shotgun _gun;
        private Collider2D _fieldCollider;
        private Vector2 _centerOfMass;
        private float _inertia;
        private Collider2D _ignoredHolder;
        private Vector2 _size;
        private bool _lit;

        private void Start()
        {
            _gun = GetComponent<Shotgun>();
            _fieldCollider = field.GetComponent<Collider2D>();
            _centerOfMass = _gun.rb2D.centerOfMass;
            _inertia = _gun.rb2D.inertia;
            field.SetBaseForce(Values.BladeForce);
            Resize(Values.BladeSize);
            field.gameObject.SetActive(false);
        }

        private void Update()
        {
            bool held = _gun.equipped && _gun.owner != null && _gun.owner.healthSystem != null;
            if (held != _lit)
            {
                if (held) Light(); else Douse(true);
            }
            if (!_lit) return;

            // The shotgun drops its own collider for a moment as it fires; the blade does too, so the volley isn't stopped by it
            SetCollider(_gun.shotgunCollider.enabled);
            FitToWalls();
        }

        private void Light()
        {
            _lit = true;
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
            if (_fieldCollider != null) SetCollider(false);
            if (field != null) field.gameObject.SetActive(false);
            if (beamSound != null) beamSound.Pause();
            if (sound && offSound != null) offSound.Play();
        }

        // A collider change makes Unity recompute the body's mass spread from its colliders; the blade would pull the balance up the barrel
        private void SetCollider(bool enabled)
        {
            if (_fieldCollider.enabled == enabled) return;
            _fieldCollider.enabled = enabled;
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
            RaycastHit2D hit = Physics2D.Raycast(origin.position, origin.up, Values.BladeSize.y * origin.lossyScale.y, GameController.instance.worldLayers);
            float length = hit.collider != null ? Mathf.Clamp(hit.distance / origin.lossyScale.y, 1f, Values.BladeSize.y) : Values.BladeSize.y;
            Resize(new Vector2(Values.BladeSize.x, length));
        }

        private void Resize(Vector2 size)
        {
            if (_size != Vector2.zero && Vector2.Distance(size, _size) <= 0.5f) return;
            field.Resize(size);
            _size = size;
        }
    }
}
