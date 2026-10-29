using Logger = Silk.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StatsMod;

// Another part adds lines and titles to a survival run's report; Stats asks for them when it builds the report
public interface IRunReportSource
{
    IEnumerable<TitleDefinition> DescribedTitles { get; }
    IEnumerable<string> ReportLines();
    IEnumerable<TitleEntry> Titles();
}

public static class RunReportSources
{
    private const int MaxTitlePriority = 1000;

    private static readonly List<IRunReportSource> _sources = new();

    public static void Register(IRunReportSource source)
    {
        if (!_sources.Contains(source)) _sources.Add(source);
    }

    public static void Unregister(IRunReportSource source) => _sources.Remove(source);

    internal static IEnumerable<TitleDefinition> DescribedTitles => _sources.SelectMany(source => source.DescribedTitles);

    internal static List<string> ReportLines() => Collect(source => source.ReportLines(), "report lines");

    internal static List<TitleEntry> Titles() => Collect(source => source.Titles(), "titles");

    // A title for leader with the native priority rule: 10 per requirement plus the bonus
    public static TitleEntry TitleFor(PlayerController leader, string titleName, string description, IEnumerable<string> requirements, int bonusPriority)
    {
        if (leader == null || !PlayerTracker.Instance.GetActivePlayers().TryGetValue(leader, out var playerData))
        {
            Logger.LogWarning($"Stats could not find player data for title '{titleName}'");
            return null;
        }
        var required = new HashSet<string>(requirements);
        return new TitleEntry(new KeyValuePair<PlayerController, PlayerTracker.PlayerData>(leader, playerData))
        {
            TitleName = titleName,
            Description = description,
            Priority = required.Count * 10 + Mathf.Clamp(bonusPriority, -MaxTitlePriority, MaxTitlePriority),
            Requirements = required
        };
    }

    private static List<T> Collect<T>(Func<IRunReportSource, IEnumerable<T>> read, string what)
    {
        var all = new List<T>();
        foreach (var source in _sources)
        {
            try
            {
                all.AddRange(read(source).Where(item => item != null));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Stats could not read {what} from {source.GetType().Name}: {ex}");
            }
        }
        return all;
    }
}
