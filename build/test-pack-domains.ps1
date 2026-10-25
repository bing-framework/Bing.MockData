$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('Bing.MockData-domain-gate-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$global:DomainGateCalls = [Collections.Generic.List[string]]::new()
function Fake-DomainDotnet {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments)
    $global:DomainGateCalls.Add(($Arguments -join ' '))
    $global:LASTEXITCODE = if ($Arguments[0] -eq $global:DomainGateFail) { 17 } else { 0 }
}
try {
    foreach ($step in @('restore','test','build','pack')) {
        $global:DomainGateFail = $step
        $global:DomainGateCalls.Clear()
        $failed = $false
        try { & (Join-Path $PSScriptRoot 'pack-domains.ps1') -ArtifactsRoot $root -DotnetCommand Fake-DomainDotnet }
        catch { if ($_.Exception.Message -notlike "dotnet $step failed*") { throw }; $failed=$true }
        if (-not $failed -or $global:DomainGateCalls[-1] -notlike "$step *") { throw "Gate did not stop at $step" }
        if (($global:DomainGateCalls | Where-Object { $_ -like "$step *" }).Count -ne 1) { throw "Gate continued after $step failure" }
    }
    'PASS: restore, test, build and pack failures stop immediately.'
}
finally {
    $resolved = (Resolve-Path -LiteralPath $root).Path
    if (-not $resolved.StartsWith([IO.Path]::GetTempPath(),[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
    Remove-Item Function:Fake-DomainDotnet
}
