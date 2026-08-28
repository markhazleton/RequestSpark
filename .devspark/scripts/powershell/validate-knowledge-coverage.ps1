#!/usr/bin/env pwsh

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$FeatureDir,
    [switch]$Json,
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

if ($Help) {
    Write-Output "Usage: ./validate-knowledge-coverage.ps1 -FeatureDir <path> [-Json]"
    exit 0
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
try {
    $repoRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "git unavailable"
    }
} catch {
    $repoRoot = (Resolve-Path (Join-Path $scriptDir "../..")).Path
}

$knowledgeScript = Join-Path $repoRoot 'src/devspark_cli/_knowledge.py'
$argsList = @($knowledgeScript, '--feature-dir', $FeatureDir)
if ($Json) {
    $argsList += '--json'
}

python @argsList
exit $LASTEXITCODE
