$ErrorActionPreference = 'Stop'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) 'surge/UI/HoldConfirmation.cs'
Add-Type -TypeDefinition ([IO.File]::ReadAllText($source))
$checks = 0

function Assert-Equal($actual, $expected, [string]$label) {
    if ($actual -ne $expected) { throw "$label expected '$expected', got '$actual'" }
    $script:checks++
}

$hold = [SpiderSurge.HoldConfirmation]::new()
Assert-Equal ($hold.Tick(0, $true, $false, $true)) $false 'Inherited press cannot start a hold'
Assert-Equal ($hold.Tick(10, $true, $false, $true)) $false 'Inherited press never confirms'
Assert-Equal $hold.Progress 0 'Inherited press shows no progress'
Assert-Equal ($hold.Tick(11, $false, $true, $true)) $false 'Release arms confirmation'
Assert-Equal ($hold.Tick(12, $true, $false, $true)) $false 'Fresh press starts hold'
Assert-Equal ($hold.Tick(12.4, $true, $false, $true)) $false 'Half the hold is too short'
Assert-Equal ([Math]::Abs($hold.Progress - 0.5) -lt .00001) $true 'Halfway progress'
Assert-Equal ($hold.Tick(12.79, $true, $false, $true)) $false 'Just under 0.8 seconds is too short'
Assert-Equal ($hold.Tick(12.81, $true, $false, $true)) $true 'A completed 0.8-second hold confirms'
Assert-Equal ($hold.Tick(20, $true, $false, $true)) $false 'Continued hold cannot confirm twice'

$hold.Reset()
[void]$hold.Tick(0, $false, $true, $true)
[void]$hold.Tick(1, $true, $false, $true)
[void]$hold.Tick(1.5, $true, $false, $true)
Assert-Equal ($hold.Tick(1.6, $false, $true, $true)) $false 'Early release cancels'
Assert-Equal $hold.Progress 0 'Early release clears progress'
[void]$hold.Tick(3, $true, $false, $true)
Assert-Equal ($hold.Tick(3.4, $true, $false, $true)) $false 'Separate presses do not accumulate'
Assert-Equal ($hold.Tick(5, $true, $false, $true)) $true 'New hold gets its own duration'

foreach ($interruption in 'Pause', 'Focus loss', 'Hidden panel', 'Disabled button') {
    $hold.Reset()
    [void]$hold.Tick(0, $false, $true, $true)
    [void]$hold.Tick(1, $true, $false, $true)
    [void]$hold.Tick(1.5, $true, $false, $true)
    Assert-Equal ($hold.Tick(1.6, $true, $false, $false)) $false "$interruption cancels"
    Assert-Equal $hold.Progress 0 "$interruption clears progress"
    Assert-Equal ($hold.Tick(10, $true, $false, $true)) $false "$interruption requires release"
    [void]$hold.Tick(11, $false, $true, $true)
    [void]$hold.Tick(12, $true, $false, $true)
    Assert-Equal ($hold.Tick(12.81, $true, $false, $true)) $true "$interruption allows a fresh hold"
}

$hold.Reset()
[void]$hold.Tick(0, $false, $true, $true)
[void]$hold.Tick(1, $true, $false, $true)
[void]$hold.Tick(1.5, $true, $false, $true)
$hold.Reset()
Assert-Equal ($hold.Tick(10, $true, $false, $true)) $false 'Switching buttons requires release'
[void]$hold.Tick(11, $false, $false, $true)
Assert-Equal ($hold.Tick(12, $true, $false, $true)) $false 'Another held input prevents arming'
[void]$hold.Tick(12.4, $false, $true, $true)
[void]$hold.Tick(14, $true, $false, $true)
Assert-Equal ($hold.Tick(14, $true, $false, $true)) $false 'Repeated frame does not advance progress'
Assert-Equal ($hold.Tick(16, $true, $false, $true)) $true 'Elapsed real time completes the hold'

$skip = [SpiderSurge.HoldConfirmation]::new()
Assert-Equal ($skip.Tick(0, $true, $false, $true)) $false 'Skip rejects the previous screen press'
[void]$skip.Tick(1, $false, $true, $true)
Assert-Equal ($skip.Tick(2, $true, $false, $true)) $false 'Clicking Skip starts a hold without confirming'
Assert-Equal ($skip.Tick(2.2, $false, $true, $true)) $false 'Releasing Skip early cancels'
Assert-Equal $skip.Progress 0 'Cancelled Skip clears progress'
[void]$skip.Tick(3, $true, $false, $true)
Assert-Equal ($skip.Tick(3.79, $true, $false, $true)) $false 'Skip waits for the full duration'
Assert-Equal ($skip.Tick(3.81, $true, $false, $true)) $true 'Holding Skip for 0.8 seconds confirms'
Assert-Equal ($skip.Tick(4, $true, $false, $true)) $false 'Skip responds once per hold'
Write-Output "PASS: $checks hold confirmation checks"
