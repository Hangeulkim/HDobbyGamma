param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
$cliHome = Join-Path $projectRoot '.dotnet-home'
$dist = Join-Path $projectRoot 'dist'
$systemDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$projectDotnet = Join-Path $projectRoot '.sdk\dotnet.exe'
$workspaceDotnet = Join-Path $workspaceRoot 'source\.sdk\dotnet.exe'
$nugetPackages = Join-Path $projectRoot '.nuget-packages'
$candidates = @()
$candidates += $projectDotnet
if ($systemDotnet) { $candidates += $systemDotnet.Source }
$candidates += $workspaceDotnet
$dotnet = $null

$env:DOTNET_CLI_HOME = $cliHome
$env:NUGET_PACKAGES = $nugetPackages
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'

Push-Location -LiteralPath $projectRoot
try {
    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
        $resolvedVersion = & $candidate --version 2>$null
        if ($LASTEXITCODE -eq 0 -and $resolvedVersion -match '^10\.0\.') {
            $dotnet = $candidate
            break
        }
    }
    if (-not $dotnet) {
        throw '.NET SDK 10.0.400 (or a compatible 10.0.4xx patch) was not found.'
    }

    if (Test-Path -LiteralPath $dist) {
        $resolvedDist = (Resolve-Path -LiteralPath $dist).Path
        if ($resolvedDist -ne (Join-Path $projectRoot 'dist')) {
            throw "Unexpected output path: $resolvedDist"
        }
        Remove-Item -LiteralPath $resolvedDist -Recurse -Force
    }

    & $dotnet restore (Join-Path $projectRoot 'tests\GammaControl.Tests.csproj')
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $SkipTests) {
        & $dotnet run --project (Join-Path $projectRoot 'tests\GammaControl.Tests.csproj') --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    & $dotnet restore (Join-Path $projectRoot 'GammaControl.csproj') --runtime win-x64
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    & $dotnet publish (Join-Path $projectRoot 'GammaControl.csproj') `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $dist `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $publishedFiles = @(Get-ChildItem -LiteralPath $dist -File)
    if ($publishedFiles.Count -ne 1 -or $publishedFiles[0].Name -ne 'HDobbyGamma.exe') {
        throw 'Expected exactly one publish artifact named HDobbyGamma.exe.'
    }

    $publishedFiles | Select-Object Name, Length
}
finally {
    Pop-Location
}
