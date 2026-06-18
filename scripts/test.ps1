#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Run AutoContext tests (TypeScript and/or .NET; unit and/or smoke).

.DESCRIPTION
    Fast inner-loop wrapper that runs the TypeScript (vitest) and/or .NET
    (dotnet test) unit suites by delegating to the AutoContext.Build module.
    Pass -Smoke to also run the smoke suites.

    NOTE: All .NET suites run with `--no-build`, so they assume a prior
    compile. Run scripts/compile.ps1 (or build.ps1 Compile) first if the
    output is stale. The smoke suites additionally require the packaged
    extension layout to be staged (e.g. via `build.ps1 Compile -Smoke` or
    `scripts/package.ps1 -Local`); this wrapper runs them but does not stage.

.PARAMETER Target
    Which stack to test: TS (alias TypeScript), DotNet (alias .NET),
    or All (default). Scopes both the unit and smoke suites: TS selects the
    VS Code smoke suite, DotNet selects the .NET smoke suite.

.PARAMETER Smoke
    Also run the smoke suites after the unit suites, scoped by Target.

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

if ($Target -in 'All', 'TS')     { Test-TypeScript -Context $context -WhatIf:$WhatIfPreference }
if ($Target -in 'All', 'DotNet') { Test-DotNet -Context $context -WhatIf:$WhatIfPreference }

if ($Smoke) {
    if ($Target -in 'All', 'TS')     { Test-VsCodeSmoke -Context $context -WhatIf:$WhatIfPreference }
    if ($Target -in 'All', 'DotNet') { Test-DotNetSmoke -Context $context -WhatIf:$WhatIfPreference }
}
