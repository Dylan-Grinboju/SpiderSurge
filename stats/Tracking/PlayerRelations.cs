using SpiderTeams;
using UnityEngine.InputSystem;

namespace StatsMod
{
    internal static class PlayerRelations
    {
        public static bool AreTeammates(PlayerInput player, PlayerInput other)
        {
            if (player == null || other == null || player == other) return false;
            if (GameSessionManager.Instance.CurrentGameMode != GameMode.Versus) return true;

            var manager = TeamManager.Instance;
            if (manager == null || !manager.TeamModeThisMatch) return false;

            var controller = player.GetComponentInChildren<PlayerController>();
            var otherController = other.GetComponentInChildren<PlayerController>();
            return controller != null && otherController != null &&
                   manager.TryGetTeamOfPlayerId(controller.playerID.Value, out int team) &&
                   manager.TryGetTeamOfPlayerId(otherController.playerID.Value, out int otherTeam) &&
                   team == otherTeam;
        }
    }
}
