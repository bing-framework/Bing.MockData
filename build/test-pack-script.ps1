$ErrorActionPreference = "Stop"
$packScript = Join-Path $PSScriptRoot "pack.ps1"
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ("Bing.MockData-pack-tests-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $testRoot | Out-Null

$global:FakeDotNetCalls = [System.Collections.Generic.List[object]]::new()
$global:FailAtStep = $null
$global:PublishedPackages = [System.Collections.Generic.List[string]]::new()

function dotnet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

    $call = @($Arguments)
    $global:FakeDotNetCalls.Add($call)
    $global:LASTEXITCODE = 0
    $shouldFail = switch -Exact ($global:FailAtStep) {
        "solution-restore" {
            $call[0] -eq "restore" -and $call[1].EndsWith(".sln", [StringComparison]::OrdinalIgnoreCase)
        }
        "consumer-restore" {
            $call[0] -eq "restore" -and $call[1].EndsWith("Bing.MockData.PackageSmoke.csproj", [StringComparison]::OrdinalIgnoreCase)
        }
        "test" { $call[0] -eq "test" }
        "standard-test" { $call[0] -eq "test" -and $call -contains "-p:MockDataTargetFramework=netstandard2.0" }
        "library-build" { $call[0] -eq "build" -and $call[1].EndsWith("Bing.MockData.csproj", [StringComparison]::OrdinalIgnoreCase) }
        "consumer-build" { $call[0] -eq "build" -and $call[1].EndsWith("Bing.MockData.PackageSmoke.csproj", [StringComparison]::OrdinalIgnoreCase) }
        "pack" { $call[0] -eq "pack" }
        "consumer-run" { $call[0] -eq "run" }
        "push" { $call[0] -eq "nuget" -and $call[1] -eq "push" }
        default { $false }
    }
    if ($shouldFail) {
        $global:LASTEXITCODE = 7
        return
    }

    if ($call[0] -eq "pack") {
        $outputIndex = [Array]::IndexOf($call, "--output")
        $version = "0.0.1-preview-20190114-1"
        foreach ($argument in $call) {
            if ($argument.StartsWith("-p:PackageVersion=")) {
                $version = $argument.Substring("-p:PackageVersion=".Length)
            }
        }
        $packageDirectory = $call[$outputIndex + 1]
        [IO.Directory]::CreateDirectory($packageDirectory) | Out-Null
        $zip = [IO.Compression.ZipFile]::Open((Join-Path $packageDirectory "Bing.MockData.$version.nupkg"), [IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($framework in @("netstandard2.0", "net10.0")) {
                foreach ($extension in @("dll", "xml")) {
                    if ($global:FailAtStep -eq "missing-asset" -and $framework -eq "netstandard2.0" -and $extension -eq "xml") { continue }
                    $zip.CreateEntry("lib/$framework/Bing.MockData.$extension") | Out-Null
                }
            }
        }
        finally { $zip.Dispose() }
    }

    if ($call.Count -ge 3 -and $call[0] -eq "nuget" -and $call[1] -eq "push") {
        $global:PublishedPackages.Add($call[2])
    }
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Equal {
    param($Expected, $Actual, [string]$Message)
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Invoke-PackScript {
    param(
        [switch]$Publish,
        [string]$PackageVersion,
        [Parameter(Mandatory)][string]$ArtifactsRoot
    )
    $scriptArguments = @{ ArtifactsRoot = $ArtifactsRoot }
    if ($Publish) {
        $scriptArguments.Publish = $true
    }
    if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
        $scriptArguments.PackageVersion = $PackageVersion
    }
    & $packScript @scriptArguments 2>&1 | Out-String
}

function Get-CommandNames {
    @($global:FakeDotNetCalls | ForEach-Object { $_[0] })
}

function Get-ConsumerRestoreCall {
    @($global:FakeDotNetCalls | Where-Object {
        $_[0] -eq "restore" -and $_[1].EndsWith("Bing.MockData.PackageSmoke.csproj", [StringComparison]::OrdinalIgnoreCase)
    }) | Select-Object -First 1
}

function Get-ArgumentValue {
    param([object[]]$Arguments, [string]$Name)
    $index = [Array]::IndexOf($Arguments, $Name)
    if ($index -lt 0 -or $index + 1 -ge $Arguments.Count) {
        throw "Command is missing $Name."
    }
    $Arguments[$index + 1]
}

function Reset-Fake {
    $global:FakeDotNetCalls.Clear()
    $global:PublishedPackages.Clear()
    $global:FailAtStep = $null
    $global:LASTEXITCODE = 0
}

function Assert-FailsAt {
    param(
        [Parameter(Mandatory)][string]$Step,
        [Parameter(Mandatory)][string]$ExpectedCommands,
        [switch]$Publish
    )
    Reset-Fake
    $global:FailAtStep = $Step
    $failed = $false
    $output = ""
    try {
        $output = Invoke-PackScript -Publish:$Publish -ArtifactsRoot $testRoot
    }
    catch {
        $failed = $true
        $output += $_ | Out-String
    }
    Assert-True $failed "A failed $Step command must fail the script."
    Assert-Equal $ExpectedCommands ((Get-CommandNames) -join ",") "$Step must stop later commands."
    Assert-Equal 0 $global:PublishedPackages.Count "$Step failure must not publish a package."
    Assert-True (-not $output.Contains($env:NUGET_API_KEY)) "A failed run wrote the API key to output."
}

try {
    $env:NUGET_API_KEY = "substitute-key-for-script-tests"

    Push-Location $testRoot
    try {
        $defaultOutput = Invoke-PackScript -ArtifactsRoot $testRoot
    }
    finally {
        Pop-Location
    }

    Assert-Equal "restore,test,test,build,pack,restore,build,run" ((Get-CommandNames) -join ",") "Default command order changed."
    Assert-Equal 0 $global:PublishedPackages.Count "Default execution must not publish."
    Assert-True (-not $defaultOutput.Contains($env:NUGET_API_KEY)) "The API key was written to output."

    $expectedSolution = Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "Bing.MockData.sln"
    $restoreCall = $global:FakeDotNetCalls[0]
    Assert-Equal $expectedSolution $restoreCall[1] "The script must resolve its root independently of the caller's working directory."
    Assert-True ($restoreCall -contains "--locked-mode") "Solution restore must use locked mode."
    Assert-True ($global:FakeDotNetCalls[1] -contains "--no-restore") "Tests must reuse the locked restore."

    $packCall = $global:FakeDotNetCalls[4]
    $packageOutput = Get-ArgumentValue $packCall "--output"
    Assert-True ($packageOutput.StartsWith((Join-Path $testRoot "package-runs"), [StringComparison]::OrdinalIgnoreCase)) "Packages must use an isolated run directory."

    $consumerRestore = Get-ConsumerRestoreCall
    Assert-True ($consumerRestore[1].StartsWith((Join-Path $testRoot "package-runs"), [StringComparison]::OrdinalIgnoreCase)) "The consumer project must be isolated under this run."
    Assert-True ($consumerRestore -contains "--locked-mode") "Consumer restore must use locked mode."
    $consumerPackages = Get-ArgumentValue $consumerRestore "--packages"
    Assert-True ($consumerPackages.StartsWith((Join-Path $testRoot "package-runs"), [StringComparison]::OrdinalIgnoreCase)) "Consumer restore must use an isolated package cache."
    $copiedPropsPath = Join-Path (Split-Path (Split-Path (Split-Path $consumerRestore[1]))) "Directory.Build.props"
    Assert-Equal (Get-Content -LiteralPath (Join-Path $PSScriptRoot "../Directory.Build.props") -Raw) (Get-Content -LiteralPath $copiedPropsPath -Raw) "External artifact roots must retain audit gates."
    $configPath = Get-ArgumentValue $consumerRestore "--configfile"
    [xml]$nugetConfig = Get-Content -LiteralPath $configPath -Raw
    Assert-Equal $packageOutput $nugetConfig.configuration.packageSources.add[0].value "Current package feed mapping changed."
    Assert-Equal "Bing.MockData" $nugetConfig.configuration.packageSourceMapping.packageSource[0].package.pattern "The current package must map only to this run's feed."
    Assert-Equal "*" $nugetConfig.configuration.packageSourceMapping.packageSource[1].package.pattern "Third-party packages must retain the NuGet source."

    $temporaryLockPath = Join-Path (Split-Path $consumerRestore[1]) "packages.lock.json"
    $temporaryLock = Get-Content -LiteralPath $temporaryLockPath -Raw | ConvertFrom-Json -AsHashtable
    $trackedLock = Get-Content -LiteralPath (Join-Path $PSScriptRoot "../tests/Bing.MockData.PackageSmoke/packages.lock.json") -Raw | ConvertFrom-Json -AsHashtable
    $lockTarget = $temporaryLock.dependencies["net10.0"]
    $trackedTarget = $trackedLock.dependencies["net10.0"]
    foreach ($thirdParty in @("Fare", "Microsoft.NETCore.Platforms", "NETStandard.Library", "NLipsum", "Newtonsoft.Json")) {
        Assert-Equal $trackedTarget[$thirdParty].resolved $lockTarget[$thirdParty].resolved "Consumer lock changed $thirdParty resolution."
        Assert-Equal $trackedTarget[$thirdParty].contentHash $lockTarget[$thirdParty].contentHash "Consumer lock changed $thirdParty content hash."
    }
    $expectedPackageHash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes((Join-Path $packageOutput "Bing.MockData.0.0.1-preview-20190114-1.nupkg"))))
    Assert-Equal $expectedPackageHash $lockTarget["Bing.MockData"].contentHash "Consumer lock must pin the current package bytes."

    $stalePackage = Join-Path $testRoot "package-runs/prior-run/packages/Bing.MockData.1.2.3.nupkg"
    New-Item -ItemType Directory -Path (Split-Path $stalePackage) -Force | Out-Null
    [IO.File]::WriteAllBytes($stalePackage, [byte[]]@(80, 75, 3, 4))
    Reset-Fake
    $publishOutput = Invoke-PackScript -Publish -PackageVersion "7.8.9-test" -ArtifactsRoot $testRoot
    Assert-Equal "restore,test,test,build,pack,restore,build,run,nuget" ((Get-CommandNames) -join ",") "Publish command order changed."
    Assert-Equal 1 $global:PublishedPackages.Count "Explicit publish must push exactly one package."
    $publishPackageOutput = Get-ArgumentValue $global:FakeDotNetCalls[4] "--output"
    $expectedPackage = Join-Path $publishPackageOutput "Bing.MockData.7.8.9-test.nupkg"
    Assert-Equal $expectedPackage $global:PublishedPackages[0] "Publish must use only the current run's package."
    Assert-True (Test-Path -LiteralPath $stalePackage) "An earlier package output must remain untouched."
    $pushCall = $global:FakeDotNetCalls[8]
    $apiKeyIndex = [Array]::IndexOf($pushCall, "--api-key")
    Assert-Equal $env:NUGET_API_KEY $pushCall[$apiKeyIndex + 1] "The environment key must be passed to NuGet."
    Assert-True (-not $publishOutput.Contains($env:NUGET_API_KEY)) "The API key was written to output."

    Assert-FailsAt -Step "solution-restore" -ExpectedCommands "restore"
    Assert-FailsAt -Step "test" -ExpectedCommands "restore,test" -Publish
    Assert-FailsAt -Step "standard-test" -ExpectedCommands "restore,test,test" -Publish
    Assert-FailsAt -Step "library-build" -ExpectedCommands "restore,test,test,build" -Publish
    Assert-FailsAt -Step "consumer-build" -ExpectedCommands "restore,test,test,build,pack,restore,build" -Publish
    Assert-FailsAt -Step "missing-asset" -ExpectedCommands "restore,test,test,build,pack" -Publish
    Assert-FailsAt -Step "pack" -ExpectedCommands "restore,test,test,build,pack" -Publish
    Assert-FailsAt -Step "consumer-restore" -ExpectedCommands "restore,test,test,build,pack,restore" -Publish
    Assert-FailsAt -Step "consumer-run" -ExpectedCommands "restore,test,test,build,pack,restore,build,run" -Publish
    Assert-FailsAt -Step "push" -ExpectedCommands "restore,test,test,build,pack,restore,build,run,nuget" -Publish

    Reset-Fake
    $env:NUGET_API_KEY = $null
    $missingKeyFailed = $false
    try {
        Invoke-PackScript -Publish -ArtifactsRoot $testRoot | Out-Null
    }
    catch {
        $missingKeyFailed = $true
    }
    Assert-True $missingKeyFailed "Publishing without NUGET_API_KEY must fail."
    Assert-Equal 0 $global:FakeDotNetCalls.Count "A missing API key must fail before invoking dotnet."

    Write-Host "Pack script substitute regression checks passed."
}
finally {
    $safeTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $safeTestRoot = [IO.Path]::GetFullPath($testRoot)
    $testPrefix = "Bing.MockData-pack-tests-"
    if ($safeTestRoot.StartsWith($safeTempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($safeTestRoot).StartsWith($testPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $safeTestRoot -Recurse -Force
    }
}
