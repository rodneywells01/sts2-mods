param(
    [string]$GamePath,
    [switch]$Uninstall,
    [switch]$NoPause
)
$ErrorActionPreference = 'Stop'
$modId = 'RandomCharacterBlacklist'
$ownedFiles = @('RandomCharacterBlacklist.dll', 'RandomCharacterBlacklist.json')

function Find-Game {
    $steamRoots = @()
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam')) {
        $value = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($value.SteamPath) { $steamRoots += $value.SteamPath }
        if ($value.InstallPath) { $steamRoots += $value.InstallPath }
    }
    if (${env:ProgramFiles(x86)}) { $steamRoots += (Join-Path ${env:ProgramFiles(x86)} 'Steam') }
    $libraries = @($steamRoots)
    foreach ($root in $steamRoots | Select-Object -Unique) {
        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($match in [regex]::Matches([IO.File]::ReadAllText($vdf), '"path"\s+"([^"]+)"')) {
                $libraries += $match.Groups[1].Value.Replace('\\', '\')
            }
        }
    }
    $found = @()
    foreach ($library in $libraries | Select-Object -Unique) {
        $manifest = Join-Path $library 'steamapps\appmanifest_2868840.acf'
        $folder = 'Slay the Spire 2'
        if (Test-Path -LiteralPath $manifest) {
            $match = [regex]::Match([IO.File]::ReadAllText($manifest), '"installdir"\s+"([^"]+)"')
            if ($match.Success) { $folder = $match.Groups[1].Value }
        }
        $candidate = Join-Path $library ('steamapps\common\' + $folder)
        if (Test-Path -LiteralPath (Join-Path $candidate 'SlayTheSpire2.exe')) { $found += $candidate }
    }
    $found = @($found | Select-Object -Unique)
    if ($found.Count -eq 1) { return $found[0] }
    if ($found.Count -gt 1) { Write-Host "More than one game installation found:`n$($found -join "`n")" }
    return (Read-Host 'Paste the Slay the Spire 2 installation folder (Steam > Manage > Browse local files)').Trim('"')
}

try {
    if (-not $GamePath) { $GamePath = Find-Game }
    $GamePath = (Resolve-Path -LiteralPath $GamePath).Path
    if (-not (Test-Path -LiteralPath (Join-Path $GamePath 'SlayTheSpire2.exe')) -or
        -not (Test-Path -LiteralPath (Join-Path $GamePath 'data_sts2_windows_x86_64\sts2.dll'))) {
        throw 'This is not a Slay the Spire 2 Windows installation.'
    }
    # Do not replace a DLL in a running game. Other installations do not block this one.
    foreach ($process in Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue) {
        if (-not $process.Path -or [IO.Path]::GetDirectoryName($process.Path) -eq $GamePath) {
            throw 'Close Slay the Spire 2 before installing or removing the mod.'
        }
    }
    $modsPath = Join-Path $GamePath 'mods'
    $destination = Join-Path $modsPath $modId
    foreach ($path in @($modsPath, $destination)) {
        if ((Test-Path -LiteralPath $path) -and ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'The mods folder is a link. Use the documented manual installation method for this setup.'
        }
    }
    if ($Uninstall) {
        $installedManifest = Join-Path $destination "$modId.json"
        if (-not (Test-Path -LiteralPath $installedManifest)) { throw 'This mod is not installed in that folder.' }
        if ((Get-Content -Raw -LiteralPath $installedManifest | ConvertFrom-Json).id -ne $modId) { throw 'Unexpected manifest; no files removed.' }
        foreach ($name in $ownedFiles) {
            $file = Join-Path $destination $name
            if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
        }
        if (@(Get-ChildItem -LiteralPath $destination -Force).Count -eq 0) { Remove-Item -LiteralPath $destination }
        Write-Host 'Mod removed. Saved exclusions and other mods were kept.'
    }
    else {
        $releaseFile = Join-Path $GamePath 'release_info.json'
        if (-not (Test-Path -LiteralPath $releaseFile)) { throw 'Cannot identify the game version. See README for manual installation.' }
        $version = (Get-Content -Raw -LiteralPath $releaseFile | ConvertFrom-Json).version
        if ($version -ne 'v0.111.0') { throw "This preview is tested for v0.111.0; found $version. Get a matching mod build before installing." }
        $payload = Join-Path $PSScriptRoot $modId
        $hashes = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'checksums.json') | ConvertFrom-Json
        foreach ($name in $ownedFiles) {
            $source = Join-Path $payload $name
            $expected = $hashes.$name
            if (-not $expected -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $expected) {
                throw "Package integrity check failed for $name. Extract a fresh copy of the complete ZIP."
            }
        }
        $existingManifest = Join-Path $destination "$modId.json"
        if ((Test-Path -LiteralPath $existingManifest) -and (Get-Content -Raw -LiteralPath $existingManifest | ConvertFrom-Json).id -ne $modId) {
            throw 'The destination contains a different mod; no files changed.'
        }
        $backup = @{}
        foreach ($name in $ownedFiles) {
            $file = Join-Path $destination $name
            if (Test-Path -LiteralPath $file) {
                if ((Get-Item -LiteralPath $file).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'A destination file is a link; no files changed.' }
                $backup[$name] = [IO.File]::ReadAllBytes($file)
            }
        }
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        try {
            foreach ($name in $ownedFiles) { Copy-Item -LiteralPath (Join-Path $payload $name) -Destination (Join-Path $destination $name) -Force }
            foreach ($name in $ownedFiles) {
                if ((Get-FileHash -LiteralPath (Join-Path $destination $name) -Algorithm SHA256).Hash -ne $hashes.$name) { throw 'Installed file verification failed.' }
            }
        }
        catch {
            $installationError = $_
            foreach ($name in $ownedFiles) {
                $file = Join-Path $destination $name
                if ($backup.ContainsKey($name)) { [IO.File]::WriteAllBytes($file, $backup[$name]) }
                elseif (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
            }
            throw $installationError
        }
        Write-Host "Installed Random Character Options 0.1.4 in:`n$destination"
        Write-Host 'Start the game, enable mods when prompted, and check Settings > Mod Settings.'
        Write-Host 'On character select, open Random options (F8). No BaseLib or other download is required.'
    }
}
catch {
    Write-Host "Could not complete the operation: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'If Windows denied write access, run Install.cmd as administrator, or copy the mod folder manually.'
    if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
    exit 1
}
if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
