#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Build orchestrator for AutoContext.

.DESCRIPTION
    Compiles, tests, packages, and publishes both the TypeScript VS Code extension
    and the .NET MCP server from a single entry point.

    The build logic lives in scripts/AutoContext.Build.psm1; this script is the
    thin CLI orchestrator that parses arguments, validates option combinations,
    builds the shared context, and dispatches to the module's action functions.
    The granular scripts/*.ps1 wrappers expose the same functions one phase at a
    time for fast inner-loop iteration.

    After modifying this script, run scripts/build.tests.ps1 to verify all
    action/target combinations still work. If a test fails, determine whether
    the script has a bug or the test expectations need updating to match the
    new behaviour.

.PARAMETER Action
    The build action to perform:
      Compile  — compile sources, verify .NET formatting (unless -NoLint),
                 then run unit tests (unless -NoTest); add -Smoke to also
                 stage the packaged extension and run smoke tests
      Prepare  — Clean + Compile + copy assets into extension
      Package  — Prepare + dotnet publish + vsce package
      Publish  — Package + vsce publish + ovsx publish
      Tag      — Compile + bump versions + git commit + annotated tag

    When omitted, defaults to Compile.

.PARAMETER Target
    Narrows the scope of an action:
      TS (or TypeScript) — TypeScript only
      DotNet (or .NET)   — .NET only
      All                — both (default for Compile)

    For Package/Publish, 'All' builds all six platform targets.
    When omitted for Package/Publish, auto-detects the current platform.

    For Tag, this positional slot accepts the version string (X.Y.Z or
    X.Y.Z-prerelease) instead of a target name.

.PARAMETER Clean
    Delete build artifacts. Can be combined with Compile,
    or used alone to only clean.
    Mutually exclusive with Prepare, Package, and Publish (they already clean).

.PARAMETER Local
    For Package only. Copies framework-dependent .NET build output into
    the extension's servers directory instead of running dotnet publish.
    Produces a runnable extension directory for local F5 development
    without self-contained single-file publishing. No .vsix is produced.

.PARAMETER RuntimeIdentifier
    .NET runtime identifier for Package/Publish (e.g. win-x64, osx-arm64).
    Mutually exclusive with Target 'All'.

.PARAMETER Smoke
    For Compile only. After compile + unit tests, stage the packaged
    extension layout (mirrors 'Package -Local') and run smoke tests.
    Combines with Target: '-Smoke' alone runs TS (VS Code) + .NET smoke,
    '-Smoke TS' runs the VS Code smoke, '-Smoke DotNet' runs the .NET
    end-to-end smoke. Compile and packaging always cover both stacks
    because smoke needs the full extension layout; Target only narrows
    which smoke suite(s) run.

.PARAMETER NoTest
    For Compile only. Skip the unit-test phase that normally follows the
    compile step. Useful for fast inner-loop syntax checks; the compile
    itself is never skipped. Combines with -Smoke (smoke tests still run;
    only the unit-test phase is skipped).

.PARAMETER NoLint
    For Compile only. Skip the .NET format-verification gate that normally
    runs after the compile step (a full-solution
    'dotnet format --verify-no-changes'). The gate is on by default so a
    green Compile guarantees a format-clean tree; pass -NoLint for fast
    inner-loop iterations or while mid-refactor. Only affects .NET;
    TypeScript linting is not wired up yet.

.PARAMETER Force
    For Tag only. Delete the existing local tag and the matching remote
    tag (if any) before re-creating it. Skips the strict auto-undo
    safety checks that normally prevent retagging when the tag already
    points elsewhere or has been pushed. The bump commit, if any, is
    left intact.

.PARAMETER Help
    Show usage information.

.EXAMPLE
    .\build.ps1                                  # Compile + unit tests (all)
    .\build.ps1 Compile                          # Compile + unit tests (all)
    .\build.ps1 Compile TS                       # Compile + unit tests (TypeScript)
    .\build.ps1 Compile DotNet                   # Compile + unit tests (.NET)
    .\build.ps1 Compile -NoTest                  # Compile only, skip unit tests
    .\build.ps1 Compile TS -NoTest               # Compile TypeScript only, skip tests
    .\build.ps1 Compile -NoLint                  # Compile + tests, skip .NET format gate
    .\build.ps1 Compile -Smoke                   # Compile + unit + smoke (TS + .NET)
    .\build.ps1 Compile -Smoke DotNet            # Compile + unit + .NET smoke only
    .\build.ps1 Compile -Smoke TS                # Compile + unit + VS Code smoke only
    .\build.ps1 Prepare                          # Clean + Compile + copy assets
    .\build.ps1 Package                          # Prepare + build for current platform
    .\build.ps1 Package -Local                   # Prepare + copy servers (local F5)
    .\build.ps1 Package All                      # Prepare + build all 6 platforms
    .\build.ps1 Package -RuntimeIdentifier win-x64
    .\build.ps1 Publish                          # Package + publish to Marketplace + Open VSX
    .\build.ps1 Tag 0.6.0                        # Bump, compile, test, commit, tag
    .\build.ps1 Tag 0.6.0-alpha                  # Prerelease tag
    .\build.ps1 Tag 0.6.0 -Force                 # Re-tag (delete local + remote first)
    .\build.ps1 -Clean                           # Delete all build artifacts
    .\build.ps1 -Clean Compile                   # Clean then compile + test
    .\build.ps1 Package -WhatIf                  # Preview what Package would do

.NOTES
    Author:   Eyal Alon
    Requires: PowerShell 7.0+, Node.js, .NET SDK
    Platform: Windows, Linux, macOS
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('Compile', 'Prepare', 'Package', 'Publish', 'Tag')]
    [string]$Action,

    [Parameter(Position = 1)]
    [ArgumentCompleter({
        param($commandName, $parameterName, $wordToComplete)
        @('All', 'TS', 'TypeScript', 'DotNet', '.NET') |
            Where-Object { $_ -like "$wordToComplete*" }
    })]
    [string]$Target,

    [switch]$Clean,

    [switch]$Local,

    [string]$RuntimeIdentifier,

    [switch]$Smoke,

    [switch]$NoTest,

    [switch]$NoLint,

    [switch]$Force,

    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

# Import the build module that houses every action function.
Import-Module (Join-Path $PSScriptRoot 'scripts' 'AutoContext.Build.psd1') -Force

# ── Help ─────────────────────────────────────────────────────────────────────

function Show-Help {
    Write-Host "`nAutoContext Build Orchestrator`n" -ForegroundColor Cyan

    Write-Host 'SYNTAX' -ForegroundColor Yellow
    Write-Host "  .\build.ps1 [Action] [Target] [-Clean] [-Local] [-Smoke] [-NoTest] [-NoLint] [-Force] [-RuntimeIdentifier <rid>] [-WhatIf] [-Help]`n"

    Write-Host 'ACTIONS' -ForegroundColor Yellow
    Write-Host '  (none)     Compile + unit tests (all sources)'
    Write-Host '  Compile    Compile sources, verify .NET formatting, then run unit tests'
    Write-Host '             (use -NoLint to skip the format gate;'
    Write-Host '              use -NoTest to skip the test phase;'
    Write-Host '              use -Smoke to also run smoke tests)'
    Write-Host '  Prepare    Clean + Compile + copy assets into extension'
    Write-Host '  Package    Prepare + dotnet publish + vsce package'
    Write-Host '  Publish    Package + vsce publish + ovsx publish'
    Write-Host "  Tag        Compile + bump versions + git commit + annotated tag`n"

    Write-Host 'TARGETS' -ForegroundColor Yellow
    Write-Host '  (none)     All (default)'
    Write-Host '  TS         TypeScript only (alias: TypeScript)'
    Write-Host '  DotNet     .NET only (alias: .NET)'
    Write-Host "  All        Both TS + .NET; for Package/Publish: all 6 platforms`n"

    Write-Host 'SWITCHES' -ForegroundColor Yellow
    Write-Host '  -Clean                Delete build artifacts (combinable with Compile)'
    Write-Host '  -Local                Copy server binaries for local F5 (Package only)'
    Write-Host '  -Smoke                Also run smoke tests after compile + unit tests'
    Write-Host '                        (Compile only; combines with Target)'
    Write-Host '  -NoTest               Skip the unit-test phase (Compile only)'
    Write-Host '  -NoLint               Skip the .NET format gate (Compile only)'
    Write-Host '  -Force                Re-tag: delete local + remote tag first (Tag only)'
    Write-Host '  -RuntimeIdentifier    .NET RID for Package/Publish (e.g. win-x64)'
    Write-Host '  -WhatIf               Preview changes without executing (works with any action and switch)'
    Write-Host "  -Help                 Show this help`n"

    Write-Host 'EXAMPLES' -ForegroundColor Yellow
    Write-Host '  .\build.ps1                                   # Compile + unit tests (all)'
    Write-Host '  .\build.ps1 Compile TS                        # TypeScript compile + tests'
    Write-Host '  .\build.ps1 Compile DotNet                    # .NET compile + tests'
    Write-Host '  .\build.ps1 Compile -NoTest                   # Compile only, skip tests'
    Write-Host '  .\build.ps1 Compile TS -NoTest                # Compile TypeScript only'
    Write-Host '  .\build.ps1 Compile -NoLint                   # Compile + tests, skip format gate'
    Write-Host '  .\build.ps1 Compile -Smoke                    # Compile + unit + smoke (all)'
    Write-Host '  .\build.ps1 Compile -Smoke DotNet             # Compile + unit + .NET smoke'
    Write-Host '  .\build.ps1 Compile -Smoke TS                 # Compile + unit + VS Code smoke'
    Write-Host '  .\build.ps1 Package                           # Current platform'
    Write-Host '  .\build.ps1 Package -Local                    # Prepare + copy servers (F5)'
    Write-Host '  .\build.ps1 Package All                       # All 6 platforms'
    Write-Host '  .\build.ps1 Package -RuntimeIdentifier win-x64'
    Write-Host '  .\build.ps1 Tag 0.6.0                         # Bump, test, commit, tag'
    Write-Host '  .\build.ps1 Tag 0.6.0-alpha                   # Prerelease tag'
    Write-Host '  .\build.ps1 Tag 0.6.0 -Force                  # Re-tag (delete local + remote first)'
    Write-Host '  .\build.ps1 -Clean Compile                    # Clean then compile + test'
    Write-Host "  .\build.ps1 Package -WhatIf                   # Preview`n"
}

# ── Main ─────────────────────────────────────────────────────────────────────

if ($Help) {
    Show-Help
    exit 0
}

# Normalize target aliases
if ($Target -eq 'TypeScript') { $Target = 'TS' }
if ($Target -eq '.NET')       { $Target = 'DotNet' }

# For Tag, the Target positional slot holds the version string
$Version = $null
if ($Action -eq 'Tag') {
    $Version = $Target
    $Target = $null
}

# Validate target values for non-Tag actions
if ($Action -ne 'Tag' -and $Target -and $Target -notin @('All', 'TS', 'DotNet')) {
    throw "Cannot validate argument on parameter 'Target'. The argument `"$Target`" does not belong to the set `"All, TS, TypeScript, DotNet, .NET`"."
}

# Validate mutually exclusive options
if ($RuntimeIdentifier -and $Target -eq 'All') {
    throw '-RuntimeIdentifier and Target ''All'' are mutually exclusive.'
}

if ($Local -and $Action -ne 'Package') {
    throw '-Local is only valid with the Package action.'
}

if ($Smoke -and $Action -and $Action -ne 'Compile') {
    throw "-Smoke is only valid with the Compile action. Usage: .\build.ps1 Compile -Smoke"
}

if ($NoTest -and $Action -and $Action -ne 'Compile') {
    throw "-NoTest is only valid with the Compile action."
}

if ($NoLint -and $Action -and $Action -ne 'Compile') {
    throw "-NoLint is only valid with the Compile action."
}

if ($Force -and $Action -ne 'Tag') {
    throw '-Force is only valid with the Tag action.'
}

if ($Local -and $RuntimeIdentifier) {
    throw '-Local and -RuntimeIdentifier are mutually exclusive.'
}

if ($Local -and $Target -eq 'All') {
    throw "-Local and Target 'All' are mutually exclusive."
}

if ($Clean -and $Action -in 'Prepare', 'Package', 'Publish') {
    throw "-Clean and '$Action' are mutually exclusive — $Action already performs a clean."
}

if ($Clean -and $Action -eq 'Tag') {
    throw "-Clean and 'Tag' are mutually exclusive."
}

if ($Action -eq 'Tag' -and $RuntimeIdentifier) {
    throw '-RuntimeIdentifier is not valid with Tag.'
}

if ($Action -eq 'Tag' -and -not $Version) {
    throw 'Tag requires a version. Usage: .\build.ps1 Tag <version>'
}

$resolvedTarget = if ($Target) { $Target } else { 'All' }

# Build the shared context once and thread it into every action function.
$context = Initialize-BuildContext -RepoRoot $PSScriptRoot

if ($context.ExtensionVersion) {
    Write-Host "AutoContext v$($context.ExtensionVersion)" -ForegroundColor Magenta
    Write-Host ''
}

if ($Clean) {
    # Compile -Smoke already cleans internally as part of staging the
    # packaged extension layout; skip the redundant top-level clean.
    if (-not ($Action -eq 'Compile' -and $Smoke)) {
        Invoke-Clean -Context $context -WhatIf:$WhatIfPreference
    }
}

if (-not $Action -and -not $Clean) {
    # Default: Compile (which also runs unit tests unless -NoTest)
    Invoke-Compile -Context $context -Scope $resolvedTarget -Smoke:$Smoke -NoLint:$NoLint -NoTest:$NoTest -WhatIf:$WhatIfPreference
}
elseif ($Action) {
    switch ($Action) {
        'Compile' { Invoke-Compile -Context $context -Scope $resolvedTarget -Smoke:$Smoke -NoLint:$NoLint -NoTest:$NoTest -WhatIf:$WhatIfPreference }
        'Prepare' { Invoke-Prepare -Context $context -WhatIf:$WhatIfPreference }
        'Package' { Invoke-Package -Context $context -Scope $Target -Local:$Local -RuntimeIdentifier $RuntimeIdentifier -WhatIf:$WhatIfPreference }
        'Publish' { Invoke-Publish -Context $context -Scope $Target -RuntimeIdentifier $RuntimeIdentifier -WhatIf:$WhatIfPreference }
        'Tag'     { Invoke-Tag -Context $context -Version $Version -Force:$Force -WhatIf:$WhatIfPreference }
    }
}
