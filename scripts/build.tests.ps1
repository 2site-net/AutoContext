#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Self-test for build.ps1 — exercises every valid combination with -WhatIf
    and verifies that invalid combinations produce the expected errors.

.DESCRIPTION
    A zero-dependency test harness that invokes build.ps1 with -WhatIf for
    every supported action × target × switch combination. Each test case is
    defined declaratively in the $testCases array, making it easy to add new
    scenarios.

.EXAMPLE
    .\scripts\build.tests.ps1            # Run all tests
    .\scripts\build.tests.ps1 -Verbose   # Show WhatIf output for each passing test
#>

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Test infrastructure ──────────────────────────────────────────────────────

class TestResult {
    [string]$Name
    [string]$Status      # Pass, Fail
    [string]$Detail
    [string]$Output      # Captured stdout+stderr (for -Verbose)
    [double]$DurationMs
}

function Invoke-TestCase {
    <#
    .SYNOPSIS
        Runs a single test case and returns a TestResult.
    #>
    [CmdletBinding()]
    [OutputType([TestResult])]
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Arguments,
        [string]$Script = 'build.ps1',
        [switch]$ExpectError,
        [string]$ErrorPattern,
        [string[]]$ExpectOutput,
        [string[]]$RejectOutput
    )

    $result = [TestResult]@{ Name = $Name }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    # Run in a child process so that all streams (including host/WhatIf) are captured.
    # $Script may target the orchestrator (build.ps1) or a granular scripts/*.ps1 wrapper.
    # $Script paths are repo-root-relative; this harness lives in scripts/, so
    # resolve against the repository root (the parent of $PSScriptRoot).
    $repoRoot = Split-Path $PSScriptRoot -Parent
    $scriptPath = Join-Path $repoRoot $Script
    $output = pwsh -NoProfile -NonInteractive -Command "& '$scriptPath' $Arguments" 2>&1 | Out-String
    $exitCode = $LASTEXITCODE

    $sw.Stop()
    $result.DurationMs = $sw.ElapsedMilliseconds

    $hasError = $exitCode -ne 0

    if ($ExpectError) {
        if (-not $hasError) {
            $result.Status = 'Fail'
            $result.Detail = 'Expected an error but the command succeeded.'
        }
        elseif ($ErrorPattern -and $output -notmatch $ErrorPattern) {
            $result.Status = 'Fail'
            $result.Detail = "Expected error matching '$ErrorPattern' but got output that didn't match."
        }
        else {
            $result.Output = $output
            $result.Status = 'Pass'
        }
        return $result
    }

    if ($hasError) {
        $result.Status = 'Fail'
        $errorLine = $output -split "`n" | Where-Object { $_ -match 'Exception|Error|throw|terminated' } | Select-Object -First 1
        $result.Detail = if ($errorLine) { $errorLine.Trim() } else { "Exit code: $exitCode" }
        return $result
    }

    # Verify expected output patterns
    foreach ($pattern in $ExpectOutput) {
        if ($output -notmatch $pattern) {
            $result.Status = 'Fail'
            $result.Detail = "Missing expected output pattern: $pattern"
            return $result
        }
    }

    # Verify rejected output patterns
    foreach ($pattern in $RejectOutput) {
        if ($output -match $pattern) {
            $result.Status = 'Fail'
            $result.Detail = "Found unexpected output pattern: $pattern"
            return $result
        }
    }

    $result.Output = $output
    $result.Status = 'Pass'

    return $result
}

