using System.IO;
using UnityEngine;

namespace SpiderSurge.Common
{
    public static class NetIO
    {
        public static void Write(this BinaryWriter writer, Color color)
        {
            writer.Write(color.r);
            writer.Write(color.g);
            writer.Write(color.b);
            writer.Write(color.a);
        }

        public static Color ReadColor(this BinaryReader reader)
        {
            return new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }


        public static void Write(this BinaryWriter writer, Vector3 vector)
        {
            writer.Write(vector.x);
            writer.Write(vector.y);
            writer.Write(vector.z);
        }

        public static Vector3 ReadVector3(this BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        public static void WriteText(this BinaryWriter writer, string text)
        {
            writer.Write(text ?? string.Empty);
        }
    }
}
