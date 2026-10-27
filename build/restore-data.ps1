[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Manifest,
    [Parameter(Mandatory)][string]$Destination,
    [Parameter(Mandatory)][string]$Version,
    [string]$Report = 'artifacts/catalog-closure/restore.json'
)
$ErrorActionPreference = 'Stop'
& python (Join-Path $PSScriptRoot '../tools/CatalogBundle/catalog_bundle.py') restore --source $Source --manifest $Manifest --destination $Destination --version $Version --report $Report
if ($LASTEXITCODE -ne 0) { throw "Catalog data restore failed with exit code $LASTEXITCODE" }
