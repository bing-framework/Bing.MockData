[CmdletBinding()]
param(
    [string]$DataRoot = (Join-Path $PSScriptRoot '../src/Bing.MockData.Catalogs/Data'),
    [Parameter(Mandatory)][string]$Archive,
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($DataRoot)) { throw 'Explicit catalog DataRoot is invalid.' }
& python (Join-Path $PSScriptRoot '../tools/CatalogBundle/catalog_bundle.py') pack --data-root $DataRoot --archive $Archive --manifest $Manifest --version $Version
if ($LASTEXITCODE -ne 0) { throw "Catalog data pack failed with exit code $LASTEXITCODE" }
