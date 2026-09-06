param([ValidateSet('layout', 'animation', 'singleplayer')][string]$Role = 'layout')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $root '.research/game-host'
$research = Join-Path $root '.research'
$override = Get-Content -Raw -LiteralPath (Join-Path $stage 'override.cfg')
if ($override -notmatch 'config/use_custom_user_dir=true' -or $override -notmatch 'config/custom_user_dir_name="STS2ModTests/host"') {
    throw 'Prepare the isolated host copy as described in docs/implementation.md.'
}
if (Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $stage 'SlayTheSpire2.exe') }) {
    throw 'Close the staged host game first.'
}
Copy-Item (Join-Path $root 'src/RandomCharacterBlacklist/bin/Release/net9.0/RandomCharacterBlacklist.dll') (Join-Path $stage 'mods/RandomCharacterBlacklist') -Force
Copy-Item (Join-Path $root 'src/RandomCharacterBlacklist/RandomCharacterBlacklist.json') (Join-Path $stage 'mods/RandomCharacterBlacklist') -Force
Copy-Item (Join-Path $root 'tests/GameProbe/bin/Release/net9.0/GameProbe.dll') (Join-Path $stage 'mods/GameProbe') -Force
$result = Join-Path $research "reveal-$Role.json"
if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
$savedEnvironment = @{}
foreach ($name in @('APPDATA', 'RCB_PROBE_ROLE', 'RCB_PROBE_OUTPUT', 'RCB_PROBE_MODE', 'RCB_PROBE_NATIVE_PEER')) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}
$process = $null
try {
    $env:APPDATA = Join-Path $research 'appdata'
    $env:RCB_PROBE_ROLE = $Role
    $env:RCB_PROBE_OUTPUT = $result
    $env:RCB_PROBE_MODE = 'lock-in'
    $env:RCB_PROBE_NATIVE_PEER = '0'
    $arguments = "--force-steam off --windowed --log-file reveal-$Role.log -fastmp host_standard"
    if ($Role -ne 'layout') { $arguments = '--headless ' + $arguments }
    $process = Start-Process -FilePath (Join-Path $stage 'SlayTheSpire2.exe') -WorkingDirectory $stage -ArgumentList $arguments -WindowStyle Hidden -PassThru
    for ($attempt = 0; $attempt -lt 55; $attempt++) {
        Start-Sleep -Seconds 1
        if (Test-Path -LiteralPath $result) {
            $content = Get-Content -Raw -LiteralPath $result
            if ($content -match 'FAIL ') { throw $content }
            if ($content -match 'COMPLETE') { Write-Host "PASS isolated $Role"; return }
        }
    }
    throw "Timed out: $Role. Inspect the isolated game log."
}
finally {
    if ($process -and -not $process.HasExited) { $process.Kill(); $process.WaitForExit(10000) | Out-Null }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process') }
}
