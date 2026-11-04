$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Get-Content (Join-Path $repo 'surge/UI/AbilityDisplayState.cs') -Raw
$checks = @'
namespace SpiderSurge {
    public static class AbilityDisplayTests {
        static int checks;
        static void Check(bool value, string message) { if (!value) throw new System.Exception(message); checks++; }
        public static int Run() {
            Check(!default(AbilityDisplayState).Ready, "Missing observer state must not claim ready");
            var active = new AbilityDisplayState(true, false, 3f, 5f, 0f, 0f);
            Check(active.Advance(1f).ActiveRemaining == 2f && active.Advance(1f).ActiveProgress == 0.4f, "Late-join active progress lost its full duration");
            Check(active.Advance(0f).ActiveRemaining == 3f, "Paused game changed active time");
            Check(active.Advance(-1f).ActiveRemaining == 3f, "Negative clock adjustment extended an effect");
            var cooldown = new AbilityDisplayState(false, true, 0f, 0f, 5f, 20f);
            Check(cooldown.CooldownProgress == 0.75f && !cooldown.Ready, "Late-join cooldown restarted at zero progress");
            Check(cooldown.Advance(30f).CooldownRemaining == 0f && !cooldown.Advance(30f).Ready, "Observer guessed readiness without a host transition");
            var ready = new AbilityDisplayState(false, false, 0f, 0f, 0f, 0f);
            Check(ready.Ready && ready.CooldownProgress == 1f, "Host readiness did not display");
            using (var stream = new System.IO.MemoryStream()) {
                var writer = new System.IO.BinaryWriter(stream);
                active.Write(writer); cooldown.Write(writer); ready.Write(writer);
                stream.Position = 0;
                var reader = new System.IO.BinaryReader(stream);
                var a = AbilityDisplayState.Read(reader); var c = AbilityDisplayState.Read(reader); var r = AbilityDisplayState.Read(reader);
                Check(a.Active && a.ActiveRemaining == 3f && a.ActiveDuration == 5f, "Active state round-trip failed");
                Check(c.CoolingDown && c.CooldownRemaining == 5f && c.CooldownDuration == 20f, "Cooldown state round-trip failed");
                Check(r.Ready && stream.Position == stream.Length, "Ready state round-trip failed");
            }
            return checks;
        }
    }
}
'@
Add-Type -TypeDefinition ($source + $checks)
Write-Output ("PASS: {0} ability display checks" -f [SpiderSurge.AbilityDisplayTests]::Run())
