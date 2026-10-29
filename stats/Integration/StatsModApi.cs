using Logger = Silk.Logger;
using System.Collections.Generic;
using UnityEngine;
namespace StatsMod;

/// <summary>
/// Public API for external mods to register custom stats and titles.
/// External titles participate in the domination/priority pipeline alongside native titles.
/// </summary>
public static class StatsModApi
{
    public const int API_VERSION = 1;

    internal static readonly IRunReportSource Source = new ApiSource();
    private static readonly object _sync = new object();
    private static readonly List<string> externalStatLines = new();
    private static readonly List<TitleEntry> externalTitles = new();
    private static readonly List<TitleDefinition> describedTitles = new();

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

        lock (_sync)
        {
            if (externalStatLines.Count + lines.Count > MAX_STATS_LINES * 5) return false; // Prevent extreme spam from multiple mods
            externalStatLines.AddRange(lines);
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
    public static bool RegisterCustomTitle(string titleName, string description, string[] requirementNames, PlayerController leader, bool leaderHasStat, int bonusPriority)
    {
        if (string.IsNullOrEmpty(titleName) || titleName.Length > MAX_TITLE_LENGTH || requirementNames == null || requirementNames.Length == 0) return false;

        TitleEntry title;

        if (leader != null)
        {
            if (!leaderHasStat) return false;
            title = RunReportSources.TitleFor(leader, titleName, description, requirementNames, bonusPriority);
            if (title == null) return false;
            Logger.LogInfo($"StatsModApi: Registered title '{titleName}' for {title.PlayerName}");
        }
        else
        {
            var requirements = new HashSet<string>(requirementNames);
            title = new TitleEntry()
            {
                TitleName = titleName,
                Description = description,
                Priority = requirements.Count * 10 + Mathf.Clamp(bonusPriority, -MAX_TITLE_PRIORITY, MAX_TITLE_PRIORITY),
                Requirements = requirements
            };
            Logger.LogInfo($"StatsModApi: Registered title '{titleName}' (player will be resolved from requirements)");
        }

        lock (_sync)
        {
            if (externalTitles.Count > 100) return false; // Prevent title spam
            externalTitles.Add(title);
        }

        return true;
    }

    /// <summary>
    /// Lists a title in the title collection in the Mods menu, so players can see it and how to earn it before they unlock it.
    /// Call once at startup; registering the same name again replaces its text.
    /// </summary>
    /// <param name="titleName">The title's name, exactly as passed to RegisterCustomTitle.</param>
    /// <param name="howToEarn">A short line on how to earn it.</param>
    /// <returns>True if the title was listed, false if the inputs were invalid.</returns>
    public static bool DescribeTitle(string titleName, string howToEarn)
    {
        if (string.IsNullOrEmpty(titleName) || titleName.Length > MAX_TITLE_LENGTH || howToEarn == null || howToEarn.Length > MAX_TITLE_LENGTH * 2) return false;

        lock (_sync)
        {
            describedTitles.RemoveAll(t => t.Name == titleName);
            if (describedTitles.Count >= MAX_STATS_LINES) return false;
            describedTitles.Add(new TitleDefinition(titleName, howToEarn));
        }
        return true;
    }

    private sealed class ApiSource : IRunReportSource
    {
        public IEnumerable<TitleDefinition> DescribedTitles => GetDescribedTitles();
        public IEnumerable<string> ReportLines() => GetExternalStats();
        public IEnumerable<TitleEntry> Titles() => GetExternalTitles();
    }

    public static List<TitleDefinition> GetDescribedTitles()
    {
        lock (_sync)
        {
            return new List<TitleDefinition>(describedTitles);
        }
    }

    public static List<string> GetExternalStats()
    {
        lock (_sync)
        {
            return new List<string>(externalStatLines);
        }
    }

    public static List<TitleEntry> GetExternalTitles()
    {
        lock (_sync)
        {
            return externalTitles.ConvertAll(t => new TitleEntry
            {
                TitleName = t.TitleName,
                Description = t.Description,
                PlayerName = t.PlayerName,
                PrimaryColor = t.PrimaryColor,
                SecondaryColor = t.SecondaryColor,
                Player = t.Player,
                PlayerId = t.PlayerId,
                Priority = t.Priority,
                Requirements = t.Requirements != null ? new HashSet<string>(t.Requirements) : new HashSet<string>()
            });
        }
    }

    public static void ClearExternalData()
    {
        lock (_sync)
        {
            externalStatLines.Clear();
            externalTitles.Clear();
        }
    }
}
