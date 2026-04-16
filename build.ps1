#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Build orchestrator for AutoContext.

.DESCRIPTION
    Compiles, tests, packages, and publishes both the TypeScript VS Code extension
    and the .NET MCP server from a single entry point.

    After modifying this script, run build.tests.ps1 to verify all action/target
    combinations still work. If a test fails, determine whether the script has a
    bug or the test expectations need updating to match the new behaviour.

.PARAMETER Action
    The build action to perform:
      Compile  — compile TypeScript and/or .NET sources
      Test     — run unit tests (without compiling)
      Prepare  — Clean + Compile + Test + copy assets into extension
      Package  — Prepare + dotnet publish + vsce package
      Publish  — Package + vsce publish
      Tag      — Compile + Test + bump versions + git commit + annotated tag

    When omitted, defaults to Compile + Test.

.PARAMETER Target
    Narrows the scope of an action:
      TS (or TypeScript) — TypeScript only
      DotNet (or .NET)   — .NET only
      All                — both (default for Compile/Test)

    For Package/Publish, 'All' builds all six platform targets.
    When omitted for Package/Publish, auto-detects the current platform.

    For Tag, this positional slot accepts the version string (X.Y.Z or
    X.Y.Z-prerelease) instead of a target name.

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
    .\build.ps1 Tag 0.6.0                        # Bump, compile, test, commit, tag
    .\build.ps1 Tag 0.6.0-alpha                  # Prerelease tag
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
    [ValidateSet('Compile', 'Test', 'Prepare', 'Package', 'Publish', 'Tag')]
    [string]$Action,

    [Parameter(Position = 1)]
    [ArgumentCompleter({
        param($commandName, $parameterName, $wordToComplete)
        @('All', 'TS', 'TypeScript', 'DotNet', '.NET') |
            Where-Object { $_ -like "$wordToComplete*" }
    })]
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
$extensionDir = Join-Path $repoRoot 'src' 'AutoContext.VsCode'

# Discover vitest configs
$vitestConfigs = @(Get-ChildItem $repoRoot -Filter 'vitest.config.ts' -Recurse -File -Depth 4)
$vitestConfigPath = if ($vitestConfigs.Count -gt 0) { $vitestConfigs[0].FullName } else { $null }

$serversDir = Join-Path $extensionDir 'servers'
$publishDir = Join-Path $extensionDir 'publish'

# Web MCP server directory (TypeScript/Node.js-based server)
$webServerDir = Join-Path $repoRoot 'src' 'AutoContext.Mcp.Web'

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

$serverProjectPaths = @($dotnetProjects | Where-Object {
    [xml]$csproj = Get-Content $_
    $outputType = $csproj.SelectSingleNode('//OutputType')?.InnerText
    $isPackable = $csproj.SelectSingleNode('//IsPackable')?.InnerText
    $outputType -eq 'Exe' -and $isPackable -ne 'false'
})

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

function Invoke-WithRetry {
    param(
        [Parameter(Mandatory)][scriptblock]$ScriptBlock,
        [scriptblock]$IsRetryable = { $false },
        [int]$MaxAttempts = 3,
        [int]$DelaySeconds = 30
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        $output = & $ScriptBlock
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            return @{ Output = $output; ExitCode = 0 }
        }

        $retryable = & $IsRetryable $output
        if (-not $retryable -or $attempt -eq $MaxAttempts) {
            return @{ Output = $output; ExitCode = $exitCode }
        }

        Write-Status "Attempt $attempt/$MaxAttempts failed (retryable), waiting ${DelaySeconds}s..." 'INFO'
        Start-Sleep -Seconds $DelaySeconds
    }
}

# ── Help ─────────────────────────────────────────────────────────────────────

