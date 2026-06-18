#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Prepare the extension: Clean + Compile + copy assets.

.DESCRIPTION
    Wrapper that runs the full Prepare pipeline (clean, version sync, compile
    both stacks with format gate + unit tests, then copy assets into the
    extension folder) by delegating to the AutoContext.Build module.

.EXAMPLE
    .\scripts\prepare.ps1              # Clean + Compile + copy assets
    .\scripts\prepare.ps1 -WhatIf      # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

Invoke-Prepare -Context $context -WhatIf:$WhatIfPreference
