# LM-Tool Instructions Discovery

> **Status:** Future / design note — *infrastructure prerequisites shipped*.
>
> Address the "instructions are silently skipped" problem by exposing AutoContext's instruction files to Copilot through three extension-native VS Code Language Model tools, instead of relying on `applyTo` to trigger automatic prompt injection (which it does not, in agent mode).
>
> The data plane this design depends on — the build-time metadata generator,
> the two-file manifest split, the runtime join, and the section index — is
> already implemented and shipped (see [Build-time metadata extraction](#build-time-metadata-extraction)
> and the *Implementation order* checklist below). What's still future work is
> the three LM tools themselves and the priming hint.

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

Three LM tools, all extension-native (no MCP server involvement), all registered via `vscode.lm.registerTool` and declared in `package.json` `contributes.languageModelTools`.

The three roles:

- **`list_autocontext_instructions_files`** — *what's available* (catalogue, filterable by path/category).
- **`search_autocontext_instructions_files`** — *find by intent* (full-text search across instruction bodies and descriptions).
- **`get_autocontext_instructions_file`** — *fetch the content* (normalized markdown of one named instruction).

Listing is for "what applies here?"; search is for "where is the rule about X?"; get is for "give me the actual text." The model picks the right entry point based on the question it's trying to answer.

### `list_autocontext_instructions_files`

Returns the catalogue of AutoContext instruction files available in this workspace.

**Inputs (all optional):**

- `path` *(string)* — when provided, restricts the result to instruction files whose `applyTo` glob matches the given workspace-relative path. Lets the model ask "which instructions apply to `src/Foo.cs`?" in one call.
- `category` *(string)* — filter by category as defined in [`instructions-files.json`](../../src/AutoContext.VsCode/resources/instructions-files.json) (e.g. `dotnet`, `web`).
- `includeDisabled` *(boolean, default `false`)* — include instructions disabled by user config or workspace flags.
- `includeSections` *(boolean, default `false`)* — when `true`, each entry gains a `sections` array (see [Section awareness](#section-awareness)). Off by default to keep the listing response small.

**Output:** an array of entries, each with:

- `name` (e.g. `lang-csharp.instructions.md`)
- `key` (e.g. `lang-csharp`)
- `description` (from frontmatter)
- `applyTo` (the raw glob)
- `version` (per-file semver from frontmatter — see [Build-time metadata extraction](#build-time-metadata-extraction))
- `categories`
- `enabled` (true if the user's config + workspace flags currently activate this instruction)
- `overridden` (true if a workspace-local copy at `.github/instructions/<fileName>` is shadowing the bundled file)
- `bundledVersion` *(only when `overridden: true`)* — the version of the shipped file that the workspace copy is shadowing. Lets the model spot staleness (`version` < `bundledVersion`) or local divergence without a follow-up `get_*` call.
- `sections` *(only when `includeSections: true`)* — array of `{ heading, level, anchor, parent? }` describing the file's `##` and `###` headings (extracted from the override body when one is present).

The result is small (metadata only) and cacheable. No file content.

### `search_autocontext_instructions_files`

Full-text search across AutoContext instruction bodies (post-normalization) and frontmatter descriptions. Lets the model answer questions like "is there a rule about `ConfigureAwait`?" or "what does AutoContext say about NuGet floating versions?" without first listing every file and fetching each candidate.

**Inputs:**

- `query` *(string, required)* — free-text query. Tokenized and matched case-insensitively against instruction content and description.
- `path` *(string, optional)* — restrict search to instructions whose `applyTo` glob matches this workspace-relative path. Combines with `query` (logical AND).
- `category` *(string, optional)* — restrict to a single category.
- `maxResults` *(integer, optional, default 10, max 25)* — cap on returned hits.
- `includeDisabled` *(boolean, optional, default `false`)*.

**Output:** ranked array of hits, each with:

- `name`, `key`, `description`, `applyTo`, `categories`, `enabled`, `overridden`, `bundledVersion?` — same metadata fields as `list_*` (`bundledVersion` only present when `overridden: true`).
- `score` — relative ranking score (implementation-defined; only meaningful for ordering).
- `matches` — small array of excerpts (≤ 3 per file, each ≤ ~200 chars) showing the matched terms in surrounding context, with the matched span demarcated. Each excerpt also carries `section` (heading text) and `sectionLevel` (`2` or `3`) attributing it to the most specific containing heading — see [Section awareness](#section-awareness). Lets the model judge relevance and target a specific section without fetching the full body.

No full content in the result. The model fetches via `get_autocontext_instructions_file` after deciding which hits matter.

**Search semantics (start simple):**

- Lowercase the query and the searched text; split the query on whitespace into terms.
- A file matches if every term appears at least once across `description + content` (AND across terms).
- Score = sum over terms of (term frequency in file) × (1 if term hits `description`, else 0.5). Cheap, deterministic, no dependencies.
- Ties broken by `name` ascending for stability.

This is intentionally not an embedding-based or fuzzy search. The instruction set is small (dozens of files, ~tens of KB total post-normalization), exact substring matching is enough, and the model itself handles synonyms by issuing follow-up queries. Adding a real index is YAGNI until measured.

**Excerpt extraction:** for each hit, find up to 3 distinct match positions across the file (prefer matches in `description`, then earliest matches in `content`), and slice ±~80 chars around each, snapped to word boundaries. De-duplicate overlapping windows.

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

**Search attribution:** when an excerpt falls under a `###`, the match's `section` is that `###`. When it falls under a `##` with no `###` children (flat layout), the match's `section` is that `##`. Either way `sectionLevel` reflects which granularity was used.

This section index is also what enables a future `sections?: string[]` parameter on `get_autocontext_instructions_file` for partial fetch (see [Risks & open questions](#risks--open-questions)) — the model already has the section names in hand from `list_*` or `search_*`.

### `get_autocontext_instructions_file`

Returns the full, normalized content of a single instruction file.

**Inputs:**

- `name` *(string, required)* — e.g. `lang-csharp.instructions.md` or `lang-csharp` (accept both for ergonomics).

**Output:**

- `name`, `key`, `description`, `applyTo`, `categories`
- `version` — the version of the file actually returned (override version when overridden, bundled version otherwise)
- `content` — the full markdown body, post-normalization (frontmatter and `[INST####]` tags stripped). Sourced from the workspace override at `.github/instructions/<fileName>` when present, otherwise from the bundled source.
- `enabled` flag
- `overridden` flag and, when `true`, `bundledVersion` — lets the model recognise that the workspace copy may diverge from the shipped rule and may be outdated.

Returning normalized content (matching what [`InstructionsFilesManager`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) writes today) means the model sees exactly what it would see if the file were attached normally.

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

All three tools read from data already loaded in memory:

- [`InstructionsFilesManifestLoader`](../../src/AutoContext.VsCode/src/instructions-files-manifest-loader.ts) — reads `instructions-files.json` at activation; its `load(metadata)` takes a `Map<fileName, InstructionsFileMetadata>` and joins both layers into a single in-memory manifest. The two-file split is an authoring concern; the runtime sees one logical manifest.
- [`InstructionsFilesMetadataLoader`](../../src/AutoContext.VsCode/src/instructions-files-metadata-loader.ts) — reads `instructions-files.metadata.json` and produces the metadata map consumed above. Already wired into [`extension-composition.ts`](../../src/AutoContext.VsCode/src/extension-composition.ts).
- [`InstructionsFilesManager`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) — owns the staged `instructions/.generated/<file>` artefacts. **Not** the read target for these tools — see the warning under [Filtering & gating](#filtering--gating) on why content must come from the source files.
- [`AutoContextConfigManager`](../../src/AutoContext.VsCode/src/autocontext-config-manager.ts) and [`workspace-context-detector.ts`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) — to compute the `enabled` flag.

**Where `content` comes from.** Resolution order per file:

1. **Workspace override** — if [`WorkspaceContextDetector.getOverriddenContextKeys()`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) reports an override for this entry, read `.github/instructions/<fileName>` from the workspace. The user has explicitly customised this rule for this workspace, and that customised text is what's actually attached to Copilot prompts — the LM tools must reflect the same reality.
2. **Bundled source** — otherwise read the extension-bundled source at `instructions/<fileName>` (the same input the build sees).

In either case the handler then strips frontmatter and `[INST####]` tags (the projection used by [`InstructionsFilesManager.stripInstructionIds()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts)). It does **not** read from `instructions/.generated/`. See [Filtering & gating](#filtering--gating) for the rationale.

Override awareness also affects the metadata returned alongside `content`:

- `version` is taken from the override's frontmatter (already cached by [`WorkspaceContextDetector.getOverrideVersion()`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts)) when an override is in effect, so the model sees the version the user is actually running. An additional boolean `overridden` (and, when applicable, `bundledVersion`) is returned so the model can recognise drift.
- `sections` from `instructions-files.metadata.json` describe the **bundled** body. When an override is present its structure may diverge; the handler re-extracts sections from the override body using the same parser the build uses ([`InstructionsFilesMetadataGenerator`](../../src/AutoContext.VsCode/src/instructions-files-metadata-generator.ts)'s heading walker) so `list_*` `sections` and `search_*` excerpt attribution stay accurate. This re-parse runs lazily (only for files that actually have an override) and is cached until the override watcher fires.
- Search corpus entries for overridden files use the override body, so a rule the user added locally is findable via `search_autocontext_instructions_files`.

No round-trip to the .NET MCP server. No new pipe traffic. No runtime markdown parsing for headings — those are pre-extracted into `instructions-files.metadata.json` at build time. (The previous `InstructionsFileMetadataReader` markdown re-parser has been deleted.)

### Filtering & gating

There are **two** independent disable axes in AutoContext, and the LM tools handle them differently:

1. **Whole-file enablement** — driven by per-file activation flags + workspace context keys (`autocontext.workspace.has*`) and the user's tree-view toggle. This is what the `chatInstructions` `when` clauses gate: VS Code skips attaching a file whose `when` is false. The file itself is *still written* to `instructions/.generated/` regardless — [`InstructionsFilesManager.doWrite()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) walks every manifest entry unconditionally; the only thing the user toggle gates is the `when`-clause context key. The runtime computes `enabled` from the same inputs (config + context keys) for the LM-tool response.
2. **Bullet-level disables** — the user's `disabledInstructions` list (per-file array of `[INST####]` IDs). [`InstructionsFilesManager.writeNormalized()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) strips matching bullets when staging into `.generated/`. There is no UI affordance for this today; it's edited via config.

`list_autocontext_instructions_files` and `search_autocontext_instructions_files` respect axis 1 by default — disabled-as-a-whole files are hidden. Both accept `includeDisabled` (default `false`) so the model can ask about the full set when explicitly diagnosing.

`get_autocontext_instructions_file` returns content regardless of axis 1 — if the user explicitly asked the model to fetch a disabled instruction, returning "disabled" without content is unhelpful. The `enabled` flag in the output lets the model note that fact in its reasoning.

**Bullet-level disables are deliberately ignored by these tools.** All three handlers read from the source file — the workspace override at `.github/instructions/<file>` when present, otherwise the bundled `instructions/<file>` (frontmatter and `[INST####]` tags stripped in either case) — **not** from `instructions/.generated/<file>`. Two reasons:

- The model is asking what AutoContext's rules *are*. Hiding rules the user happened to mute in config gives the model an incomplete picture and makes search hits non-deterministic per workspace.
- Reading from `.generated/` would make `get_*` results depend on whether `InstructionsFilesManager.write()` had completed for the current workspace yet — a race during activation.

If bullet-level filtering ever needs to be exposed, add an explicit `applyUserFilters?: boolean` input rather than changing the default.

### `package.json` — `contributes.languageModelTools`

Three entries, hand-authored (the set is fixed — no generator needed):

```jsonc
{
  "name": "list_autocontext_instructions_files",
  "displayName": "List AutoContext Instructions Files",
  "modelDescription": "Lists AutoContext instruction files available in this workspace, optionally filtered by a workspace-relative path (returns only files whose applyTo glob matches) or by category. Call this before editing or reviewing files to discover which language- or framework-specific rules apply. Pair with get_autocontext_instructions_file to fetch the actual content of any listed file.",
  "userDescription": "Discover AutoContext instruction files relevant to the current task.",
  "canBeReferencedInPrompt": true,
  "toolReferenceName": "autoctxInstructionsFiles",
  "tags": ["autocontext", "instructions", "discovery"],
  "inputSchema": { /* path?, category?, includeDisabled?, includeSections? */ }
},
{
  "name": "search_autocontext_instructions_files",
  "displayName": "Search AutoContext Instructions Files",
  "modelDescription": "Searches AutoContext instruction file contents and descriptions for a free-text query and returns ranked hits with short excerpts. Use this when you need to find a specific rule or convention by topic (e.g. 'ConfigureAwait', 'NuGet floating versions', 'XML doc comments') rather than by file. Optionally constrain by workspace-relative path or category. Follow up with get_autocontext_instructions_file to read full content of any hit.",
  "userDescription": "Search AutoContext instruction files by keyword.",
  "canBeReferencedInPrompt": true,
  "toolReferenceName": "autoctxSearchInstructionsFiles",
  "tags": ["autocontext", "instructions", "search"],
  "inputSchema": { /* query, path?, category?, maxResults?, includeDisabled? */ }
},
{
  "name": "get_autocontext_instructions_file",
  "displayName": "Get AutoContext Instructions File",
  "modelDescription": "Returns the full normalized content of a single AutoContext instruction file by name (e.g. 'lang-csharp.instructions.md' or 'lang-csharp'). Use this after list_autocontext_instructions_files or search_autocontext_instructions_files to read the actual rules before generating, editing, or reviewing code that the file's applyTo glob matches.",
  "userDescription": "Read the contents of a specific AutoContext instruction file.",
  "canBeReferencedInPrompt": true,
  "toolReferenceName": "autoctxInstructionsFile",
  "tags": ["autocontext", "instructions"],
  "inputSchema": { /* name */ }
}
```

`when` clauses can gate visibility on the existing `autocontext.workspace.*` keys so the tools don't appear in workspaces with zero AutoContext-relevant files (e.g. no language flags set). Open question: probably not worth gating — these are cheap and self-describing, and gating risks the model failing to discover them in mixed-language workspaces.

### Runtime

A new module, e.g. `src/lm-tools/instructions-lm-tools.ts`, that:

1. Constructs handlers wrapping `InstructionsFilesManifest` + `AutoContextConfigManager` + the content projector.
2. Registers all three tools during activation:
   - `vscode.lm.registerTool('list_autocontext_instructions_files', listHandler)`
   - `vscode.lm.registerTool('search_autocontext_instructions_files', searchHandler)`
   - `vscode.lm.registerTool('get_autocontext_instructions_file', getHandler)`
3. Returns disposables to be tracked by [`ExtensionRegistrations`](../../src/AutoContext.VsCode/src/extension-registrations.ts).

Composition in [`extension-composition.ts`](../../src/AutoContext.VsCode/src/extension-composition.ts) follows the existing manual-`new` style — no DI changes.

### Priming the model to call these tools

LM-tool registration alone makes them *available*; it doesn't make them *invoked*. Three reinforcements:

1. **Tool descriptions** (above) explicitly tell the model "call this before editing/reviewing." The wording is the most important factor.
2. **A short note in a top-level AutoContext instruction** — something like "Before generating, editing, or reviewing files, call `list_autocontext_instructions_files` with the file's path to discover applicable rules, then fetch any relevant ones with `get_autocontext_instructions_file`. To find a specific rule by topic, use `search_autocontext_instructions_files`." Two sentences. This is the equivalent of the existing "use the `read_file` tool to read it before proceeding" hint, but pointing at tools that return the content directly.
3. **`tags`** include `autocontext` so users can discover them in the tool picker.

We deliberately do *not* edit the user's `.github/copilot-instructions.md`. The hint goes in the AutoContext-shipped instructions (e.g. a top-level `autocontext.instructions.md` already always-attached) so it's part of the platform rules, not user content.

## Risks & open questions

- **Token cost on `get_autocontext_instructions_file`.** Some instruction files are long. Mitigation: encourage the model to call `list_*` or `search_*` first and only fetch the relevant ones — this is exactly what the descriptions nudge. Optional future addition: a `sections?: string[]` parameter for partial fetch, but YAGNI until measured.
- **Caching across turns.** Listing is idempotent; the model may re-call it. That's fine — the response is cheap to compute. If observed in practice, we can return a stable `version` token and let the description suggest "list once per session."
- **`applyTo` matching semantics.** Implement once, in TS, using a small glob matcher (or `vscode.languages.match` against a `DocumentSelector` if applicable). Match the matcher VS Code itself uses for `chatInstructions` so behaviour is consistent with the user's mental model.
- **Disabled instructions.** Decision point: hide from `list_*` / `search_*` by default (clean), or include with an `enabled` flag (transparent). Current proposal: include only when `includeDisabled: true`, but always return content from `get_*` if the model explicitly asks.
- **Search quality.** The naive AND-of-terms substring match will miss synonyms and morphology (e.g. `ConfigureAwait` vs. `configure await`, `NuGet` vs. `nuget`). Lowercasing handles case; everything else is left to the model issuing follow-up queries. If quality issues show up in practice, the next step is stemming or a tiny inverted index — not embeddings.
- **Excerpt size.** Hard-cap at 3 excerpts × ~200 chars per hit, ≤ 25 hits — bounded total payload regardless of corpus size or query.
- **Confusion between `list` and `search`.** Risk: the model picks the wrong entry point. Mitigation lives in the `modelDescription` wording — `list` says "discover what applies to a file/category," `search` says "find by topic." Distinct enough that it should self-sort; revisit if observed.
- **No interaction with the MCP server.** Confirmed — these tools are self-contained in the extension and don't touch the .NET surface. They have no relationship to the `--suppress-tools-registration` plan in [mcp-tool-registration-suppression.md](mcp-tool-registration-suppression.md), and the two designs are independent.

## Acceptance

- The model, prompted to edit `Foo.cs`, calls `list_autocontext_instructions_files` with `path: "src/Foo.cs"` (or similar), receives a result that includes `lang-csharp` and `dotnet-coding-standards`, then calls `get_autocontext_instructions_file` to fetch each.
- The model, asked "does AutoContext require `ConfigureAwait`?", calls `search_autocontext_instructions_files` with `query: "ConfigureAwait"`, gets `dotnet-async-await` ranked first with an excerpt containing the rule, and optionally fetches the full content.
- The fetched content is identical to what would have been written into `.github/instructions/lang-csharp.instructions.md` (post-normalization).
- In a workspace with `hasCSharp` disabled, the C# instructions either don't appear in listing/search results (default) or appear with `enabled: false` (when explicitly requested).
- No new MCP traffic. No new context keys. No platform-side changes.

## Implementation order

1. ~~Define the frontmatter contract and ensure every `*.instructions.md` source carries `name`, `description`, and (optionally) `applyTo`.~~ **Done.** Version is embedded in `name` as `(vX.Y.Z)`; all 78 source files conform.
2. ~~Add `instructions-files-metadata-generator.ts`, wire it into `Compile TS`, and emit `instructions-files.metadata.json`.~~ **Done.** The metadata file is gitignored and regenerated on every compile (no CI `git diff` gate); section/frontmatter validation is build-fatal. The optional `instructions-files.metadata.schema.json` is deferred until needed.
3. ~~Update the manifest loader to read both JSON files at activation and join them.~~ **Done.** [`InstructionsFilesMetadataLoader`](../../src/AutoContext.VsCode/src/instructions-files-metadata-loader.ts) feeds [`InstructionsFilesManifestLoader.load(metadata)`](../../src/AutoContext.VsCode/src/instructions-files-manifest-loader.ts); the previous `InstructionsFileMetadataReader` markdown re-parser is gone. A content-projection method that returns normalized markdown per instruction `fileName` from the source `instructions/<file>` (frontmatter stripped, `[INST####]` tags stripped via [`InstructionsFilesManager.stripInstructionIds()`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts), bullet-level disables **not** applied) still needs to be added. See [Filtering & gating](#filtering--gating) for why this reads from source and not from `.generated/`.
4. Implement the three LM-tool handlers in a new module `src/lm-tools/instructions-lm-tools.ts`. The content projector consults [`WorkspaceContextDetector.getOverriddenContextKeys()`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) and reads `.github/instructions/<fileName>` when an override is in effect, falling back to the bundled source otherwise. The search handler builds an in-memory corpus of `{ key, description, content, sections }` lazily on first call and caches it; invalidate on config change (same trigger that drives `InstructionsFilesManager.write()`) **and** on the override file-system watcher already maintained by `WorkspaceContextDetector` so override edits show up in subsequent searches.
5. Add `contributes.languageModelTools` entries to `package.json` (hand-authored).
6. Wire registration into `ExtensionRegistrations` and composition.
7. Add the priming sentences to the top-level always-attached AutoContext instruction.
8. Tests: a Vitest suite that constructs the manifest with fixtures and asserts list/search/get behaviour, including `path`/`category`/`includeDisabled`/`includeSections` filtering, search ranking determinism, and section attribution on excerpts. (Generator-side tests already exist in [`instructions-files-metadata-generator.test.ts`](../../src/AutoContext.VsCode/tests/unit-tests/instructions-files-metadata-generator.test.ts) and [`instructions-files-metadata-loader.test.ts`](../../src/AutoContext.VsCode/tests/unit-tests/instructions-files-metadata-loader.test.ts).)

## Out of scope

- Editor context-key tracking for `applyTo`. Not pursued — limited value and bounded by platform behaviour.
- Upstream ask to make `applyTo` actively inject. Worth filing as a separate VS Code issue, but no code dependency on it. The LM-tool design is the primary path.
- Modifying the user's `.github/copilot-instructions.md` dynamically.
- Cross-host instruction discovery (CLI, Claude Desktop). Instructions are a VS Code / Copilot concept; this design is intentionally extension-local.
