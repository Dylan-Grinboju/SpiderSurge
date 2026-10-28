using System.IO;
using System.Linq;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge.Vfx
{
    public static class WeaponVfx
    {
        private const string Channel = "surge.weapon.echo";

        public static void Initialize() => Net.Register(Channel, Receive);
        public static void Shutdown() => Net.Unregister(Channel);

        public static void Fold(Weapon weapon, Transform hand, bool retrieve)
        {
            if (weapon == null || hand == null) return;
            SurgeVfx.Safely(() =>
            {
                Vector3 position = weapon.transform.position;
                Vector3 destination = hand.position;
                float angle = weapon.transform.eulerAngles.z;
                Vector3 scale = weapon.transform.lossyScale;
                WeaponEcho.Draw(weapon, position, angle, scale, destination, hand, retrieve);
                if (retrieve) StorageVfx.Retrieve(destination); else StorageVfx.Send(position);
                if (!Net.HasRemotePlayers) return;
                int player = Players.IdOf(weapon.owner);
                Net.SendToAll(Channel, writer =>
                {
                    writer.Write((int)weapon.serializationWeaponName);
                    writer.Write(player);
                    writer.Write(position);
                    writer.Write(destination);
                    writer.Write(scale);
                    writer.Write(angle);
                    writer.Write(retrieve);
                });
            });
        }

        private static void Receive(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient || sender != Net.ServerId) return;
            var kind = (SerializationWeaponName)reader.ReadInt32();
            int player = reader.ReadInt32();
            Vector3 position = reader.ReadVector3();
            Vector3 destination = reader.ReadVector3();
            Vector3 scale = reader.ReadVector3();
            float angle = reader.ReadSingle();
            bool retrieve = reader.ReadBoolean();
            SurgeVfx.Safely(() =>
            {
                var template = Resources.FindObjectsOfTypeAll<Weapon>().FirstOrDefault(weapon => weapon.serializationWeaponName == kind && !weapon.gameObject.scene.IsValid())
                    ?? Resources.FindObjectsOfTypeAll<Weapon>().FirstOrDefault(weapon => weapon.serializationWeaponName == kind);
                var hand = Players.HealthOf(player)?.GetComponentInChildren<SpiderWeaponManager>();
                if (template != null) WeaponEcho.Draw(template, position, angle, scale, destination, hand != null ? hand.transform : null, retrieve);
                if (retrieve) StorageVfx.Retrieve(destination); else StorageVfx.Send(position);
            });
        }

        public static void Shot(Vector3 position, float angle)
        {
            SurgeVfx.Safely(() =>
            {
                Color color = Consts.Values.Vfx.ClipColor;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                ArcFlash.Draw(position, direction, color, 2.5f, 1.2f, 0.09f);
                AccentVfx.Shards(position, color, 5, 22f, 0.5f, 0.18f);
                SurgeVfx.Flash(position, color, 0.7f, 9f, 0.08f);
            });
        }
    }

}
