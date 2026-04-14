---
name: "lang-bash (v1.0.0)"
description: "Use when writing Bash or shell scripts: safety flags, quoting, error handling, portability, and script structure."
applyTo: "**/*.{sh,bash}"
---
# Bash

## Safety & Correctness

- [INST0001] **Do** start every script with `set -euo pipefail` — exit on errors (`-e`), treat unset variables as errors (`-u`), and propagate pipeline failures (`pipefail`).
- [INST0002] **Do** use `#!/usr/bin/env bash` as the shebang for portability across systems where Bash may not live at `/bin/bash`.
- [INST0003] **Do** quote all variable expansions — `"$var"`, `"${array[@]}"` — to prevent word-splitting and glob-expansion surprises.
- [INST0004] **Do** use `[[ … ]]` for conditionals instead of `[ … ]` — it handles empty strings, pattern matching, and regex without quoting pitfalls.
- [INST0005] **Don't** rely on unquoted `$*` or `$@` — use `"$@"` to preserve argument boundaries.
- [INST0006] **Don't** use `set -e` as a substitute for proper error handling — it has subtle edge-cases with subshells, `&&`/`||` chains, and command substitutions; combine it with explicit checks where failure matters.

## Variables & Quoting

- [INST0007] **Do** use `snake_case` for local variables and function names.
- [INST0008] **Do** use `UPPER_SNAKE_CASE` for exported environment variables and constants.
- [INST0009] **Do** declare local variables with `local` inside functions to avoid polluting the global scope.
- [INST0010] **Do** use `readonly` for constants that must not change after assignment.
- [INST0011] **Do** prefer `${var:-default}` for defaults and `${var:?error message}` for required variables.
- [INST0012] **Don't** use backticks for command substitution — use `$(command)` which nests cleanly and is easier to read.

## Functions & Structure

- [INST0013] **Do** define functions with `name() { … }` syntax — it is POSIX-compatible and concise.
- [INST0014] **Do** keep a `main` function and call it at the bottom (`main "$@"`) so the script can be sourced for testing without side-effects.
- [INST0015] **Do** return exit codes from functions — `return 0` for success, non-zero for specific failure modes.
- [INST0016] **Do** use `trap` to clean up temporary files and resources on `EXIT`, `ERR`, or `INT`.
- [INST0017] **Don't** define functions with the `function` keyword alone — `function name { … }` is a Bash-ism that reduces portability.

## Error Handling

- [INST0018] **Do** check the exit code of critical commands explicitly when `set -e` alone is insufficient.
- [INST0019] **Do** use `|| { echo "…" >&2; exit 1; }` after commands whose failure should halt the script with a clear message.
- [INST0020] **Do** redirect error messages to `stderr` with `>&2`.
- [INST0021] **Don't** ignore return codes from `cd`, `pushd`, `mkdir`, or `rm` — a failed `cd` means subsequent commands run in the wrong directory.

## Security

- [INST0022] **Do** validate and sanitise all external input — arguments, environment variables, file contents — before using them in commands.
- [INST0023] **Do** use `--` to separate options from operands when passing user input to commands (e.g., `grep -- "$pattern" "$file"`).
- [INST0024] **Don't** use `eval` — it executes arbitrary strings and enables injection attacks.
- [INST0025] **Don't** build commands by concatenating untrusted input into strings — use arrays and `"${cmd[@]}"` expansion for safe argument passing.
- [INST0026] **Don't** store secrets in command-line arguments — they are visible in `/proc` and `ps` output; prefer environment variables or file descriptors.

## Portability

- [INST0027] **Do** prefer POSIX built-ins and utilities when the script may run on non-Bash shells.
- [INST0028] **Do** use `command -v` instead of `which` to check if an executable exists — `which` is not POSIX and behaves inconsistently.
- [INST0029] **Do** use `printf` over `echo` for reliable output — `echo` behaviour varies across shells and platforms (e.g., `-n`, `-e` flags).
- [INST0030] **Don't** use Bash-only features (arrays, `[[ ]]`, process substitution) in scripts with a `#!/bin/sh` shebang.

## Performance

- [INST0031] **Do** prefer shell built-ins over external processes in tight loops — `${var%pattern}` is faster than spawning `sed` or `awk` per iteration.
- [INST0032] **Do** use `find … -exec command {} +` (batch mode) instead of `find … -exec command {} \;` (one-per-file) for large file sets.
- [INST0033] **Don't** use `cat file | command` when `command < file` or `command file` works — avoid useless `cat`.
