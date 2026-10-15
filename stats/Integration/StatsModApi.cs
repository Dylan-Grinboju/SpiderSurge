using Logger = Silk.Logger;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
namespace StatsMod;

/// <summary>
/// Public API for external mods to register custom stats and titles.
/// External titles participate in the domination/priority pipeline alongside native titles.
/// </summary>
public static class StatsModApi
{
    public const int API_VERSION = 1;

    private static readonly ReaderWriterLockSlim _syncLock = new ReaderWriterLockSlim();
    private static readonly List<string> externalStatLines = new();
    private static readonly List<TitleEntry> externalTitles = new();

    private const int MAX_STATS_LINES = 100;
    private const int MAX_TITLE_PRIORITY = 1000;
    private const int MAX_TITLE_LENGTH = 100;

    /// <summary>
    /// Registers a list of custom raw stat strings to be displayed on the stats screen.
    /// </summary>
    /// <param name="lines">Lines of text to append to the end-game stats menu.</param>
    /// <returns>True if registration was successful, false if inputs were invalid.</returns>
    public static bool RegisterCustomStats(List<string> lines)
    {
        if (lines == null || lines.Count == 0 || lines.Count > MAX_STATS_LINES) return false;

        _syncLock.EnterWriteLock();
        try
        {
            if (externalStatLines.Count + lines.Count > MAX_STATS_LINES * 5) return false; // Prevent extreme spam from multiple mods
            externalStatLines.AddRange(lines);
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }

        Logger.LogInfo($"StatsModApi: Registered {lines.Count} external stat lines");
        return true;
    }

    /// <summary>
    /// Registers a custom title for the post-game victory screen.
    /// </summary>
    /// <param name="titleName">The display name of the title.</param>
    /// <param name="description">A subtitle/description explaining the title. Can be expanded upon dynamically by native stats if any are passed as requirements.</param>
    /// <param name="requirementNames">A list of stat keys that must be met to get this title. Can be native stats (e.g. "MostWebSwings") or entirely custom ones.</param>
    /// <param name="leader">Explicitly assigns the player who achieved this title. If null, the mod attempts to dynamically resolve the player by checking the 'requirementNames' against native stats. NOTE: If your title uses ONLY custom requirements, you MUST provide a non-null leader here to prevent the title from being rejected.</param>
    /// <param name="leaderHasStat">Confirms the explicitly-requested leader actually achieved your custom stat (typically passed as true if the mod already did validation).</param>
    /// <param name="bonusPriority">An additional boost to the title's display priority score (Base priority is 10 per requirement).</param>
    /// <returns>True if successfully registered, false if validation failed.</returns>
    public static bool RegisterCustomTitle(string titleName, string description, string[] requirementNames, PlayerInput leader, bool leaderHasStat, int bonusPriority)
    {
        if (string.IsNullOrEmpty(titleName) || titleName.Length > MAX_TITLE_LENGTH || requirementNames == null || requirementNames.Length == 0) return false;

        bonusPriority = Mathf.Clamp(bonusPriority, -MAX_TITLE_PRIORITY, MAX_TITLE_PRIORITY);

        var requirements = new HashSet<string>(requirementNames);
        TitleEntry title;

        if (leader != null)
        {
            if (!leaderHasStat) return false;

            var players = PlayerTracker.Instance.GetActivePlayers();
            if (!players.TryGetValue(leader, out var playerData))
            {
                Logger.LogWarning($"StatsModApi: Could not find player data for title '{titleName}'");
                return false;
            }

            var kvp = new KeyValuePair<PlayerInput, PlayerTracker.PlayerData>(leader, playerData);
            title = new TitleEntry(kvp)
            {
                TitleName = titleName,
                Description = description,
                Priority = requirements.Count * 10 + bonusPriority,
                Requirements = requirements
            };
            Logger.LogInfo($"StatsModApi: Registered title '{titleName}' for {playerData.PlayerName}");
        }
        else
        {
            title = new TitleEntry()
            {
                TitleName = titleName,
                Description = description,
                Priority = requirements.Count * 10 + bonusPriority,
                Requirements = requirements
            };
            Logger.LogInfo($"StatsModApi: Registered title '{titleName}' (player will be resolved from requirements)");
        }

        _syncLock.EnterWriteLock();
        try
        {
            if (externalTitles.Count > 100) return false; // Prevent title spam
            externalTitles.Add(title);
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }

        return true;
    }

    public static List<string> GetExternalStats()
    {
        _syncLock.EnterReadLock();
        try
        {
            return new List<string>(externalStatLines);
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    public static List<TitleEntry> GetExternalTitles()
    {
        _syncLock.EnterReadLock();
        try
        {
            return externalTitles.ConvertAll(t => new TitleEntry
            {
                TitleName = t.TitleName,
                Description = t.Description,
                PlayerName = t.PlayerName,
                PrimaryColor = t.PrimaryColor,
                SecondaryColor = t.SecondaryColor,
                Player = t.Player,
                Priority = t.Priority,
                Requirements = t.Requirements != null ? new HashSet<string>(t.Requirements) : new HashSet<string>()
            });
        }
        finally
        {
            _syncLock.ExitReadLock();
        }
    }

    public static void ClearExternalData()
    {
        _syncLock.EnterWriteLock();
        try
        {
            externalStatLines.Clear();
            externalTitles.Clear();
        }
        finally
        {
            _syncLock.ExitWriteLock();
        }
    }
}
