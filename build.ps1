#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Build orchestrator for SharpPilot.

.DESCRIPTION
    Compiles, tests, packages, and publishes both the TypeScript VS Code extension
    and the .NET MCP server from a single entry point.

.PARAMETER Action
    The build action to perform:
      Compile  — compile TypeScript and/or .NET sources
      Test     — run unit tests (without compiling)
      Prepare  — Clean + Compile + Test + copy assets into extension
      Package  — Prepare + dotnet publish + vsce package
      Publish  — Package + vsce publish

    When omitted, defaults to Compile + Test.

.PARAMETER Target
    Narrows the scope of an action:
      TS (or TypeScript) — TypeScript only
      DotNet (or .NET)   — .NET only
      All                — both (default for Compile/Test)

    For Package/Publish, 'All' builds all six platform targets.
    When omitted for Package/Publish, auto-detects the current platform.

.PARAMETER Clean
    Delete build artifacts. Can be combined with Compile or Test,
    or used alone to only clean.
    Mutually exclusive with Prepare, Package, and Publish (they already clean).

.PARAMETER RuntimeIdentifier
    .NET runtime identifier for Package/Publish (e.g. win-x64, osx-arm64).
    Mutually exclusive with Target 'All'.

.PARAMETER Help
    Show usage information.

.EXAMPLE
    .\build.ps1                                  # Compile + Test (all)
    .\build.ps1 Compile                          # Compile TS + .NET
    .\build.ps1 Compile TS                       # Compile TypeScript only
    .\build.ps1 Test DotNet                      # Test .NET only
    .\build.ps1 Prepare                          # Clean + Compile + Test + copy assets
    .\build.ps1 Package                          # Prepare + build for current platform
    .\build.ps1 Package All                      # Prepare + build all 6 platforms
    .\build.ps1 Package -RuntimeIdentifier win-x64
    .\build.ps1 Publish                          # Package + publish to Marketplace
    .\build.ps1 -Clean                           # Delete all build artifacts
    .\build.ps1 -Clean Compile                   # Clean then compile
    .\build.ps1 Package -WhatIf                  # Preview what Package would do

.NOTES
    Author:   Eyal Alon
    Requires: PowerShell 7.0+, Node.js, .NET SDK
    Platform: Windows, Linux, macOS
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Position = 0)]
    [ValidateSet('Compile', 'Test', 'Prepare', 'Package', 'Publish')]
    [string]$Action,

    [Parameter(Position = 1)]
    [ValidateSet('All', 'TS', 'TypeScript', 'DotNet', '.NET')]
    [string]$Target,

    [switch]$Clean,

    [string]$RuntimeIdentifier,

    [switch]$Help
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Paths ────────────────────────────────────────────────────────────────────

$repoRoot = $PSScriptRoot

# VS Code extension directory (fixed — used for packaging, publishing, assets)
$extensionDir = Join-Path $repoRoot 'src' 'SharpPilot.VSCode'

# Discover vitest config
$vitestConfig = Get-ChildItem $repoRoot -Filter 'vitest.config.ts' -Recurse -File -Depth 4 |
    Select-Object -First 1
$vitestConfigPath = if ($vitestConfig) { $vitestConfig.FullName } else { $null }

$mcpDir = Join-Path $extensionDir 'mcp'
$publishDir = Join-Path $extensionDir 'publish'

# Read extension version from package.json
$packageJsonPath = Join-Path $extensionDir 'package.json'
$extensionVersion = if (Test-Path $packageJsonPath) {
    (Get-Content $packageJsonPath -Raw | ConvertFrom-Json).version
}

# Discover solution file (.slnx preferred, .sln fallback)
$solutionFile = Get-ChildItem $repoRoot -Filter '*.slnx' -File | Select-Object -First 1
if (-not $solutionFile) {
    $solutionFile = Get-ChildItem $repoRoot -Filter '*.sln' -File | Select-Object -First 1
}

# Discover .NET project paths from the solution file
$dotnetProjects = @()
if ($solutionFile -and $solutionFile.Extension -eq '.slnx') {
    [xml]$solutionXml = Get-Content $solutionFile.FullName
    $dotnetProjects = @($solutionXml.SelectNodes('//Project/@Path') |
        ForEach-Object { Join-Path $repoRoot $_.Value })
}

$serverProjectPath = $dotnetProjects |
    Where-Object { $_ -notmatch '\.Tests\.' } |
    Select-Object -First 1