function Write-TestResult {
    <#
    .SYNOPSIS
        Prints a single test result to the host.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory, ValueFromPipeline)][TestResult]$Result)

    process {
        $icon  = if ($Result.Status -eq 'Pass') { 'v' } else { 'x' }
        $color = if ($Result.Status -eq 'Pass') { 'Green' } else { 'Red' }
        $time  = '{0,6:N0}ms' -f $Result.DurationMs

        Write-Host "  [$icon] " -ForegroundColor $color -NoNewline
        Write-Host "$($Result.Name) " -NoNewline
        Write-Host $time -ForegroundColor DarkGray

        if ($Result.Detail) {
            Write-Host "       $($Result.Detail)" -ForegroundColor Red
        }

        if ($Result.Output) {
            # Condense to a single line: extract "=== Section Name" headings and join with " | ".
            # Collapse "Compile TypeScript | Compile .NET" → "Compile All", etc.
            # Collapse repeated platform publish/package pairs into a compact RID list.
            $headings = $Result.Output -split "`n" |
                Where-Object { $_ -match '^\s*=== (.+)' } |
                ForEach-Object { ($Matches[1]).Trim() }

            # Collapse TS + .NET pairs into "X All"
            $compileTS    = 'Compile TypeScript'
            $compileDotNet = 'Compile .NET'
            $testTS       = 'Test TypeScript'
            $testDotNet   = 'Test .NET'
            $knownPairs   = @($compileTS, $compileDotNet, $testTS, $testDotNet)

            $rids = @()
            $general = [System.Collections.Generic.List[string]]::new()

            if ($headings -contains $compileTS -and $headings -contains $compileDotNet) { $general.Add('Compile All') }
            elseif ($headings -contains $compileTS)    { $general.Add($compileTS) }
            elseif ($headings -contains $compileDotNet) { $general.Add($compileDotNet) }

            if ($headings -contains $testTS -and $headings -contains $testDotNet) { $general.Add('Test All') }
            elseif ($headings -contains $testTS)    { $general.Add($testTS) }
            elseif ($headings -contains $testDotNet) { $general.Add($testDotNet) }

            # Remaining headings (Compile/Test already handled via $knownPairs)
            foreach ($h in $headings) {
                if ($h -in $knownPairs) { continue }
                if ($h -match '^Package \.NET servers \((.+)\)$') {
                    $rids += $Matches[1]
                }
                elseif ($h -match '^Package VSIX \(') {
                    # Skip — always pairs 1:1 with the .NET heading above
                }
                else {
                    $general.Add($h)
                }
            }

            if ($rids.Count -gt 1) {
                $general.Add("Package: $($rids -join ', ')")
            }
            elseif ($rids.Count -eq 1) {
                $general.Add("Package .NET servers ($($rids[0]))")
                $vsceHead = $headings | Where-Object { $_ -match '^Package VSIX' } | Select-Object -First 1
                if ($vsceHead) { $general.Add($vsceHead) }
            }

            if ($general.Count -gt 0 -and $VerbosePreference -ne 'SilentlyContinue') {
                Write-Host ''
                Write-Verbose ("       " + ($general -join ' | '))
                Write-Host ''
            }
        }
    }
}

function Write-Summary {
    <#
    .SYNOPSIS
        Prints test run summary and returns the number of failures.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param([Parameter(Mandatory)][TestResult[]]$Results)

    $passed     = @($Results | Where-Object Status -eq 'Pass').Count
    $failed     = @($Results | Where-Object Status -eq 'Fail').Count
    $total      = $Results.Count
    $totalTime  = ($Results | Measure-Object -Property DurationMs -Sum).Sum

    Write-Host ''
    Write-Host ('  {0} passed, {1} failed, {2} total ({3:N1}s)' -f $passed, $failed, $total, ($totalTime / 1000)) -ForegroundColor $(if ($failed -gt 0) { 'Red' } else { 'Green' })

    if ($failed -gt 0) {
        Write-Host ''
        Write-Host '  Failures:' -ForegroundColor Red
        foreach ($fail in ($Results | Where-Object Status -eq 'Fail')) {
            Write-Host "    x $($fail.Name): $($fail.Detail)" -ForegroundColor Red
        }
    }

    return $failed
}

# ── Test cases ───────────────────────────────────────────────────────────────
#
# Each entry is a hashtable with:
#   Name          — human-readable test label
#   Arguments     — arguments passed to build.ps1 (always includes -WhatIf unless testing -Help)
#   ExpectError   — $true if the command should throw
#   ErrorPattern  — regex the error message must match (optional, used with ExpectError)
#   ExpectOutput  — array of regex patterns that must appear in stdout (optional)
#
# Add new test cases here — the runner picks them up automatically.

