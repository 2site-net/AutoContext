# LM-Tool Instructions Discovery

> **Status:** Future / design note — *infrastructure prerequisites shipped*.
>
> Address the "instructions are silently skipped" problem by exposing AutoContext's instruction files to Copilot through four extension-native VS Code Language Model tools, instead of relying on `applyTo` to trigger automatic prompt injection (which it does not, in agent mode).
>
> The data plane this design depends on — the build-time metadata generator,
> the two-file manifest split, the runtime join, and the section index — is
> already implemented and shipped (see [Build-time metadata extraction](#build-time-metadata-extraction)
> and the *Implementation order* checklist below). What's still future work is
> the four LM tools themselves and the priming hint.

## Background

### How `applyTo` works today

Instruction files use frontmatter like:

```yaml
applyTo: **/*.cs
```

In VS Code's editor experience, `applyTo` works as a *filter*: when the user is editing `Foo.cs`, the editor can show "this instruction file applies." It's a passive matcher — useful for display/scoping.

`copilot-instructions.md` is different: it's auto-attached to every Copilot prompt regardless of context. No `applyTo` needed.

### The problem: passive filtering in an active loop

In an agent loop (Copilot Chat agent mode, where the model iteratively reads files, edits them, runs tools), the relationship between "what file is in context" and "what instructions apply" should be active, not passive:

- If the agent reads `Foo.cs`, the matching `lang-csharp.instructions.md` should be attached to the prompt right then — the same way `copilot-instructions.md` always is.
- Today's behaviour: `applyTo` is just a filter for display/scoping. The agent doesn't get the C# instructions auto-attached when it touches a `.cs` file. It must know they exist, find them, and read them itself.

You can observe this in agent sessions: the system lists all instruction files with their `applyTo` patterns, but the *contents* aren't loaded. A note tells the model to "use the `read_file` tool to read it before proceeding." That's exactly the gap — the model has to remember.

### Why this matters for AutoContext

AutoContext ships many language- and framework-specific instruction files (`lang-csharp`, `dotnet-async-await`, `web-vitest`, etc.). They only deliver value if the model reads them at the right moment. Passive `applyTo` filtering means many are silently skipped during agent runs.

### Why a workaround beats waiting for the platform

The behaviour of `chatInstructions` `applyTo` is owned by VS Code / Copilot — an extension cannot change how those entries are attached to the prompt. Two earlier candidate workarounds were considered and rejected:

- **Editor context-key tracker.** Subscribe to active-editor changes; flip a per-instruction context key when matching files come into scope; OR-branch each `chatInstructions` entry's `when` clause on that key. Limitation: agent loops don't necessarily re-evaluate `when` clauses per turn, so the model still won't see fresh instructions when it reads `Foo.cs` via `read_file` mid-loop. It improves matching at conversation boundaries; it doesn't close the active-loop gap.
- **`@autocontext` chat participant.** Requires `@`-mention, doesn't help in pure agent mode.

The right shape is to convert "passive filter" into "active fetch" — a tool the model calls. That works in every Copilot mode (chat, edits, agent) and is fully under the extension's control.

## Goal

Make every AutoContext instruction file reliably reachable by the model on demand, with zero platform changes and zero new context-key plumbing. The model finds out what AutoContext ships via always-available tools and pulls the content of any specific instruction when needed.

## Design

Four LM tools, all extension-native (no MCP server involvement), all registered via `vscode.lm.registerTool` and declared in `package.json` `contributes.languageModelTools`.

The four roles:

- **`list_autocontext_instructions_files`** — *what's available* (catalogue, filterable by `applyTo` glob and category). Ergonomic shorthand for the most common discovery question.
- **`search_autocontext_instructions_files_by_metadata`** — *find by attribute* (predicate over any metadata key: `description`, `version`, `hasChangelog`, `sections.heading`, etc.). The general form; `list_*` is a thin wrapper over this.
- **`search_autocontext_instructions_files_by_content`** — *find by intent* (full-text search across instruction bodies and descriptions).
- **`get_autocontext_instructions_file`** — *fetch the content* (normalized markdown of one named instruction).

Listing is for "what applies here?"; metadata search is for "which files have *X* in their attributes / structure?"; content search is for "where is the rule about *X*?"; get is for "give me the actual text." Splitting metadata search from content search with explicit suffixes (`_by_metadata` / `_by_content`) eliminates the ambiguity an undecorated `search_*` would have created — the model picks the surface, not the intent.

### `list_autocontext_instructions_files`

Returns the catalogue of AutoContext instruction files available in this workspace. Ergonomic wrapper over [`search_*_by_metadata`](#search_autocontext_instructions_files_by_metadata) with the two filters models reach for most often.

**Inputs (all optional):**

- `applyTo` *(string — glob)* — when provided, restricts the result to instruction files whose own `applyTo` glob would attach to **some file the user's glob describes**. This is *glob-vs-glob set intersection*, not string equality: passing `applyTo: "*.{cs,js}"` returns instructions that target `.cs` files (e.g. `lang-csharp` with `applyTo: "**/*.cs"`) **and** instructions that target `.js` files (e.g. `lang-javascript`), because the input glob and each instruction's `applyTo` share matching files in the abstract. The matcher is `vscode.languages.match` (or the equivalent primitive VS Code itself uses to evaluate `chatInstructions` `applyTo`) — see the [Settled trade-off](#settled-trade-offs) on platform parity. To find instructions for a single concrete file, pass that file's relative path verbatim (e.g. `applyTo: "src/Foo.cs"`); a path is just a glob that matches one file.
- `category` *(string | string[])* — filter by category as defined in [`instructions-files.json`](../../src/AutoContext.VsCode/resources/instructions-files.json) (e.g. `"dotnet"`, `["dotnet", "web"]`). When an array is supplied, a file matches if **any** of its categories is in the input list (OR across categories).
- `includeSections` *(boolean, default `false`)* — when `true`, each entry gains a `sections` array (see [Section awareness](#section-awareness)). Off by default to keep the listing response small.

Disabled instructions (whether by user config or by unmet workspace flags) are not listed — see [Filtering & gating](#filtering--gating). There is no opt-in to surface them.

**Output:** an array of entries, each with:

- `name` (e.g. `lang-csharp.instructions.md`)
- `key` (e.g. `lang-csharp`)
- `description` (from frontmatter)
- `applyTo` (the raw glob)
- `version` (per-file semver from frontmatter — see [Build-time metadata extraction](#build-time-metadata-extraction))
- `categories`
- `overridden` (true if a workspace-local copy at `.github/instructions/<fileName>` is shadowing the bundled file)
- `bundledVersion` *(only when `overridden: true`)* — the version of the shipped file that the workspace copy is shadowing. Lets the model spot staleness (`version` < `bundledVersion`) or local divergence without a follow-up `get_*` call.
- `sections` *(only when `includeSections: true`)* — array of `{ heading, level, anchor, parent? }` describing the file's `##` and `###` headings (extracted from the override body when one is present).

The result is small (metadata only) and cacheable. No file content.

### `search_autocontext_instructions_files_by_metadata`

The general predicate tool. Returns the same catalogue shape as [`list_*`](#list_autocontext_instructions_files), filtered by an arbitrary record of metadata predicates. `list_*` is a thin convenience wrapper over this tool.

**Input:** a single object whose keys are metadata field paths (dot-notation, drilling into nested fields and arrays) and whose values are matchers. AND across keys: every supplied key must match for a file to be returned. An empty object `{}` returns the full catalogue (equivalent to `list_*` with no filter).

**Addressable fields** (the metadata surface a predicate may target):

- Top-level: `name`, `key`, `fileName`, `description`, `version`, `bundledVersion`, `applyTo`, `hasChangelog`, `overridden`.
- Arrays: `categories[]` (string array), `sections[]` (array of `{ heading, level, anchor, parent? }`).
- Nested via dot-notation: `sections.heading`, `sections.level`, `sections.anchor`, `sections.parent`.

**Matcher semantics:**

- **String value → case-insensitive regex** against the field. A bare literal works without anchors (`"description": "configure"` matches any description containing the substring `configure`, ignoring case). Invalid patterns and patterns longer than a fixed cap (e.g. 256 chars) are rejected with a structured error — the handler does not throw, it returns `{ error: "invalid-regex", field, reason }` so the model can recover.
- **Boolean / number value → exact equality** (`"hasChangelog": true`, `"sections.level": 2`).
- **Array traversal** — when a path crosses an array (`categories`, `sections.heading`), the predicate is satisfied if **any** element matches. No `[*]` syntax needed; the array crossing is implicit.
- **`applyTo` is the lone exception:** its value is treated as a **glob**, never as regex, with the same set-intersection semantics described under [`list_*`](#list_autocontext_instructions_files). Trying to pass a regex-shaped string here will simply be interpreted as a (mostly non-matching) glob — the input schema documents this explicitly so the model doesn't try.

**Examples:**

```jsonc
// All files whose description mentions "async" (case-insensitive).
{ "description": "async" }

// All files with a Security section.
{ "sections.heading": "^Security$" }

// All .cs / .js files with a changelog at version 1.x.
{ "applyTo": "*.{cs,js}", "hasChangelog": true, "version": "^1\\." }

// Files in the dotnet OR web category whose filename starts with "lang-".
{ "categories": "^(dotnet|web)$", "fileName": "^lang-" }

// Equivalent to list_*({ applyTo: "src/Foo.cs" }).
{ "applyTo": "src/Foo.cs" }
```

**Output:** array of catalogue entries, same shape as `list_*`. Two additions when the predicate touched `sections.*`:

- `sections` is always returned (regardless of whether `includeSections` was hypothetically requested), so the model can chain into `get_*({ name, sections: [...] })` without re-querying.
- Each entry gains a `matchedAnchors: string[]` listing the anchors of the sections that satisfied the `sections.*` predicate(s). Lets the model jump straight to relevant section content.

Disabled instructions are excluded — see [Filtering & gating](#filtering--gating).

### `search_autocontext_instructions_files_by_content`

Full-text search across AutoContext instruction bodies (post-normalization) and frontmatter descriptions. Lets the model answer questions like "is there a rule about `ConfigureAwait`?" or "what does AutoContext say about NuGet floating versions?" without first listing every file and fetching each candidate.

**Inputs:**

- `query` *(string, required)* — free-text query. Tokenized and matched case-insensitively against instruction content and description.
- `applyTo` *(string — glob, optional)* — restrict search to instructions whose own `applyTo` glob set-intersects with this glob. Same semantics as [`list_*`](#list_autocontext_instructions_files)'s `applyTo`. Combines with `query` (logical AND). Pass a concrete relative path (e.g. `"src/Foo.cs"`) to scope to one file.
- `category` *(string | string[], optional)* — restrict to one or more categories. Same semantics as [`list_*`](#list_autocontext_instructions_files)'s `category`.
- `maxResults` *(integer, optional, default 10, max 25)* — cap on returned hits.

Disabled instructions are excluded from the search corpus — see [Filtering & gating](#filtering--gating). There is no opt-in to surface them.

**Output:** ranked array of hits, each with:

- `name`, `key`, `description`, `applyTo`, `categories`, `overridden`, `bundledVersion?` — same metadata fields as `list_*` (`bundledVersion` only present when `overridden: true`).
- `score` — relative ranking score (implementation-defined; only meaningful for ordering).
- `matches` — small array of excerpts (≤ 3 per file, each ≤ ~200 chars) showing the matched terms in surrounding context, with the matched span demarcated. Each excerpt also carries `section` (heading text) and `sectionLevel` (`2` or `3`) attributing it to the most specific containing heading — see [Section awareness](#section-awareness). Lets the model judge relevance and target a specific section without fetching the full body.

No full content in the result. The model fetches via `get_autocontext_instructions_file` after deciding which hits matter.

**Search semantics:** identifier-aware tokenization with AND-of-terms matching. The corpus is full of identifiers (`ConfigureAwait`, `IAsyncEnumerable`, `dotnet-async-await`, `NuGet`), so a pure substring match would silently miss the most common real query shape (`configure await` written with a space, or `nuget` searched against `NuGet`). The tokenizer fixes that without pulling in a search library.

- **Tokenizer (used for both query and corpus):**
  1. Split on `\W+` (any run of non-word characters), discarding empties.
  2. For each resulting token, also split on camelCase / PascalCase boundaries (`/(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])/`) and on `-` / `_`.
  3. Lowercase everything.
  4. Emit both the original (lowercased) whole token **and** its split pieces. So `ConfigureAwait` produces `[configureawait, configure, await]`; `NuGet` produces `[nuget, nu, get]`; `dotnet-async-await` produces `[dotnet, async, await]` (the hyphenated form was already split by step&nbsp;1).
  5. Drop pieces shorter than 2 characters to keep the index lean.
- **Per-file index:** at corpus-build time, tokenize `description` and `content` separately, producing two `Map<token, count>` per file. Built once at activation, cached, invalidated on the same triggers as the rest of the search corpus (config change, override watcher).
- **Match:** tokenize the query the same way. A file matches iff **every** distinct query token appears as a key in either of the file's two token maps (AND across query tokens).
- **Score:** for each matching query token, add `descriptionHits × 2 + contentHits × 1`. Sum across query tokens. Ties broken by `name` ascending.
- **No stemming, no fuzzy matching, no embeddings.** Morphology beyond identifier-splitting (plurals, tenses) is left to the model: if it cares about "configuring" vs. "configure", it issues a follow-up query.

This is deliberately not an embedding-based or fuzzy search. The instruction set is small (dozens of files, ~tens of KB total post-normalization); the tokenizer plus AND-of-terms is enough to cover the realistic query shapes (identifiers, multi-word phrases, kebab-cased file keys), and adding `lunr`/`minisearch`/`fuse.js` is YAGNI until we observe misses the tokenizer can't explain.

**Excerpt extraction:** operates on the raw normalized body, not on tokens, so excerpts read naturally. For each hit, find up to 3 distinct match positions across the file (prefer matches in `description`, then earliest matches in `content`), and slice ±~80 chars around each, snapped to word boundaries. A position counts as a match if any query token appears there as a case-insensitive substring; this re-uses the original (un-split) query tokens so excerpt windows centre on the actual phrase the user searched for, not on the tokenizer's split pieces. De-duplicate overlapping windows.

### Section awareness

Instruction files have meaningful internal structure. Two layouts exist in practice:

- **Flat** — a single `## Rules` (or similarly named) heading with rule bullets directly underneath, no `###` children.
- **Grouped** — a topical `##` heading (e.g. `## Naming`, `## Formatting`) with `###` children that group related rules.

Mixed files (a flat `## Rules` plus other `##` sections like `## Examples`) are also common. Every shipped instruction file has at least one `##`.

Sections are extracted **at build time** by [`instructions-files-metadata-generator.ts`](../../src/AutoContext.VsCode/src/instructions-files-metadata-generator.ts) and stored in `instructions-files.metadata.json`. The runtime never parses markdown for structure — it reads the metadata manifest via [`InstructionsFilesMetadataLoader`](../../src/AutoContext.VsCode/src/instructions-files-metadata-loader.ts). This keeps activation cheap and lets the build validate heading sanity (anchor collisions are fatal; fenced-code headings are correctly ignored) loudly.

**What's indexed:** `##` and `###` only. The doc title (`#`) is skipped; `####` and deeper are ignored as too granular.

**Per-entry shape** (used by `sections` on `list_*` and by `section`/`sectionLevel` attribution on `search_*` excerpts):

```jsonc
{ "heading": "Naming",  "level": 2, "anchor": "naming",
  "charStart": 124, "charEnd": 1893 },
{ "heading": "Types",   "level": 3, "anchor": "naming-types",
  "parent": "Naming", "charStart": 138, "charEnd": 612 },
{ "heading": "Members", "level": 3, "anchor": "naming-members",
  "parent": "Naming", "charStart": 614, "charEnd": 1100 },
{ "heading": "Rules",   "level": 2, "anchor": "rules",
  "charStart": 50,  "charEnd": 980 }   // flat layout — kept as a single level-2 entry
```

Rules:

- A `##` with no `###` children is still a section — it's emitted as a single `level: 2` entry. Flat-layout files are first-class.
- Each `###` records its nearest preceding `##` as `parent`, so `### Types` under `## Naming` is distinguishable from `### Types` under `## Members`.
- `anchor` is GitHub-style slugified heading text. `###` anchors are prefixed with the parent slug (`naming-types`, not bare `types`) for in-file uniqueness.
- `charStart` / `charEnd` are character offsets into the normalized body, used by the search handler to attribute an excerpt to a section in O(log n) instead of rescanning headings on every query. The fields can be omitted from `list_*` results if response size matters; they always live in the manifest.

**Search attribution:** when an excerpt falls under a `###`, the match's `section` is that `###`. When it falls under a `##` with no `###` children (flat layout), the match's `section` is that `##`. Either way `sectionLevel` reflects which granularity was used. The `anchor` returned alongside each excerpt is the same identifier `get_autocontext_instructions_file` accepts in its `sections` input, so the model can fetch exactly the matched section without re-listing.

This section index is what makes section-scoped partial fetch on [`get_autocontext_instructions_file`](#get_autocontext_instructions_file) a first-class feature rather than a future optimisation — the model already has the anchors in hand from `list_*`, `search_*_by_metadata` (`matchedAnchors`), or `search_*_by_content` (`matches[].anchor`).

### `get_autocontext_instructions_file`

Returns the normalized content of a single instruction file — either the full body or a chosen subset of sections.

**Inputs:**

- `name` *(string, required)* — e.g. `lang-csharp.instructions.md` or `lang-csharp` (accept both for ergonomics).
- `sections` *(string[], optional)* — array of section anchors (as published by `list_*` / `search_*`, e.g. `"naming-types"`, `"async"`) to fetch instead of the full body. When provided, only those sections are returned. Unknown anchors are ignored; if every supplied anchor is unknown the result is an empty `content` plus a `notFoundSections` array so the model can recover.

**Output:**

- `name`, `key`, `description`, `applyTo`, `categories`
- `version` — the version of the file actually returned (override version when overridden, bundled version otherwise)
- `content` — the normalized markdown body (frontmatter and `[INST####]` tags stripped). When `sections` was supplied, this is the concatenation of the requested section slices in document order, each prefixed by its original heading; otherwise it's the full body. Sourced from the workspace override at `.github/instructions/<fileName>` when present, otherwise from the bundled source.
- `returnedSections` — array of `{ anchor, heading, level }` describing exactly what `content` contains, in order. Always present (it equals every section of the file when `sections` was omitted), so the model can reason about what it actually got.
- `notFoundSections` *(only when `sections` was supplied and at least one anchor was unknown)* — the unmatched anchors echoed back.
- `overridden` flag and, when `true`, `bundledVersion` — lets the model recognise that the workspace copy may diverge from the shipped rule and may be outdated.

If the named instruction is currently disabled, `get_*` returns a minimal `{ name, key, disabled: true }` envelope with no `content` and no metadata beyond identity. The model cannot route around the user's choice by fetching directly. See [Filtering & gating](#filtering--gating).

Returning normalized content (matching what [`InstructionsFilesManager`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) writes today) means the model sees exactly what it would see if the file were attached normally. Partial fetch via `sections` is a first-class feature, not an optimisation: the section index is the same one published by `list_*`, `search_*_by_metadata`, and `search_*_by_content`, so the model can chain `search → get(sections=[hit.anchor])` without ever pulling a whole file when one section answers the question.

### Build-time metadata extraction

**Status:** implemented. The shipped extension contains two manifest files describing instruction files. They live next to each other in [`src/AutoContext.VsCode/resources/`](../../src/AutoContext.VsCode/resources/); both ship inside the VSIX.

- **`instructions-files.json`** — hand-authored. Curatorial / policy layer: the `categories` taxonomy with descriptions, and per-file `label`, `categories` membership, `activationFlags`, `schemaVersion`. Not regenerated.
- **`instructions-files.metadata.json`** — build-generated. File-intrinsic layer: per-file `id`, `fileName`, `name`, `description`, optional `applyTo`, `version`, `hasChangelog`, `contentHash`, and `sections` (with character offsets). Sole writer is the build.

[`instructions-files-metadata-generator.ts`](../../src/AutoContext.VsCode/src/instructions-files-metadata-generator.ts) runs during `Compile TS` (wired into `build.ps1`), ahead of `package-instructions-manifest-generator.ts` and `tsc`. For every `*.instructions.md` source file it:

1. Parses YAML frontmatter via [`InstructionsFileParser.parseFrontmatter()`](../../src/AutoContext.VsCode/src/instructions-file-parser.ts) → `{ name, description, applyTo?, version }`.
2. Validates: `name` matches `<id> (vX.Y.Z)`, the `<id>` portion equals the file basename, `description` is non-empty, `applyTo` (when present) is non-empty.
3. Strips frontmatter to produce the normalized body (preserving `[INST####]` tags).
4. Walks the body for `##` / `###` headings — fence-aware (headings inside ```` ``` ```` blocks are ignored) — emitting `sections` per [Section awareness](#section-awareness). Duplicate anchors fail the build.
5. Hashes normalized body → `contentHash` (sha256, useful for cache invalidation).
6. Records `hasChangelog` from the existence of a sibling `<id>.CHANGELOG.md`, so the runtime never has to touch the filesystem to check for changelogs.
7. Emits a deterministic JSON document (entries sorted by `id`, no `generatedAt` timestamp, trailing newline).
8. Cross-validates: the set of `*.instructions.md` files on disk must match the `instructions[].name` set in `instructions-files.json`, except `copilot.instructions.md` which is exempt by design (always-attached, lives outside the curated manifest).

The metadata file is **gitignored** (next to `resources/servers.json`). Build correctness comes from the generator running deterministically on every compile — no CI `git diff` gate, no committed snapshot.

**Frontmatter contract** — every `*.instructions.md` source file declares:

```yaml
---
name: "<id> (vX.Y.Z)"
description: "<one-line summary>"
applyTo: "<glob>"   # optional; omit for cross-cutting / always-attached files
---
```

There is no separate `version` field — the version is parsed out of the `(vX.Y.Z)` suffix on `name`. This avoids VS Code's "unsupported attribute" diagnostic on a bare `version` key. Bumping is manual; the generator validates well-formedness only.

### Data sources

All four tools read from data already loaded in memory:

- [`InstructionsFilesManifestLoader`](../../src/AutoContext.VsCode/src/instructions-files-manifest-loader.ts) — reads `instructions-files.json` at activation; its `load(metadata)` takes a `Map<fileName, InstructionsFileMetadata>` and joins both layers into a single in-memory manifest. The two-file split is an authoring concern; the runtime sees one logical manifest.
- [`InstructionsFilesMetadataLoader`](../../src/AutoContext.VsCode/src/instructions-files-metadata-loader.ts) — reads `instructions-files.metadata.json` and produces the metadata map consumed above. Already wired into [`extension-composition.ts`](../../src/AutoContext.VsCode/src/extension-composition.ts).
- [`InstructionsFilesManager`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) — owns the staged `instructions/.generated/<file>` artefacts. **Not** the read target for these tools — see the warning under [Filtering & gating](#filtering--gating) on why content must come from the source files.
- [`AutoContextConfigManager`](../../src/AutoContext.VsCode/src/autocontext-config-manager.ts) and [`workspace-context-detector.ts`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) — to determine which files are disabled (per-file activation flags vs. workspace context keys vs. user toggle) so they can be filtered out of all four tools' results. See [Filtering & gating](#filtering--gating).

**Where `content` comes from.** Resolution order per file:

1. **Workspace override** — if [`WorkspaceContextDetector.getOverriddenContextKeys()`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) reports an override for this entry, read `.github/instructions/<fileName>` from the workspace. The user has explicitly customised this rule for this workspace, and that customised text is what's actually attached to Copilot prompts — the LM tools must reflect the same reality.
2. **Bundled source** — otherwise read the extension-bundled source at `instructions/<fileName>` (the same input the build sees).

In either case the handler then strips frontmatter and `[INST####]` tags (the projection used by [`InstructionsFilesManager.stripInstructionIds()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts)). It does **not** read from `instructions/.generated/`. See [Filtering & gating](#filtering--gating) for the rationale.

Override awareness also affects the metadata returned alongside `content`:

- `version` is taken from the override's frontmatter (already cached by [`WorkspaceContextDetector.getOverrideVersion()`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts)) when an override is in effect, so the model sees the version the user is actually running. An additional boolean `overridden` (and, when applicable, `bundledVersion`) is returned so the model can recognise drift.
- `sections` from `instructions-files.metadata.json` describe the **bundled** body. When an override is present its structure may diverge; the handler re-extracts sections from the override body using the same parser the build uses ([`InstructionsFilesMetadataGenerator`](../../src/AutoContext.VsCode/src/instructions-files-metadata-generator.ts)'s heading walker) so `list_*` `sections` and `search_*` excerpt attribution stay accurate. This re-parse runs lazily (only for files that actually have an override) and is cached until the override watcher fires.
- Search corpus entries for overridden files use the override body, so a rule the user added locally is findable via `search_autocontext_instructions_files_by_content`.

No round-trip to the .NET MCP server. No new pipe traffic. No runtime markdown parsing for headings — those are pre-extracted into `instructions-files.metadata.json` at build time. (The previous `InstructionsFileMetadataReader` markdown re-parser has been deleted.)

### Filtering & gating

There are **two** independent disable axes in AutoContext, and the LM tools handle them differently:

1. **Whole-file enablement** — driven by per-file activation flags + workspace context keys (`autocontext.workspace.has*`) and the user's tree-view toggle. This is what the `chatInstructions` `when` clauses gate: VS Code skips attaching a file whose `when` is false. The file itself is *still written* to `instructions/.generated/` regardless — [`InstructionsFilesManager.doWrite()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) walks every manifest entry unconditionally; the only thing the user toggle gates is the `when`-clause context key.
2. **Bullet-level disables** — the user's `disabledInstructions` list (per-file array of `[INST####]` IDs). [`InstructionsFilesManager.writeNormalized()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) strips matching bullets when staging into `.generated/`. There is no UI affordance for this today; it's edited via config.

All four LM tools respect axis 1 unconditionally. Disabled-as-a-whole files do not appear in `list_*`, `search_*_by_metadata`, or `search_*_by_content` results, and `get_*` returns `{ disabled: true }` with no content when the named file is disabled. There is no `includeDisabled` escape hatch and no `enabled` flag in the output: the user disabled the rule for a reason, and the model is not given a transparency channel that would effectively re-inject it ("here is the disabled rule you must avoid" leaks the same content). The model sees what the editor would attach — nothing more.

**Bullet-level disables are deliberately ignored by these tools.** All four handlers read from the source file — the workspace override at `.github/instructions/<file>` when present, otherwise the bundled `instructions/<file>` (frontmatter and `[INST####]` tags stripped in either case) — **not** from `instructions/.generated/<file>`. Two reasons:

- The model is asking what AutoContext's rules *are*. Hiding rules the user happened to mute in config gives the model an incomplete picture and makes search hits non-deterministic per workspace.
- Reading from `.generated/` would make `get_*` results depend on whether `InstructionsFilesManager.write()` had completed for the current workspace yet — a race during activation.

If bullet-level filtering ever needs to be exposed, add an explicit `applyUserFilters?: boolean` input rather than changing the default.

### `package.json` — `contributes.languageModelTools`

Four entries, hand-authored (the set is fixed — no generator needed):

```jsonc
{
  "name": "list_autocontext_instructions_files",
  "displayName": "List AutoContext Instructions Files",
  "modelDescription": "Lists AutoContext instruction files available in this workspace. Optionally filter by an applyTo glob (set-intersection: 'src/Foo.cs' or '*.{cs,js}' returns files whose own applyTo would attach to the matching workspace files) and/or by category. Use this for the common 'which rules apply to <path-or-glob>?' question. For attribute-level queries (sections.heading, hasChangelog, version, fileName regex, ...) use search_autocontext_instructions_files_by_metadata. For body/keyword queries use search_autocontext_instructions_files_by_content.",
  "userDescription": "Discover AutoContext instruction files relevant to the current task.",
  "canBeReferencedInPrompt": true,
  "toolReferenceName": "autoctxInstructionsFiles",
  "tags": ["autocontext", "instructions", "discovery"],
  "inputSchema": { /* applyTo?, category?, includeSections? */ }
},
{
  "name": "search_autocontext_instructions_files_by_metadata",
  "displayName": "Search AutoContext Instructions Files by Metadata",
  "modelDescription": "Filters AutoContext instruction files by an arbitrary metadata predicate. Pass an object whose keys are metadata field paths (dot-notation: 'description', 'version', 'hasChangelog', 'sections.heading', 'categories', 'fileName', ...) and whose values are matchers. String values are case-insensitive regex against the field; numbers/booleans are exact equality; arrays match if any element matches. The single exception is 'applyTo', whose value is a glob (set-intersection), never a regex. AND across keys. Returns the same shape as list_autocontext_instructions_files plus a 'matchedAnchors' array on each hit when the predicate referenced sections.*. Use this for attribute queries (e.g. 'files with a Security section', 'files in the dotnet category whose description mentions threading'). For free-text searches over rule bodies, use search_autocontext_instructions_files_by_content instead.",
  "userDescription": "Filter AutoContext instruction files by metadata attributes.",
  "canBeReferencedInPrompt": true,
  "toolReferenceName": "autoctxInstructionsFilesByMetadata",
  "tags": ["autocontext", "instructions", "metadata"],
  "inputSchema": { /* arbitrary record, see modelDescription */ }
},
{
  "name": "search_autocontext_instructions_files_by_content",
  "displayName": "Search AutoContext Instructions Files by Content",
  "modelDescription": "Searches AutoContext instruction file BODIES and descriptions for a free-text query and returns ranked hits with short excerpts. Use this when you need to find a specific rule or convention by topic (e.g. 'ConfigureAwait', 'NuGet floating versions', 'XML doc comments') rather than by attribute. Optionally constrain by applyTo glob (set-intersection) or category. For attribute queries (heading text, version, hasChangelog, fileName regex) use search_autocontext_instructions_files_by_metadata instead. Follow up with get_autocontext_instructions_file to read full content of any hit.",
  "userDescription": "Search AutoContext instruction files by keyword.",
  "canBeReferencedInPrompt": true,
  "toolReferenceName": "autoctxInstructionsFilesByContent",
  "tags": ["autocontext", "instructions", "search"],
  "inputSchema": { /* query, applyTo?, category?, maxResults? */ }
},
{
  "name": "get_autocontext_instructions_file",
  "displayName": "Get AutoContext Instructions File",
  "modelDescription": "Returns the normalized content of a single AutoContext instruction file by name (e.g. 'lang-csharp.instructions.md' or 'lang-csharp'). Use this after list_autocontext_instructions_files, search_autocontext_instructions_files_by_metadata, or search_autocontext_instructions_files_by_content to read the actual rules before generating, editing, or reviewing code that the file's applyTo glob matches. Pass the optional 'sections' array of anchors (returned by list/search as 'matchedAnchors' or 'matches[].anchor') to fetch only specific sections instead of the whole file — prefer this when a search hit pinpointed the relevant section.",
  "userDescription": "Read the contents of a specific AutoContext instruction file.",
  "canBeReferencedInPrompt": true,
  "toolReferenceName": "autoctxInstructionsFile",
  "tags": ["autocontext", "instructions"],
  "inputSchema": { /* name, sections? */ }
}
```

`when` clauses can gate visibility on the existing `autocontext.workspace.*` keys so the tools don't appear in workspaces with zero AutoContext-relevant files (e.g. no language flags set). Open question: probably not worth gating — these are cheap and self-describing, and gating risks the model failing to discover them in mixed-language workspaces.

### Runtime

A new module, e.g. `src/lm-tools/instructions-lm-tools.ts`, that:

1. Constructs handlers wrapping `InstructionsFilesManifest` + `AutoContextConfigManager` + the content projector.
2. Registers all four tools during activation:
   - `vscode.lm.registerTool('list_autocontext_instructions_files', listHandler)`
   - `vscode.lm.registerTool('search_autocontext_instructions_files_by_metadata', searchByMetadataHandler)`
   - `vscode.lm.registerTool('search_autocontext_instructions_files_by_content', searchByContentHandler)`
   - `vscode.lm.registerTool('get_autocontext_instructions_file', getHandler)`
3. Returns disposables to be tracked by [`ExtensionRegistrations`](../../src/AutoContext.VsCode/src/extension-registrations.ts).

The `list_*` handler is implemented as a translation into a metadata predicate (`{ applyTo?, category? }`) and a delegated call to the metadata-search handler — same code path, narrower input shape — so behaviour can never drift between the two.

Composition in [`extension-composition.ts`](../../src/AutoContext.VsCode/src/extension-composition.ts) follows the existing manual-`new` style — no DI changes.

### Priming the model to call these tools

LM-tool registration alone makes them *available*; it doesn't make them *invoked*. Three reinforcements:

1. **Tool descriptions** (above) explicitly tell the model "call this before editing/reviewing" and which surface each one targets (`_by_metadata` for attributes, `_by_content` for prose, `list_*` for the common path/category case). The wording is the most important factor.
2. **A short note in a top-level AutoContext instruction** — something like "Before generating, editing, or reviewing files, call `list_autocontext_instructions_files` with the file's path (as `applyTo`) to discover applicable rules, then fetch any relevant ones with `get_autocontext_instructions_file`. To find a specific rule by topic, use `search_autocontext_instructions_files_by_content`; to filter files by metadata (heading text, hasChangelog, version, ...) use `search_autocontext_instructions_files_by_metadata`." Two or three sentences. This is the equivalent of the existing "use the `read_file` tool to read it before proceeding" hint, but pointing at tools that return the content directly.
3. **`tags`** include `autocontext` so users can discover them in the tool picker.

We deliberately do *not* edit the user's `.github/copilot-instructions.md`. The hint goes in the AutoContext-shipped instructions (e.g. a top-level `autocontext.instructions.md` already always-attached) so it's part of the platform rules, not user content.

## Settled trade-offs

- **`list_*` is sugar over `search_*_by_metadata`.** The metadata predicate tool is the general form; `list_*` is just the same tool with a fixed-shape input (`applyTo?` glob and `category?`). One matching engine, two surfaces — no risk of behaviour drift between "which rules apply to `Foo.cs`?" and "which rules match this metadata query?" because there's only one implementation. Splitting metadata search from content search with explicit `_by_metadata` / `_by_content` suffixes also removes the original "which `search_*` did the model mean?" ambiguity at the schema level, not at the prose-description level.
- **Disabled instructions are invisible.** When a file is disabled (user config or unmet workspace flags), it does not appear in `list_*`, `search_*_by_metadata`, or `search_*_by_content` results, and `get_*` returns a minimal `{ disabled: true }` envelope with no content. The user disabled it for a reason — that is the signal. We deliberately do not offer an `includeDisabled` opt-in, an `enabled: false` flag, or any other transparency channel: each of those would let the model see (or reason about, or quote) the very text the user removed from its prompt, which defeats the purpose of disabling it. The model sees exactly what the editor would attach — no more.
- **`applyTo` matching uses VS Code's matcher.** Wherever an LM tool accepts an `applyTo` input (on `list_*`, on `search_*_by_metadata`, on `search_*_by_content`), the value is a glob and matching is glob-vs-glob set intersection resolved via `vscode.languages.match` (or the equivalent glob primitive VS Code itself uses to evaluate `chatInstructions` `applyTo`) — not a hand-rolled glob library, not regex. The model's idea of "which instructions apply to `Foo.cs`?" must agree exactly with what VS Code would attach in the editor, otherwise the user sees one answer in the panel and the model reasons from another. Parity with the platform is the constraint; everything else (which API call, how globs are normalized) follows from that.
- **Identifier-aware tokenization, no search library.** The corpus is dominated by identifiers (`ConfigureAwait`, `IAsyncEnumerable`, `dotnet-async-await`), so the search tokenizer splits on `\W+`, on camelCase/PascalCase boundaries, and on `-`/`_`, and indexes both the whole token and its pieces. That single rule covers the realistic miss cases (`configure await` against `ConfigureAwait`, `nuget` against `NuGet`, `async await` against `dotnet-async-await`) without bringing in `lunr`/`minisearch`/`fuse.js`. Stemming, fuzzy matching, and embeddings are explicitly off the table; if the tokenizer ever proves insufficient the next step is a tiny hand-rolled inverted index, not a dependency. See [Search semantics](#search_autocontext_instructions_files_by_content) for the exact rules.
- **Section-scoped fetch is a first-class feature, not an optimisation.** The build-time section index gives every `##` and `###` a stable anchor with character offsets, and all three discovery tools publish those anchors in shapes the model can feed straight back into `get_*`: `list_*` returns `sections[]` (when requested), `search_*_by_metadata` adds `matchedAnchors` whenever the predicate touched `sections.*`, and `search_*_by_content` carries an `anchor` on each excerpt. `get_autocontext_instructions_file` accepts `sections: string[]` from day one and slices in O(k) via the precomputed offsets. The tool descriptions explicitly nudge `search → get(sections=[hit.anchor])`. Whether the model actually threads anchors through (versus pulling whole files out of habit) is a behavioural question to be answered by real-session telemetry, not a design question — the affordance is in place.
- **Cache tokens are deferred, not designed away.** A full ETag/If-None-Match scheme — `catalogToken` on `list_*`/`_by_metadata` outputs (sha256 over the deterministic JSON of the included entries' `version`+`contentHash` pairs in `name`-order), per-entry `etag` on every catalogue row and content-search hit, an optional `ifChangedSince` input on the discovery tools and on `get_*` that short-circuits to a `{ unchanged: true, ... }` envelope when the token matches — is fully specified and cheap to add (one new sha256 per catalogue assembly; the per-file `contentHash` already exists in `instructions-files.metadata.json`; override files re-use the lazy re-parse already specified in [Data sources](#data-sources)). It is *not* shipped on day one for the same reason section-scoped fetch's behavioural unknown is filed under "telemetry, not design": we don't yet know whether models will thread an opaque token back through subsequent calls, and shipping the contract pre-emptively bakes in a wire format before we have evidence it gets used. Defer until real-session traces show either repeated `list_*` calls within a session or repeated `get_*` calls for the same `name` — both are the trigger to wire it up. Two implementation notes for when we do: (1) tokens are sha256 hex strings, not opaque base64, because models copy hex reliably and sometimes "tidy up" opaque blobs; (2) the catalogue token covers entry contents only, not the filter arguments — different `applyTo`/`category` arguments produce different results regardless, so folding them into the token would just defeat reuse across slightly-different list calls.
- **Excerpt budget.** Hard-cap at ≤ 3 excerpts × ~200 chars per hit, ≤ 25 hits per query, so total payload is bounded regardless of corpus size or how loose the query is.

## Acceptance

- The model, prompted to edit `Foo.cs`, calls `list_autocontext_instructions_files` with `applyTo: "src/Foo.cs"`, receives a result that includes `lang-csharp` and `dotnet-coding-standards`, then calls `get_autocontext_instructions_file` to fetch each.
- The model, asked "does AutoContext require `ConfigureAwait`?", calls `search_autocontext_instructions_files_by_content` with `query: "ConfigureAwait"`, gets `dotnet-async-await` ranked first with an excerpt containing the rule, then calls `get_autocontext_instructions_file` with `sections: ["<matched-anchor>"]` to read only that section — not the whole file.
- The model, asked "which AutoContext rules have a Security section?", calls `search_autocontext_instructions_files_by_metadata` with `{ "sections.heading": "Security" }`, receives the matching catalogue entries each carrying a `matchedAnchors` array, then calls `get_autocontext_instructions_file({ name, sections: matchedAnchors })` to read only those sections.
- The fetched content is identical to what would have been written into `.github/instructions/lang-csharp.instructions.md` (post-normalization). Section-scoped fetches return exactly the slices named by their anchors, in document order, each prefixed by its original heading.
- In a workspace with `hasCSharp` disabled, the C# instructions do not appear in `list_*`, `search_*_by_metadata`, or `search_*_by_content` results, and `get_autocontext_instructions_file({ name: "lang-csharp" })` returns `{ disabled: true }` with no content.
- `list_autocontext_instructions_files({ applyTo: "x" })` produces exactly the same result as `search_autocontext_instructions_files_by_metadata({ "applyTo": "x" })` for any glob `x` — verified by a property-style test — because the former delegates to the latter.
- No new MCP traffic. No new context keys. No platform-side changes.

## Implementation order

1. ~~Define the frontmatter contract and ensure every `*.instructions.md` source carries `name`, `description`, and (optionally) `applyTo`.~~ **Done.** Version is embedded in `name` as `(vX.Y.Z)`; all 78 source files conform.
2. ~~Add `instructions-files-metadata-generator.ts`, wire it into `Compile TS`, and emit `instructions-files.metadata.json`.~~ **Done.** The metadata file is gitignored and regenerated on every compile (no CI `git diff` gate); section/frontmatter validation is build-fatal. The optional `instructions-files.metadata.schema.json` is deferred until needed.
3. ~~Update the manifest loader to read both JSON files at activation and join them.~~ **Done.** [`InstructionsFilesMetadataLoader`](../../src/AutoContext.VsCode/src/instructions-files-metadata-loader.ts) feeds [`InstructionsFilesManifestLoader.load(metadata)`](../../src/AutoContext.VsCode/src/instructions-files-manifest-loader.ts); the previous `InstructionsFileMetadataReader` markdown re-parser is gone. A content-projection method that returns normalized markdown per instruction `fileName` from the source `instructions/<file>` (frontmatter stripped, `[INST####]` tags stripped via [`InstructionsFilesManager.stripInstructionIds()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts), bullet-level disables **not** applied) still needs to be added. See [Filtering & gating](#filtering--gating) for why this reads from source and not from `.generated/`.
4. Implement the four LM-tool handlers in a new module `src/lm-tools/instructions-lm-tools.ts`. The metadata-predicate engine is the core; `list_*` translates its narrow input into a metadata predicate and delegates. The content projector consults [`WorkspaceContextDetector.getOverriddenContextKeys()`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) and reads `.github/instructions/<fileName>` when an override is in effect, falling back to the bundled source otherwise. `get_autocontext_instructions_file` supports the `sections` input from day one — the section index from `instructions-files.metadata.json` (or the lazy override re-parse) gives `charStart`/`charEnd` per anchor, so slicing is an O(k) array of substring copies followed by concatenation in document order. The content-search handler builds an in-memory corpus of `{ key, description, content, sections }` lazily on first call and caches it; invalidate on config change (same trigger that drives `InstructionsFilesManager.write()`) **and** on the override file-system watcher already maintained by `WorkspaceContextDetector` so override edits show up in subsequent searches.
5. Add `contributes.languageModelTools` entries to `package.json` (hand-authored, four entries).
6. Wire registration into `ExtensionRegistrations` and composition.
7. Add the priming sentences to the top-level always-attached AutoContext instruction.
8. Tests: a Vitest suite that constructs the manifest with fixtures and asserts list/metadata-search/content-search/get behaviour, including `applyTo`/`category`/`includeSections` filtering, the metadata predicate semantics (regex on strings, glob on `applyTo`, array traversal, AND across keys, invalid-regex error envelope), `list_*` ≡ `search_*_by_metadata` equivalence, disabled-file invisibility across all four tools, content-search ranking determinism, and section attribution on excerpts. (Generator-side tests already exist in [`instructions-files-metadata-generator.test.ts`](../../src/AutoContext.VsCode/tests/unit-tests/instructions-files-metadata-generator.test.ts) and [`instructions-files-metadata-loader.test.ts`](../../src/AutoContext.VsCode/tests/unit-tests/instructions-files-metadata-loader.test.ts).)

## Out of scope

- Editor context-key tracking for `applyTo`. Not pursued — limited value and bounded by platform behaviour.
- Upstream ask to make `applyTo` actively inject. Worth filing as a separate VS Code issue, but no code dependency on it. The LM-tool design is the primary path.
- Modifying the user's `.github/copilot-instructions.md` dynamically.
- Cross-host instruction discovery (CLI, Claude Desktop). Instructions are a VS Code / Copilot concept; this design is intentionally extension-local.
