#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Publishes the MCP servers as self-contained executables into the extension's
  servers/ directory.

.PARAMETER RuntimeIdentifier
  The .NET runtime identifier to publish for (e.g. win-x64, osx-arm64, linux-x64).
  Defaults to the current platform.
#>
param(
    [string]$RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'

if (-not $RuntimeIdentifier) {
    $RuntimeIdentifier = dotnet --info |
        Select-String 'RID:\s+(\S+)' |
        ForEach-Object { $_.Matches[0].Groups[1].Value }

    if (-not $RuntimeIdentifier) {
        throw 'Could not detect the runtime identifier. Pass -RuntimeIdentifier explicitly.'
    }

    Write-Host "Detected runtime identifier: $RuntimeIdentifier"
}

$extensionDir = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $extensionDir
$serversDir = Join-Path $extensionDir 'servers'

if (Test-Path $serversDir) { Remove-Item $serversDir -Recurse -Force }

$publishArgs = @(
    'publish'
    '-c', 'Release'
    '-r', $RuntimeIdentifier
    '--self-contained'
    '-p:PublishSingleFile=true'
)

dotnet @publishArgs `
    "$repoRoot/DotNetQaMcp/src/DotNetQaMcp/DotNetQaMcp.csproj" `
    -o (Join-Path $serversDir 'DotNetQaMcp')

dotnet @publishArgs `
    "$repoRoot/GitQaMcp/src/GitQaMcp/GitQaMcp.csproj" `
    -o (Join-Path $serversDir 'GitQaMcp')
