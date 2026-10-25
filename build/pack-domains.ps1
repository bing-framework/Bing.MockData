[CmdletBinding()]
param([string]$ArtifactsRoot = "artifacts/domain-pack", [string]$DotnetCommand = "dotnet", [switch]$SkipTests)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not [IO.Path]::IsPathRooted($ArtifactsRoot)) { $ArtifactsRoot = Join-Path $repo $ArtifactsRoot }
$run = Join-Path $ArtifactsRoot ([Guid]::NewGuid().ToString("N"))
$packages = Join-Path $run "packages"
New-Item -ItemType Directory -Force $packages | Out-Null
function Invoke-DotnetChecked([string[]]$Arguments) {
    & $DotnetCommand @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE" }
}
$names = @("Bing.MockData", "Bing.MockData.Catalogs", "Bing.MockData.Schema", "Bing.MockData.Transport", "Bing.MockData.Education", "Bing.MockData.Healthcare", "Bing.MockData.Devices")
Push-Location $repo
try {
    Invoke-DotnetChecked @("restore", "Bing.MockData.sln", "--locked-mode")
    if (-not $SkipTests) {
        Invoke-DotnetChecked @("test", "Bing.MockData.sln", "-c", "Release", "--no-restore")
        foreach ($test in @("Bing.MockData.Tests", "Bing.MockData.Catalogs.Tests", "Bing.MockData.Schema.Tests", "Bing.MockData.Domains.Tests")) {
            Invoke-DotnetChecked @("test", "tests/$test/$test.csproj", "-c", "Release", "-p:MockDataTargetFramework=netstandard2.0")
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
            foreach ($tfm in @("netstandard2.0", "net10.0")) {
                foreach ($ext in @("dll", "xml")) {
                    if ($null -eq $zip.GetEntry("lib/$tfm/$id.$ext")) { throw "$id missing $tfm $ext" }
                }
            }
        } finally { $zip.Dispose() }
    }
    if ($versions.Count -ne $names.Count) { throw "Expected seven independently consumable code packages." }
    $data = Join-Path $repo "src/Bing.MockData.Catalogs/Data"
    [IO.Compression.ZipFile]::CreateFromDirectory($data, (Join-Path $run "catalog-data.zip"))
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
    [PSCustomObject]@{ Status="PASS"; Run=$run; Versions=$versions; Runtime=(& $DotnetCommand --version); Packages=@(Get-ChildItem $packages -Filter '*.nupkg' | ForEach-Object { [PSCustomObject]@{Name=$_.Name;SHA256=(Get-FileHash $_.FullName).Hash} }) } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $run "evidence.json")
    Write-Output "Domain package gate PASS: $run"
} finally { Pop-Location }
