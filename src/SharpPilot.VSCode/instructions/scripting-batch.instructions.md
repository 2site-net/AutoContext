---
description: "Use when writing Windows Batch (CMD) scripts: variable handling, error checking, quoting, and script structure."
applyTo: "**/*.{bat,cmd}"
---
# Batch (CMD)

## Safety & Structure

- **Do** start scripts with `@echo off` to suppress command echoing, followed by `setlocal EnableDelayedExpansion` when variables are set and read inside blocks.
- **Do** use `exit /b <code>` to return an exit code without closing the parent `cmd.exe` session.
- **Do** check `%ERRORLEVEL%` (or use `if errorlevel 1`) after critical commands — Batch does not stop on failure by default.
- **Do** end scripts with `endlocal` (or rely on implicit endlocal at script exit) to avoid leaking variables into the caller's environment.
- **Don't** omit `setlocal` — without it, every `set` pollutes the calling shell's environment.

## Variables & Expansion

- **Do** use `%variable%` for normal expansion and `!variable!` (delayed expansion) inside `for` loops and `if`/`else` blocks where the value changes within the block.
- **Do** wrap variable references in quotes when they may contain spaces or special characters — `"%MY_PATH%"`.
- **Do** use `set "var=value"` (quotes around the assignment) to avoid trailing spaces being captured in the value.
- **Don't** use `set var = value` — spaces around `=` become part of the variable name or value.
- **Don't** assume environment variables are defined — guard with `if defined VAR` or `if "%VAR%"==""` before use.

## Error Handling

- **Do** use `if %ERRORLEVEL% neq 0` (or `if errorlevel 1`) after commands that may fail.
- **Do** use `call :label args` for internal subroutines and check `%ERRORLEVEL%` on return.
- **Do** redirect error output to `stderr` with `1>&2` when writing diagnostic messages with `echo`.
- **Don't** assume commands set `ERRORLEVEL` — some built-in commands (e.g., `set`, `echo`) do not update it; test explicitly where it matters.
- **Don't** chain unrelated commands with `&` when the second depends on the first — use `&&` for conditional execution.

## Quoting & Special Characters

- **Do** use double quotes around paths and arguments that may contain spaces.
- **Do** escape special characters (`&`, `|`, `<`, `>`, `^`, `%`) with `^` when they appear in literals — e.g., `echo This ^& that`.
- **Do** use `%%i` in `for` loops inside scripts and `%i` at the interactive prompt — Batch doubles the percent sign in batch files.
- **Don't** use single quotes for string delimiters — Batch does not recognise them; they become literal characters.

## Security

- **Do** validate file paths before operations — use `if exist` to guard against missing targets and avoid path traversal.
- **Don't** pass unsanitised user input into commands — Batch has no built-in escaping; validate or whitelist expected values.
- **Don't** store secrets in plain-text variables or echo them — they are visible in process listings and logs.

## File & Path Handling

- **Do** use `%~dp0` to reference the script's own directory — it resolves to the drive and path of the batch file regardless of the working directory.
- **Do** use `pushd` / `popd` to change directories temporarily and return reliably.
- **Do** use `%~nx1` modifiers to extract file name and extension from arguments.
- **Don't** hard-code absolute paths — use environment variables (`%TEMP%`, `%USERPROFILE%`, `%ProgramFiles%`) or `%~dp0`-relative paths.

## Portability

- **Do** keep scripts compatible with `cmd.exe` — avoid PowerShell or Unix syntax in `.bat`/`.cmd` files.
- **Do** use `where` instead of `which` to locate executables on Windows.
- **Don't** rely on case-sensitive comparisons — `cmd.exe` environment variables and commands are case-insensitive.
