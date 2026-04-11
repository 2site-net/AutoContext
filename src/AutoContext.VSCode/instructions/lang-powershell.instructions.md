---
description: "Use when writing PowerShell scripts, modules, or automation: naming, error handling, pipeline design, security, and module structure."
applyTo: "**/*.{ps1,psm1,psd1}"
version: "1.0.0"
---
# PowerShell

## Naming & Style

- [INST0001] **Do** use `Verb-Noun` format for function and cmdlet names — use only approved verbs from `Get-Verb` (e.g., `Get-UserProfile`, `Set-Configuration`, `Invoke-BuildPipeline`).
- [INST0002] **Do** use PascalCase for function names, parameters, and public variables.
- [INST0003] **Do** use `$camelCase` for local/private variables within a function scope.
- [INST0004] **Do** prefix module-internal helper functions with a consistent private marker or avoid exporting them — keep the public surface minimal.
- [INST0005] **Do** name boolean parameters with an affirmative phrase — `$Force`, `$PassThru`, `$WhatIf`.

## Parameters & Input

- [INST0006] **Do** use `[CmdletBinding()]` and `[Parameter()]` attributes to declare parameters — they enable common parameters (`-Verbose`, `-ErrorAction`, `-WhatIf`) for free.
- [INST0007] **Do** use `[ValidateNotNullOrEmpty()]`, `[ValidateSet()]`, `[ValidateRange()]`, and `[ValidatePattern()]` to enforce constraints declaratively.
- [INST0008] **Do** support pipeline input with `[Parameter(ValueFromPipeline)]` or `[Parameter(ValueFromPipelineByPropertyName)]` when the function processes collections.
- [INST0009] **Do** declare `[OutputType()]` on advanced functions so downstream consumers know what to expect.
- [INST0010] **Don't** parse raw arguments with `$args` when `param()` block with typed parameters already handles validation, tab-completion, and help generation.

## Error Handling

- [INST0011] **Do** use `$ErrorActionPreference = 'Stop'` or `-ErrorAction Stop` to turn non-terminating errors into catchable exceptions.
- [INST0012] **Do** use `try`/`catch`/`finally` for recoverable operations — log or rethrow with context, never silently swallow.
- [INST0013] **Do** use `Write-Error` for non-terminating errors and `throw` or `$PSCmdlet.ThrowTerminatingError()` for fatal ones.
- [INST0014] **Don't** use `$ErrorActionPreference = 'SilentlyContinue'` globally — it hides real failures.
- [INST0015] **Don't** catch exceptions without handling them — at minimum, log the error record (`$_.Exception.Message`).

## Pipeline & Output

- [INST0016] **Do** emit objects to the pipeline — return structured data (`[PSCustomObject]@{…}`) rather than formatted strings.
- [INST0017] **Do** use `Write-Verbose`, `Write-Debug`, and `Write-Information` for diagnostic output — reserve the output stream for data.
- [INST0018] **Do** use `ForEach-Object`, `Where-Object`, and `Select-Object` in pipelines — they stream one item at a time and keep memory flat.
- [INST0019] **Don't** use `Write-Host` for data output — it bypasses the pipeline and cannot be captured or redirected.
- [INST0020] **Don't** mix diagnostic text with object output on the success stream — downstream consumers cannot distinguish data from noise.

## Security

- [INST0021] **Do** use `SecureString` or `PSCredential` for secrets — never store passwords in plain-text variables or logs.
- [INST0022] **Do** validate and sanitise file paths with `Resolve-Path` or `Test-Path` before use — prevent path-traversal attacks.
- [INST0023] **Do** use `-LiteralPath` instead of `-Path` when the path may contain wildcard characters.
- [INST0024] **Don't** use `Invoke-Expression` — it evaluates arbitrary strings and opens the door to injection attacks.
- [INST0025] **Don't** build commands by concatenating user-supplied strings — use splatting or parameter binding instead.

## Modules & Structure

- [INST0026] **Do** organise reusable code into modules (`.psm1` + manifest `.psd1`) with explicit `Export-ModuleMember` or `FunctionsToExport` in the manifest.
- [INST0027] **Do** keep one function per file when the module is large — use dot-sourcing in the `.psm1` to load them.
- [INST0028] **Do** pin module dependencies in the manifest `RequiredModules` to specific versions or minimum versions.
- [INST0029] **Don't** dot-source scripts from untrusted or user-controllable paths.
- [INST0030] **Don't** rely on global scope for state — pass data through parameters and return values.

## Performance

- [INST0031] **Do** prefer `foreach` statement over `ForEach-Object` cmdlet in tight loops where streaming is not needed — loop statement avoids per-item cmdlet overhead.
- [INST0032] **Do** use `[System.Collections.Generic.List[T]]` or `[System.Text.StringBuilder]` for large accumulations instead of `+=` on arrays or strings.
- [INST0033] **Don't** call `Get-ChildItem -Recurse` without `-Filter` or `-Include` on large trees — filter early to avoid scanning thousands of unnecessary items.
