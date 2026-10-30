$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $repo 'surge/Core/PersonalLoadouts.cs'))
$checks = @'
namespace SpiderSurge {
    public static class PersonalChoiceTests {
        private static int checks;
        private static void Check(bool value, string message) { if (!value) throw new System.Exception(message); checks++; }
        public static int Run() {
            var loads = new PersonalLoadouts();
            string[] abilities = { "immune", "ammo", "pulse", "storage", "freeze" };
            for (int a = 0; a < 5; a++) for (int u = 0; u < 5; u++) {
                loads.Clear();
                loads.Set(0, abilities[a], false); loads.Set(0, abilities[u] + "Ultimate", true);
                loads.Set(1, abilities[(a+1)%5], false);
                Check(loads.Get(0, false) == abilities[a], "Ultimate changed base ability");
                Check(loads.Get(0, true) == abilities[u] + "Ultimate", "Mixed ultimate missing");
                Check(loads.Get(1, true) == null, "Ultimate leaked to another player");
            }
            var round = new PersonalChoiceRound();
            int[] players = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Range(0, 16));
            round.Begin(abilities, players, false);
            int first = round.Epoch;
            Check(round.Picks.Count == 16 && !round.Complete, "Large party does not wait for everyone");
            Check(!round.Choose(first, 99, 0, true), "Unknown participant accepted");
            Check(!round.Choose(first, 0, 5, true) && !round.Choose(first, 0, -1, true), "Invalid index accepted");
            for (int i = 0; i < 16; i++) round.Choose(first, i, 2, true);
            Check(round.Complete, "Repeated choices are not allowed");
            round.Choose(first, 7, 2, false);
            Check(!round.Complete, "Undo did not stop completion");
            round.Reconcile(System.Linq.Enumerable.Where(players, p => p != 7));
            Check(round.Complete, "Disconnected participant blocks completion");
            round.Reconcile(players);
            Check(!round.Complete && !round.Picks[7].Ready, "Joining participant inherited readiness");
            round.Choose(first, 7, 4, true);
            using (var stream = new System.IO.MemoryStream()) {
                var writer = new System.IO.BinaryWriter(stream);
                loads.Write(writer); round.Write(writer); stream.Position = 0;
                var reader = new System.IO.BinaryReader(stream);
                var copy = new PersonalLoadouts(); var remote = new PersonalChoiceRound();
                copy.Read(reader); remote.Read(reader);
                Check(copy.Get(0, false) == loads.Get(0, false) && copy.Get(0, true) == loads.Get(0, true), "Loadout round-trip failed");
                Check(remote.Complete && remote.Picks.Count == 16 && remote.Picks[7].Index == 4, "Choice round-trip failed");
                Check(stream.Position == stream.Length, "Snapshot not fully read");
            }
            int beforeReplacement = round.Epoch;
            round.InvalidateRequests();
            Check(!round.Choose(beforeReplacement, 0, 1, true, 999), "Replaced slot accepted a stale request");
            Check(round.Picks[0].Ready, "Roster invalidation lost another player's lock");
            Check(round.Choose(round.Epoch, 0, 1, false, 1000), "Current revision rejected");
            Check(!round.Choose(round.Epoch, 0, 2, true, 999), "Out-of-order input replaced newer choice");
            round.Begin(new[] { "ammoUltimate", "freezeUltimate" }, players, true);
            Check(!round.Choose(first, 0, 0, true), "Previous screen request affected ultimate screen");
            Check(round.Ultimate && !round.Complete, "Ultimate screen carried over readiness");
            round.Reconcile(new int[0]);
            Check(!round.Complete, "Empty lobby completed a choice");
            loads.Remove(0);
            Check(loads.Get(0, false) == null && loads.Get(0, true) == null && loads.Get(1, false) != null, "Departed slot cleanup damaged another player");
            return checks;
        }
    }
}
'@
Add-Type -TypeDefinition ($source + $checks)
Write-Output ("PASS: {0} personal choice checks" -f [SpiderSurge.PersonalChoiceTests]::Run())
