param(
    [string]$Version = "1.0.2",
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
$stagingRoot = Join-Path $root ("bin\Release\package-staging\" + [Guid]::NewGuid().ToString("N"))
$appPublish = Join-Path $stagingRoot "app"
$launcherPublish = Join-Path $stagingRoot "launcher"

function Invoke-Native {
    & $args[0] @($args | Select-Object -Skip 1)
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $($args -join ' ')"
    }
}

Invoke-Native dotnet publish (Join-Path $root "PokemonGen3Hack.csproj") `
    -c Release `
    -f $framework `
    -o $appPublish `
    -p:RuntimeIdentifierOverride=$RuntimeIdentifier `
    -p:WindowsPackageType=None `
    -p:SatelliteResourceLanguages=ko

Invoke-Native dotnet publish (Join-Path $root "Launcher\PokemonGen3Hack.Launcher.csproj") `
    -c Release `
    -f $framework `
    -r $RuntimeIdentifier `
    -o $launcherPublish `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

foreach ($excludedDirectory in @("Tests", "dist", "Launcher", "obj", ".git", ".agents", ".claude")) {
    if (Test-Path -LiteralPath (Join-Path $appPublish $excludedDirectory)) {
        throw "Publish output contains a development directory: $excludedDirectory"
    }
}

if (Test-Path $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
Copy-Item -Path (Join-Path $appPublish "*") -Destination $appRoot -Recurse -Force
Copy-Item -Path (Join-Path $launcherPublish "PokemonGen3Hack.exe") -Destination (Join-Path $packageRoot "PokemonGen3Hack.exe") -Force
Set-Content -Path (Join-Path $packageRoot "version.txt") -Value $Version -NoNewline

$requiredFiles = @(
    (Join-Path $packageRoot "PokemonGen3Hack.exe"),
    (Join-Path $appRoot "PokemonGen3Hack.exe"),
    (Join-Path $appRoot "PokemonGen3Hack.dll"),
    (Join-Path $appRoot "wwwroot\index.html"),
    (Join-Path $appRoot "wwwroot\_framework\blazor.webview.js")
)

foreach ($requiredFile in $requiredFiles) {
    if (!(Test-Path $requiredFile)) {
        throw "Package is incomplete. Missing required file: $requiredFile"
    }
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $zipPath -Force

Write-Host "Package: $packageRoot"
Write-Host "Zip:     $zipPath"
