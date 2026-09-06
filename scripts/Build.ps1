param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
$env:DOTNET_CLI_HOME = Join-Path $root '.tools\cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $dotnet build (Join-Path $root 'src\RandomCharacterBlacklist\RandomCharacterBlacklist.csproj') -c Release "-p:GameDir=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
& $dotnet run --project (Join-Path $root 'tests\CoreTests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
$version = (Get-Content -Raw (Join-Path $root 'src\RandomCharacterBlacklist\RandomCharacterBlacklist.json') | ConvertFrom-Json).version
$package = Join-Path $root "dist\RandomCharacterBlacklist-$version"
$payload = Join-Path $package 'RandomCharacterBlacklist'
New-Item -ItemType Directory -Force $payload | Out-Null
foreach ($name in @('RandomCharacterBlacklist.dll', 'RandomCharacterBlacklist.json')) {
    Copy-Item -LiteralPath (Join-Path $root "src\RandomCharacterBlacklist\bin\Release\net9.0\$name") -Destination $payload -Force
}
Copy-Item (Join-Path $root 'packaging\*') -Destination $package -Force
$hashes = [ordered]@{}
Get-ChildItem -LiteralPath $payload -File | ForEach-Object { $hashes[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'checksums.json')
Compress-Archive -Path "$package\*" -DestinationPath "$package.zip" -Force
Write-Host "Package: $package.zip"
