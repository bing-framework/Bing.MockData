[CmdletBinding()]
param(
    [string]$PackageVersion,
    [switch]$Publish,
    [string]$ArtifactsRoot,
    [string]$DotnetCommand = "dotnet",
    [string]$NuGetSource = "https://api.nuget.org/v3/index.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solutionPath = Join-Path $repoRoot "Bing.MockData.sln"
$libraryProjectPath = Join-Path $repoRoot "src/Bing.MockData/Bing.MockData.csproj"
$smokeProjectPath = Join-Path $repoRoot "tests/Bing.MockData.PackageSmoke/Bing.MockData.PackageSmoke.csproj"
$smokeSourcePath = Join-Path $repoRoot "tests/Bing.MockData.PackageSmoke/Program.cs"
$smokeLockPath = Join-Path $repoRoot "tests/Bing.MockData.PackageSmoke/packages.lock.json"

if ($Publish -and [string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
    throw "Set NUGET_API_KEY before publishing."
}

if ([string]::IsNullOrWhiteSpace($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $repoRoot "artifacts"
}
elseif (-not [IO.Path]::IsPathRooted($ArtifactsRoot)) {
    $ArtifactsRoot = Join-Path $repoRoot $ArtifactsRoot
}

$runId = [Guid]::NewGuid().ToString("N")
$runDirectory = Join-Path $ArtifactsRoot (Join-Path "package-runs" $runId)
$packageOutput = Join-Path $runDirectory "packages"
New-Item -ItemType Directory -Path $packageOutput -Force | Out-Null

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & $DotnetCommand @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE."
    }
}

Push-Location $repoRoot
try {
    Invoke-DotNet @("restore", $solutionPath, "--locked-mode")
    Invoke-DotNet @("test", $solutionPath, "--configuration", "Release", "--no-restore")
    Invoke-DotNet @("test", (Join-Path $repoRoot "tests/Bing.MockData.Tests/Bing.MockData.Tests.csproj"), "--configuration", "Release", "--no-restore", "-p:MockDataTargetFramework=netstandard2.0")
    Invoke-DotNet @("build", $libraryProjectPath, "--configuration", "Release", "--no-restore")

    $packArguments = @(
        "pack",
        $libraryProjectPath,
        "--configuration", "Release",
        "--no-build",
        "--no-restore",
        "--output", $packageOutput
    )
    if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
        $packArguments += "-p:PackageVersion=$PackageVersion"
    }
    Invoke-DotNet $packArguments

    $packages = @(Get-ChildItem -LiteralPath $packageOutput -File -Filter "Bing.MockData.*.nupkg")
    if ($packages.Count -ne 1) {
        throw "Expected one Bing.MockData package in this run's output; found $($packages.Count)."
    }
    $package = $packages[0]
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        foreach ($framework in @("netstandard2.0", "net10.0")) {
            foreach ($extension in @("dll", "xml")) {
                if ($null -eq $archive.GetEntry("lib/$framework/Bing.MockData.$extension")) {
                    throw "Package is missing the $framework $extension asset."
                }
            }
        }
    }
    finally { $archive.Dispose() }
    $packagePrefix = "Bing.MockData."
    if (-not $package.BaseName.StartsWith($packagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unexpected package name: $($package.Name)."
    }
    $resolvedPackageVersion = $package.BaseName.Substring($packagePrefix.Length)

    $consumerRoot = Join-Path $runDirectory "consumer"
    $consumerProjectDirectory = Join-Path $consumerRoot "tests/Bing.MockData.PackageSmoke"
    $consumerBuildDirectory = Join-Path $consumerRoot "build"
    $consumerPackages = Join-Path $runDirectory "consumer-packages"
    $consumerProject = Join-Path $consumerProjectDirectory "Bing.MockData.PackageSmoke.csproj"
    $consumerLock = Join-Path $consumerProjectDirectory "packages.lock.json"
    $nugetConfigPath = Join-Path $runDirectory "NuGet.Config"
    New-Item -ItemType Directory -Path $consumerProjectDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $consumerBuildDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $consumerPackages -Force | Out-Null
    Copy-Item -LiteralPath $smokeProjectPath -Destination $consumerProjectDirectory
    Copy-Item -LiteralPath $smokeSourcePath -Destination $consumerProjectDirectory
    Copy-Item -LiteralPath $smokeLockPath -Destination $consumerProjectDirectory
    Copy-Item -LiteralPath (Join-Path $repoRoot "build/version.props") -Destination $consumerBuildDirectory
    # 即使 ArtifactsRoot 位于仓库外，消费项目也必须继承相同的审计门禁。
    Copy-Item -LiteralPath (Join-Path $repoRoot "Directory.Build.props") -Destination $consumerRoot

    $lockData = Get-Content -LiteralPath $consumerLock -Raw | ConvertFrom-Json -AsHashtable
    $foundPackage = $false
    foreach ($target in $lockData.dependencies.Values) {
        foreach ($packageId in @($target.Keys)) {
            if ($packageId -ieq "Bing.MockData") {
                $target[$packageId].requested = "[$resolvedPackageVersion, )"
                $target[$packageId].resolved = $resolvedPackageVersion
                $target[$packageId].contentHash = [Convert]::ToBase64String(
                    [Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($package.FullName)))
                $foundPackage = $true
            }
        }
    }
    if (-not $foundPackage) {
        throw "The consumer lock file does not contain Bing.MockData."
    }
    [IO.File]::WriteAllText(
        $consumerLock,
        (($lockData | ConvertTo-Json -Depth 100) + [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false))

    $packageOutputXml = [Security.SecurityElement]::Escape($packageOutput)
    $nugetSourceXml = [Security.SecurityElement]::Escape($NuGetSource)
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="current-run" value="$packageOutputXml" />
    <add key="nuget.org" value="$nugetSourceXml" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="current-run">
      <package pattern="Bing.MockData" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
    [IO.File]::WriteAllText($nugetConfigPath, $nugetConfig, [Text.UTF8Encoding]::new($false))

    $consumerVersion = "-p:BingMockDataPackageVersion=$resolvedPackageVersion"
    Invoke-DotNet @(
        "restore",
        $consumerProject,
        "--locked-mode",
        "--configfile", $nugetConfigPath,
        "--packages", $consumerPackages,
        $consumerVersion
    )
    Invoke-DotNet @(
        "build", $consumerProject,
        "--framework", "netstandard2.0",
        "--configuration", "Release", "--no-restore", $consumerVersion
    )
    Invoke-DotNet @(
        "run",
        "--project", $consumerProject,
        "--framework", "net10.0",
        "--configuration", "Release",
        "--no-restore",
        $consumerVersion
    )

    if ($Publish) {
        Invoke-DotNet @(
            "nuget", "push", $package.FullName,
            "--api-key", $env:NUGET_API_KEY,
            "--source", $NuGetSource
        )
    }

    Write-Host "Package and consumer smoke check completed: $($package.FullName)"
}
finally {
    Pop-Location
}
