$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'surge/Meter/ChaosRules.cs'
Add-Type -TypeDefinition ([IO.File]::ReadAllText($source))
$checks = 0

function Assert-Equal($actual, $expected, [string]$label) {
    if ("$actual" -ne "$expected") { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}

foreach ($count in 2, 3, 4, 5) {
    foreach ($slot in 0..($count - 1)) {
        Assert-Equal ([SpiderSurge.ChaosRules]::BonusSlot(0, $count, $slot)) $slot 'Winning roll preserves selected slot'
        Assert-Equal ([SpiderSurge.ChaosRules]::BonusSlot(.749999, $count, $slot)) $slot 'Roll immediately below 75 percent wins'
        Assert-Equal ([SpiderSurge.ChaosRules]::BonusSlot(.75, $count, $slot)) -1 '75 percent boundary has no bonus'
        Assert-Equal ([SpiderSurge.ChaosRules]::BonusSlot(1, $count, $slot)) -1 'Maximum roll has no bonus'
    }
}
Assert-Equal ([SpiderSurge.ChaosRules]::BonusSlot(0, 0, 0)) -1 'Empty choices have no bonus'

$cases = @(
    @{ Lives = 1; Abilities = $false; Ultimate = $false; Expected = 'LifeWager,Overrun' },
    @{ Lives = 2; Abilities = $false; Ultimate = $false; Expected = 'LifeWager,Overrun,PoisonedHeart' },
    @{ Lives = 1; Abilities = $true; Ultimate = $false; Expected = 'DoubleCooldown,LifeWager,NoRegularAbilities,Overrun' },
    @{ Lives = 2; Abilities = $true; Ultimate = $false; Expected = 'DoubleCooldown,LifeWager,NoRegularAbilities,Overrun,PoisonedHeart' },
    @{ Lives = 2; Abilities = $true; Ultimate = $true; Expected = 'DoubleCooldown,LifeWager,NoRegularAbilities,NoUltimate,Overrun,PoisonedHeart' },
    @{ Lives = 2; Abilities = $false; Ultimate = $true; Expected = 'LifeWager,Overrun,PoisonedHeart' }
)
foreach ($case in $cases) {
    $eligible = [SpiderSurge.ChaosRules]::Eligible($case.Lives, $case.Abilities, $case.Ultimate)
    $names = @($eligible | ForEach-Object { $_.ToString() } | Sort-Object)
    Assert-Equal ($names -join ',') $case.Expected 'Challenge eligibility'
    Assert-Equal (@($names | Select-Object -Unique).Count) $names.Count 'Uniform draw has no duplicate candidates'
}

$rewards = @{ None = 0; PoisonedHeart = 2; DoubleCooldown = 1; NoRegularAbilities = 2; LifeWager = 1; NoUltimate = 1; Overrun = 2 }
foreach ($kind in $rewards.Keys) {
    Assert-Equal ([SpiderSurge.ChaosRules]::Reward([SpiderSurge.ChallengeKind]$kind)) $rewards[$kind] "$kind reward"
}

Assert-Equal ([SpiderSurge.ChaosRules]::Points(1, $false)) '1 chaos point' 'Singular points'
Assert-Equal ([SpiderSurge.ChaosRules]::Points(2, $true)) '+2 chaos points' 'Signed plural points'
Assert-Equal ([SpiderSurge.ChaosRules]::Points(-1, $true)) '-1 chaos point' 'Signed loss'
Assert-Equal ([SpiderSurge.ChaosRules]::Points(0, $true)) '0 chaos points' 'Zero is plural and unsigned'
Write-Output "PASS: $checks chaos rule checks"
