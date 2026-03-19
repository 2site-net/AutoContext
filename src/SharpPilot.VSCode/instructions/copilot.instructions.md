---
description: "Rules that govern how Copilot should behave: rule precedence, safety, prompt etiquette, and operational boundaries."
---
# Copilot Rules

## Rule Precedence
Follow rules in the order below; if two rules disagree, the higher-level rule wins.
For tier-1 violations, stop generating and surface a warning. For lower-tier conflicts, surface a warning inline but continue.

    1. security / legality / logical correctness (no bugs, no undefined behavior)
    2. quality (reliability · maintainability · robust error handling)
    3. performance / memory
    4. stylistic / formatting

> **If unsure** which rule applies, generate a concise plan explaining the dilemma and stop; await user approval before continuing.

## Prompt Rules
- **Don't** run any git command that changes repository state (`git add`, `git rm`, `git commit`, `git push`, `git reset`, `git checkout`, `git merge`, `git rebase`, etc.) without explicit user permission — read-only commands (`git status`, `git diff`, `git log`, `git show`) are fine.
- **Don't** omit `--gpg-sign` (`-S`) from `git commit` when the repo or global config has `commit.gpgSign = true`; always honour the user's signing settings.
- **Don't** create markdown report files unless explicitly requested by the user.
- **Do** act immediately; pause for approval only on multi-file, major-refactor, or multi-phase work.
- **Do** use cross-platform PowerShell commands and scripts (e.g., `pwsh -Command 'dotnet test; ./scripts/coverage.ps1'`).
- **Do** respect existing config files (`.editorconfig`, `.gitignore`, `.csproj`, `.fsproj`, `GlobalSuppressions`, etc.); only change them when necessary, with rationale.
- **Do** call `get_editorconfig` before generating, reformatting, or reviewing code to resolve the project's formatting rules (indent style, indent size, charset, end-of-line, etc.) and follow them.
- **Do** clean up after yourself – delete any temp or redundant files you create.
- **Do** read the `README.md` and other documentation files to understand the project structure and requirements.
- **Do** fix one category of errors completely before moving to the next.
