# MCP Tool Registration Suppression (`--suppress-tools-registration`)

> **Status:** Future / design note
>
> Plumbing-level change to `AutoContext.Mcp.Server` that lets the host (today: the VS Code extension; tomorrow: any embedded host) tell the server "do not register your tools with the MCP SDK on startup." Enables future LM-tool promotion of execution tools (`check_csharp_all`, `read_editorconfig`, etc.) without double-exposure inside VS Code, while leaving the CLI / Inspector / external MCP clients fully intact.

## Background

### "Deferred" MCP tools today

In an agent session, VS Code surfaces MCP tools in an `<availableDeferredTools>` block instead of the default tool list once the count crosses a threshold. AutoContext currently exposes 20+ tools, so its MCP tools are deferred. Examples:

- `mcp_autocontext_d_check_csharp_all`
- `mcp_autocontext_d_check_nuget_hygiene`
- `mcp_autocontext_e_get_editorconfig`
- `mcp_autocontext_g_check_git_all`
- `mcp_autocontext_t_check_typescript_all`

Deferred tools are not directly callable. The model must:

1. Realize a tool *might* exist for the task.
2. Call `tool_search` with a natural-language query.
3. Wait for the search to return matching tool definitions.
4. *Then* call the tool.

### The discoverability tax

- The model has to *guess* a relevant tool exists before searching. If it doesn't think to search, it never finds the tool — even when the tool would have been the correct choice.
- For tools the user wants invoked routinely (e.g. `check_csharp_all` before declaring a C# task done, or `get_editorconfig` before edits), this guess-first model is unreliable.
- "Always-available" tools like `read_file` or `grep_search` don't pay this cost.

### What "promotion" would look like

VS Code 1.95+ ships `vscode.lm.registerTool()` + `contributes.languageModelTools`. Tools registered this way are first-class chat tools — always-available, `#`-mentionable, never deferred by count.

A future change could promote a curated set of execution tools (e.g. `check_csharp_all`, `check_nuget_hygiene`, `check_git_all`, `read_editorconfig`) to LM tools. The .NET MCP server stays the execution engine; a thin TS shim in the extension wraps each promoted tool and forwards calls to the same MCP server over the existing pipe.

### Why suppression is needed

If both surfaces are live for the same tool, inside VS Code the model sees it twice — once as `#check_csharp_all` (LM tool) and once as a deferred MCP tool. The MCP-side registration becomes redundant noise: every promoted tool would still appear in `tool_search` results, polluting the deferred surface for no benefit.

The cleanest answer in the embedded VS Code scenario: when LM tools cover the AutoContext surface, suppress *all* MCP-side tool registration. The pipe stays alive for forwarded calls; the MCP `tools/list` is empty.

In every other host (the future [`autoctx` CLI](autoctx-cli.md), MCP Inspector, Claude Desktop, Cursor, …), nothing changes — the server keeps registering its full tool set, because there are no LM tools to take over.

## Goal

Add a single, generic, host-neutral switch to `AutoContext.Mcp.Server` that disables tool registration with the MCP SDK at startup. Default off (current behaviour preserved). Passed only by hosts that have arranged an alternative tool surface.

This is **infrastructure**, not a feature shipped to users. Until/unless an LM-tool shim is added in `AutoContext.VsCode`, the flag has no consumer and is not passed.

## Design

### The flag

`--suppress-tools-registration` — boolean, no value.

- **Absent (default):** server registers every tool with the MCP SDK normally.
- **Present:** server does **not** register any tool with the MCP SDK. The dispatcher / handler infrastructure remains fully populated so calls forwarded over the pipe still execute.

### Server-side semantics

Two equivalent implementation strategies; pick whichever the .NET MCP SDK supports cleanly:

1. **Skip registration.** During startup, when `--suppress-tools-registration` is set, the server iterates [`mcp-workers-registry.json`](../../src/AutoContext.Mcp.Server/mcp-workers-registry.json) but does not call `[Tool]` registration on the SDK. The dispatcher (the in-process map of tool name → handler) is still populated, so a forwarded call from an LM-tool shim — which arrives over the existing worker pipe / JSON-RPC channel — finds and runs the handler.
2. **Empty `tools/list` response.** Register everything as today, but when the flag is set return an empty list to MCP `tools/list` requests. Same observable behaviour from the client's perspective; differs only in internal SDK state.

Strategy (1) is preferred — fewer registered objects, cleaner trace logs, no risk of an SDK code path leaking the suppressed tools into capabilities advertisement.

The flag does **not** affect:

- MCP protocol handshake / capability advertisement (other than the empty tool list).
- Resources, prompts, or sampling — none of which AutoContext currently exposes, but the flag's name ("tools-registration") makes its scope explicit.
- The pipe / worker control channels used by the extension to drive the server.
- Logging, health monitoring, configuration streaming, or any of the existing `--service` channels in [`McpServerProvider`](../../src/AutoContext.VsCode/src/mcp-server-provider.ts).

### Host-side decision: who passes the flag, and when

The flag is passed if and only if the host has registered LM tools (or some other surface) that supersede the MCP-side tool exposure entirely. Concretely:

- **VS Code extension:** passes the flag once an LM-tool shim is shipped that forwards calls back to the embedded MCP server. Until then, does not pass it.
- **`autoctx` CLI:** never passes it. The CLI has no alternative tool surface; tools must be advertised normally over MCP.
- **MCP Inspector, Claude Desktop, Cursor, future hosts:** never pass it. Same reasoning.

This keeps the embedded-vs-standalone distinction explicit in the spawn arguments. Reading the server's launch line in logs immediately tells you which mode it's running in.

The "which tools" decision stays in the extension where it belongs (it depends on which tools the extension promotes to LM tools — a VS-Code-only concept). The server just answers the simpler question "should I advertise tools at all?"

### Relationship to the `autoctx` CLI plan

[`autoctx-cli.md`](autoctx-cli.md) defines a host-agnostic CLI that spawns the same `AutoContext.Mcp.Server` binary. The CLI never passes `--suppress-tools-registration`, so it sees the full MCP tool surface. The flag is invisible to CLI users and to the CLI's composition root.

This satisfies the principle in that doc that the framework / server contains no VS Code vocabulary: the server's flag is "do not register tools," not "I'm running inside VS Code." The decision to hide tools because LM tools exist is made in `AutoContext.VsCode`; the server only knows the consequence.

## Where this leaves Feature 1 ("promote MCP tools to LM tools")

The flag is the **prerequisite plumbing** for promotion. Promotion itself — adding LM-tool shims for `check_csharp_all` etc. — is a separate, additive step that depends on this flag being available.

Whether to actually do the promotion is a deferred decision:

- The instruction-discovery LM tools described in [lm-tool-instructions-discovery.md](lm-tool-instructions-discovery.md) close most of the AutoContext discoverability gap on the instructions side without needing this flag.
- Execution tools (`check_*`) are called less frequently than instruction lookups; the deferred-via-`tool_search` cost is real but lower-impact than instruction injection failures.
- Promotion has its own risk surface (LM-tool result-shape parity, tool budget, capability gaps vs. MCP) worth handling in its own design pass.

The recommendation: **ship the flag now, ship promotion later (or never).** Shipping the flag first is cheap, tested in isolation (with no consumer), and unblocks promotion experimentation when motivation arises.

Counter-argument worth considering: shipping a flag with no consumer is dead code until used. If the team prefers strictly demand-driven additions, defer this entire doc until promotion is committed to. Current preference: ship the flag now, because (a) it's small, (b) it removes a coordination obstacle later, and (c) it forces the design conversation about scope and naming while the context is fresh.

## Risks & open questions

- **SDK behaviour on empty registration.** Verify that the .NET MCP SDK accepts a server with zero `[Tool]` registrations — it should (per spec, `tools/list` can be empty), but worth a smoke test before committing to strategy (1).
- **Capability advertisement.** The server currently advertises `tools` capability. Decide whether to suppress that too when the flag is set (consistent: "we have no tools") or keep it as-is (clients can call `tools/list`, get an empty list, and infer). Probably leave the capability advertised — it's the standard MCP signal that this server *can* expose tools, even if it currently doesn't.
- **Future LM-tool shim implementation.** Out of scope here, but the design must account for: (a) result-shape conversion from MCP tool result → `LanguageModelToolResult`, (b) cancellation token wiring through to the dispatcher, (c) error reporting consistency. Tracked as a follow-up doc when promotion is committed to.
- **No interaction with [lm-tool-instructions-discovery.md](lm-tool-instructions-discovery.md).** Those tools are extension-native and don't touch the MCP server. The two designs are independent.

## Acceptance

- `AutoContext.Mcp.Server` accepts `--suppress-tools-registration` with no value.
- When passed: `tools/list` over MCP returns `[]`, no tool is registered with the SDK, but the in-process dispatcher is still populated and able to execute any tool by name when called via the existing forwarded-call channel.
- When absent (default): identical behaviour to today.
- The CLI (`autoctx service mcp://...`, per [autoctx-cli.md](autoctx-cli.md)) does not pass the flag and sees the full MCP tool surface.
- The VS Code extension does **not** pass the flag in its current shape (no LM-tool shim yet). The flag exists but is dormant until a promotion lands.
- Unit / smoke test in `AutoContext.Mcp.Server.Tests` covering both modes (flag set / unset) and asserting `tools/list` content.

## Implementation order

1. Add CLI argument parsing for `--suppress-tools-registration` to `AutoContext.Mcp.Server` (alongside the existing `--instance-id`, `--service` flags).
2. Thread the boolean into the server's startup composition.
3. Branch the tool-registration step on the flag.
4. Tests: verify dispatcher still serves direct calls when the flag is set; verify `tools/list` is empty.
5. *(Deferred)* — when LM-tool promotion is committed to, update [`McpServerProvider.provideMcpServerDefinitions`](../../src/AutoContext.VsCode/src/mcp-server-provider.ts) to append `--suppress-tools-registration` to the spawn args.

## Out of scope

- Adding the LM-tool shim itself in `AutoContext.VsCode`. That's a separate design when (and if) promotion is decided.
- Affecting MCP protocol layers other than tools (resources, prompts, sampling).
- Modifying the registry schema. The registry stays the host-agnostic source of truth; suppression is a runtime flag, not a registry annotation.
