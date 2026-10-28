using System;
using I2.Loc;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Renames the lobby's versus platform label while team mode is on; restoring re-localizes it through I2.
    internal static class LobbyLabel
    {
        private static TMP_Text _label;
        private static Localize _localize;
        private static string _original;
        private static bool _renamed;
        private static bool _loggedError;

        public static void Refresh()
        {
            try
            {
                if (_label == null && !Find())
                {
                    return;
                }

                if (ModConfig.TeamsEnabled && Net.IsHost)
                {
                    if (!_renamed)
                    {
                        _original = _label.text;
                        _renamed = true;
                    }
                    if (_label.text != Consts.LobbyLabel.TeamText)
                    {
                        _label.text = Consts.LobbyLabel.TeamText;
                    }
                }
                else if (_renamed)
                {
                    RestoreText();
                }
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Logger.LogError($"SpiderTeams could not update the lobby label: {ex}");
                }
            }
        }

        public static void Restore()
        {
            try
            {
                if (_label != null && _renamed)
                {
                    RestoreText();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderTeams could not restore the lobby label: {ex.Message}");
            }
            _label = null;
            _localize = null;
            _original = null;
            _renamed = false;
        }

        private static void RestoreText()
        {
            _renamed = false;
            if (_localize != null)
            {
                _localize.OnLocalize(true);
            }
            else if (_original != null)
            {
                _label.text = _original;
            }
        }

        private static bool Find()
        {
            _renamed = false;
            _original = null;
            _localize = null;

            GameObject target = GameObject.Find(Consts.LobbyLabel.Path);
            _label = target != null ? target.GetComponent<TMP_Text>() : null;
            if (_label == null)
            {
                _label = FindByTerm();
            }
            if (_label == null)
            {
                return false;
            }

            _localize = _label.GetComponent<Localize>();
            return true;
        }

        private static TMP_Text FindByTerm()
        {
            GameObject platform = GameObject.Find(Consts.LobbyLabel.PlatformPath);
            if (platform == null)
            {
                return null;
            }

            foreach (Localize localize in platform.GetComponentsInChildren<Localize>(true))
            {
                if (localize.mTerm == Consts.LobbyLabel.Term)
                {
                    TMP_Text text = localize.GetComponent<TMP_Text>();
                    if (text != null)
                    {
                        return text;
                    }
                }
            }
            return null;
        }
    }
}
