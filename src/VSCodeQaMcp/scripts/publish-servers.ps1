#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Publishes the MCP servers as self-contained executables into the extension's
  servers/ directory. Optionally packages and/or publishes the extension.

.PARAMETER RuntimeIdentifier
  The .NET runtime identifier to publish for (e.g. win-x64, osx-arm64, linux-x64).
  Defaults to the current platform. Ignored when -All is set.

.PARAMETER Package
  When set, also packages the extension into a platform-specific VSIX in the
  publish/ directory.

.PARAMETER All
  When set, builds and packages the extension for all supported platforms.
  Implies -Package.

.PARAMETER Publish
  When set, publishes VSIX files to the VS Code Marketplace. For each platform,
  if a matching VSIX already exists in publish/, it is reused; otherwise that
  platform is built and packaged first.
#>
param(
    [string]$RuntimeIdentifier,
    [switch]$Package,
    [switch]$All,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'

$ridToTarget = @{
    'win-x64'     = 'win32-x64'
    'win-arm64'   = 'win32-arm64'
    'linux-x64'   = 'linux-x64'
    'linux-arm64' = 'linux-arm64'
    'osx-x64'     = 'darwin-x64'
    'osx-arm64'   = 'darwin-arm64'
}

if ($Publish) { $Package = $true }
if ($All) { $Package = $true }

$extensionDir = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $extensionDir)

function Invoke-BuildAndPackage([string]$rid) {
    $serversDir = Join-Path $extensionDir 'servers'

    if (Test-Path $serversDir) { Remove-Item $serversDir -Recurse -Force }

    $publishArgs = @(
        'publish'
        '-c', 'Release'
        '-r', $rid
        '--self-contained'
        '-p:PublishSingleFile=true'
    )

    dotnet @publishArgs `
        "$repoRoot/src/QaMcp/QaMcp.csproj" `
        -o (Join-Path $serversDir 'QaMcp')

    if ($Package) {
        $vsceTarget = $ridToTarget[$rid]
        if (-not $vsceTarget) {
            throw "No VS Code target mapping for runtime identifier '$rid'."
        }

        Push-Location $extensionDir
        try {
            npx vsce package --target $vsceTarget --allow-missing-repository
            if ($LASTEXITCODE -ne 0) { throw 'vsce package failed.' }

            $publishDir = Join-Path $extensionDir 'publish'
            New-Item $publishDir -ItemType Directory -Force | Out-Null
            Move-Item *.vsix $publishDir -Force
        }
        finally {
            Pop-Location
        }
    }
}

if ($Publish) {
    $publishDir = Join-Path $extensionDir 'publish'
    New-Item $publishDir -ItemType Directory -Force | Out-Null

    $packageJson = Get-Content (Join-Path $extensionDir 'package.json') -Raw | ConvertFrom-Json
    $name = $packageJson.name
    $version = $packageJson.version

    foreach ($rid in $ridToTarget.Keys) {
        $vsceTarget = $ridToTarget[$rid]
        $vsixName = "$name-$vsceTarget-$version.vsix"
        $vsixPath = Join-Path $publishDir $vsixName

        if (Test-Path $vsixPath) {
            Write-Host "Found existing $vsixName — skipping build for $rid."
        }
        else {
            Write-Host "No VSIX for $rid — building..."
            Invoke-BuildAndPackage $rid
        }
    }

    $vsixFiles = Get-ChildItem (Join-Path $publishDir '*.vsix')
    if ($vsixFiles.Count -eq 0) {
        throw 'No VSIX files found in publish/ directory.'
    }

    Push-Location $extensionDir
    try {
        foreach ($vsix in $vsixFiles) {
            Write-Host "`nPublishing $($vsix.Name)..."
            npx vsce publish --packagePath $vsix.FullName
            if ($LASTEXITCODE -ne 0) { throw "Failed to publish $($vsix.Name)." }
        }
    }
    finally {
        Pop-Location
    }
}
elseif ($All) {
    foreach ($rid in $ridToTarget.Keys) {
        Write-Host "`n=== Building for $rid ===`n"
        Invoke-BuildAndPackage $rid
    }
}
else {
    if (-not $RuntimeIdentifier) {
        $RuntimeIdentifier = dotnet --info |
            Select-String 'RID:\s+(\S+)' |
            ForEach-Object { $_.Matches[0].Groups[1].Value }

        if (-not $RuntimeIdentifier) {
            throw 'Could not detect the runtime identifier. Pass -RuntimeIdentifier explicitly.'
        }

        Write-Host "Detected runtime identifier: $RuntimeIdentifier"
    }

    Invoke-BuildAndPackage $RuntimeIdentifier
}
