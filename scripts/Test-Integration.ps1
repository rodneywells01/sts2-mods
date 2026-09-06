param([ValidateSet('both', 'host-only', 'client-only')][string[]]$Cases = @('both', 'host-only', 'client-only'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$research = Join-Path $root '.research'
$stages = @{ host = (Join-Path $research 'game-host'); client = (Join-Path $research 'game-client') }
foreach ($stage in $stages.Values) {
    if (-not (Test-Path -LiteralPath (Join-Path $stage 'override.cfg'))) { throw 'Prepare isolated copies as described in docs/implementation.md first.' }
    if (Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $stage 'SlayTheSpire2.exe') }) { throw 'Close the staged test games first.' }
    Copy-Item (Join-Path $root 'src\RandomCharacterBlacklist\bin\Release\net9.0\RandomCharacterBlacklist.dll') (Join-Path $stage 'mods\RandomCharacterBlacklist') -Force
    Copy-Item (Join-Path $root 'tests\GameProbe\bin\Release\net9.0\GameProbe.dll') (Join-Path $stage 'mods\GameProbe') -Force
}
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
            $result = Join-Path $research "final-$case-$role.json"
            if (Test-Path -LiteralPath $result) { Remove-Item -LiteralPath $result }
            $env:APPDATA = Join-Path $research 'appdata'
            $env:RCB_PROBE_ROLE = $role
            $env:RCB_PROBE_OUTPUT = $result
            $arguments = "--headless --force-steam off --log-file final-$case-$role.log -fastmp "
            if ($role -eq 'host') { $arguments += 'host_standard' } else { $arguments += 'join -clientId 1000' }
            $processes += Start-Process -FilePath (Join-Path $stages[$role] 'SlayTheSpire2.exe') -WorkingDirectory $stages[$role] -ArgumentList $arguments -WindowStyle Hidden -PassThru
        }
        $passed = $false
        for ($attempt = 0; $attempt -lt 55; $attempt++) {
            Start-Sleep -Seconds 1
            $results = @('host','client') | ForEach-Object { Join-Path $research "final-$case-$_.json" }
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
        Write-Host "PASS multiplayer $case (both peers completed)"
    }
    finally {
        foreach ($process in $processes) { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null } }
        if ($hiddenManifest -and (Test-Path -LiteralPath $hiddenManifest)) { Move-Item -LiteralPath $hiddenManifest -Destination $originalManifest }
    }
}
