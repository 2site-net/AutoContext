#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Centralized version tool for AutoContext.

.DESCRIPTION
    Reads the canonical version from version.json at the repository root
    and stamps it into project files.

.PARAMETER Command
    Export       — Write a TypeScript version constant to the given path.
    Sync         — Stamp version into all package.json, package-lock.json,
                   and .csproj files discovered from the solution.
    SyncAndExport — Run Sync then Export.

.PARAMETER Path
    For Export / SyncAndExport: the file path (relative to the current
    working directory, or absolute) where the generated TypeScript
    constant is written.

.EXAMPLE
    .\versionize.ps1 Export  src/AutoContext.Mcp.Web/src/version.ts
    .\versionize.ps1 Sync
    .\versionize.ps1 SyncAndExport src/AutoContext.Mcp.Web/src/version.ts
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('Export', 'Sync', 'SyncAndExport')]
    [string]$Command,

    [Parameter(Position = 1)]
    [string]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$versionJsonPath = Join-Path $repoRoot 'version.json'

if (-not (Test-Path $versionJsonPath)) {
    throw "version.json not found at $versionJsonPath"
}

$version = (Get-Content $versionJsonPath -Raw | ConvertFrom-Json).version
if (-not $version) {
    throw 'version.json does not contain a "version" property.'
}

# ── Export ───────────────────────────────────────────────────────────────────

function Export-VersionTs {
    param([Parameter(Mandatory)][string]$TargetPath)

    $resolvedPath = if ([System.IO.Path]::IsPathRooted($TargetPath)) {
        $TargetPath
    }
    else {
        Join-Path (Get-Location).Path $TargetPath
    }

    $content = "export const VERSION = `"$version`";" + [System.Environment]::NewLine
    Set-Content -LiteralPath $resolvedPath -Value $content -NoNewline
    Write-Host "Exported version $version -> $resolvedPath"
}

# ── Sync ─────────────────────────────────────────────────────────────────────

function Sync-AllVersions {
    # ── Discover npm directories (any src/*/ with package.json) ──
    $npmDirs = @(Get-ChildItem (Join-Path $repoRoot 'src') -Filter 'package.json' -Recurse -Depth 1 |
        ForEach-Object { $_.Directory.FullName })

    foreach ($dir in $npmDirs) {
        $pkgPath = Join-Path $dir 'package.json'
        $lockPath = Join-Path $dir 'package-lock.json'
        $dirName = Split-Path $dir -Leaf

        if (Test-Path $pkgPath) {
            $raw = Get-Content $pkgPath -Raw
            $updated = $raw -replace '"version":\s*"[^"]*"', "`"version`": `"$version`""
            if ($updated -ne $raw) {
                Set-Content $pkgPath $updated -NoNewline
                Write-Host "Synced $dirName/package.json -> $version"
            }
        }

        if (Test-Path $lockPath) {
            $lockRaw = Get-Content $lockPath -Raw
            $lockJson = $lockRaw | ConvertFrom-Json -AsHashtable
            $currentLockVersion = $lockJson['version']

            if ($currentLockVersion -ne $version) {
                $pattern = [regex]::new('"version":\s*"' + [regex]::Escape($currentLockVersion) + '"')
                $lockRaw = $pattern.Replace($lockRaw, "`"version`": `"$version`"", 2)
                Set-Content $lockPath $lockRaw -NoNewline
                Write-Host "Synced $dirName/package-lock.json -> $version"
            }
        }
    }

    # ── Discover .NET projects from solution ──
    $solutionFile = Get-ChildItem $repoRoot -Filter '*.slnx' -File | Select-Object -First 1
    if (-not $solutionFile) {
        $solutionFile = Get-ChildItem $repoRoot -Filter '*.sln' -File | Select-Object -First 1
    }

    if ($solutionFile -and $solutionFile.Extension -eq '.slnx') {
        [xml]$solutionXml = Get-Content $solutionFile.FullName
        $dotnetProjects = @($solutionXml.SelectNodes('//Project/@Path') |
            ForEach-Object { Join-Path $repoRoot $_.Value })

        foreach ($projectPath in $dotnetProjects) {
            $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
            if (-not (Test-Path $projectPath)) { continue }

            $raw = Get-Content $projectPath -Raw

            if ($raw -match '<Version>[^<]*</Version>') {
                $updated = $raw -replace '<Version>[^<]*</Version>', "<Version>$version</Version>"
            }
            else {
                $propGroupRegex = [regex]::new('(<PropertyGroup>)(\r?\n)')
                $updated = $propGroupRegex.Replace($raw, "`${1}`${2}    <Version>$version</Version>`${2}", 1)
            }

            if ($updated -ne $raw) {
                Set-Content $projectPath $updated -NoNewline
                Write-Host "Synced $projectName.csproj -> $version"
            }
        }
    }
}

# ── Dispatch ─────────────────────────────────────────────────────────────────

switch ($Command) {
    'Export' {
        if (-not $Path) { throw 'Export requires a target path. Usage: .\versionize.ps1 Export <path>' }
        Export-VersionTs -TargetPath $Path
    }
    'Sync' {
        Sync-AllVersions
    }
    'SyncAndExport' {
        if (-not $Path) { throw 'SyncAndExport requires a target path. Usage: .\versionize.ps1 SyncAndExport <path>' }
        Sync-AllVersions
        Export-VersionTs -TargetPath $Path
    }
}
