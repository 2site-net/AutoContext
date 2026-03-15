---
description: "Use when writing git commit messages, formatting commit subjects and bodies, or following Conventional Commits."
---
# Git Commit Format

- **Do** use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) format: `type(scope): description` (e.g., `feat(auth): add token refresh`, `fix(dns): handle null zone`, `refactor(hub): consolidate middleware`).
- **Do** review the status of files with `git status` and `git diff` before committing — always run these commands fresh; never reuse output from a previous invocation or conversation turn.
- **Do** scope the commit message to **staged files only** (`git diff --cached`); if no files are staged, then scope it to all modified files.
- **Do** keep the subject line under 50 characters.
- **Do** wrap the body at 72 characters.
- **Do** separate the subject line from the body with a blank line.
- **Do** use the body to explain what and why as opposed to how.
- **Do** focus on behavioral changes — what the software does now that it didn't before; only mention implementation details when they are significant to the change (e.g., switching from polling to WebSockets).
- **Don't** use the body to describe how the change was made; that belongs in the code itself.
- **Don't** list file paths, folder structures, or per-file breakdowns — the diff already shows that.
- **Don't** include counts (number of tests, demos, cases, lines changed, etc.).
- **Don't** describe CSS techniques, HTML patterns, internal wiring, or implementation mechanics.
- **Don't** enumerate parameters, properties, or method names added — summarize the capability instead.
- **Don't** mention checklist updates, README badges, or documentation housekeeping unless that's the primary purpose of the commit.
- **Don't** use bullet lists or "Key features:" sections in commit messages — write prose.
- **Don’t** include sensitive information in commit messages.