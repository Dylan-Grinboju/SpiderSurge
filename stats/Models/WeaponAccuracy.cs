using System.Globalization;

namespace StatsMod
{
    public sealed class WeaponAccuracy
    {
        internal static readonly (string Key, string Label)[] Weapons =
        {
            ("Shotgun", "Shotgun"),
            ("MiniShotgun", "Baby Shotgun"),
            ("AutoShotgun", "Auto Shotgun"),
            ("Railvolver", "Railvolver"),
            ("DoubleRailvolver", "Double Railvolver"),
            ("LaserCannon", "Laser Cannon"),
            ("DeathRay", "Death Ray"),
            ("KhepriStaff", "Staff of Khepri"),
            ("HeckSaw", "Hecksaw"),
            ("GravitySaw", "Gravity Saw"),
            ("RocketLauncher", "Rocket Launcher"),
            ("GrenadeLauncher", "Grenade Launcher"),
            ("BurstLauncher", "Burst Launcher"),
            ("FireworkLauncher", "Firework Launcher"),
            ("ParticleBladeLauncher", "Particle Blade Launcher"),
        };

        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }
        public double? Percentage => ShotsFired > 0 ? 100.0 * ShotsHit / ShotsFired : (double?)null;
        public string Display => Percentage?.ToString("0.#", CultureInfo.InvariantCulture) + (Percentage.HasValue ? "%" : "--");

        internal void Fire() => ShotsFired++;
        internal void Hit() => ShotsHit++;
    }
}
