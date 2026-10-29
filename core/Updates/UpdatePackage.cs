using System;
using System.Collections.Generic;
using System.Globalization;
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
        [DataMember(Name = "draft")] public bool Draft { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "assets")] public UpdateAsset[] Assets { get; set; }
        internal string Version => Tag?.TrimStart('v');
        internal ReleaseVersion Number => ReleaseVersion.Parse(Version);
        internal string PageUrl => "https://github.com/Dylan-Grinboju/SpiderSurge/releases/tag/" + Uri.EscapeDataString(Tag);
        internal bool IsStable => !Draft && !Prerelease && Number?.PreRelease.Length == 0;
        internal bool IsBeta => !Draft && Prerelease && Number?.IsBeta == true;
        private string DownloadUrl => "https://github.com/Dylan-Grinboju/SpiderSurge/releases/download/" + Uri.EscapeDataString(Tag) + "/SpiderSurge.dll";

        // A beta DLL is also offered newer betas; a stable release newer than them moves it back to stable releases
        internal static UpdateRelease Newest(IEnumerable<UpdateRelease> releases, bool beta) =>
            releases.Where(item => item != null && (item.IsStable || beta && item.IsBeta)).OrderByDescending(item => item.Number).FirstOrDefault();

        internal bool TryGetInstallableAsset(out UpdateAsset asset, out string problem)
        {
            var matches = Assets?.Where(item => item != null && item.Name == "SpiderSurge.dll").ToArray();
            asset = matches?.Length == 1 ? matches[0] : null;
            problem = !IsStable && !IsBeta ? "The release is not a stable or beta version."
                : asset == null || asset.Size <= 0 || asset.Size > UpdatePackage.MaxBytes || asset.State != "uploaded" ? "This release has no installable SpiderSurge.dll. Install it by hand from its release page."
                : asset.Url != DownloadUrl || asset.Digest == null || !asset.Digest.StartsWith("sha256:") || !UpdatePackage.IsHash(asset.Digest.Substring(7)) ? "This release has no verified download. Install it by hand from its release page."
                : null;
            if (problem != null) asset = null;
            return asset != null;
        }
    }

    // A release number X.Y.Z with an optional pre-release such as beta.2, ordered as in SemVer; build metadata after + is ignored
    internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
    {
        internal Version Core { get; private set; }
        internal string[] PreRelease { get; private set; }
        internal bool IsBeta => PreRelease.Length > 0 && PreRelease[0] == "beta";

        internal static ReleaseVersion Parse(string text)
        {
            var parts = text?.Split('+')[0].Split(new[] { '-' }, 2);
            if (parts == null || !Version.TryParse(parts[0], out var core) || core.Build < 0 || core.Revision >= 0) return null;
            var preRelease = parts.Length > 1 ? parts[1].Split('.') : new string[0];
            return preRelease.Any(string.IsNullOrEmpty) ? null : new ReleaseVersion { Core = core, PreRelease = preRelease };
        }

        internal static bool IsNewer(string latest, string current)
        {
            var next = Parse(latest);
            var installed = Parse(current);
            return next != null && installed != null && next.CompareTo(installed) > 0;
        }

        // A release is newer than its pre-releases; numeric identifiers compare as numbers and before words
        public int CompareTo(ReleaseVersion other)
        {
            if (other == null) return 1;
            int order = Core.CompareTo(other.Core);
            if (order != 0) return order;
            if (PreRelease.Length == 0 || other.PreRelease.Length == 0) return other.PreRelease.Length.CompareTo(PreRelease.Length);
            for (int i = 0; i < Math.Min(PreRelease.Length, other.PreRelease.Length); i++)
            {
                bool numeric = long.TryParse(PreRelease[i], NumberStyles.None, CultureInfo.InvariantCulture, out long number);
                bool otherNumeric = long.TryParse(other.PreRelease[i], NumberStyles.None, CultureInfo.InvariantCulture, out long otherNumber);
                order = numeric && otherNumeric ? number.CompareTo(otherNumber) : numeric != otherNumeric ? (numeric ? -1 : 1) : string.CompareOrdinal(PreRelease[i], other.PreRelease[i]);
                if (order != 0) return order;
            }
            return PreRelease.Length.CompareTo(other.PreRelease.Length);
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
    }

    internal static class UpdatePackage
    {
        internal const long MaxBytes = 50 * 1024 * 1024;
        internal const string LockName = "update.lock";

        // The game holds the update folder's lock while it prepares an install and until it exits; the installer holds it while installing
        internal static FileStream TryLock(string folder)
        {
            try { return new FileStream(Path.Combine(folder, LockName), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { return null; }
        }

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
            var expected = ReleaseVersion.Parse(version)?.Core ?? throw new InvalidDataException("The release version is not valid.");
            if (assembly.Name != "SpiderSurge" || assembly.Version.Major != expected.Major
                || assembly.Version.Minor != expected.Minor || assembly.Version.Build != expected.Build)
                throw new InvalidDataException("The downloaded assembly does not match this release.");
        }
    }
}