function Show-Help {
    Write-Host "`nAutoContext Build Orchestrator`n" -ForegroundColor Cyan

    Write-Host 'SYNTAX' -ForegroundColor Yellow
    Write-Host "  .\build.ps1 [Action] [Target] [-Clean] [-RuntimeIdentifier <rid>] [-WhatIf] [-Help]`n"

    Write-Host 'ACTIONS' -ForegroundColor Yellow
    Write-Host '  (none)     Compile + Test (all sources)'
    Write-Host '  Compile    Compile TypeScript and/or .NET sources'
    Write-Host '  Test       Run unit tests (without compiling)'
    Write-Host '  Prepare    Clean + Compile + Test + copy assets into extension'
    Write-Host '  Package    Prepare + dotnet publish + vsce package'
    Write-Host '  Publish    Package + vsce publish'
    Write-Host "  Tag        Compile + Test + bump versions + git commit + annotated tag`n"

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
    Write-Host '  .\build.ps1 Tag 0.6.0                         # Bump, test, commit, tag'
    Write-Host '  .\build.ps1 Tag 0.6.0-alpha                   # Prerelease tag'
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

function Compare-SemVer {
    <#
    .SYNOPSIS
        Compares two semver strings. Returns positive if New > Current,
        negative if New < Current, zero if equal.
    #>
    [OutputType([int])]
    param(
        [Parameter(Mandatory)][string]$Current,
        [Parameter(Mandatory)][string]$New
    )

    $semverPattern = '^(\d+)\.(\d+)\.(\d+)(?:-(.+))?$'

    if ($Current -notmatch $semverPattern) { throw "Invalid current version: '$Current'" }
    $curBase = [System.Version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"
    $curPre = $Matches[4]

    if ($New -notmatch $semverPattern) { throw "Invalid new version: '$New'" }
    $newBase = [System.Version]"$($Matches[1]).$($Matches[2]).$($Matches[3])"
    $newPre = $Matches[4]

    $baseCmp = $newBase.CompareTo($curBase)
    if ($baseCmp -ne 0) { return $baseCmp }

    # Same base: release (no prerelease) beats any prerelease
    if (-not $curPre -and -not $newPre) { return 0 }
    if ($curPre -and -not $newPre) { return 1 }
    if (-not $curPre -and $newPre) { return -1 }

    # Both have prerelease — compare dot-separated identifiers per semver spec
    $curIds = $curPre -split '\.'
    $newIds = $newPre -split '\.'
    $count = [math]::Max($curIds.Count, $newIds.Count)

    for ($i = 0; $i -lt $count; $i++) {
        if ($i -ge $curIds.Count) { return 1 }
        if ($i -ge $newIds.Count) { return -1 }

        $curId = $curIds[$i]
        $newId = $newIds[$i]
        $curIsNum = $curId -match '^\d+$'
        $newIsNum = $newId -match '^\d+$'

        if ($curIsNum -and $newIsNum) {
            $cmp = ([int]$newId).CompareTo([int]$curId)
            if ($cmp -ne 0) { return $cmp }
        }
        elseif ($curIsNum) { return 1 }
        elseif ($newIsNum) { return -1 }
        else {
            $cmp = [string]::Compare($newId, $curId, [System.StringComparison]::Ordinal)
            if ($cmp -ne 0) { return [math]::Sign($cmp) }
        }
    }

    return 0
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
            Write-Status 'Installing extension dependencies...' 'INFO'
            npm install
            if ($LASTEXITCODE -ne 0) { throw 'Extension npm install failed.' }

            Write-Status 'Generating chat-instructions manifest...' 'INFO'
            npx tsx src/chat-instructions-manifest.ts
            if ($LASTEXITCODE -ne 0) { throw 'Chat-instructions manifest generation failed.' }
            Write-Status 'Chat-instructions manifest generated' 'OK'

            Write-Status 'Generating MCP tools manifest...' 'INFO'
            npx tsx src/mcp-tools-manifest.ts
            if ($LASTEXITCODE -ne 0) { throw 'MCP tools manifest generation failed.' }
            Write-Status 'MCP tools manifest generated' 'OK'

            Write-Status 'Compiling TypeScript...' 'INFO'
            npx tsc -p ./tsconfig.build.json
            if ($LASTEXITCODE -ne 0) { throw 'TypeScript compilation failed.' }
            Write-Status 'TypeScript compiled' 'OK'
        }
        finally {
            Pop-Location
        }

        if (Test-Path $webServerDir) {
            Push-Location $webServerDir
            try {
                Write-Status 'Installing Web MCP server dependencies...' 'INFO'
                npm install
                if ($LASTEXITCODE -ne 0) { throw 'Web MCP server npm install failed.' }

                Write-Status 'Compiling Web MCP server...' 'INFO'
                npx tsc -p ./tsconfig.build.json
                if ($LASTEXITCODE -ne 0) { throw 'Web MCP server compilation failed.' }
                Write-Status 'Web MCP server compiled' 'OK'
            }
            finally {
                Pop-Location
            }
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

    if ($vitestConfigs.Count -eq 0) { throw 'No vitest.config.ts found — cannot locate TypeScript tests.' }

    if ($PSCmdlet.ShouldProcess('vitest', 'Run TypeScript tests')) {
        Assert-ExternalCommand 'npx'

        foreach ($config in $vitestConfigs) {
            # Resolve project root as the nearest ancestor containing package.json
            $searchDir = $config.Directory
            while ($searchDir -and -not (Test-Path (Join-Path $searchDir.FullName 'package.json'))) {
                $searchDir = $searchDir.Parent
            }
            $projectDir = if ($searchDir) { $searchDir.FullName } else { $config.Directory.FullName }
            $projectName = Split-Path $projectDir -Leaf

            Push-Location $projectDir
            try {
                $relativeConfig = [System.IO.Path]::GetRelativePath($projectDir, $config.FullName)
                npx vitest run --config $relativeConfig
                if ($LASTEXITCODE -ne 0) { throw "TypeScript tests failed ($projectName)." }
                Write-Status "TypeScript tests passed ($projectName)" 'OK'
            }
            finally {
                Pop-Location
            }
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
        @{ Source = 'LICENSE';        Label = 'LICENSE' }
        @{ Source = 'COMMERCIAL.md';  Label = 'COMMERCIAL.md' }
        @{ Source = 'TRADEMARKS.md';  Label = 'TRADEMARKS.md' }
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

function Invoke-DotNetPackage {
    [CmdletBinding(SupportsShouldProcess)]
    param([Parameter(Mandatory)][string]$Rid)

    Write-Section "Package .NET servers ($Rid)"
    if ($serverProjectPaths.Count -eq 0) { throw 'No non-test .NET projects found in the solution.' }
    if ($PSCmdlet.ShouldProcess("$($serverProjectPaths.Count) project(s) → $Rid", 'dotnet publish')) {
        Assert-ExternalCommand 'dotnet'

        $publishArgs = @(
            'publish'
            '-c', 'Release'
            '-r', $Rid
            '--self-contained'
            '-p:PublishSingleFile=true'
        )

        foreach ($projectPath in $serverProjectPaths) {
            $serverName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
            $serverDir = Join-Path $serversDir $serverName
            if (Test-Path $serverDir) { Remove-Item $serverDir -Recurse -Force }
            dotnet @publishArgs $projectPath -o $serverDir
            if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $serverName ($Rid)." }
            Write-Status "$serverName packaged ($Rid)" 'OK'
        }
    }
}

function Sync-WebServerVersion {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    if (-not $extensionVersion) { return }

    $webPkgPath = Join-Path $webServerDir 'package.json'
    if (-not (Test-Path $webPkgPath)) { return }

    $webPkg = Get-Content $webPkgPath -Raw | ConvertFrom-Json
    if ($webPkg.version -eq $extensionVersion) { return }

    if ($PSCmdlet.ShouldProcess($webPkgPath, "Sync version to $extensionVersion")) {
        $raw = Get-Content $webPkgPath -Raw
        $raw = $raw -replace '"version":\s*"[^"]*"', "`"version`": `"$extensionVersion`""
        Set-Content $webPkgPath $raw -NoNewline
        Write-Status "Synced Web MCP server version to $extensionVersion" 'OK'
    }
}

function Invoke-WebServerPackage {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Section 'Package Web MCP server'

    if (-not (Test-Path $webServerDir)) {
        Write-Status 'Web MCP server directory not found — skipping' 'INFO'
        return
    }

    Sync-WebServerVersion

    $outDir = Join-Path $webServerDir 'out'
    if (-not (Test-Path $outDir)) { throw 'Web MCP server not compiled — run Compile first.' }

    $targetDir = Join-Path $serversDir 'AutoContext.Mcp.Web'

    if ($PSCmdlet.ShouldProcess($targetDir, 'Copy Web MCP server')) {
        New-Item $targetDir -ItemType Directory -Force | Out-Null
        Copy-Item (Join-Path $outDir '*') $targetDir -Recurse -Force

        # Copy package.json first so npm install can read it from the target
        Copy-Item (Join-Path $webServerDir 'package.json') $targetDir -Force

        # Bundle production dependencies
        Push-Location $webServerDir
        try {
            npm install --omit=dev --prefix $targetDir
            if ($LASTEXITCODE -ne 0) { throw 'npm install for Web MCP server failed.' }
        }
        finally {
            Pop-Location
        }

        Write-Status 'Web MCP server packaged' 'OK'
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

        $env:AUTOCONTEXT_VSCE_BYPASS = '1'
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
            Remove-Item Env:\AUTOCONTEXT_VSCE_BYPASS -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-VscePublish {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Section 'Publish to Marketplace'

    $vsixFiles = Get-ChildItem (Join-Path $publishDir '*.vsix') -ErrorAction SilentlyContinue
    if (-not $vsixFiles -or $vsixFiles.Count -eq 0) {
        if ($WhatIfPreference) {
            Write-Status 'No VSIX files (skipped in WhatIf)' 'INFO'
            return
        }
        throw 'No VSIX files found in publish/ directory.'
    }

    if ($PSCmdlet.ShouldProcess("$($vsixFiles.Count) VSIX file(s)", 'Publish to Marketplace')) {
        Assert-ExternalCommand 'npx'

        Push-Location $extensionDir
        try {
            foreach ($vsix in $vsixFiles) {
                Write-Status "Publishing $($vsix.Name)..." 'INFO'
                $result = Invoke-WithRetry -ScriptBlock {
                    npx vsce publish --packagePath $vsix.FullName 2>&1
                } -IsRetryable {
                    param($output)
                    $output -match 'timeout|ETIMEDOUT|ECONNRESET|ECONNREFUSED|503'
                } -MaxAttempts 4 -DelaySeconds 300

                if ($result.ExitCode -ne 0) {
                    if ($result.Output -match 'already exists') {
                        Write-Status "Skipped $($vsix.Name) (already published)" 'INFO'
                    }
                    else {
                        $result.Output | Write-Host
                        throw "Failed to publish $($vsix.Name)."
                    }
                }
                else {
                    $result.Output | Write-Host
                    Write-Status "Published $($vsix.Name)" 'OK'
                }
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

function Update-ProjectVersion {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$OldVersion,
        [Parameter(Mandatory)][string]$NewVersion
    )

    Write-Section 'Update versions'

    # ── npm projects ──
    $npmDirs = @($extensionDir)
    if (Test-Path $webServerDir) { $npmDirs += $webServerDir }

    foreach ($dir in $npmDirs) {
        $pkgPath = Join-Path $dir 'package.json'
        $lockPath = Join-Path $dir 'package-lock.json'
        $dirName = Split-Path $dir -Leaf

        if (Test-Path $pkgPath) {
            if ($PSCmdlet.ShouldProcess($pkgPath, "Update version to $NewVersion")) {
                $raw = Get-Content $pkgPath -Raw
                $updated = $raw -replace ('"version":\s*"' + [regex]::Escape($OldVersion) + '"'), "`"version`": `"$NewVersion`""
                if ($updated -eq $raw) { throw "Version '$OldVersion' not found in $pkgPath" }
                Set-Content $pkgPath $updated -NoNewline
                Write-Status "$dirName/package.json -> $NewVersion" 'OK'
            }
        }

        if (Test-Path $lockPath) {
            if ($PSCmdlet.ShouldProcess($lockPath, "Update version to $NewVersion")) {
                $lockRaw = Get-Content $lockPath -Raw

                # Validate root version before replacing
                $lockJson = $lockRaw | ConvertFrom-Json -AsHashtable
                if ($lockJson['version'] -ne $OldVersion) {
                    throw "package-lock.json root version is '$($lockJson['version'])', expected '$OldVersion'."
                }

                # Replace only the first two occurrences (root + packages."" entry)
                $pattern = [regex]::new('"version":\s*"' + [regex]::Escape($OldVersion) + '"')
                $lockRaw = $pattern.Replace($lockRaw, "`"version`": `"$NewVersion`"", 2)
                Set-Content $lockPath $lockRaw -NoNewline
                Write-Status "$dirName/package-lock.json -> $NewVersion" 'OK'
            }
        }
    }

    # ── .csproj files ──
    foreach ($projectPath in $dotnetProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
        if ($PSCmdlet.ShouldProcess($projectPath, "Update version to $NewVersion")) {
            $raw = Get-Content $projectPath -Raw

            if ($raw -match '<Version>[^<]*</Version>') {
                $raw = $raw -replace '<Version>[^<]*</Version>', "<Version>$NewVersion</Version>"
            }
            else {
                $propGroupRegex = [regex]::new('(<PropertyGroup>)(\r?\n)')
                $raw = $propGroupRegex.Replace($raw, "`${1}`${2}    <Version>$NewVersion</Version>`${2}", 1)
            }

            Set-Content $projectPath $raw -NoNewline
            Write-Status "$projectName -> $NewVersion" 'OK'
        }
    }
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

    Invoke-WebServerPackage

    if ($Scope -eq 'All') {
        # Explicit "Package All" — build all six platforms
        foreach ($rid in $ridToTarget.Keys) {
            Invoke-DotNetPackage -Rid $rid
            Invoke-VscePackage -Rid $rid
        }

        # Clean up staging directory — each VSIX already contains its server binary
        if (Test-Path $serversDir) { Remove-Item $serversDir -Recurse -Force }
    }
    else {
        # Single platform: explicit -RuntimeIdentifier or auto-detect
        $rid = Resolve-RuntimeIdentifier
        Invoke-DotNetPackage -Rid $rid
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

    Invoke-WebServerPackage

    foreach ($rid in $rids) {
        $vsceTarget = $ridToTarget[$rid]
        $vsixName = "$name-$vsceTarget-$version.vsix"
        $vsixPath = Join-Path $publishDir $vsixName

        if (-not $WhatIfPreference -and (Test-Path $vsixPath)) {
            Write-Status "Found existing $vsixName — skipping build for $rid" 'INFO'
        }
        else {
            Invoke-DotNetPackage -Rid $rid
            Invoke-VscePackage -Rid $rid
        }
    }

    Invoke-VscePublish
}

function Undo-PreviousTag {
    <#
    .SYNOPSIS
        If a previous local-only tag attempt for the same version exists,
        automatically undo it so the Tag action can re-run cleanly.
        Returns $true if a previous attempt was undone, $false otherwise.
    #>
    [CmdletBinding(SupportsShouldProcess)]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][string]$Version
    )

    $existingTag = git tag -l $Version
    if (-not $existingTag) { return $false }

    # Tag exists — check safety conditions for auto-undo
    $tagSha = git rev-parse "refs/tags/$Version" 2>&1
    $headSha = git rev-parse HEAD 2>&1
    $tagPointsToHead = $tagSha -eq $headSha

    # An annotated tag's ref resolves to the tag object, not the commit.
    # Dereference to get the commit it points to.
    if (-not $tagPointsToHead) {
        $tagCommitSha = git rev-parse "${Version}^{commit}" 2>&1
        $tagPointsToHead = $tagCommitSha -eq $headSha
    }

    if (-not $tagPointsToHead) {
        throw "Tag '$Version' already exists but does not point to HEAD. Delete it manually."
    }

    $headMsg = git log -1 --format='%s' HEAD
    if ($headMsg -ne "chore: bump version to $Version") {
        throw "Tag '$Version' already exists but HEAD commit message does not match expected bump commit. Delete it manually."
    }

    # Check the tag is not on any remote
    $remoteTags = git ls-remote --tags origin "refs/tags/$Version" 2>&1
    if ($remoteTags) {
        throw "Tag '$Version' already exists and has been pushed to remote. Delete it manually."
    }

    # All safety checks passed — undo
    if ($PSCmdlet.ShouldProcess("tag $Version + bump commit", 'Undo previous tag attempt')) {
        Write-Section 'Undo previous tag attempt'

        git tag -d $Version 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Failed to delete local tag '$Version'." }
        Write-Status "Deleted local tag $Version" 'OK'

        git reset --mixed HEAD~1 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to reset bump commit.' }
        Write-Status 'Reset bump commit' 'OK'

        git checkout -- . 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Failed to restore working tree.' }
        Write-Status 'Restored working tree' 'OK'

        return $true
    }

    # WhatIf mode — nothing was actually undone
    return $false
}

function Invoke-Tag {
    [CmdletBinding(SupportsShouldProcess)]
    param(
        [Parameter(Mandatory)][string]$Version
    )

    Write-Header 'Tag'

    # ── Validate format ──
    $semverPattern = '^\d+\.\d+\.\d+(-[a-zA-Z0-9]+([.][a-zA-Z0-9]+)*)?$'
    if ($Version -notmatch $semverPattern) {
        throw "Invalid version '$Version'. Expected format: X.Y.Z or X.Y.Z-prerelease"
    }

    Assert-ExternalCommand 'git'

    # ── Auto-undo previous local-only tag attempt ──
    $wasUndone = Undo-PreviousTag -Version $Version
    $currentVersion = if ($wasUndone) {
        # Re-read version from disk since the undo reverted the bump
        (Get-Content $packageJsonPath -Raw | ConvertFrom-Json).version
    }
    else {
        $extensionVersion
    }

    # ── Validate version ──
    if (-not $currentVersion) {
        throw "Cannot read current version from $packageJsonPath"
    }

    $versionCmp = Compare-SemVer -Current $currentVersion -New $Version
    if ($versionCmp -lt 0) {
        throw "Version '$Version' is less than current version '$currentVersion'."
    }

    $needsBump = $versionCmp -gt 0

    # ── Validate working tree ──
    $gitStatus = git status --porcelain 2>&1
    if ($gitStatus) {
        throw 'Working tree is not clean. Commit or stash your changes before tagging.'
    }

    # ── Build gate ──
    Invoke-Compile -Scope 'All'
    Invoke-Test -Scope 'All'

    # ── Bump versions + commit (only if version changed) ──
    if ($needsBump) {
        Write-Header 'Bump Versions'
        Update-ProjectVersion -OldVersion $currentVersion -NewVersion $Version

        Write-Section 'Git commit'
        if ($PSCmdlet.ShouldProcess("version $Version", 'Git commit')) {
            git add -A
            if ($LASTEXITCODE -ne 0) { throw 'git add failed.' }

            git commit -m "chore: bump version to $Version"
            if ($LASTEXITCODE -ne 0) { throw 'git commit failed.' }
            Write-Status "Committed version bump to $Version" 'OK'
        }
    }
    else {
        Write-Status "Version already at $Version — skipping bump" 'INFO'
    }

    # ── Git tag ──
    Write-Section 'Git tag'
    if ($PSCmdlet.ShouldProcess("version $Version", 'Create annotated tag')) {
        git tag -a $Version -m "Release $Version"
        if ($LASTEXITCODE -ne 0) { throw 'git tag failed.' }
        Write-Status "Created annotated tag $Version" 'OK'
    }

    Write-Host ''
    Write-Status 'Push with: git push origin main --follow-tags' 'INFO'
}

function Invoke-Clean {
    [CmdletBinding(SupportsShouldProcess)]
    param()

    Write-Header 'Clean'

    $targets = @()

    $targets += @{ Path = (Join-Path $extensionDir 'dist');    Label = 'TypeScript output (dist/)' }
    $targets += @{ Path = (Join-Path $webServerDir 'out');    Label = 'Web MCP server output (out/)' }
    $targets += @{ Path = $serversDir;                          Label = 'Servers (servers/)' }
    $targets += @{ Path = $publishDir;                         Label = 'VSIX packages (publish/)' }
    $targets += @{ Path = (Join-Path $extensionDir 'LICENSE');       Label = 'Extension LICENSE copy' }

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

if ($extensionVersion) {
    Write-Host "AutoContext v$extensionVersion" -ForegroundColor Magenta
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
        'Tag'     { Invoke-Tag -Version $Version }
    }
}
