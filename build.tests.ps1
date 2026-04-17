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
    .\build.tests.ps1            # Run all tests
    .\build.tests.ps1 -Verbose   # Show WhatIf output for each passing test
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
        [switch]$ExpectError,
        [string]$ErrorPattern,
        [string[]]$ExpectOutput,
        [string[]]$RejectOutput
    )

    $result = [TestResult]@{ Name = $Name }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    # Run in a child process so that all streams (including host/WhatIf) are captured.
    $scriptPath = Join-Path $PSScriptRoot 'build.ps1'
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

    # ── Default (no action) ──────────────────────────────────────────────

    @{
        Name         = 'Default (Compile + Test all)'
        Arguments    = '-WhatIf'
        ExpectOutput = @('Compile TypeScript.*tsc', 'dotnet build.*AutoContext', 'Run TypeScript tests.*vitest', 'dotnet test')
    }

    # ── Compile ──────────────────────────────────────────────────────────

    @{
        Name         = 'Compile (all)'
        Arguments    = 'Compile -WhatIf'
        ExpectOutput = @('Compile TypeScript', 'dotnet build')
    }
    @{
        Name         = 'Compile TS'
        Arguments    = 'Compile TS -WhatIf'
        ExpectOutput = @('Compile TypeScript')
    }
    @{
        Name         = 'Compile TypeScript (alias)'
        Arguments    = 'Compile TypeScript -WhatIf'
        ExpectOutput = @('Compile TypeScript')
    }
    @{
        Name         = 'Compile DotNet'
        Arguments    = 'Compile DotNet -WhatIf'
        ExpectOutput = @('dotnet build')
    }
    @{
        Name         = 'Compile .NET (alias)'
        Arguments    = "Compile '.NET' -WhatIf"
        ExpectOutput = @('dotnet build')
    }
    @{
        Name         = 'Compile All (explicit)'
        Arguments    = 'Compile All -WhatIf'
        ExpectOutput = @('Compile TypeScript', 'dotnet build')
    }

    # ── Test ─────────────────────────────────────────────────────────────

    @{
        Name         = 'Test (all)'
        Arguments    = 'Test -WhatIf'
        ExpectOutput = @('Run TypeScript tests', 'dotnet test')
    }
    @{
        Name         = 'Test TS'
        Arguments    = 'Test TS -WhatIf'
        ExpectOutput = @('Run TypeScript tests')
    }
    @{
        Name         = 'Test DotNet'
        Arguments    = 'Test DotNet -WhatIf'
        ExpectOutput = @('dotnet test')
    }
    @{
        Name         = 'Test All (explicit)'
        Arguments    = 'Test All -WhatIf'
        ExpectOutput = @('Run TypeScript tests', 'dotnet test')
    }

    # ── Clean ────────────────────────────────────────────────────────────

    @{
        Name         = 'Clean (standalone)'
        Arguments    = '-Clean -WhatIf'
        ExpectOutput = @('Delete TypeScript output|TypeScript output.*not found', 'Delete Servers|Servers.*not found', 'Delete VSIX packages|VSIX packages.*not found')
    }
    @{
        Name         = 'Clean + Compile'
        Arguments    = '-Clean Compile -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Compile TypeScript', 'dotnet build')
    }
    @{
        Name         = 'Clean + Compile TS'
        Arguments    = '-Clean Compile TS -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Compile TypeScript')
    }
    @{
        Name         = 'Clean + Test'
        Arguments    = '-Clean Test -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Run TypeScript tests', 'dotnet test')
    }
    @{
        Name         = 'Clean + Test DotNet'
        Arguments    = '-Clean Test DotNet -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'dotnet test')
    }

    # ── Prepare ──────────────────────────────────────────────────────────

    @{
        Name         = 'Prepare'
        Arguments    = 'Prepare -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Compile TypeScript', 'dotnet build', 'Run TypeScript tests', 'dotnet test', 'Copy LICENSE')
    }

    # ── Package ──────────────────────────────────────────────────────────

    @{
        Name         = 'Package (auto-detect RID)'
        Arguments    = 'Package -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Compile TypeScript', 'dotnet build', 'Run TypeScript tests', 'dotnet publish', 'vsce package')
    }
    @{
        Name         = 'Package All (6 platforms)'
        Arguments    = 'Package All -WhatIf'
        ExpectOutput = @('win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
    }
    @{
        Name         = 'Package -RuntimeIdentifier win-x64'
        Arguments    = 'Package -RuntimeIdentifier win-x64 -WhatIf'
        ExpectOutput = @('dotnet publish.*win-x64', 'vsce package --target win32-x64')
    }
    @{
        Name         = 'Package -RuntimeIdentifier osx-arm64'
        Arguments    = 'Package -RuntimeIdentifier osx-arm64 -WhatIf'
        ExpectOutput = @('dotnet publish.*osx-arm64', 'vsce package --target darwin-arm64')
    }
    @{
        Name         = 'Package -RuntimeIdentifier linux-x64'
        Arguments    = 'Package -RuntimeIdentifier linux-x64 -WhatIf'
        ExpectOutput = @('dotnet publish.*linux-x64', 'vsce package --target linux-x64')
    }
    @{
        Name         = 'Package -Local'
        Arguments    = 'Package -Local -WhatIf'
        ExpectOutput = @('Delete TypeScript output', 'Compile TypeScript', 'dotnet build', 'Copy .NET servers \(local\)', 'Copy Web MCP server')
        RejectOutput = @('dotnet publish', 'vsce package')
    }

    # ── Publish ──────────────────────────────────────────────────────────

    @{
        Name         = 'Publish (auto-detect RID)'
        Arguments    = 'Publish -WhatIf'
        ExpectOutput = @('Delete TypeScript output|TypeScript output.*not found', 'Compile TypeScript', 'dotnet build', 'Run TypeScript tests', 'Copy LICENSE', 'dotnet publish', 'vsce package', 'Publish to Marketplace', 'Publish to Open VSX')
    }
    @{
        Name         = 'Publish All (6 platforms)'
        Arguments    = 'Publish All -WhatIf'
        ExpectOutput = @('win-x64', 'linux-x64', 'osx-arm64', 'Publish to Marketplace', 'Publish to Open VSX')
    }

    # ── Invalid combinations (expect errors) ─────────────────────────────

    @{
        Name         = 'Clean + Prepare (mutually exclusive)'
        Arguments    = '-Clean Prepare -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'Clean + Package (mutually exclusive)'
        Arguments    = '-Clean Package -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'Clean + Publish (mutually exclusive)'
        Arguments    = '-Clean Publish -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'Package All + RuntimeIdentifier (mutually exclusive)'
        Arguments    = 'Package All -RuntimeIdentifier win-x64 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'Local without Package (invalid)'
        Arguments    = 'Compile -Local -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'only valid with the Package action'
    }
    @{
        Name         = 'Package -Local + RuntimeIdentifier (mutually exclusive)'
        Arguments    = 'Package -Local -RuntimeIdentifier win-x64 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'Package -Local + All (mutually exclusive)'
        Arguments    = 'Package All -Local -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'Reject unknown Action'
        Arguments    = 'InvalidAction -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'ValidateSet|cannot be validated'
    }
    @{
        Name         = 'Reject unknown Target'
        Arguments    = 'Compile InvalidTarget -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'does not belong to the set'
    }

    # ── Tag ──────────────────────────────────────────────────────────────

    @{
        Name         = 'Tag without version (error)'
        Arguments    = 'Tag -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'Tag requires a version'
    }
    @{
        Name         = 'Tag with invalid semver (error)'
        Arguments    = 'Tag abc -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'Invalid version'
    }
    @{
        Name         = 'Tag with lower version (error)'
        Arguments    = 'Tag 0.0.1 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'less than current'
    }
    @{
        Name         = 'Tag + Clean (mutually exclusive)'
        Arguments    = '-Clean Tag 99.0.0 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'mutually exclusive'
    }
    @{
        Name         = 'Tag + RuntimeIdentifier (invalid)'
        Arguments    = 'Tag 99.0.0 -RuntimeIdentifier win-x64 -WhatIf'
        ExpectError  = $true
        ErrorPattern = 'not valid with Tag'
    }

    # ── Help ─────────────────────────────────────────────────────────────

    @{
        Name         = 'Help flag'
        Arguments    = '-Help'
        ExpectOutput = @('SYNTAX', 'ACTIONS', 'TARGETS', 'SWITCHES', 'EXAMPLES')
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
