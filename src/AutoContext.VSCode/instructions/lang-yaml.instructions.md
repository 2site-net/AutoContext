---
description: "Use when writing, reviewing, or troubleshooting YAML files such as CI/CD pipelines, Docker Compose, Kubernetes manifests, or configuration files."
applyTo: "**/*.{yml,yaml}"
---
# YAML

## Formatting

- [INST0001] **Do** use consistent indentation — never tabs.
- [INST0002] **Do** keep lines under 120 characters; split long values with YAML block scalars.
- [INST0003] **Do** use a consistent style for block scalars — prefer `|` (literal) for multi-line strings that need preserved newlines and `>` (folded) for prose that should be unwrapped.
- [INST0004] **Do** end every file with a single trailing newline.

## Quoting & Type Safety

- [INST0005] **Do** quote strings that YAML would silently coerce to another type: `"no"`, `"yes"`, `"on"`, `"off"`, `"true"`, `"false"`, `"null"`, `"3.10"`.
- [INST0006] **Do** quote strings that start with special characters (`*`, `&`, `!`, `%`, `@`, `` ` ``).
- [INST0007] **Do** quote port mappings and version numbers that contain a colon or could be interpreted as floats: `"8080:80"`, `"3.10"`.
- [INST0008] **Don't** rely on implicit type resolution — if a value must be a string, quote it explicitly.

## Structure & Keys

- [INST0009] **Do** use lowercase `kebab-case` for keys unless the target system requires a different convention.
- [INST0010] **Do** use block-style (one key per line) for mappings and sequences with more than one element.
- [INST0011] **Don't** mix block and flow (`{...}` / `[...]`) styles in the same file except for trivially short inline values.

## Anchors & Aliases

- [INST0012] **Do** use anchors (`&name`) and aliases (`*name`) to eliminate repetition when the duplicated fragment is non-trivial.
- [INST0013] **Don't** overuse anchors — a single reused value does not justify anchor/alias indirection.

## Multi-document & Comments

- [INST0014] **Do** start each document with `---` when a file contains multiple documents.
- [INST0015] **Do** add comments above complex or non-obvious values explaining their purpose.
- [INST0016] **Don't** use document end markers (`...`) unless required by the consuming tool.

## Security

- [INST0017] **Don't** embed secrets, tokens, or credentials in YAML files — use environment variable references, secret managers, or encrypted value placeholders.
- [INST0018] **Don't** use the `!!python/object` or other language-specific tags that enable arbitrary code execution — they are a deserialization attack vector.
