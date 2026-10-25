[CmdletBinding()]
param(
    [string]$InputPath = (Join-Path $PSScriptRoot "data/yescallop-areacodes-2da87e7/2025.txt"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "../src/Bing.MockData/Datas/Resources/ChinaDivisions2025.json")
)

$ErrorActionPreference = "Stop"

$inputFile = (Resolve-Path -LiteralPath $InputPath).Path
$outputFile = [IO.Path]::GetFullPath($OutputPath)
$rows = [Collections.Generic.List[object]]::new()
$byCode = [Collections.Generic.Dictionary[string, object]]::new([StringComparer]::Ordinal)

foreach ($line in [IO.File]::ReadAllLines($inputFile, [Text.Encoding]::UTF8)) {
    if ($line -notmatch '^\s*(?<code>[0-9]{6})\s+(?<name>.+?)\s*$') {
        throw "Invalid division row: '$line'"
    }

    $code = $Matches.code
    if ($byCode.ContainsKey($code)) {
        throw "Duplicate division code: $code"
    }

    $level = if ($code.EndsWith("0000", [StringComparison]::Ordinal)) { 1 }
        elseif ($code.EndsWith("00", [StringComparison]::Ordinal)) { 2 }
        else { 3 }

    $row = [pscustomobject]@{
        Code = $code
        Name = $Matches.name
        Level = $level
        ParentCode = $null
    }
    $rows.Add($row)
    $byCode.Add($code, $row)
}

if ($rows.Count -ne 3212) {
    throw "Expected 3212 source rows; found $($rows.Count)."
}

foreach ($row in $rows) {
    if ($row.Level -eq 1) { continue }

    $provinceCode = $row.Code.Substring(0, 2) + "0000"
    if (-not $byCode.ContainsKey($provinceCode) -or $byCode[$provinceCode].Level -ne 1) {
        throw "Missing province parent '$provinceCode' for '$($row.Code)'."
    }

    if ($row.Level -eq 2) {
        $row.ParentCode = $provinceCode
        continue
    }

    $prefectureCode = $row.Code.Substring(0, 4) + "00"
    if ($byCode.ContainsKey($prefectureCode) -and $byCode[$prefectureCode].Level -eq 2) {
        $row.ParentCode = $prefectureCode
    }
    else {
        $row.ParentCode = $provinceCode
    }
}

foreach ($row in $rows) {
    if ($null -ne $row.ParentCode) {
        if (-not $byCode.ContainsKey($row.ParentCode)) {
            throw "Orphan division '$($row.Code)' references '$($row.ParentCode)'."
        }
        if ($byCode[$row.ParentCode].Level -ge $row.Level) {
            throw "Invalid parent level for '$($row.Code)' -> '$($row.ParentCode)'."
        }
    }

    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $current = $row
    while ($null -ne $current.ParentCode) {
        if (-not $seen.Add($current.Code)) {
            throw "Cycle detected at division '$($current.Code)'."
        }
        $current = $byCode[$current.ParentCode]
    }
}

foreach ($prefix in @("71", "81", "82")) {
    $rootCode = $prefix + "0000"
    if (-not $byCode.ContainsKey($rootCode) -or @($rows | Where-Object { $_.Code.StartsWith($prefix, [StringComparison]::Ordinal) }).Count -ne 1) {
        throw "Expected '$rootCode' to be the only source row for prefix '$prefix'."
    }
}

$outputDirectory = Split-Path -Parent $outputFile
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$json = ConvertTo-Json -InputObject @($rows) -Depth 4
[IO.File]::WriteAllText($outputFile, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
Write-Host "Wrote $($rows.Count) divisions to $outputFile"
