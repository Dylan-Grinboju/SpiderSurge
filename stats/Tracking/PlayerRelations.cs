using SpiderTeams;

namespace StatsMod
{
    internal static class PlayerRelations
    {
        public static bool AreTeammates(PlayerController player, PlayerController other)
        {
            if (player == null || other == null || player == other) return false;
            if (GameSessionManager.Instance.CurrentGameMode != GameMode.Versus) return true;

            var manager = TeamManager.Instance;
            // Teams plays free-for-all when fewer than 2 teams have players, so Stats follows the same rule
            if (manager == null || !TeamManager.TeamMatchActive) return false;

            return manager.TryGetTeamOfPlayerId(player.playerID.Value, out int team) &&
                   manager.TryGetTeamOfPlayerId(other.playerID.Value, out int otherTeam) &&
                   team == otherTeam;
        }
    }
}
