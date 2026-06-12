param(
    [string]$Version = "1.0.0",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$framework = "net10.0-windows10.0.19041.0"
$packageName = "PokemonGen3Hack-v$Version-$RuntimeIdentifier"
$distRoot = Join-Path $root "dist"
$packageRoot = Join-Path $distRoot $packageName
$appRoot = Join-Path $packageRoot "app"
$zipPath = Join-Path $distRoot "$packageName.zip"
$appPublish = Join-Path $root "bin\Release\$framework\$RuntimeIdentifier\publish"
$launcherPublish = Join-Path $root "Launcher\bin\Release\$framework\$RuntimeIdentifier\publish"

function Invoke-Native {
    & $args[0] @($args | Select-Object -Skip 1)
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $($args -join ' ')"
    }
}

Invoke-Native dotnet publish (Join-Path $root "PokemonGen3Hack.csproj") `
    -c Release `
    -f $framework `
    -p:RuntimeIdentifierOverride=$RuntimeIdentifier `
    -p:WindowsPackageType=None `
    -p:SatelliteResourceLanguages=ko

Invoke-Native dotnet publish (Join-Path $root "Launcher\PokemonGen3Hack.Launcher.csproj") `
    -c Release `
    -f $framework `
    -r $RuntimeIdentifier `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
Copy-Item -Path (Join-Path $appPublish "*") -Destination $appRoot -Recurse -Force
Copy-Item -Path (Join-Path $launcherPublish "PokemonGen3Hack.exe") -Destination (Join-Path $packageRoot "PokemonGen3Hack.exe") -Force

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -Force

Write-Host "Package: $packageRoot"
Write-Host "Zip:     $zipPath"
