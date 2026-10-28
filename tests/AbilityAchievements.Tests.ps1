$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'surge/Achievements/AbilityAchievementProgress.cs'
Add-Type -TypeDefinition ([IO.File]::ReadAllText($source))
$checks = 0

function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}

$freeze = [SpiderSurge.FreezeAchievementCast]::new(1, 10)
Assert-Equal ($freeze.RecordKill(1, 100, 5)) $false 'One kill is insufficient'
Assert-Equal ($freeze.RecordKill(1, 100, 6)) $false 'Duplicate death callbacks count once'
Assert-Equal ($freeze.RecordKill(0, 101, 6)) $false 'A teammate cannot contribute kills'
Assert-Equal ($freeze.RecordKill(1, 102, 6)) $false 'Two distinct kills are insufficient'
Assert-Equal ($freeze.RecordKill(1, 103, 9.99)) $true 'Third kill before thaw unlocks for P2'
Assert-Equal ($freeze.RecordKill(1, 104, 9.99)) $false 'Same cast only completes once'
$later = [SpiderSurge.FreezeAchievementCast]::new(1, 20)
Assert-Equal ($later.RecordKill(1, 105, 11)) $false 'Another activation starts from zero'
Assert-Equal ($later.RecordKill(1, 106, 20)) $false 'Death exactly at thaw does not count'
Assert-Equal ($later.RecordKill(1, 107, 21)) $false 'Death after thaw does not count'
$other = [SpiderSurge.FreezeAchievementCast]::new(0, 20)
Assert-Equal ($other.RecordKill(0, 108, 12)) $false 'Another player has independent progress'
$invalid = [SpiderSurge.FreezeAchievementCast]::new(-1, 20)
Assert-Equal ($invalid.RecordKill(-1, 109, 12)) $false 'Missing player is never credited'

$weapon = [SpiderSurge.StoredWeaponJourney]::new('A')
Assert-Equal ($weapon.Arrive('A')) $false 'Death or retry on the same map adds nothing'
Assert-Equal $weapon.MapChanges 0 'Retry count stays zero'
Assert-Equal ($weapon.Arrive('B')) $false 'First map change'
Assert-Equal ($weapon.Arrive('B')) $false 'Repeated restore in one map adds nothing'
Assert-Equal ($weapon.Arrive('Lobby')) $false 'Lobby never counts'
Assert-Equal ($weapon.Arrive('')) $false 'Missing scene never counts'
Assert-Equal $weapon.MapChanges 1 'Only one real change counted'
Assert-Equal ($weapon.Arrive('A')) $false 'Returning to an earlier map is a new change'
Assert-Equal ($weapon.Arrive('C')) $true 'Third change unlocks'
Assert-Equal ($weapon.Arrive('D')) $false 'Journey completes only once'
$replacement = [SpiderSurge.StoredWeaponJourney]::new('C')
Assert-Equal ($replacement.Arrive('D')) $false 'A replacement weapon starts a separate journey'

$meal = [SpiderSurge.BlackHoleMeal]::new()
Assert-Equal ($meal.Eat('Wasp(Clone)')) $false 'First type'
Assert-Equal ($meal.Eat('Wasp Shielded(Clone)')) $false 'Shielded Wasp is still one type'
Assert-Equal ($meal.Eat('LaserWasp(Clone)')) $false 'Laser Wasp is still one type'
Assert-Equal ($meal.Eat('WaspCocoon(Clone)')) $false 'Cocoon is distinct from its hatchling'
Assert-Equal ($meal.Eat('ShieldedWaspCocoon(Clone)')) $false 'Cocoon shield does not add a type'
Assert-Equal ($meal.Eat('MissileWhisp(Clone)')) $true 'Third distinct type unlocks'
Assert-Equal ($meal.Eat('TwinWhisp(Clone)')) $false 'A fourth type does not complete again'
$nextMeal = [SpiderSurge.BlackHoleMeal]::new()
Assert-Equal ($nextMeal.Eat('TwinWhisp(Clone)')) $false 'Different Black Holes do not pool progress'

Write-Output "PASS: $checks ability achievement checks"