# ── RID → vsce target mapping ───────────────────────────────────────────────

$ridToTarget = @{
    'win-x64'     = 'win32-x64'
    'win-arm64'   = 'win32-arm64'
    'linux-x64'   = 'linux-x64'
    'linux-arm64'  = 'linux-arm64'
    'osx-x64'     = 'darwin-x64'
    'osx-arm64'   = 'darwin-arm64'
}

# ── Output helpers ───────────────────────────────────────────────────────────

function Write-Header {
    param(
        [Parameter(Mandatory)][string]$Title,
        [ConsoleColor]$Color = 'Cyan'
    )

    $width = 64
    $padding = $width - $Title.Length
    $leftPad = [math]::Floor($padding / 2)
    $rightPad = [math]::Ceiling($padding / 2)
    $centeredTitle = (' ' * $leftPad) + $Title + (' ' * $rightPad)

    Write-Host ('=' * $width) -ForegroundColor $Color
    Write-Host $centeredTitle -ForegroundColor $Color
    Write-Host ('=' * $width) -ForegroundColor $Color
}

function Write-Section {
    param([Parameter(Mandatory)][string]$Title)
    Write-Host ("`n=== {0}" -f $Title) -ForegroundColor Cyan
}

function Write-Status {
    param(
        [Parameter(Mandatory)][string]$Message,
        [ValidateSet('OK', 'FAIL', 'INFO')][string]$Status
    )

    $icon = switch ($Status) {
        'OK'   { 'v' }
        'FAIL' { 'x' }
        'INFO' { '*' }
    }

    $color = switch ($Status) {
        'OK'   { 'Green' }
        'FAIL' { 'Red' }
        'INFO' { 'Gray' }
    }

    Write-Host ('  [{0}] {1}' -f $icon, $Message) -ForegroundColor $color
}

# ── Help ─────────────────────────────────────────────────────────────────────

function Show-Help {
    Write-Host "`nSharpPilot Build Orchestrator`n" -ForegroundColor Cyan

    Write-Host 'SYNTAX' -ForegroundColor Yellow
    Write-Host "  .\build.ps1 [Action] [Target] [-Clean] [-RuntimeIdentifier <rid>] [-WhatIf] [-Help]`n"

    Write-Host 'ACTIONS' -ForegroundColor Yellow
    Write-Host '  (none)     Compile + Test (all sources)'
    Write-Host '  Compile    Compile TypeScript and/or .NET sources'
    Write-Host '  Test       Run unit tests (without compiling)'
    Write-Host '  Prepare    Clean + Compile + Test + copy assets into extension'
    Write-Host '  Package    Prepare + dotnet publish + vsce package'
    Write-Host "  Publish    Package + vsce publish`n"

    Write-Host 'TARGETS' -ForegroundColor Yellow
    Write-Host '  (none)     All (default)'
    Write-Host '  TS         TypeScript only (alias: TypeScript)'
    Write-Host '  DotNet     .NET only (alias: .NET)'
    Write-Host "  All        Both TS + .NET; for Package/Publish: all 6 platforms`n"

    Write-Host 'SWITCHES' -ForegroundColor Yellow
    Write-Host '  -Clean                Delete build artifacts (combinable with Compile/Test)'
    Write-Host '  -RuntimeIdentifier    .NET RID for Package/Publish (e.g. win-x64)'
    Write-Host '  -WhatIf               Preview changes without executing (works with any action and switch)'
    Write-Host "  -Help                 Show this help`n"

    Write-Host 'EXAMPLES' -ForegroundColor Yellow
    Write-Host '  .\build.ps1                                   # Compile + Test'
    Write-Host '  .\build.ps1 Compile TS                        # TypeScript only'
    Write-Host '  .\build.ps1 Test DotNet                       # .NET tests only'
    Write-Host '  .\build.ps1 Package                           # Current platform'
    Write-Host '  .\build.ps1 Package All                       # All 6 platforms'
    Write-Host '  .\build.ps1 Package -RuntimeIdentifier win-x64'
    Write-Host '  .\build.ps1 -Clean Compile                    # Clean then compile'
    Write-Host "  .\build.ps1 Package -WhatIf                   # Preview`n"
}

# ── Validation ───────────────────────────────────────────────────────────────

