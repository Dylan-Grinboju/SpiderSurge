$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = Get-Content (Join-Path $repo 'stats/UI/TitleStatRow.cs') -Raw
Add-Type -TypeDefinition ($source.Replace('internal sealed class', 'public sealed class'))
$checks = 0
function Assert-Row($text, $label, $value, $direction) {
    $rows = [StatsMod.TitleStatRow]::Parse($text)
    if ($rows.Length -ne 1 -or $rows[0].Label -cne $label -or $rows[0].Value -cne $value -or $rows[0].Direction -ne $direction) {
        throw "Unexpected row for '$text': $($rows | ConvertTo-Json -Compress)"
    }
    $script:checks++
}
Assert-Row 'Most Web Swings (229)' 'Web swings' '229' 1
Assert-Row 'Least Damage Taken (0)' 'Damage taken' '0' -1
Assert-Row 'Highest Point (638.9m)' 'Peak height' '639 m' 1
Assert-Row 'Lowest Point (-41.0m)' 'Peak height' '-41 m' -1
Assert-Row 'Most Airborne Time (545.2s)' 'Air time' '9:05' 1
Assert-Row 'Least Airborne Time (59.9s)' 'Air time' '1:00' -1
Assert-Row 'Most Alive Time (3661.2s)' 'Time alive' '61:01' 1
Assert-Row 'Most Alive Time (0.1s)' 'Time alive' '0.1 s' 1
Assert-Row 'Most Alive Time (59.4s)' 'Time alive' '59.4 s' 1
Assert-Row 'Max Kill Streak While Solo (12)' 'Solo kill streak' '12' 1
Assert-Row 'Most Kills While Airborne (15)' 'Airborne kills' '15' 1
Assert-Row 'Best Accuracy (87.5%)' 'Accuracy' '87.5%' 1
Assert-Row 'Worst Accuracy (0%)' 'Accuracy' '0%' -1
Assert-Row 'Best Railvolver Accuracy (80% (8/10))' 'Railvolver accuracy' '80% (8/10)' 1
Assert-Row 'Most Ability Activations (123456)' 'Ability uses' '123456' 1
Assert-Row 'Least Ultimate Activations (0)' 'Ultimate uses' '0' -1
foreach ($text in @('An unusual achievement', 'Most Custom Stat (3)', 'Most Web Swings', 'Most Web Swings ()', 'Most Web Swings (3')) {
    Assert-Row $text $text '' 0
}
$previousCulture = [Threading.Thread]::CurrentThread.CurrentCulture
try {
    [Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('de-DE')
    Assert-Row 'Most Airborne Time (545.2s)' 'Air time' '9:05' 1
    Assert-Row 'Most Airborne Time (545,2s)' 'Air time' '9:05' 1
    Assert-Row 'Highest Point (638,9m)' 'Peak height' '639 m' 1
} finally { [Threading.Thread]::CurrentThread.CurrentCulture = $previousCulture }
$rows = [StatsMod.TitleStatRow]::Parse("Most Offense (10)`r`nLeast Deaths (0)`nA custom note")
if ($rows.Length -ne 3 -or $rows[0].Direction -ne 1 -or $rows[1].Direction -ne -1 -or $rows[2].Label -cne 'A custom note') { throw 'Mixed rows changed order or lost custom text' }
if ([StatsMod.TitleStatRow]::Parse($null).Length -ne 0 -or [StatsMod.TitleStatRow]::Parse('').Length -ne 0) { throw 'Empty descriptions should have no rows' }
$catalog = (Get-Content (Join-Path $repo 'stats/Tracking/TitlesManager.cs') -Raw) + (Get-Content (Join-Path $repo 'stats/Tracking/TitleCatalog.cs') -Raw) + (Get-Content (Join-Path $repo 'surge/Integration/SurgeReportSource.cs') -Raw)
$labels = [regex]::Matches($catalog, '"((?:Most|Least|Highest|Lowest|Max|Best|Worst) [A-Za-z ]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
foreach ($label in $labels) {
    if ([StatsMod.TitleStatRow]::Parse("$label (1)")[0].Direction -eq 0) { throw "Unrecognized generated stat label: $label" }
}
Write-Output "PASS: $checks stat formatting cases, $($labels.Count) generated labels, mixed multiline descriptions and empty descriptions"
