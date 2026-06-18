#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Run AutoContext tests (TypeScript and/or .NET; unit and/or smoke).

.DESCRIPTION
    Fast inner-loop wrapper that runs the TypeScript (vitest) and/or .NET
    (dotnet test) unit suites by delegating to the AutoContext.Build module.
    Pass -Smoke to instead stage the packaged extension layout and run the
    smoke suites.

    NOTE: The unit suites run with `--no-build`, so they assume a prior
    compile. Run scripts/compile.ps1 (or build.ps1) first if the output is
    stale. The -Smoke path is self-staging: it runs the full Package -Local
    pipeline (clean, version sync, compile/lint/test gate, and a
    framework-dependent server copy) before the smoke suites, so it does not
    assume a prior compile.

.PARAMETER Target
    Which stack to test: TS (alias TypeScript), DotNet (alias .NET),
    or All (default). Without -Smoke it scopes the unit suites. With -Smoke
    the staging always builds both stacks (the packaged layout needs them);
    Target then only selects which smoke suite runs: TS selects the VS Code
    smoke suite, DotNet selects the .NET smoke suite.

.PARAMETER Smoke
    Stage the packaged extension layout (Package -Local) and run the smoke
    suites, scoped by Target. Replaces the unit-only run.

.EXAMPLE
    .\scripts\test.ps1                 # Unit tests, both stacks
    .\scripts\test.ps1 TS              # TypeScript unit tests only
    .\scripts\test.ps1 DotNet          # .NET unit tests only
    .\scripts\test.ps1 -Smoke          # Unit + smoke, both stacks
    .\scripts\test.ps1 TS -Smoke       # TS unit + VS Code smoke
    .\scripts\test.ps1 DotNet -Smoke   # .NET unit + .NET smoke
    .\scripts\test.ps1 -WhatIf         # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('All', 'TS', 'TypeScript', 'DotNet', '.NET')]
    [string]$Target = 'All',

    [switch]$Smoke
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

if ($Target -eq 'TypeScript') { $Target = 'TS' }
if ($Target -eq '.NET')       { $Target = 'DotNet' }

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

if ($Smoke) {
    Invoke-Smoke -Context $context -Scope $Target -WhatIf:$WhatIfPreference
}
else {
    if ($Target -in 'All', 'TS')     { Test-TypeScript -Context $context -WhatIf:$WhatIfPreference }
    if ($Target -in 'All', 'DotNet') { Test-DotNet -Context $context -WhatIf:$WhatIfPreference }
}
