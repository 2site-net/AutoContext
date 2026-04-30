---
name: "git-commit (v1.0.0)"
description: "Apply when drafting a git commit message (Conventional Commits subject and body)."
---

# Git Commit Format Instructions

## MCP Tool Validation

Before proposing any git commit message to the user, call the
`analyze_git_commit_message` MCP tool with the full drafted message
(subject and body) as `content`. The validation loop is:

1. Run `git status` and `git diff --cached` fresh.
2. Draft the full message (subject and body).
3. Validate with the tool.
4. Fix any reported violation.
5. Re-validate.
6. Repeat until the tool reports clean before showing the message
   to the user.

If a calling prompt mandates a specific output shape (e.g. "output
only the final commit message in a Markdown code block"), that
output constraint takes precedence over default chat verbosity —
omit any surrounding commentary.

## Rules

### Format & Structure

- [INST0001] **Do** use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) format: `type(scope): description` (e.g., `feat(auth): add token refresh`, `fix(dns): handle null zone`, `refactor(hub): consolidate middleware`).
- [INST0002] **Do** review the status of files with `git status` and `git diff` before committing — always run these commands fresh; never reuse output from a previous invocation or conversation turn.
- [INST0003] **Do** scope the commit message to **staged files only** (`git diff --cached`); if no files are staged, then scope it to all modified files.
- [INST0004] **Do** keep the subject line under 50 characters. If the natural subject runs long, broaden the description or pick a wider scope rather than truncating words — a long subject usually signals that the framing is too narrow for what the body actually covers.
- [INST0005] **Do** wrap the body at 72 characters.
- [INST0006] **Do** separate the subject line from the body with a blank line.
- [INST0017] **Do** make the subject line a true summary of the body. Every theme present in the body should be implied by the subject, and the subject should not promise more than the body delivers. If the body covers several related changes, raise the subject to the umbrella that covers all of them; if the body is about one specific thing, name that thing in the subject.

### Content

- [INST0007] **Do** use the body to explain what and why as opposed to how.
- [INST0008] **Do** focus on behavioral changes — what the software does now that it didn't before; only mention implementation details when they are significant to the change (e.g., switching from polling to WebSockets).
- [INST0009] **Don't** use the body to describe how the change was made; that belongs in the code itself.
- [INST0010] **Don't** list file paths, folder structures, or per-file breakdowns — the diff already shows that.
- [INST0011] **Don't** include counts (number of tests, demos, cases, lines changed, etc.).
- [INST0012] **Don't** describe CSS techniques, HTML patterns, internal wiring, or implementation mechanics.
- [INST0013] **Don't** enumerate parameters, properties, or method names added — summarize the capability instead.
- [INST0014] **Don't** mention checklist updates, README badges, or documentation housekeeping unless that's the primary purpose of the commit.
- [INST0015] **Do** prefer prose. Use lists only when the commit genuinely covers multiple independent changes, and ensure each item explains behavior or rationale — not file paths, counts, or internal names. When you do use lists:
  - Use `-` (hyphen) for bullets, never `*` or `•`.
  - Use `1.`, `2.`, `3.` for ordered or sequential steps.
  - Don't nest deeper than two levels.
  - Don't mix bullet styles in the same body.
  - Never use "Key features:" or similar section headers.
- [INST0016] **Don't** include sensitive information in commit messages.
- [INST0018] **Do** treat documentation rewrites as behavioral changes for the *reader*: explain what the documentation now conveys (or no longer misleads about) rather than which sections were edited. A docs body should describe the new accuracy or new guidance the reader gets, not a tour of the headings that were touched.
