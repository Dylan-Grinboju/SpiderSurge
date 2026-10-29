using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

namespace SpiderSurge.Common
{
    [DataContract]
    internal sealed class UpdateRelease
    {
        [DataMember(Name = "tag_name")] public string Tag { get; set; }
        [DataMember(Name = "body")] public string Notes { get; set; }
        [DataMember(Name = "draft")] public bool Draft { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "assets")] public UpdateAsset[] Assets { get; set; }
        internal string Version => Tag?.TrimStart('v');
        internal string PageUrl => "https://github.com/Dylan-Grinboju/SpiderSurge/releases/tag/" + Uri.EscapeDataString(Tag);

        internal UpdateAsset InstallableAsset()
        {
            if (Draft || Prerelease || !System.Version.TryParse(Version, out var version) || version.Build < 0)
                throw new InvalidDataException("The release is not a stable version.");
            var matches = Assets?.Where(item => item != null && item.Name == "SpiderSurge.dll").ToArray();
            var asset = matches?.Length == 1 ? matches[0] : null;
            if (asset == null || asset.Size <= 0 || asset.Size > UpdatePackage.MaxBytes || asset.State != "uploaded")
                throw new InvalidDataException("This release does not contain an installable SpiderSurge.dll.");
            string expected = "https://github.com/Dylan-Grinboju/SpiderSurge/releases/download/" + Uri.EscapeDataString(Tag) + "/SpiderSurge.dll";
            if (asset.Url != expected || asset.Digest == null || !asset.Digest.StartsWith("sha256:") || !UpdatePackage.IsHash(asset.Digest.Substring(7)))
                throw new InvalidDataException("This release has no verified download. Use its release page to update manually.");
            return asset;
        }
    }

    [DataContract]
    internal sealed class UpdateAsset
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "browser_download_url")] public string Url { get; set; }
        [DataMember(Name = "digest")] public string Digest { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "state")] public string State { get; set; }
    }

    [DataContract]
    internal sealed class UpdateRequest
    {
        [DataMember] public string Target { get; set; }
        [DataMember] public string OriginalHash { get; set; }
        [DataMember] public string Hash { get; set; }
        [DataMember] public string Version { get; set; }
        [DataMember] public int ProcessId { get; set; }
        [DataMember] public long ProcessStart { get; set; }
    }

    internal static class UpdatePackage
    {
        internal const long MaxBytes = 50 * 1024 * 1024;
        internal static T Read<T>(Stream stream) => (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        internal static void Write<T>(string path, T value)
        {
            using (var stream = File.Create(path)) new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
        }
        internal static bool IsHash(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        internal static string Hash(string path)
        {
            using (var hash = SHA256.Create())
            using (var file = File.OpenRead(path))
                return BitConverter.ToString(hash.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        internal static void Verify(string path, string hash, string version)
        {
            if (!IsHash(hash) || !string.Equals(Hash(path), hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The downloaded file failed its SHA-256 check.");
            var assembly = AssemblyName.GetAssemblyName(path);
            var expected = new Version(version);
            if (assembly.Name != "SpiderSurge" || assembly.Version.Major != expected.Major
                || assembly.Version.Minor != expected.Minor || assembly.Version.Build != expected.Build)
                throw new InvalidDataException("The downloaded assembly does not match this release.");
        }
    }
}

