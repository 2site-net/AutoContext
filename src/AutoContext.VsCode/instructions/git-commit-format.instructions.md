---
name: "git-commit-format (v1.0.0)"
description: "Apply when drafting a git commit message (Conventional Commits subject and body)."
---

# Git Commit Format Instructions

## MCP Tool Validation

Before proposing any git commit message to the user, call the
`analyze_git_commit_message` MCP tool with the full drafted message
(subject and body) as `content`. Fix any reported violation and
re-validate until it reports clean.

## Rules

### Format & Structure

- [INST0001] **Do** use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) format: `type(scope): description` (e.g., `feat(auth): add token refresh`, `fix(dns): handle null zone`, `refactor(hub): consolidate middleware`).
- [INST0002] **Do** review the status of files with `git status` and `git diff` before committing — always run these commands fresh; never reuse output from a previous invocation or conversation turn.
- [INST0003] **Do** scope the commit message to **staged files only** (`git diff --cached`); if no files are staged, then scope it to all modified files.
- [INST0004] **Do** keep the subject line under 50 characters.
- [INST0005] **Do** wrap the body at 72 characters.
- [INST0006] **Do** separate the subject line from the body with a blank line.

### Content

- [INST0007] **Do** use the body to explain what and why as opposed to how.
- [INST0008] **Do** focus on behavioral changes — what the software does now that it didn't before; only mention implementation details when they are significant to the change (e.g., switching from polling to WebSockets).
- [INST0009] **Don't** use the body to describe how the change was made; that belongs in the code itself.
- [INST0010] **Don't** list file paths, folder structures, or per-file breakdowns — the diff already shows that.
- [INST0011] **Don't** include counts (number of tests, demos, cases, lines changed, etc.).
- [INST0012] **Don't** describe CSS techniques, HTML patterns, internal wiring, or implementation mechanics.
- [INST0013] **Don't** enumerate parameters, properties, or method names added — summarize the capability instead.
- [INST0014] **Don't** mention checklist updates, README badges, or documentation housekeeping unless that's the primary purpose of the commit.
- [INST0015] **Don't** use bullet lists or "Key features:" sections in commit messages — write prose.
- [INST0016] **Don't** include sensitive information in commit messages.
