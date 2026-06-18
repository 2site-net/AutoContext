#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Build gate for AutoContext — compile, verify .NET formatting, and run unit tests.

.DESCRIPTION
    Runs the AutoContext build gate: compile, verify .NET formatting, and run
    unit tests for the requested stack.

    A build gate is the quality checkpoint that must pass before work is
    considered complete or a commit is proposed. With no arguments, this gate
    covers both stacks: the TypeScript VS Code extension and the .NET solution.
    Use Target to narrow the run to TypeScript, .NET, or both.

    This script is intentionally small: it only runs the gate. Packaging,
    publishing, tagging, and faster inner-loop wrappers live under scripts/.
    Shared build logic lives in scripts/AutoContext.Build.psm1.

    After modifying this script, run scripts/build.tests.ps1 to verify that all
    target and switch combinations still work.

.PARAMETER Target
    Narrows the scope of the gate:
      TS (or TypeScript) — TypeScript only (compile + TS unit tests)
      DotNet (or .NET)   — .NET only (compile + format + .NET unit tests)
      All                — both (default)

    The .NET format gate only applies to the .NET stack, so the TS scope
    compiles and tests TypeScript without a format step.

.PARAMETER Clean
    Delete build artifacts. Used alone it only cleans; combined with a target
    (including 'All') it cleans first and then runs the gate for that scope.
    For a pure clean you can also use scripts/clean.ps1.

.PARAMETER Help
    Show usage information.

.EXAMPLE
    .\build.ps1                  # Compile + format + unit tests (all)
    .\build.ps1 TS               # TypeScript only (compile + TS tests)
    .\build.ps1 DotNet           # .NET only (compile + format + .NET tests)
    .\build.ps1 All              # Both stacks (same as no argument)
    .\build.ps1 -Clean           # Delete all build artifacts (clean only)
    .\build.ps1 -Clean All       # Clean then run the full gate
    .\build.ps1 -Clean TS        # Clean then build + test TypeScript
    .\build.ps1 -Clean DotNet    # Clean then build + test .NET
    .\build.ps1 -WhatIf          # Preview what the gate would do

.NOTES
    Author:   Eyal Alon
    Requires: PowerShell 7.0+, Node.js, .NET SDK
    Platform: Windows, Linux, macOS
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('All', 'TS', 'TypeScript', 'DotNet', '.NET')]
    [string]$Target,

    [switch]$Clean,

    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$global:LASTEXITCODE = 0

# Import the build module that houses every action function.
Import-Module (Join-Path $PSScriptRoot 'scripts' 'AutoContext.Build.psd1') -Force

# ── Help ─────────────────────────────────────────────────────────────────────

function Show-Help {
    Write-Host "`nAutoContext Build Gate`n" -ForegroundColor Cyan

    Write-Host 'SYNTAX' -ForegroundColor Yellow
    Write-Host "  .\build.ps1 [Target] [-Clean] [-WhatIf] [-Help]`n"

    Write-Host 'WHAT IT DOES' -ForegroundColor Yellow
    Write-Host '  Compiles, verifies .NET formatting, and runs unit tests.'
    Write-Host "  With no target it covers both stacks (TypeScript + .NET).`n"

    Write-Host 'TARGETS' -ForegroundColor Yellow
    Write-Host '  (none)     All (default)'
    Write-Host '  TS         TypeScript only — compile + TS tests (alias: TypeScript)'
    Write-Host '  DotNet     .NET only — compile + format + .NET tests (alias: .NET)'
    Write-Host "  All        Both TypeScript + .NET`n"

    Write-Host 'SWITCHES' -ForegroundColor Yellow
    Write-Host '  -Clean     Delete build artifacts. Alone: clean only.'
    Write-Host '             With a target: clean first, then run the gate.'
    Write-Host '  -WhatIf    Preview changes without executing'
    Write-Host "  -Help      Show this help`n"

    Write-Host 'EXAMPLES' -ForegroundColor Yellow
    Write-Host '  .\build.ps1                  # Compile + format + unit tests (all)'
    Write-Host '  .\build.ps1 TS               # TypeScript only'
    Write-Host '  .\build.ps1 DotNet           # .NET only'
    Write-Host '  .\build.ps1 -Clean           # Delete all build artifacts'
    Write-Host '  .\build.ps1 -Clean All       # Clean then run the full gate'
    Write-Host "  .\build.ps1 -WhatIf          # Preview`n"

    Write-Host 'OTHER TASKS' -ForegroundColor Yellow
    Write-Host '  Packaging, publishing, tagging, and inner-loop wrappers live'
    Write-Host '  under scripts/ — e.g. scripts/package.ps1, scripts/publish.ps1,'
    Write-Host "  scripts/tag.ps1, scripts/compile.ps1, scripts/test.ps1.`n"
}

# ── Main ─────────────────────────────────────────────────────────────────────

if ($Help) {
    Show-Help
    exit 0
}

# Normalize target aliases
if ($Target -eq 'TypeScript') { $Target = 'TS' }
if ($Target -eq '.NET')       { $Target = 'DotNet' }

# Build the shared context once and thread it into every action function.
$context = Initialize-BuildContext -RepoRoot $PSScriptRoot

if ($context.ExtensionVersion) {
    Write-Host "AutoContext v$($context.ExtensionVersion)" -ForegroundColor Magenta
    Write-Host ''
}

if ($Clean) {
    Invoke-Clean -Context $context -WhatIf:$WhatIfPreference
}

# Run the gate unless the invocation was a standalone clean.
# A target (including the explicit 'All') opts back into the gate after a clean.
if ($Target -or -not $Clean) {
    $scope = if ($Target) { $Target } else { 'All' }
    Invoke-Build -Context $context -Scope $scope -WhatIf:$WhatIfPreference
}
