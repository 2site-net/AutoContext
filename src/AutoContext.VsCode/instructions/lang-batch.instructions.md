---
name: "lang-batch (v1.0.0)"
description: "Apply when writing or reviewing Windows Batch (CMD) scripts (variables, error checking, quoting, structure)."
applyTo: "**/*.{bat,cmd}"
---

# Batch (CMD) Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Safety & Structure

- [INST0001] **Do** start scripts with `@echo off` to suppress command echoing, followed by `setlocal EnableDelayedExpansion` when variables are set and read inside blocks.
- [INST0002] **Do** use `exit /b <code>` to return an exit code without closing the parent `cmd.exe` session.
- [INST0003] **Do** check `%ERRORLEVEL%` (or use `if errorlevel 1`) after critical commands — Batch does not stop on failure by default.
- [INST0004] **Do** end scripts with `endlocal` (or rely on implicit endlocal at script exit) to avoid leaking variables into the caller's environment.
- [INST0005] **Don't** omit `setlocal` — without it, every `set` pollutes the calling shell's environment.

### Variables & Expansion

- [INST0006] **Do** use `%variable%` for normal expansion and `!variable!` (delayed expansion) inside `for` loops and `if`/`else` blocks where the value changes within the block.
- [INST0007] **Do** wrap variable references in quotes when they may contain spaces or special characters — `"%MY_PATH%"`.
- [INST0008] **Do** use `set "var=value"` (quotes around the assignment) to avoid trailing spaces being captured in the value.
- [INST0009] **Don't** use `set var = value` — spaces around `=` become part of the variable name or value.
- [INST0010] **Don't** assume environment variables are defined — guard with `if defined VAR` or `if "%VAR%"==""` before use.

### Error Handling

- [INST0011] **Do** use `if %ERRORLEVEL% neq 0` (or `if errorlevel 1`) after commands that may fail.
- [INST0012] **Do** use `call :label args` for internal subroutines and check `%ERRORLEVEL%` on return.
- [INST0013] **Do** redirect error output to `stderr` with `1>&2` when writing diagnostic messages with `echo`.
- [INST0014] **Don't** assume commands set `ERRORLEVEL` — some built-in commands (e.g., `set`, `echo`) do not update it; test explicitly where it matters.
- [INST0015] **Don't** chain unrelated commands with `&` when the second depends on the first — use `&&` for conditional execution.

### Quoting & Special Characters

- [INST0016] **Do** use double quotes around paths and arguments that may contain spaces.
- [INST0017] **Do** escape special characters (`&`, `|`, `<`, `>`, `^`, `%`) with `^` when they appear in literals — e.g., `echo This ^& that`.
- [INST0018] **Do** use `%%i` in `for` loops inside scripts and `%i` at the interactive prompt — Batch doubles the percent sign in batch files.
- [INST0019] **Don't** use single quotes for string delimiters — Batch does not recognise them; they become literal characters.

### Security

- [INST0020] **Do** validate file paths before operations — use `if exist` to guard against missing targets and avoid path traversal.
- [INST0021] **Don't** pass unsanitised user input into commands — Batch has no built-in escaping; validate or whitelist expected values.
- [INST0022] **Don't** store secrets in plain-text variables or echo them — they are visible in process listings and logs.

### File & Path Handling

- [INST0023] **Do** use `%~dp0` to reference the script's own directory — it resolves to the drive and path of the batch file regardless of the working directory.
- [INST0024] **Do** use `pushd` / `popd` to change directories temporarily and return reliably.
- [INST0025] **Do** use `%~nx1` modifiers to extract file name and extension from arguments.
- [INST0026] **Don't** hard-code absolute paths — use environment variables (`%TEMP%`, `%USERPROFILE%`, `%ProgramFiles%`) or `%~dp0`-relative paths.

### Portability

- [INST0027] **Do** keep scripts compatible with `cmd.exe` — avoid PowerShell or Unix syntax in `.bat`/`.cmd` files.
- [INST0028] **Do** use `where` instead of `which` to locate executables on Windows.
- [INST0029] **Don't** rely on case-sensitive comparisons — `cmd.exe` environment variables and commands are case-insensitive.
