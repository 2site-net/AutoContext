#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Publish the extension to the Marketplace and Open VSX.

.DESCRIPTION
    Wrapper that runs the Publish pipeline (Package + vsce publish + ovsx
    publish) by delegating to the AutoContext.Build module. With no options it
    auto-detects the current platform; pass Target 'All' for all six platforms
    or -RuntimeIdentifier for a specific RID.

.PARAMETER Target
    'All' publishes all six platform targets. Omit to auto-detect the current
    platform.

.PARAMETER RuntimeIdentifier
    .NET runtime identifier (e.g. win-x64). Mutually exclusive with Target 'All'.

.EXAMPLE
    .\scripts\publish.ps1                          # Current platform
    .\scripts\publish.ps1 All                       # All 6 platforms
    .\scripts\publish.ps1 -RuntimeIdentifier win-x64
    .\scripts\publish.ps1 -WhatIf                  # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('All')]
    [string]$Target,

    [string]$RuntimeIdentifier
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($RuntimeIdentifier -and $Target -eq 'All') {
    throw "-RuntimeIdentifier and Target 'All' are mutually exclusive."
}

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

Invoke-Publish -Context $context -Scope $Target -RuntimeIdentifier $RuntimeIdentifier -WhatIf:$WhatIfPreference