function Assert-ExternalCommand {
    param([Parameter(Mandatory)][string]$Command)

    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        throw "'$Command' is not installed or not on PATH."
    }
}

# ── Core actions ─────────────────────────────────────────────────────────────

function Invoke-CompileTS {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Section 'Compile TypeScript'

    if (-not (Test-Path $extensionDir)) { throw "Extension directory not found: $extensionDir" }

    if ($PSCmdlet.ShouldProcess('chat-instructions manifest + tsc', 'Compile TypeScript')) {
        Assert-ExternalCommand 'npx'

        Push-Location $extensionDir
        try {
            Write-Status 'Generating chat-instructions manifest...' 'INFO'
            npx tsx src/chat-instructions-manifest.ts
            if ($LASTEXITCODE -ne 0) { throw 'Chat-instructions manifest generation failed.' }
            Write-Status 'Chat-instructions manifest generated' 'OK'

            Write-Status 'Compiling TypeScript...' 'INFO'
            npx tsc -p ./tsconfig.build.json
            if ($LASTEXITCODE -ne 0) { throw 'TypeScript compilation failed.' }
            Write-Status 'TypeScript compiled' 'OK'
        }
        finally {
            Pop-Location
        }
    }
}

function Invoke-CompileDotNet {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Section 'Compile .NET'

    if (-not $solutionFile) { throw 'No .slnx or .sln file found in the repository root.' }

    if ($PSCmdlet.ShouldProcess($solutionFile.Name, 'dotnet build')) {
        Assert-ExternalCommand 'dotnet'

        dotnet build $solutionFile.FullName -c Release
        if ($LASTEXITCODE -ne 0) { throw '.NET compilation failed.' }
        Write-Status ".NET solution compiled ($($solutionFile.Name))" 'OK'
    }
}

function Invoke-TestTS {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Section 'Test TypeScript'

    if (-not $vitestConfigPath) { throw 'No vitest.config.ts found — cannot locate TypeScript tests.' }

    if ($PSCmdlet.ShouldProcess('vitest', 'Run TypeScript tests')) {
        Assert-ExternalCommand 'npx'

        Push-Location $extensionDir
        try {
            $relativeConfig = [System.IO.Path]::GetRelativePath($extensionDir, $vitestConfigPath)
            npx vitest run --config $relativeConfig
            if ($LASTEXITCODE -ne 0) { throw 'TypeScript tests failed.' }
            Write-Status 'TypeScript tests passed' 'OK'
        }
        finally {
            Pop-Location
        }
    }
}

function Invoke-TestDotNet {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Section 'Test .NET'

    if (-not $solutionFile) { throw 'No .slnx or .sln file found in the repository root.' }

    if ($PSCmdlet.ShouldProcess($solutionFile.Name, 'dotnet test --no-build')) {
        Assert-ExternalCommand 'dotnet'

        dotnet test $solutionFile.FullName -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw '.NET tests failed.' }
        Write-Status '.NET tests passed' 'OK'
    }
}

function Invoke-CopyAssets {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    $assets = @(
        @{ Source = 'LICENSE';       Label = 'LICENSE' }
        @{ Source = 'logo.png';      Label = 'logo.png' }
        @{ Source = 'small-logo.png'; Label = 'small-logo.png' }
    )

    foreach ($asset in $assets) {
        $source      = Join-Path $repoRoot $asset.Source
        $destination = Join-Path $extensionDir $asset.Source
        if ($PSCmdlet.ShouldProcess($destination, "Copy $($asset.Label)")) {
            Copy-Item -LiteralPath $source -Destination $destination -Force
            Write-Status "$($asset.Label) copied to extension" 'OK'
        }
    }
}

function Invoke-DotNetPublish {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Rid)

    Write-Section "Publish .NET server ($Rid)"
    if (-not $serverProjectPath) { throw 'No non-test .NET project found in the solution.' }
    if ($PSCmdlet.ShouldProcess("$serverProjectPath → $Rid", 'dotnet publish')) {
        Assert-ExternalCommand 'dotnet'

        if (Test-Path $mcpDir) { Remove-Item $mcpDir -Recurse -Force }

        $publishArgs = @(
            'publish'
            '-c', 'Release'
            '-r', $Rid
            '--self-contained'
            '-p:PublishSingleFile=true'
        )

        $serverName = [System.IO.Path]::GetFileNameWithoutExtension($serverProjectPath)
        dotnet @publishArgs $serverProjectPath -o (Join-Path $mcpDir $serverName)
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Rid." }
        Write-Status ".NET server published ($Rid)" 'OK'
    }
}

