param(
    [ValidateSet('both', 'host-only', 'client-only')][string[]]$Cases = @('both', 'host-only', 'client-only'),
    [ValidateSet('immediate', 'lock-in')][string]$Mode = 'immediate',
    [switch]$NativePeerRandom
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$research = Join-Path $root '.research'
$stages = @{ host = (Join-Path $research 'game-host'); client = (Join-Path $research 'game-client') }
if ($NativePeerRandom -and ($Mode -ne 'lock-in' -or $Cases -contains 'both')) { throw 'NativePeerRandom requires lock-in mode and mixed-install cases.' }
$resultMode = if ($NativePeerRandom) { "$Mode-native-peer" } else { $Mode }
foreach ($stage in $stages.Values) {
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'override.cfg'))) { throw 'Prepare isolated copies as described in docs/implementation.md first.' }
    $roleName = if ($stage -eq $stages.host) { 'host' } else { 'client' }
    $override = Get-Content -Raw -LiteralPath (Join-Path $stage 'override.cfg')
    if ($override -notmatch 'config/use_custom_user_dir=true' -or -not $override.Contains('config/custom_user_dir_name="STS2ModTests/' + $roleName + '"')) {
        throw "The $roleName copy must use its isolated user-data directory."
    }
    if (Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $stage 'SlayTheSpire2.exe') }) { throw 'Close the staged test games first.' }
    Copy-Item (Join-Path $root 'src\RandomCharacterBlacklist\bin\Release\net9.0\RandomCharacterBlacklist.dll') (Join-Path $stage 'mods\RandomCharacterBlacklist') -Force
    Copy-Item (Join-Path $root 'src\RandomCharacterBlacklist\RandomCharacterBlacklist.json') (Join-Path $stage 'mods\RandomCharacterBlacklist') -Force
    Copy-Item (Join-Path $root 'tests\GameProbe\bin\Release\net9.0\GameProbe.dll') (Join-Path $stage 'mods\GameProbe') -Force
}
$savedEnvironment = @{}
foreach ($name in @('APPDATA', 'RCB_PROBE_ROLE', 'RCB_PROBE_OUTPUT', 'RCB_PROBE_MODE', 'RCB_PROBE_NATIVE_PEER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
try {
foreach ($case in $Cases) {
    $processes = @()
    $hiddenManifest = $null
    $originalManifest = $null
    try {
        $disabledRole = switch ($case) { 'host-only' { 'client' } 'client-only' { 'host' } default { $null } }
        if ($disabledRole) {
            $originalManifest = Join-Path $stages[$disabledRole] 'mods\RandomCharacterBlacklist\RandomCharacterBlacklist.json'
            $hiddenManifest = Join-Path $research "$case-manifest.disabled"
            Move-Item -LiteralPath $originalManifest -Destination $hiddenManifest
        }
        foreach ($role in @('host', 'client')) {
            $result = Join-Path $research "final-$resultMode-$case-$role.json"
            if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
            $env:APPDATA = Join-Path $research 'appdata'
            $env:RCB_PROBE_ROLE = $role
            $env:RCB_PROBE_MODE = $Mode
            $env:RCB_PROBE_NATIVE_PEER = if ($NativePeerRandom) { '1' } else { '0' }
            $env:RCB_PROBE_OUTPUT = $result
            $arguments = "--headless --force-steam off --log-file final-$resultMode-$case-$role.log -fastmp "
            if ($role -eq 'host') { $arguments += 'host_standard' } else { $arguments += 'join -clientId 1000' }
            $processes += Start-Process -FilePath (Join-Path $stages[$role] 'SlayTheSpire2.exe') -WorkingDirectory $stages[$role] -ArgumentList $arguments -WindowStyle Hidden -PassThru
        }
        $passed = $false
        for ($attempt = 0; $attempt -lt 55; $attempt++) {
            Start-Sleep -Seconds 1
            $results = @('host','client') | ForEach-Object { Join-Path $research "final-$resultMode-$case-$_.json" }
            $complete = 0
            foreach ($result in $results) {
                if (Test-Path -LiteralPath $result) {
                    $content = Get-Content -Raw -LiteralPath $result
                    if ($content -match 'FAIL ') { throw $content }
                    if ($content -match 'COMPLETE') { $complete++ }
                }
            }
            if ($complete -eq 2) { $passed = $true; break }
        }
        if (-not $passed) { throw "Timed out: $case. Inspect the isolated game logs." }
        $rosters = @($results | ForEach-Object { ((Get-Content -Raw -LiteralPath $_ | ConvertFrom-Json).checks | Where-Object { $_ -like 'Run roster *' }) })
        if ($rosters.Count -ne 2 -or $rosters[0] -ne $rosters[1]) { throw "Peers disagree on final roster: $rosters" }
        Write-Host "PASS multiplayer $resultMode $case (both peers completed; matching roster)"
    }
    finally {
        foreach ($process in $processes) { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null } }
        if ($hiddenManifest -and (Test-Path -LiteralPath $hiddenManifest)) { Move-Item -LiteralPath $hiddenManifest -Destination $originalManifest }
    }
}
}
finally {
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}
