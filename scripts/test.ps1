#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Run AutoContext unit tests (TypeScript and/or .NET).

.DESCRIPTION
    Fast inner-loop wrapper that runs the TypeScript (vitest) and/or .NET
    (dotnet test) unit suites by delegating to the AutoContext.Build module.

    NOTE: The .NET suite runs with `--no-build`, so it assumes a prior
    compile. Run scripts/compile.ps1 (or build.ps1 Compile) first if the
    output is stale.

.PARAMETER Target
    Which stack to test: TS (alias TypeScript), DotNet (alias .NET),
    or All (default).

.EXAMPLE
    .\scripts\test.ps1                 # Run both test suites
    .\scripts\test.ps1 TS              # TypeScript tests only
    .\scripts\test.ps1 DotNet          # .NET tests only
    .\scripts\test.ps1 -WhatIf         # Preview
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

if ($Target -in 'All', 'TS')     { Test-TypeScript -Context $context -WhatIf:$WhatIfPreference }
if ($Target -in 'All', 'DotNet') { Test-DotNet -Context $context -WhatIf:$WhatIfPreference }