$testCases = @(

    # ── build.ps1 gate — compile + format + unit tests ───────────────────

    @{
        Name         = 'Default (all) — compile + format + unit tests'
        Arguments    = '-WhatIf'
        ExpectOutput = @('Compile TypeScript.*tsc', 'dotnet build.*AutoContext', 'dotnet format', 'Run TypeScript tests.*vitest', 'dotnet test')
    }
    @{
        Name         = 'All (explicit)'
        Arguments    = 'All -WhatIf'
        ExpectOutput = @('Compile TypeScript', 'dotnet build', 'dotnet format', 'Run TypeScript tests', 'dotnet test')
    }
    @{
        Name         = 'TS — TypeScript only'
        Arguments    = 'TS -WhatIf'
        ExpectOutput = @('Compile TypeScript', 'Run TypeScript tests')
        RejectOutput = @('dotnet build', 'dotnet test', 'dotnet format')
    }
    @{
        Name         = 'TypeScript (alias)'
        Arguments    = 'TypeScript -WhatIf'
        ExpectOutput = @('Compile TypeScript', 'Run TypeScript tests')
        RejectOutput = @('dotnet build')
    }
    @{
        Name         = 'DotNet — .NET only'
        Arguments    = 'DotNet -WhatIf'
        ExpectOutput = @('dotnet build', 'dotnet format', 'dotnet test')
        RejectOutput = @('Compile TypeScript', 'Run TypeScript tests')
    }
    @{
        Name         = '.NET (alias)'
        Arguments    = "'.NET' -WhatIf"
        ExpectOutput = @('dotnet build', 'dotnet test')
        RejectOutput = @('Compile TypeScript')
    }

    # ── Clean ────────────────────────────────────────────────────────────

    @{
        Name         = 'Clean (standalone) — clean only'
        Arguments    = '-Clean -WhatIf'
        ExpectOutput = @('Delete TypeScript output|TypeScript output.*not found', 'Delete Servers|Servers.*not found', 'Delete VSIX packages|VSIX packages.*not found')
        RejectOutput = @('Compile TypeScript', 'dotnet build', 'dotnet test')
    }
    @{
        Name         = 'Clean + All — clean then run the gate'
        Arguments    = '-Clean All -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Compile TypeScript', 'dotnet build', 'dotnet format', 'Run TypeScript tests', 'dotnet test')
    }
    @{
        Name         = 'Clean + TS — clean then build + test TypeScript'
        Arguments    = '-Clean TS -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Compile TypeScript', 'Run TypeScript tests')
        RejectOutput = @('dotnet build', 'dotnet test')
    }
    @{
        Name         = 'Clean + DotNet — clean then build + test .NET'
        Arguments    = '-Clean DotNet -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'dotnet build', 'dotnet format', 'dotnet test')
        RejectOutput = @('Compile TypeScript', 'Run TypeScript tests')
    }

    # ── Invalid arguments (expect errors) ────────────────────────────────

    @{
        Name         = 'Reject unknown Target'
        Arguments    = 'InvalidTarget -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'does not belong to the set|cannot be validated'
    }

    # ── Granular wrappers (scripts/*.ps1) ────────────────────────────────

    @{
        Name         = 'scripts/compile.ps1 (all) — compile only, no tests/format'
        Script       = 'scripts/compile.ps1'
        Arguments    = '-WhatIf'
        ExpectOutput = @('Compile TypeScript', 'dotnet build')
        RejectOutput = @('dotnet format', 'dotnet test', 'Run TypeScript tests')
    }
    @{
        Name         = 'scripts/compile.ps1 TS'
        Script       = 'scripts/compile.ps1'
        Arguments    = 'TS -WhatIf'
        ExpectOutput = @('Compile TypeScript')
        RejectOutput = @('dotnet build')
    }
    @{
        Name         = 'scripts/compile.ps1 DotNet'
        Script       = 'scripts/compile.ps1'
        Arguments    = 'DotNet -WhatIf'
        ExpectOutput = @('dotnet build')
        RejectOutput = @('Compile TypeScript')
    }
    @{
        Name         = 'scripts/test.ps1 (all)'
        Script       = 'scripts/test.ps1'
        Arguments    = '-WhatIf'
        ExpectOutput = @('Run TypeScript tests', 'dotnet test')
        RejectOutput = @('Compile TypeScript', 'dotnet build')
    }
    @{
        Name         = 'scripts/test.ps1 TS'
        Script       = 'scripts/test.ps1'
        Arguments    = 'TS -WhatIf'
        ExpectOutput = @('Run TypeScript tests')
        RejectOutput = @('dotnet test')
    }
    @{
        Name         = 'scripts/test.ps1 DotNet'
        Script       = 'scripts/test.ps1'
        Arguments    = 'DotNet -WhatIf'
        ExpectOutput = @('dotnet test')
        RejectOutput = @('Run TypeScript tests')
    }
    @{
        Name         = 'scripts/test.ps1 -Smoke (all)'
        Script       = 'scripts/test.ps1'
        Arguments    = '-Smoke -WhatIf'
        ExpectOutput = @('Run TypeScript tests', 'dotnet test --no-build \(unit\)', 'Run VS Code smoke tests', 'dotnet test --no-build \(smoke\)')
    }
    @{
        Name         = 'scripts/test.ps1 TS -Smoke'
        Script       = 'scripts/test.ps1'
        Arguments    = 'TS -Smoke -WhatIf'
        ExpectOutput = @('Run TypeScript tests', 'Run VS Code smoke tests')
        RejectOutput = @('dotnet test')
    }
    @{
        Name         = 'scripts/test.ps1 DotNet -Smoke'
        Script       = 'scripts/test.ps1'
        Arguments    = 'DotNet -Smoke -WhatIf'
        ExpectOutput = @('dotnet test --no-build \(unit\)', 'dotnet test --no-build \(smoke\)')
        RejectOutput = @('Run TypeScript tests', 'Run VS Code smoke tests')
    }
    @{
        Name         = 'scripts/format.ps1'
        Script       = 'scripts/format.ps1'
        Arguments    = '-WhatIf'
        ExpectOutput = @('dotnet format')
        RejectOutput = @('dotnet build', 'dotnet test')
    }
    @{
        Name         = 'scripts/clean.ps1'
        Script       = 'scripts/clean.ps1'
        Arguments    = '-WhatIf'
        ExpectOutput = @('Delete TypeScript output|TypeScript output.*not found', 'Delete Servers|Servers.*not found', 'Delete VSIX packages|VSIX packages.*not found')
    }
    @{
        Name         = 'scripts/prepare.ps1'
        Script       = 'scripts/prepare.ps1'
        Arguments    = '-WhatIf'
        ExpectOutput = @('Compile TypeScript', 'dotnet build', 'Run TypeScript tests', 'dotnet test', 'Copy LICENSE')
    }
    @{
        Name         = 'scripts/package.ps1 (auto-detect RID)'
        Script       = 'scripts/package.ps1'
        Arguments    = '-WhatIf'
        ExpectOutput = @('Compile TypeScript', 'dotnet build', 'dotnet publish', 'vsce package')
    }
    @{
        Name         = 'scripts/package.ps1 All (6 platforms)'
        Script       = 'scripts/package.ps1'
        Arguments    = 'All -WhatIf'
        ExpectOutput = @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
    }
    @{
        Name         = 'scripts/package.ps1 -Local'
        Script       = 'scripts/package.ps1'
        Arguments    = '-Local -WhatIf'
        ExpectOutput = @('Compile TypeScript', 'dotnet build', 'Copy .NET servers \(local\)')
        RejectOutput = @('dotnet publish', 'vsce package')
    }
    @{
        Name         = 'scripts/package.ps1 -Local + RuntimeIdentifier (mutually exclusive)'
        Script       = 'scripts/package.ps1'
        Arguments    = '-Local -RuntimeIdentifier win-x64 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'scripts/package.ps1 All + RuntimeIdentifier (mutually exclusive)'
        Script       = 'scripts/package.ps1'
        Arguments    = 'All -RuntimeIdentifier win-x64 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'scripts/publish.ps1 (auto-detect RID)'
        Script       = 'scripts/publish.ps1'
        Arguments    = '-WhatIf'
        ExpectOutput = @('dotnet publish', 'vsce package', 'Publish to Marketplace', 'Publish to Open VSX')
    }
    @{
        Name         = 'scripts/publish.ps1 All + RuntimeIdentifier (mutually exclusive)'
        Script       = 'scripts/publish.ps1'
        Arguments    = 'All -RuntimeIdentifier win-x64 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'scripts/tag.ps1 with invalid semver (error)'
        Script       = 'scripts/tag.ps1'
        Arguments    = 'abc -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'Invalid version'
    }
    @{
        Name         = 'scripts/tag.ps1 with lower version (error)'
        Script       = 'scripts/tag.ps1'
        Arguments    = '0.0.1 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'less than current'
    }

    # ── Help ─────────────────────────────────────────────────────────────

    @{
        Name         = 'Help flag'
        Arguments    = '-Help'
        ExpectOutput = @('SYNTAX', 'TARGETS', 'SWITCHES', 'EXAMPLES')
    }
)

