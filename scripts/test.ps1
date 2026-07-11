#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Run AutoContext tests (TypeScript and/or .NET; unit and/or smoke).

.DESCRIPTION
    Inner-loop wrapper that compiles and runs the TypeScript (vitest) and/or
    .NET (dotnet test) unit suites by delegating to the AutoContext.Build
    module. Pass -Smoke to instead stage the packaged extension layout and run
    the smoke suites.

    NOTE: By default the unit path compiles the selected stack(s) first, so
    the `--no-build` test run never executes against stale output. Pass
    -NoCompile to skip the compile when you know the output is already fresh
    (e.g. immediately after scripts/compile.ps1 or build.ps1). The -Smoke path
    is self-staging: it runs the full Package -Local pipeline (clean, version
    sync, compile/lint/test gate, and a framework-dependent server copy)
    before the smoke suites, so -NoCompile does not apply to it.

.PARAMETER Target
    Which stack to test: TS (alias TypeScript), DotNet (alias .NET),
    or All (default). Without -Smoke it scopes the compile and the unit
    suites. With -Smoke the staging always builds both stacks (the packaged
    layout needs them); Target then only selects which smoke suite runs: TS
    selects the VS Code smoke suite, DotNet selects the .NET smoke suite.

.PARAMETER NoCompile
    Skip the pre-test compile and run the unit suites against the existing
    build output. Ignored when -Smoke is set (the smoke path always stages).

.PARAMETER Smoke
    Stage the packaged extension layout (Package -Local) and run the smoke
    suites, scoped by Target. Replaces the unit-only run.

.PARAMETER Times
    Repeat the selected test run this many times (default 1). The compile
    (unit) or the staging (smoke) happens once up front; only the test run
    repeats. Iterations continue past failures so an intermittent failure
    surfaces as a rate, and a summary reports which iterations failed. Use
    it to hunt flaky tests, e.g. -Times 40.

.EXAMPLE
    .\scripts\test.ps1                 # Compile + unit tests, both stacks
    .\scripts\test.ps1 TS              # Compile + unit tests, TypeScript only
    .\scripts\test.ps1 DotNet          # Compile + unit tests, .NET only
    .\scripts\test.ps1 -NoCompile      # Unit tests only (assume fresh build)
    .\scripts\test.ps1 DotNet -Times 40   # Compile once, run .NET units 40x
    .\scripts\test.ps1 -Smoke          # Stage + smoke, both stacks
    .\scripts\test.ps1 TS -Smoke       # Stage + VS Code smoke
    .\scripts\test.ps1 DotNet -Smoke   # Stage + .NET smoke
    .\scripts\test.ps1 DotNet -Smoke -Times 40  # Stage once, run .NET smoke 40x
    .\scripts\test.ps1 -WhatIf         # Preview
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('All', 'TS', 'TypeScript', 'DotNet', '.NET')]
    [string]$Target = 'All',

    [switch]$NoCompile,

    [switch]$Smoke,

    [ValidateRange(1, 100000)]
    [int]$Times = 1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

if ($Target -eq 'TypeScript') { $Target = 'TS' }
if ($Target -eq '.NET')       { $Target = 'DotNet' }

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

if ($Smoke) {
    Invoke-SmokeTests -Context $context -Scope $Target -Times $Times -WhatIf:$WhatIfPreference
}
else {
    # Compile first by default so the `--no-build` unit runs never test stale
    # output. -NoCompile opts out when the build output is known to be fresh.
    if (-not $NoCompile) {
        if ($Target -in 'All', 'TS')     { Build-TypeScript -Context $context -WhatIf:$WhatIfPreference }
        if ($Target -in 'All', 'DotNet') { Build-DotNet -Context $context -WhatIf:$WhatIfPreference }
    }

    # Compile once above; -Times repeats only the test run so a flake
    # surfaces without recompiling.
    Invoke-TestStress -Label 'Unit tests' -Times $Times -Run ({
        if ($Target -in 'All', 'TS')     { Test-TypeScript -Context $context -WhatIf:$WhatIfPreference }
        if ($Target -in 'All', 'DotNet') { Test-DotNet -Context $context -WhatIf:$WhatIfPreference }
    }.GetNewClosure())
}