function Invoke-VscePackage {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Rid)

    $vsceTarget = $ridToTarget[$Rid]
    if (-not $vsceTarget) {
        throw "No VS Code target mapping for runtime identifier '$Rid'."
    }

    Write-Section "Package VSIX ($vsceTarget)"

    if ($PSCmdlet.ShouldProcess("vsce package --target $vsceTarget", 'Package extension')) {
        Assert-ExternalCommand 'npx'

        $env:SHARPPILOT_VSCE_BYPASS = '1'
        Push-Location $extensionDir
        try {
            npx vsce package --target $vsceTarget --allow-missing-repository
            if ($LASTEXITCODE -ne 0) { throw 'vsce package failed.' }

            New-Item $publishDir -ItemType Directory -Force | Out-Null
            Move-Item (Join-Path $extensionDir '*.vsix') $publishDir -Force
            Write-Status "VSIX packaged ($vsceTarget)" 'OK'
        }
        finally {
            Pop-Location
            Remove-Item Env:\SHARPPILOT_VSCE_BYPASS -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-VscePublish {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Section 'Publish to Marketplace'

    $vsixFiles = Get-ChildItem (Join-Path $publishDir '*.vsix') -ErrorAction SilentlyContinue
    if (-not $vsixFiles -or $vsixFiles.Count -eq 0) {
        throw 'No VSIX files found in publish/ directory.'
    }

    if ($PSCmdlet.ShouldProcess("$($vsixFiles.Count) VSIX file(s)", 'Publish to Marketplace')) {
        Assert-ExternalCommand 'npx'

        Push-Location $extensionDir
        try {
            foreach ($vsix in $vsixFiles) {
                Write-Status "Publishing $($vsix.Name)..." 'INFO'
                npx vsce publish --packagePath $vsix.FullName
                if ($LASTEXITCODE -ne 0) { throw "Failed to publish $($vsix.Name)." }
                Write-Status "Published $($vsix.Name)" 'OK'
            }
        }
        finally {
            Pop-Location
        }
    }
}

function Resolve-RuntimeIdentifier {
    if ($RuntimeIdentifier) { return $RuntimeIdentifier }

    $detected = dotnet --info |
        Select-String 'RID:\s+(\S+)' |
        ForEach-Object { $_.Matches[0].Groups[1].Value }

    if (-not $detected) {
        throw 'Could not detect the runtime identifier. Pass -RuntimeIdentifier explicitly.'
    }

    Write-Status "Detected runtime identifier: $detected" 'INFO'
    return $detected
}

# ── Composite actions ────────────────────────────────────────────────────────

function Invoke-Compile {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Scope = 'All')

    Write-Header 'Compile'
    if ($Scope -in 'All', 'TS')     { Invoke-CompileTS }
    if ($Scope -in 'All', 'DotNet') { Invoke-CompileDotNet }
}

function Invoke-Test {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Scope = 'All')

    Write-Header 'Test'
    if ($Scope -in 'All', 'TS')     { Invoke-TestTS }
    if ($Scope -in 'All', 'DotNet') { Invoke-TestDotNet }
}

function Invoke-Prepare {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Invoke-Clean
    Invoke-Compile -Scope 'All'
    Invoke-Test -Scope 'All'

    Write-Header 'Prepare'
    Invoke-CopyAssets
}

function Invoke-Package {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Scope)

    Invoke-Prepare

    Write-Header 'Package'

    if ($Scope -eq 'All') {
        # Explicit "Package All" — build all six platforms
        foreach ($rid in $ridToTarget.Keys) {
            Invoke-DotNetPublish -Rid $rid
            Invoke-VscePackage -Rid $rid
        }

        # Clean up staging directory — each VSIX already contains its server binary
        if (Test-Path $mcpDir) { Remove-Item $mcpDir -Recurse -Force }
    }
    else {
        # Single platform: explicit -RuntimeIdentifier or auto-detect
        $rid = Resolve-RuntimeIdentifier
        Invoke-DotNetPublish -Rid $rid
        Invoke-VscePackage -Rid $rid
    }
}

