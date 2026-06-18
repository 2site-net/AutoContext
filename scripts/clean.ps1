#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Delete AutoContext build artifacts.

.DESCRIPTION
    Wrapper that removes TypeScript output, .NET bin/obj, staged servers, and
    incremental build caches by delegating to the AutoContext.Build module.

.EXAMPLE
    .\scripts\clean.ps1                # Delete all build artifacts
    .\scripts\clean.ps1 -WhatIf        # Preview what would be deleted
#>

[CmdletBinding(SupportsShouldProcess)]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

Invoke-Clean -Context $context -WhatIf:$WhatIfPreference
