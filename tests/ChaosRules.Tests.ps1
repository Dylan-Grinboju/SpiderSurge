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

foreach ($level in 0, 1, 2) {
    foreach ($count in 0..5) {
        $first = [SpiderSurge.ChaosRules]::BonusSlot(1, $count, 0, $level)
        Assert-Equal ($first -ge 0) ($level -gt 0 -and $count -gt 0) 'Cartographer guarantees the first bonus even at maximum roll'
        Assert-Equal ([SpiderSurge.ChaosRules]::SecondBonusSlot(0, $count, $first, 0, $level) -ge 0) ($level -eq 2 -and $count -gt 1) 'Only level 2 can mark a second distinct map'
    }
    foreach ($roll in 0, .499999, .5, .749999, .75, 1) {
        $threshold = if ($level -eq 0) { .5 } else { .75 }
        Assert-Equal ([SpiderSurge.ChaosRules]::OffersChallenge($roll, $level)) ($roll -lt $threshold) 'Challenge chance boundary'
        foreach ($kind in [Enum]::GetValues([SpiderSurge.ChallengeKind])) {
            $bonus = if ($kind -ne 'None' -and $level -eq 2 -and $roll -lt .5) { 1 } else { 0 }
            Assert-Equal ([SpiderSurge.ChaosRules]::ChallengeReward($kind, $level, $roll)) ([SpiderSurge.ChaosRules]::Reward($kind) + $bonus) 'Challenge bonus is exactly one and only at level 2'
        }
    }
}
foreach ($count in 2..5) {
    foreach ($first in 0..($count - 1)) {
        $others = @()
        foreach ($selected in 0..($count - 2)) {
            $second = [SpiderSurge.ChaosRules]::SecondBonusSlot(.499999, $count, $first, $selected, 2)
            Assert-Equal ($second -ge 0 -and $second -lt $count -and $second -ne $first) $true 'Second bonus stays in range and cannot duplicate the first'
            $others += $second
            Assert-Equal ([SpiderSurge.ChaosRules]::SecondBonusSlot(.5, $count, $first, $selected, 2)) -1 'Second-map roll fails at the 50 percent boundary'
        }
        Assert-Equal (@($others | Select-Object -Unique).Count) ($count - 1) 'Every remaining card can be selected uniformly'
    }
}

$cases = @(
    @{ Lives = 1; Abilities = $false; Ultimate = $false; Expected = 'LifeWager,Overrun' },
    @{ Lives = 2; Abilities = $false; Ultimate = $false; Expected = 'LifeWager,Overrun,PoisonedHeart' },
    @{ Lives = 1; Abilities = $true; Ultimate = $false; Expected = 'DoubleCooldown,LifeWager,NoRegularAbilities,Overrun' },
    @{ Lives = 2; Abilities = $true; Ultimate = $false; Expected = 'DoubleCooldown,LifeWager,NoRegularAbilities,Overrun,PoisonedHeart' },
    @{ Lives = 2; Abilities = $true; Ultimate = $true; Expected = 'DoubleCooldown,LifeWager,NoRegularAbilities,NoUltimate,Overrun,PoisonedHeart' },
    @{ Lives = 2; Abilities = $false; Ultimate = $true; Expected = 'LifeWager,Overrun,PoisonedHeart' }
)
foreach ($case in $cases) {
    $eligible = [SpiderSurge.ChaosRules]::Eligible($case.Lives, $case.Abilities, $case.Ultimate, 1)
    $names = @($eligible | ForEach-Object { $_.ToString() } | Sort-Object)
    Assert-Equal ($names -join ',') $case.Expected 'Challenge eligibility'
    Assert-Equal (@($names | Select-Object -Unique).Count) $names.Count 'Uniform draw has no duplicate candidates'
}

$rewards = @{ None = 0; PoisonedHeart = 3; DoubleCooldown = 1; NoRegularAbilities = 2; LifeWager = 1; NoUltimate = 1; Overrun = 2; CleanHands = 1 }
foreach ($kind in $rewards.Keys) {
    Assert-Equal ([SpiderSurge.ChaosRules]::Reward([SpiderSurge.ChallengeKind]$kind)) $rewards[$kind] "$kind reward"
}

Assert-Equal ([SpiderSurge.ChaosRules]::Points(1, $false)) '1 chaos point' 'Singular points'
Assert-Equal ([SpiderSurge.ChaosRules]::Points(2, $true)) '+2 chaos points' 'Signed plural points'
Assert-Equal ([SpiderSurge.ChaosRules]::Points(-1, $true)) '-1 chaos point' 'Signed loss'
Assert-Equal ([SpiderSurge.ChaosRules]::Points(0, $true)) '0 chaos points' 'Zero is plural and unsigned'
foreach ($players in 0, 1, 2, 32) {
    $eligible = [SpiderSurge.ChaosRules]::Eligible(2, $true, $true, $players)
    Assert-Equal ($eligible.Contains([SpiderSurge.ChallengeKind]::CleanHands)) ($players -ge 2) 'Clean Hands needs teammates'
}
foreach ($lives in 1, 2) {
    foreach ($last in [Enum]::GetValues([SpiderSurge.ChallengeKind])) {
        foreach ($previous in [Enum]::GetValues([SpiderSurge.ChallengeKind])) {
            $eligible = [SpiderSurge.ChaosRules]::Eligible($lives, $false, $false, 1)
            [SpiderSurge.ChaosRules]::AvoidRecent($eligible, $last, $previous)
            Assert-Equal ($eligible.Count -gt 0) $true 'Offer rotation always leaves a candidate'
            Assert-Equal ($eligible.Contains($last)) $false 'Most recent offer is avoided'
        }
    }
}
Write-Output "PASS: $checks chaos rule checks"
