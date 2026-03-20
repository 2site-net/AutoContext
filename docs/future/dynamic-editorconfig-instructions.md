# Dynamic EditorConfig Instruction Injection

## Summary

Replace the current `get_editorconfig` MCP tool with a dynamic instruction
provider that injects `.editorconfig` rules directly into the Copilot chat
context — no explicit tool call required.

## Current Approach

The `get_editorconfig` MCP tool reads and parses `.editorconfig` files on demand.
Copilot must call it explicitly before generating or reviewing code. The
`copilot.instructions.md` file instructs Copilot to do so, but compliance
depends on the model following that instruction.

## Proposed Approach

Use `vscode.chat.registerInstructionsProvider()` to dynamically generate an
instruction file derived from the workspace `.editorconfig`. Fire
`onDidChangeInstructions` whenever `.editorconfig` is created, modified, or
deleted so Copilot always has up-to-date formatting rules in context
automatically.

This removes the dependency on Copilot remembering to call a tool and ensures
style rules are always present in the prompt.

## Blocking Issue

The API is part of the `chatPromptFiles` proposed API, first documented in the
VS Code 1.109 release notes. As of VS Code 1.110 (March 2026) it remains
proposed and is not available in stable builds or Marketplace extensions.

**Proposed APIs cannot be used in published extensions.** They require
`"enabledApiProposals": ["chatPromptFiles"]` in `package.json` and only work
in VS Code Insiders or with `--enable-proposed-api`.

## When to Revisit

Check each VS Code release's **API Finalization** section in the release notes.
Once `chatPromptFiles` graduates to stable:

1. Bump the `engines.vscode` minimum to the version that stabilized the API.
2. Implement an `InstructionsProvider` that watches `.editorconfig` files.
3. Evaluate whether the `get_editorconfig` MCP tool can be retired or kept as a
   fallback for non-VS Code MCP clients.
