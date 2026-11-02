param([string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $root '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
$release = Get-Content -Raw -LiteralPath (Join-Path $GamePath 'release_info.json') | ConvertFrom-Json
if ($release.version -ne 'v0.111.0') { throw "Unsupported game version: $($release.version). Revalidate against that game before building." }
$env:DOTNET_CLI_HOME = Join-Path $root '.tools/cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
& $dotnet build (Join-Path $root 'src/RelicRpsStats') -c Release "-p:GameDir=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'RPS mod build failed.' }
& $dotnet run --project (Join-Path $root 'tests/RpsCoreTests') -c Release
if ($LASTEXITCODE -ne 0) { throw 'RPS core tests failed.' }
$version = (Get-Content -Raw (Join-Path $root 'src/RelicRpsStats/RelicRpsStats.json') | ConvertFrom-Json).version
$package = Join-Path $root ('.research/rps-package-' + [Guid]::NewGuid().ToString('N'))
$zip = Join-Path $root "dist/RelicRpsStats-$version.zip"
New-Item -ItemType Directory -Force (Join-Path $root 'dist') | Out-Null
$payload = Join-Path $package 'RelicRpsStats'
New-Item -ItemType Directory -Force $payload | Out-Null
foreach ($name in @('RelicRpsStats.dll','RelicRpsStats.json')) {
    Copy-Item -LiteralPath (Join-Path $root "src/RelicRpsStats/bin/Release/net9.0/$name") -Destination $payload -Force
}
Copy-Item -LiteralPath (Join-Path $root 'docs/relic-rps-stats.md') -Destination (Join-Path $package 'INSTALL.md') -Force
$hashes = [ordered]@{}
Get-ChildItem -LiteralPath $payload -File | ForEach-Object { $hashes[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $package 'checksums.json')
Compress-Archive -Path "$package/*" -DestinationPath $zip -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $expected = @('checksums.json','INSTALL.md','RelicRpsStats/RelicRpsStats.dll','RelicRpsStats/RelicRpsStats.json') | Sort-Object
    $actual = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\','/') }) | Sort-Object
    if (Compare-Object $expected $actual) { throw 'Unexpected package entries.' }
} finally { $archive.Dispose() }
Write-Host "Local preview package: $zip"
