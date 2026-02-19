$ErrorActionPreference = 'Stop'

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)?.Source
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    $dotnetCandidates = @(
        (Join-Path $env:ProgramFiles 'dotnet/dotnet.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'dotnet/dotnet.exe')
    ) | Where-Object { $_ -and (Test-Path $_) }

    $dotnet = $dotnetCandidates | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($dotnet)) {
    throw 'dotnet executable was not found. Install .NET SDK or add dotnet to PATH.'
}

& $dotnet build
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$source = Join-Path $PSScriptRoot '../bin/Debug/net472/SpiderSurge.dll'
$source = [System.IO.Path]::GetFullPath($source)
if (!(Test-Path $source)) {
    throw "Build output not found: $source"
}

$gamePath = $env:SPIDERHECK_GAME_PATH
if ([string]::IsNullOrWhiteSpace($gamePath)) {
    throw 'Set environment variable SPIDERHECK_GAME_PATH to your Spiderheck game folder (or Silk/mods folder).'
}
if (!(Test-Path $gamePath)) {
    throw "SPIDERHECK_GAME_PATH does not exist: $gamePath"
}

$leaf = Split-Path -Leaf $gamePath
if ($leaf -ieq 'mods') {
    $targetDir = $gamePath
} elseif ($leaf -ieq 'Silk') {
    $targetDir = Join-Path $gamePath 'mods'
} else {
    $targetDir = Join-Path $gamePath 'Silk/mods'
}

New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item -Path $source -Destination (Join-Path $targetDir 'SpiderSurge.dll') -Force
Write-Host "Deployed to $targetDir"
