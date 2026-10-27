[CmdletBinding()]
param([string]$ArtifactsRoot = "artifacts/domain-pack", [string]$DotnetCommand = "dotnet", [switch]$SkipTests, [string]$DataRoot)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $PSBoundParameters.ContainsKey('DataRoot')) {
    $DataRoot = if ($null -ne $env:MOCKDATA_DATA_ROOT) { $env:MOCKDATA_DATA_ROOT } else { Join-Path $repo 'src/Bing.MockData.Catalogs/Data' }
}
if ([string]::IsNullOrWhiteSpace($DataRoot) -or -not (Test-Path -LiteralPath $DataRoot -PathType Container)) { throw 'Explicit catalog DataRoot is invalid.' }
$data = (Resolve-Path -LiteralPath $DataRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $data 'catalogs.manifest.json') -PathType Leaf)) { throw 'Catalog DataRoot is missing catalogs.manifest.json.' }
if (-not [IO.Path]::IsPathRooted($ArtifactsRoot)) { $ArtifactsRoot = Join-Path $repo $ArtifactsRoot }
$run = Join-Path $ArtifactsRoot ([Guid]::NewGuid().ToString("N"))
$packages = Join-Path $run "packages"
New-Item -ItemType Directory -Force $packages | Out-Null
function Invoke-DotnetChecked([string[]]$Arguments) {
    & $DotnetCommand @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE" }
}
$names = @("Bing.MockData", "Bing.MockData.Catalogs", "Bing.MockData.Schema", "Bing.MockData.Transport", "Bing.MockData.Education", "Bing.MockData.Healthcare", "Bing.MockData.Devices")
$previousDataRoot = $env:MOCKDATA_DATA_ROOT
$previousEvidence = $env:MOCKDATA_EVIDENCE_DIRECTORY
$env:MOCKDATA_DATA_ROOT = $data
$env:MOCKDATA_EVIDENCE_DIRECTORY = Join-Path $run 'reports'
New-Item -ItemType Directory -Force $env:MOCKDATA_EVIDENCE_DIRECTORY | Out-Null
Push-Location $repo
try {
    $sourceFiles = @(Get-ChildItem src,tests,build,.github -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj|TestResults)[\\/]' -and $_.Extension -in '.cs','.csproj','.props','.targets','.json','.txt','.ps1','.yml'
    } | Sort-Object FullName | ForEach-Object {
        [PSCustomObject]@{ Path=[IO.Path]::GetRelativePath($repo,$_.FullName).Replace('\','/'); SHA256=(Get-FileHash $_.FullName).Hash }
    })
    $candidateText = ($sourceFiles | ForEach-Object { $_.Path + ':' + $_.SHA256 }) -join "`n"
    $sourceHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($candidateText)))
    [PSCustomObject]@{ BaseCommit=(& git rev-parse HEAD); WorkingTree=(& git status --porcelain); SourceScopeSHA256=$sourceHash; SourceFiles=$sourceFiles; DataRoot=$data; DataManifestSHA256=(Get-FileHash (Join-Path $data 'catalogs.manifest.json')).Hash } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $run 'candidate.json')
    Invoke-DotnetChecked @("restore", "Bing.MockData.sln", "--locked-mode")
    if (-not $SkipTests) {
        Invoke-DotnetChecked @("test", "Bing.MockData.sln", "-c", "Release", "--no-restore", "--logger", "trx;LogFilePrefix=modern", "--results-directory", $env:MOCKDATA_EVIDENCE_DIRECTORY)
        foreach ($test in @("Bing.MockData.Tests", "Bing.MockData.Catalogs.Tests", "Bing.MockData.Schema.Tests", "Bing.MockData.Domains.Tests")) {
            Invoke-DotnetChecked @("test", "tests/$test/$test.csproj", "-c", "Release", "-p:MockDataTargetFramework=netstandard2.0", "--logger", "trx;LogFileName=$test-standard.trx", "--results-directory", $env:MOCKDATA_EVIDENCE_DIRECTORY)
        }
    }
    $versions = @{}
    foreach ($name in $names) {
        $project = "src/$name/$name.csproj"
        Invoke-DotnetChecked @("build", $project, "-c", "Release", "--no-restore")
        Invoke-DotnetChecked @("pack", $project, "-c", "Release", "--no-build", "--no-restore", "-o", $packages)
    }
    foreach ($file in Get-ChildItem $packages -Filter '*.nupkg') {
        $zip = [IO.Compression.ZipFile]::OpenRead($file.FullName)
        try {
            $nuspec = $zip.Entries | Where-Object FullName -Like '*.nuspec' | Select-Object -First 1
            $reader = [IO.StreamReader]::new($nuspec.Open())
            try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $id = $spec.package.metadata.id
            $versions[$id] = $spec.package.metadata.version
            if ($id -eq 'Bing.MockData') {
                foreach ($notice in @('licenses/areacodes-CC0.txt/LICENSE','licenses/faker-MIT.txt','licenses/cldr-Unicode-3.0.txt','licenses/geonames-attribution.txt','licenses/iban-js-MIT.txt','licenses/w3c-color4.txt','licenses/bankcardinfo-ISC.txt','licenses/bankcardinfo-NOTICE.txt')) {
                    if ($null -eq $zip.GetEntry($notice)) { throw "Core package missing required data license: $notice" }
                }
            }
            foreach ($tfm in @("netstandard2.0", "net10.0")) {
                foreach ($ext in @("dll", "xml")) {
                    if ($null -eq $zip.GetEntry("lib/$tfm/$id.$ext")) { throw "$id missing $tfm $ext" }
                }
            }
        } finally { $zip.Dispose() }
    }
    if ($versions.Count -ne $names.Count) { throw "Expected seven independently consumable code packages." }
    # Offline data is distributed by pack-data.ps1 and restored independently.
    foreach ($tfm in @("netstandard2.0", "net10.0")) {
        $consumer = Join-Path $run "consumer-$tfm"
        New-Item -ItemType Directory -Force $consumer | Out-Null
        $references = ($names | ForEach-Object { '<PackageReference Include="' + $_ + '" Version="' + $versions[$_] + '" />' }) -join "`n"
        $outputType = if ($tfm -eq "net10.0") { "Exe" } else { "Library" }
        $project = "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>$tfm</TargetFramework><LangVersion>latest</LangVersion><OutputType>$outputType</OutputType><NuGetAudit>true</NuGetAudit><WarningsAsErrors>NU1901;NU1902;NU1903;NU1904</WarningsAsErrors></PropertyGroup><ItemGroup>$references</ItemGroup></Project>"
        [IO.File]::WriteAllText((Join-Path $consumer "Consumer.csproj"), $project)
        Copy-Item "tests/Bing.MockData.DomainPackageSmoke/Program.cs" (Join-Path $consumer "Program.cs")
        $config = Join-Path $consumer "NuGet.Config"
        $escapedPackages = [Security.SecurityElement]::Escape($packages)
        [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key=`"local`" value=`"$escapedPackages`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>")
        Invoke-DotnetChecked @("restore", (Join-Path $consumer "Consumer.csproj"), "--configfile", $config, "--packages", (Join-Path $run "nuget-cache"))
        Invoke-DotnetChecked @("build", (Join-Path $consumer "Consumer.csproj"), "-c", "Release", "--no-restore")
        if ($tfm -eq "net10.0") { Invoke-DotnetChecked @("run", "--project", (Join-Path $consumer "Consumer.csproj"), "-c", "Release", "--no-build", "--", $data) }
    }
    [PSCustomObject]@{ Status="PASS"; Run=$run; DataRoot=$data; Tests=($(if ($SkipTests) {'SKIPPED'} else {'PASS'})); Versions=$versions; Runtime=(& $DotnetCommand --version); Packages=@(Get-ChildItem $packages -Filter '*.nupkg' | ForEach-Object { [PSCustomObject]@{Name=$_.Name;SHA256=(Get-FileHash $_.FullName).Hash} }) } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run "evidence.json")
    Write-Output "Domain package gate PASS: $run"
} catch {
    [PSCustomObject]@{ Status='FAIL'; Error=$_.Exception.Message; DataRoot=$data; Run=$run } | ConvertTo-Json | Set-Content (Join-Path $run 'evidence.json')
    throw
} finally {
    $env:MOCKDATA_DATA_ROOT = $previousDataRoot
    $env:MOCKDATA_EVIDENCE_DIRECTORY = $previousEvidence
    Pop-Location
}
