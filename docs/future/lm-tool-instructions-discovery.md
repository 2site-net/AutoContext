# LM-Tool Instructions Discovery

> **Status:** Future / design note
>
> Address the "instructions are silently skipped" problem by exposing AutoContext's instruction files to Copilot through three extension-native VS Code Language Model tools, instead of relying on `applyTo` to trigger automatic prompt injection (which it does not, in agent mode).

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
- `sections` *(only when `includeSections: true`)* — array of `{ heading, level, anchor, parent? }` describing the file's `##` and `###` headings.

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

- `name`, `key`, `description`, `applyTo`, `categories`, `enabled` — same metadata fields as `list_*`.
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

Sections are extracted **at build time** by [`instructions-files-metadata-generator.ts`](#build-time-metadata-extraction) and stored in `instructions-files.metadata.json`. The runtime never parses markdown for structure — it reads the metadata manifest. This keeps activation cheap and lets the build validate heading sanity (no orphan `###`, no anchor collisions, etc.) loudly.

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

- `name`, `key`, `description`, `applyTo`, `version`, `categories`
- `content` — the full markdown body, post-normalization (after disabled instructions removed, `[INST####]` tags stripped — same content the user gets in `.github/instructions/`)
- `enabled` flag

Returning normalized content (matching what [`InstructionsFilesManager`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) writes today) means the model sees exactly what it would see if the file were attached normally.

### Build-time metadata extraction

The shipped extension contains two manifest files describing instruction files. They live next to each other in [`src/AutoContext.VsCode/resources/`](../../src/AutoContext.VsCode/resources/) and are both packaged into the VSIX.

- **`instructions-files.json`** — hand-authored. Curatorial / policy layer: the `categories` taxonomy with descriptions, and per-file `label`, `categories` membership, `activationFlags`, `schemaVersion`. Not regenerated.
- **`instructions-files.metadata.json`** — build-generated. File-intrinsic layer: per-file `description`, `applyTo`, `version`, `contentHash`, and `sections` (with character offsets). Sole writer is the build.

A new build script, `instructions-files-metadata-generator.ts`, runs before TypeScript compile (wired into `build.ps1 Compile`). For every `*.instructions.md` source file it:

1. Parses YAML frontmatter → `{ description, applyTo, version }`.
2. Validates required fields, glob syntax on `applyTo`, semver shape on `version`, and version monotonicity (no decreases relative to the previous build).
3. Normalizes the body (existing `[INST####]` handling — same projection used by [`InstructionsFilesManager`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts)).
4. Walks normalized body for `##` / `###` headings → `sections` per [Section awareness](#section-awareness).
5. Hashes normalized body → `contentHash` (sha256, useful for cache invalidation and CI checks).
6. Emits a deterministic `instructions-files.metadata.json` (entries sorted by `name`).
7. Cross-validates: the set of `name`s in `instructions-files.metadata.json` must equal the set in `instructions-files.json`. Any mismatch fails the build.

The metadata file is **committed** (not gitignored). The diff is the changelog of structural changes to the instruction corpus. CI runs the generator and gates on `git diff --exit-code` against `instructions-files.metadata.json` to force regeneration before merge.

**Frontmatter contract** — every `*.instructions.md` source file declares:

```yaml
---
description: "<one-line summary>"
applyTo: "<glob>"
version: "1.0"
---
```

`version` is manual semver, bumped by the author when the rules meaningfully change. The generator validates well-formedness; it does not auto-bump.

### Data sources

All three tools read from data already loaded in memory:

- [`InstructionsFilesManifest`](../../src/AutoContext.VsCode/src/instructions-files-manifest.ts) — reads both `instructions-files.json` and `instructions-files.metadata.json` at activation, joins them by `name`, exposes a single in-memory model with all fields populated. The two-file split is an authoring concern; the runtime sees one logical manifest.
- [`InstructionsFilesManager`](../../src/AutoContext.VsCode/src/instructions-files-manager.ts) — already produces normalized files; reuse the same projection logic for `content` (or read the staged/generated file from disk if simpler).
- [`AutoContextConfigManager`](../../src/AutoContext.VsCode/src/autocontext-config-manager.ts) and [`workspace-context-detector.ts`](../../src/AutoContext.VsCode/src/workspace-context-detector.ts) — to compute the `enabled` flag.

No round-trip to the .NET MCP server. No new pipe traffic. No runtime markdown parsing — frontmatter and headings are pre-extracted into `instructions-files.metadata.json` at build time.

### Filtering & gating

`list_autocontext_instructions_files` and `search_autocontext_instructions_files` respect the user's enabled/disabled state by default. Both accept `includeDisabled` (default `false`) so the model can ask about the full set when explicitly diagnosing.

`get_autocontext_instructions_file` returns content regardless of `enabled` — if the user explicitly asked the model to fetch a disabled instruction, returning "disabled" without content is unhelpful. The `enabled` flag in the output lets the model note that fact in its reasoning.

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

1. Define the frontmatter contract and ensure every `*.instructions.md` source carries `description`, `applyTo`, and `version`. (`description` and `applyTo` are likely already there; `version` is new.)
2. Add `instructions-files-metadata-generator.ts` and a matching `instructions-files.metadata.schema.json`. Wire it into `build.ps1 Compile` before TS compile. Commit the generated `instructions-files.metadata.json`. Add a CI gate that regenerates and asserts `git diff --exit-code`.
3. Update [`InstructionsFilesManifest`](../../src/AutoContext.VsCode/src/instructions-files-manifest.ts) to load both JSON files at activation and join by `name`. Add a content-projection method that returns normalized markdown per instruction `key` synchronously from in-memory state (reuse the projection used by `write()` so the result matches the on-disk artefact).
4. Implement the three LM-tool handlers in a new module `src/lm-tools/instructions-lm-tools.ts`. The search handler builds an in-memory corpus of `{ key, description, content, sections }` lazily on first call and caches it; invalidate on config change (same trigger that drives `InstructionsFilesManager.write()`).
5. Add `contributes.languageModelTools` entries to `package.json` (hand-authored).
6. Wire registration into `ExtensionRegistrations` and composition.
7. Add the priming sentences to the top-level always-attached AutoContext instruction.
8. Tests: a Vitest suite that constructs the manifest with fixtures and asserts list/search/get behaviour, including `path`/`category`/`includeDisabled`/`includeSections` filtering, search ranking determinism, and section attribution on excerpts. Separate tests for the metadata generator (frontmatter parsing, heading extraction, validation failures).

## Out of scope

- Editor context-key tracking for `applyTo`. Not pursued — limited value and bounded by platform behaviour.
- Upstream ask to make `applyTo` actively inject. Worth filing as a separate VS Code issue, but no code dependency on it. The LM-tool design is the primary path.
- Modifying the user's `.github/copilot-instructions.md` dynamically.
- Cross-host instruction discovery (CLI, Claude Desktop). Instructions are a VS Code / Copilot concept; this design is intentionally extension-local.
