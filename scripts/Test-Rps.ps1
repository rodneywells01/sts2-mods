param(
    [ValidateSet('both','host-only','client-only','compendium')][string]$Case = 'both',
    [switch]$Visible,
    [switch]$KeepOpen
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$research = Join-Path $root '.research'
$roles = if ($Case -eq 'compendium') { @('host') } else { @('host','client') }
$processes = @()
$manifests = @()
$createdManifests = @()
$passed = $false
try {
    foreach ($role in $roles) {
        $stage = Join-Path $research "game-$role"
        $override = Get-Content -Raw -LiteralPath (Join-Path $stage 'override.cfg')
        if ($override -notmatch "STS2ModTests/$role") { throw 'Isolated override.cfg required.' }
        if (Get-Process SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $stage 'SlayTheSpire2.exe') }) { throw 'Close the staged game first.' }
        # Preserve earlier test mods while disabling their manifests in this isolated copy.
        foreach ($id in @('GameProbe','RandomCharacterBlacklist','RelicRpsStats','RpsProbe')) {
            $original = Join-Path $stage "mods/$id/$id.json"
            if (Test-Path -LiteralPath $original) {
                $hidden = Join-Path $research "rps-$role-$id.disabled"
                if (Test-Path -LiteralPath $hidden) { throw "Unrestored test manifest: $hidden" }
                Move-Item -LiteralPath $original -Destination $hidden
                $manifests += @{ Original = $original; Hidden = $hidden }
            }
        }
        $modded = $Case -eq 'both' -or $Case -eq 'compendium' -or $Case -eq "$role-only"
        foreach ($id in @('RelicRpsStats','RpsProbe')) { New-Item -ItemType Directory -Force (Join-Path $stage "mods/$id") | Out-Null }
        Copy-Item -LiteralPath (Join-Path $root 'src/RelicRpsStats/bin/Release/net9.0/RelicRpsStats.dll') -Destination (Join-Path $stage 'mods/RelicRpsStats') -Force
        # The observer DLL is a test-probe reference on unmodded peers, but its initializer is never invoked there.
        Copy-Item -LiteralPath (Join-Path $root 'src/RelicRpsStats/bin/Release/net9.0/RelicRpsStats.dll') -Destination (Join-Path $stage 'mods/RpsProbe') -Force
        Copy-Item -LiteralPath (Join-Path $root 'tests/RpsProbe/bin/Release/net9.0/RpsProbe.dll') -Destination (Join-Path $stage 'mods/RpsProbe') -Force
        Copy-Item -LiteralPath (Join-Path $root 'tests/RpsProbe/RpsProbe.json') -Destination (Join-Path $stage 'mods/RpsProbe') -Force
        $createdManifests += Join-Path $stage 'mods/RpsProbe/RpsProbe.json'
        if ($modded) {
            Copy-Item -LiteralPath (Join-Path $root 'src/RelicRpsStats/RelicRpsStats.json') -Destination (Join-Path $stage 'mods/RelicRpsStats') -Force
            $createdManifests += Join-Path $stage 'mods/RelicRpsStats/RelicRpsStats.json'
        }
        $account = if ($role -eq 'host') { '1' } else { '1000' }
        $settingsPath = Join-Path $research "appdata/STS2ModTests/$role/default/$account/settings.save"
        $settings = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
        $settings.mod_settings.mods_enabled = $true
        $settings.mod_settings.mod_list = @(@{id='RpsProbe';is_enabled=$true;source='mods_directory'}, @{id='RelicRpsStats';is_enabled=$true;source='mods_directory'})
        $settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $settingsPath
        $saved = Join-Path $research "appdata/STS2ModTests/$role/RelicRpsStats"
        if (Test-Path -LiteralPath $saved) {
            # Archive rather than delete prior test observations.
            $archive = Join-Path $research ("rps-archive-" + $role + '-' + [Guid]::NewGuid().ToString('N'))
            $resolvedResearch = [IO.Path]::GetFullPath($research) + [IO.Path]::DirectorySeparatorChar
            foreach ($target in @($saved, $archive)) {
                if (-not [IO.Path]::GetFullPath($target).StartsWith($resolvedResearch, [StringComparison]::OrdinalIgnoreCase)) { throw 'Archive path escapes isolated workspace.' }
            }
            Move-Item -LiteralPath $saved -Destination $archive
        }
        $env:APPDATA = Join-Path $research 'appdata'
        $env:RPS_PROBE_ROLE = if ($Case -eq 'compendium') { 'compendium' } else { $role }
        $env:RPS_PROBE_EXPECT_MODDED = if ($modded) { '1' } else { '0' }
        $result = Join-Path $research "rps-$Case-$role.json"
        if (Test-Path -LiteralPath $result) { Move-Item -LiteralPath $result -Destination ($result + '.' + [Guid]::NewGuid().ToString('N') + '.old') }
        $env:RPS_PROBE_OUTPUT = $result
        $arguments = "--force-steam off --log-file rps-$Case-$role.log "
        if ($Visible -and $role -eq 'host') { $arguments += '--windowed --resolution 1280x720 ' } else { $arguments += '--headless ' }
        if ($Case -ne 'compendium') { if ($role -eq 'host') { $arguments += '-fastmp host_standard' } else { $arguments += '-fastmp join -clientId 1000' } }
        $processes += Start-Process -FilePath (Join-Path $stage 'SlayTheSpire2.exe') -WorkingDirectory $stage -ArgumentList $arguments -WindowStyle Hidden -PassThru
    }
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        Start-Sleep -Seconds 1
        $complete = 0
        foreach ($role in $roles) {
            $result = Join-Path $research "rps-$Case-$role.json"
            if (Test-Path -LiteralPath $result) {
                $content = Get-Content -Raw -LiteralPath $result
                if ($content -match 'FAIL ') { throw $content }
                if ($content -match 'COMPLETE') { $complete++ }
            }
        }
        if ($complete -eq $roles.Count) { $passed = $true; break }
    }
    if (-not $passed) { throw "Timed out: $Case; inspect isolated logs." }
    Write-Host "PASS RPS $Case"
}
finally {
    if (-not ($KeepOpen -and $passed)) {
        foreach ($process in $processes) { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit(5000) | Out-Null } }
        foreach ($active in $createdManifests) {
            if (Test-Path -LiteralPath $active) { Move-Item -LiteralPath $active -Destination (Join-Path $research ("rps-disabled-" + [Guid]::NewGuid().ToString('N') + '.json.disabled')) }
        }
        foreach ($manifest in $manifests) { Move-Item -LiteralPath $manifest.Hidden -Destination $manifest.Original }
    }
}
