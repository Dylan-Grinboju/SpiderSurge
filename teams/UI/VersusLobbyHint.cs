using SpiderSurge.Common;
using UnityEngine;

namespace SpiderTeams
{
    internal sealed class VersusLobbyHint : MonoBehaviour
    {
        private GUIStyle _style;
        private int _fontVersion = -1;
        private static readonly string Hint = $"Press {Controls.Label(Controls.TeamController)} / Triangle (controller) or {Controls.Label(Controls.TeamKeyboard)} (keyboard)\nto change team";

        private void OnGUI()
        {
            var manager = TeamManager.Instance;
            var game = GameController.instance;
            if (!TeamManager.InVersusLobby || manager == null || !manager.PicksOpen || game == null || !game.acceptingInputs || GameMenus.Open || SettingsPage.IsOpen
                || LevelController.IsLoading || StatsMod.UIManager.TitlesShowing)
            {
                return;
            }

            Hud.EnsureFonts();
            if (_style == null || _fontVersion != Hud.FontVersion)
            {
                _style = Hud.Style(20, anchor: TextAnchor.MiddleCenter, wrap: true);
                _fontVersion = Hud.FontVersion;
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Hud.Resize(_style, 20, scale);
            var rect = new Rect(24f * scale, Screen.height - 200f * scale, Screen.width - 48f * scale, 68f * scale);
            Hud.Label(rect, Hint, _style, Hud.Text, shadow: true);
        }
    }
}
