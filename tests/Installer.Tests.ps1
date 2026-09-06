$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('.research\installer-test-' + [Guid]::NewGuid().ToString('N'))
$game = Join-Path $fixture 'Steam Library\steamapps\common\Slay the Spire 2'
$package = Join-Path $fixture 'Package with spaces'
New-Item -ItemType Directory -Force "$game\data_sts2_windows_x86_64", $package | Out-Null
Copy-Item (Join-Path $root 'dist\RandomCharacterBlacklist-0.1.4\*') $package -Recurse
Set-Content -LiteralPath "$game\SlayTheSpire2.exe" -Value 'test fixture'
Set-Content -LiteralPath "$game\data_sts2_windows_x86_64\sts2.dll" -Value 'test fixture'
Set-Content -LiteralPath "$game\release_info.json" -Value '{"version":"v0.111.0"}'
function Run-Installer([string[]]$Options = @(), [int]$ExpectedExit = 0) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$package\Install.ps1" -GamePath $game -NoPause @Options
    if ($LASTEXITCODE -ne $ExpectedExit) { throw "Unexpected installer exit: $LASTEXITCODE (expected $ExpectedExit)" }
}
Run-Installer
$destination = "$game\mods\RandomCharacterBlacklist"
if ((Get-FileHash "$destination\RandomCharacterBlacklist.dll").Hash -ne (Get-FileHash "$package\RandomCharacterBlacklist\RandomCharacterBlacklist.dll").Hash) { throw 'Installed DLL mismatch' }
Set-Content "$destination\keep.txt" 'unrelated file'
Run-Installer
Run-Installer -Options @('-Uninstall')
if (-not (Test-Path "$destination\keep.txt") -or (Test-Path "$destination\RandomCharacterBlacklist.dll")) { throw 'Uninstall ownership check failed' }
Add-Content "$package\RandomCharacterBlacklist\RandomCharacterBlacklist.dll" 'tampered'
Run-Installer -ExpectedExit 1
if (Test-Path "$destination\RandomCharacterBlacklist.dll") { throw 'Tampered package wrote a DLL' }
Copy-Item (Join-Path $root 'dist\RandomCharacterBlacklist-0.1.4\RandomCharacterBlacklist\RandomCharacterBlacklist.dll') "$package\RandomCharacterBlacklist\RandomCharacterBlacklist.dll" -Force
Set-Content "$game\release_info.json" '{"version":"v0.999.0"}'
Run-Installer -ExpectedExit 1
if (Test-Path "$destination\RandomCharacterBlacklist.dll") { throw 'Wrong version wrote a DLL' }
# Exercise the actual VDF expression with spaces and escaped backslashes.
$vdf = '"path" "D:\\Steam Library"'
$match = [regex]::Match($vdf, '"path"\s+"([^"]+)"')
if ($match.Groups[1].Value.Replace('\\', '\') -ne 'D:\Steam Library') { throw 'Steam library parser failed' }
Write-Host 'PASS: install, update, owned-file uninstall, tamper rejection, version rejection, Steam path parsing, and paths with spaces.'
