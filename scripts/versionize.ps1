#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Centralized version tool for AutoContext.

.DESCRIPTION
    Thin wrapper over the AutoContext.Build module. Reads the canonical
    version from version.json at the repository root and stamps it into
    project files. The stamping logic lives in the module functions
    Sync-ProjectFileVersions and Export-VersionConstant; this script only
    parses the command verb and delegates.

.PARAMETER Command
    Export        — Write a TypeScript version constant to the given path.
    Sync          — Stamp version into all package.json, package-lock.json,
                    and .csproj files discovered from the solution.
    SyncAndExport — Run Sync then Export.

.PARAMETER Path
    For Export / SyncAndExport: the file path (relative to the current
    working directory, or absolute) where the generated TypeScript
    constant is written.

.EXAMPLE
    .\scripts\versionize.ps1 Export  src/AutoContext.Worker.Web/src/version.ts
    .\scripts\versionize.ps1 Sync
    .\scripts\versionize.ps1 SyncAndExport src/AutoContext.Worker.Web/src/version.ts
    .\scripts\versionize.ps1 Sync -WhatIf
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('Export', 'Sync', 'SyncAndExport')]
    [string]$Command,

    [Parameter(Position = 1)]
    [string]$Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'AutoContext.Build.psd1') -Force

$context = Initialize-BuildContext -RepoRoot (Split-Path $PSScriptRoot -Parent)

switch ($Command) {
    'Export' {
        if (-not $Path) { throw 'Export requires a target path. Usage: scripts/versionize.ps1 Export <path>' }
        Export-VersionConstant -Context $context -TargetPath $Path -WhatIf:$WhatIfPreference
    }
    'Sync' {
        Sync-ProjectFileVersions -Context $context -WhatIf:$WhatIfPreference
    }
    'SyncAndExport' {
        if (-not $Path) { throw 'SyncAndExport requires a target path. Usage: scripts/versionize.ps1 SyncAndExport <path>' }
        Sync-ProjectFileVersions -Context $context -WhatIf:$WhatIfPreference
        Export-VersionConstant -Context $context -TargetPath $Path -WhatIf:$WhatIfPreference
    }
}
