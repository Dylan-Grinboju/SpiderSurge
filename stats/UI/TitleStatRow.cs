using System;
using System.Globalization;

namespace StatsMod;

// Interpret the existing report text at the display boundary, including titles received from the host.
internal sealed class TitleStatRow
{
    public string Label { get; private set; }
    public string Value { get; private set; } = "";
    public int Direction { get; private set; }

    public static TitleStatRow[] Parse(string description)
    {
        if (string.IsNullOrEmpty(description)) return Array.Empty<TitleStatRow>();
        string[] lines = description.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var rows = new TitleStatRow[lines.Length];
        for (int i = 0; i < lines.Length; i++) rows[i] = ParseLine(lines[i].TrimEnd('\r'));
        return rows;
    }

    private static TitleStatRow ParseLine(string line)
    {
        var row = new TitleStatRow { Label = line };
        int split = line.IndexOf(" (", StringComparison.Ordinal);
        if (split < 0 || !line.EndsWith(")", StringComparison.Ordinal)) return row;

        string heading = line.Substring(0, split);
        int space = heading.IndexOf(' ');
        if (space < 0) return row;
        string rank = heading.Substring(0, space);
        int direction = rank == "Most" || rank == "Highest" || rank == "Max" || rank == "Best" ? 1
            : rank == "Least" || rank == "Lowest" || rank == "Worst" ? -1 : 0;
        if (direction == 0) return row;

        string stat = heading.Substring(space + 1);
        string label = stat switch
        {
            "Point" => "Peak height",
            "Airborne Time" => "Air time",
            "Kills While Airborne" => "Airborne kills",
            "Kills While Solo" => "Solo kills",
            "Kill Streak While Solo" => "Solo kill streak",
            "Alive Time" => "Time alive",
            "Ability Activations" => "Ability uses",
            "Ultimate Activations" => "Ultimate uses",
            "Web Swings" => "Web swings",
            "Wave Clutches" => "Wave clutches",
            "Kill Streak" => "Kill streak",
            "Offense" => "Offense",
            "Damage Taken" => "Damage taken",
            "Friendly Fire" => "Friendly fire",
            "Shields Lost" => "Shields lost",
            "Deaths" => "Deaths",
            "Lava Deaths" => "Lava deaths",
            "Gun Kills" or "Gun Offense" => "Gun offense",
            "Explosive Kills" or "Explosive Offense" => "Explosive offense",
            "Blade Kills" or "Blade Offense" => "Blade offense",
            "Hornets Killed" => "Hornet kills",
            "Whisps Killed" => "Whisp kills",
            "Khepris Killed" => "Khepri kills",
            "Astral Returns" => "Astral returns",
            "Shots Fired" => "Shots fired",
            "Accuracy" => "Accuracy",
            "Railvolver Accuracy" => "Railvolver accuracy",
            _ => null,
        };
        if (label == null) return row;

        string value = line.Substring(split + 2, line.Length - split - 3);
        if (value.Length == 0) return row;
        row.Label = label;
        row.Direction = direction;
        row.Value = FormatValue(stat, value);
        return row;
    }

    private static string FormatValue(string stat, string value)
    {
        bool seconds = stat == "Airborne Time" || stat == "Alive Time";
        bool meters = stat == "Point";
        string unit = seconds ? "s" : "m";
        if ((!seconds && !meters) || !value.EndsWith(unit, StringComparison.Ordinal)) return value;
        // The host's decimal separator can differ from the viewer's; these source values have no grouping.
        if (!double.TryParse(value.Substring(0, value.Length - 1).Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number)) return value;
        if (meters) return number.ToString("0", CultureInfo.InvariantCulture) + " m";
        if (number < 59.5 || number > int.MaxValue) return number.ToString("0.#", CultureInfo.InvariantCulture) + " s";
        long total = (long)Math.Round(number, MidpointRounding.AwayFromZero);
        return (total / 60).ToString(CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
    }
}
