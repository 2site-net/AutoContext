---
description: "Use when reviewing code, giving feedback on a pull request, or auditing a change for correctness, security, and quality."
---
# Code Review Guidelines

## Output Structure

- [INST0001] **Do** cite the specific line or block and explain *why* it is a problem — not just *what* is wrong.
- [INST0002] **Do** suggest a concrete fix or direction for each finding.
- [INST0003] **Do** classify each finding as **blocking** (must fix), **suggestion** (should consider), or **nit** (optional polish) — surface blocking issues first.
- [INST0004] **Don't** rewrite the author's approach because you would have done it differently — only flag concrete problems.
- [INST0005] **Don't** repeat the same finding on every occurrence — identify the pattern once and note how many times it appears.

## What to Check

- **Correctness** — logic errors, off-by-one, null dereferences, race conditions, incorrect state transitions.
- **Security** — injection vectors, broken auth, sensitive data exposure, insecure defaults.
- **Error handling** — unhandled exceptions, silent failures, missing cancellation, resource leaks.
- **Code quality** — dead code, duplicated logic, unnecessary complexity, missed simplification or existing utility reuse.
- **Unintended changes** — accidentally staged files, removed lines that shouldn't have been deleted, debug leftovers.
- **Consistency** — does the change follow the patterns already established in the surrounding code and project?
- **Tests** — are new behaviors covered? are edge cases and failure paths tested?
- **API surface** — are new public members necessary? are names clear to callers who lack context?
- **Side effects** — does the change affect shared state, configuration, or infrastructure in ways not obvious from the diff?
