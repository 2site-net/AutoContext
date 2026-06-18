#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Compile AutoContext sources (compile only — no tests, no format gate).

.DESCRIPTION
    Fast inner-loop wrapper that compiles the TypeScript extension and/or the
    .NET solution by delegating to the AutoContext.Build module.

    NOTE: This is COMPILE-ONLY and intentionally differs from
    `build.ps1 Compile`, which is a composite that also verifies .NET
    formatting and runs unit tests. Use this script for quick syntax checks;
    run `build.ps1 Compile` (or scripts/test.ps1 + scripts/format.ps1)
    before declaring work done.

.PARAMETER Target
    Which stack to compile: TS (alias TypeScript), DotNet (alias .NET),
    or All (default).

.EXAMPLE
    .\scripts\compile.ps1                 # Compile both stacks
    .\scripts\compile.ps1 TS              # Compile TypeScript only
    .\scripts\compile.ps1 DotNet          # Compile .NET only
    .\scripts\compile.ps1 -WhatIf         # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('All', 'TS', 'TypeScript', 'DotNet', '.NET')]
    [string]$Target = 'All'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

if ($Target -eq 'TypeScript') { $Target = 'TS' }
if ($Target -eq '.NET')       { $Target = 'DotNet' }

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

if ($Target -in 'All', 'TS')     { Build-TypeScript -Context $context -WhatIf:$WhatIfPreference }
if ($Target -in 'All', 'DotNet') { Build-DotNet -Context $context -WhatIf:$WhatIfPreference }