function Invoke-Publish {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Scope)

    Invoke-Prepare

    Write-Header 'Publish'

    # Read after Prepare so the manifest is up to date
    $packageJson = Get-Content (Join-Path $extensionDir 'package.json') -Raw | ConvertFrom-Json
    $name = $packageJson.name
    $version = $packageJson.version

    New-Item $publishDir -ItemType Directory -Force | Out-Null

    # Explicit "Publish All" = all platforms; otherwise single platform
    $rids = if ($Scope -eq 'All') { $ridToTarget.Keys } else { @(Resolve-RuntimeIdentifier) }

    foreach ($rid in $rids) {
        $vsceTarget = $ridToTarget[$rid]
        $vsixName = "$name-$vsceTarget-$version.vsix"
        $vsixPath = Join-Path $publishDir $vsixName

        if (-not $WhatIfPreference -and (Test-Path $vsixPath)) {
            Write-Status "Found existing $vsixName — skipping build for $rid" 'INFO'
        }
        else {
            Invoke-DotNetPublish -Rid $rid
            Invoke-VscePackage -Rid $rid
        }
    }

    Invoke-VscePublish
}

function Invoke-Clean {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Header 'Clean'

    $targets = @()

    $targets += @{ Path = (Join-Path $extensionDir 'out');     Label = 'TypeScript output (out/)' }
    $targets += @{ Path = $mcpDir;                            Label = 'MCP servers (mcp/)' }
    $targets += @{ Path = $publishDir;                         Label = 'VSIX packages (publish/)' }
    $targets += @{ Path = (Join-Path $extensionDir 'LICENSE');       Label = 'Extension LICENSE copy' }
    $targets += @{ Path = (Join-Path $extensionDir 'logo.png');       Label = 'Extension logo.png copy' }
    $targets += @{ Path = (Join-Path $extensionDir 'small-logo.png'); Label = 'Extension small-logo.png copy' }

    $instructionsDir = Join-Path $extensionDir 'instructions'
    $targets += @{ Path = (Join-Path $instructionsDir '.generated');  Label = 'Generated instructions (.generated/)' }
    $targets += @{ Path = (Join-Path $instructionsDir '.workspaces'); Label = 'Workspace instructions (.workspaces/)' }

    foreach ($project in $dotnetProjects) {
        $projectDir = Split-Path $project -Parent
        $projectName = Split-Path $projectDir -Leaf
        $targets += @{ Path = (Join-Path $projectDir 'bin'); Label = "$projectName bin/" }
        $targets += @{ Path = (Join-Path $projectDir 'obj'); Label = "$projectName obj/" }
    }

    foreach ($entry in $targets) {
        if (Test-Path $entry.Path) {
            if ($PSCmdlet.ShouldProcess($entry.Path, "Delete $($entry.Label)")) {
                Remove-Item $entry.Path -Recurse -Force
                Write-Status "Deleted $($entry.Label)" 'OK'
            }
        }
        else {
            Write-Status "$($entry.Label) — not found, skipping" 'INFO'
        }
    }
}

# ── Main ─────────────────────────────────────────────────────────────────────

if ($Help) {
    Show-Help
    exit 0
}

# Normalize target aliases
if ($Target -eq 'TypeScript') { $Target = 'TS' }
if ($Target -eq '.NET')       { $Target = 'DotNet' }

# Validate mutually exclusive options
if ($RuntimeIdentifier -and $Target -eq 'All') {
    throw '-RuntimeIdentifier and Target ''All'' are mutually exclusive.'
}

if ($Clean -and $Action -in 'Prepare', 'Package', 'Publish') {
    throw "-Clean and '$Action' are mutually exclusive — $Action already performs a clean."
}

$resolvedTarget = if ($Target) { $Target } else { 'All' }

if ($extensionVersion) {
    Write-Host "SharpPilot v$extensionVersion" -ForegroundColor Magenta
    Write-Host ''
}

if ($Clean) {
    Invoke-Clean
}

if (-not $Action -and -not $Clean) {
    # Default: Compile + Test
    Invoke-Compile -Scope $resolvedTarget
    Invoke-Test -Scope $resolvedTarget
}
elseif ($Action) {
    switch ($Action) {
        'Compile' { Invoke-Compile -Scope $resolvedTarget }
        'Test'    { Invoke-Test -Scope $resolvedTarget }
        'Prepare' { Invoke-Prepare }
        'Package' { Invoke-Package -Scope $Target }
        'Publish' { Invoke-Publish -Scope $Target }
    }
}