# ── Runner ───────────────────────────────────────────────────────────────────

$width = 64
Write-Host ('=' * $width) -ForegroundColor Cyan
Write-Host ((' ' * 9) + 'AutoContext Build Script — Test Suite') -ForegroundColor Cyan
Write-Host ('=' * $width) -ForegroundColor Cyan
Write-Host "  Running $($testCases.Count) tests..." -ForegroundColor Gray
Write-Host ''

$results = [System.Collections.Generic.List[TestResult]]::new($testCases.Count)

foreach ($case in $testCases) {
    $params = @{
        Name      = $case.Name
        Arguments = $case.Arguments
    }
    if ($case.ContainsKey('Script')       -and $case.Script)       { $params.Script        = $case.Script }
    if ($case.ContainsKey('ExpectError')  -and $case.ExpectError)  { $params.ExpectError  = [switch]$true }
    if ($case.ContainsKey('ErrorPattern') -and $case.ErrorPattern) { $params.ErrorPattern  = $case.ErrorPattern }
    if ($case.ContainsKey('ExpectOutput') -and $case.ExpectOutput) { $params.ExpectOutput  = $case.ExpectOutput }
    if ($case.ContainsKey('RejectOutput') -and $case.RejectOutput) { $params.RejectOutput  = $case.RejectOutput }

    $testResult = Invoke-TestCase @params
    $testResult | Write-TestResult
    $results.Add($testResult)
}

Write-Host ''
Write-Host ('=' * $width) -ForegroundColor Cyan

$failCount = Write-Summary -Results $results.ToArray()
exit $failCount
