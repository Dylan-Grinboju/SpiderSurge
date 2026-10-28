using System;
using System.IO;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // Hands each embedded file with the extension to load, named after its last dot; a file that can't be read or loaded is logged and skipped
    public static class EmbeddedResources
    {
        public static void Load(string extension, string label, string kind, string decoder, Action<string, byte[]> load)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] resourceNames = assembly.GetManifestResourceNames();

            foreach (string resourceName in resourceNames)
            {
                if (resourceName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    string name = NameOf(resourceName, extension);

                    try
                    {
                        byte[] data = Read(assembly, resourceName, label, decoder);
                        if (data != null)
                        {
                            load(name, data);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"[{label}] Failed to load embedded {kind} '{resourceName}': {ex.Message}");
                    }
                }
            }
        }

        private static string NameOf(string resourceName, string extension)
        {
            string withoutExtension = resourceName.Substring(0, resourceName.Length - extension.Length);
            int lastDot = withoutExtension.LastIndexOf('.');
            if (lastDot >= 0)
            {
                return withoutExtension.Substring(lastDot + 1);
            }
            return withoutExtension;
        }

        private static byte[] Read(Assembly assembly, string resourceName, string label, string decoder)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Logger.LogWarning($"[{label}] Could not load resource stream: {resourceName}");
                    return null;
                }

                long length = stream.Length;
                if (length > int.MaxValue)
                {
                    Logger.LogWarning($"[{label}] Resource {resourceName} is too large.");
                    return null;
                }

                byte[] data = new byte[(int)length];
                int totalRead = 0;
                while (totalRead < data.Length)
                {
                    int bytesRead = stream.Read(data, totalRead, data.Length - totalRead);
                    if (bytesRead == 0)
                    {
                        Logger.LogWarning($"[{label}] Unexpected EOF while reading resource: {resourceName}. Expected {data.Length} bytes, got {totalRead}.");
                        break;
                    }
                    totalRead += bytesRead;
                }

                if (totalRead != data.Length)
                {
                    Logger.LogWarning($"[{label}] Incomplete read for resource: {resourceName}. Skipping {decoder}.");
                    return null;
                }
                return data;
            }
        }
    }
}
