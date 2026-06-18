#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Package the extension: Prepare + dotnet publish + vsce package.

.DESCRIPTION
    Wrapper that runs the Package pipeline by delegating to the
    AutoContext.Build module. With no options it auto-detects the current
    platform and produces a single .vsix. Pass -Local for a runnable local-F5
    layout (framework-dependent server copy, no .vsix), Target 'All' for all
    six platforms, or -RuntimeIdentifier for a specific RID.

.PARAMETER Target
    'All' builds all six platform targets. Omit to auto-detect the current
    platform.

.PARAMETER Local
    Copy framework-dependent .NET build output into the extension's servers
    directory instead of running dotnet publish. No .vsix is produced.

.PARAMETER RuntimeIdentifier
    .NET runtime identifier (e.g. win-x64, osx-arm64). Mutually exclusive with
    Target 'All' and with -Local.

.EXAMPLE
    .\scripts\package.ps1                          # Current platform
    .\scripts\package.ps1 -Local                   # Local F5 layout
    .\scripts\package.ps1 All                       # All 6 platforms
    .\scripts\package.ps1 -RuntimeIdentifier win-x64
    .\scripts\package.ps1 -WhatIf                  # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('All')]
    [string]$Target,

    [switch]$Local,

    [string]$RuntimeIdentifier
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Local -and $RuntimeIdentifier) {
    throw '-Local and -RuntimeIdentifier are mutually exclusive.'
}
if ($Local -and $Target -eq 'All') {
    throw "-Local and Target 'All' are mutually exclusive."
}
if ($RuntimeIdentifier -and $Target -eq 'All') {
    throw "-RuntimeIdentifier and Target 'All' are mutually exclusive."
}

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

Invoke-Package -Context $context -Scope $Target -Local:$Local -RuntimeIdentifier $RuntimeIdentifier -WhatIf:$WhatIfPreference
