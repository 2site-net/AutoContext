#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Verify .NET code formatting (dotnet format --verify-no-changes).

.DESCRIPTION
    Inner-loop wrapper that runs the .NET format gate by delegating to the
    AutoContext.Build module. This is the same gate `build.ps1 Compile` runs
    by default; run it standalone for a quick format check. TypeScript linting
    is not wired up yet, so this covers .NET only.

.EXAMPLE
    .\scripts\format.ps1               # Verify .NET formatting
    .\scripts\format.ps1 -WhatIf       # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

Test-DotNetFormat -Context $context -WhatIf:$WhatIfPreference
