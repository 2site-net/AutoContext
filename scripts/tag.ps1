#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Bump versions, compile, commit, and create an annotated release tag.

.DESCRIPTION
    Wrapper that runs the Tag pipeline by delegating to the AutoContext.Build
    module: validates the version, syncs version files, runs a full compile
    gate, commits the bump (if the version changed), and creates an annotated
    git tag. Use -Force to delete an existing local + remote tag first.

.PARAMETER Version
    The release version (X.Y.Z or X.Y.Z-prerelease).

.PARAMETER Force
    Delete the existing local tag and the matching remote tag (if any) before
    re-creating it. The bump commit, if any, is left intact.

.EXAMPLE
    .\scripts\tag.ps1 0.6.0            # Bump, compile, commit, tag
    .\scripts\tag.ps1 0.6.0-alpha      # Prerelease tag
    .\scripts\tag.ps1 0.6.0 -Force     # Re-tag (delete local + remote first)
    .\scripts\tag.ps1 0.6.0 -WhatIf    # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0, Mandatory)]
    [string]$Version,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

Invoke-Tag -Context $context -Version $Version -Force:$Force -WhatIf:$WhatIfPreference
