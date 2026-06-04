---
name: "autocontext (v1.0.0)"
description: "How Copilot should use AutoContext's instruction-discovery tools to find and apply the curated rules that ship with this extension."
---
# AutoContext — Tool Usage Guide

This guidance ships with the AutoContext VS Code extension. Apply it to every chat turn in a workspace where AutoContext is installed, regardless of the task — writing code, reviewing diffs, planning a refactor, drafting a commit message, answering a question about the codebase, or anything else where AutoContext's rules may apply.

## Discovering Applicable Rules
Whenever a task involves a specific file, topic, or repo convention — reading, writing, reviewing, refactoring, planning, answering questions, or drafting prose about it — find the AutoContext rule files that govern it before responding. AutoContext exposes its discovery tools as VS Code Language Model tools (`contributes.languageModelTools` in this extension's manifest); they are always available in chat and do not require activation.

- **Do** call `list_autocontext_instructions_files` with the file's workspace-relative path as `applyTo` whenever you encounter a file you have not yet checked. The result is the list of curated rule files governing that path.
- **Do** call `search_autocontext_instructions_files_by_content` for topical questions (e.g. "how should I name xUnit tests?", "does AutoContext require ConfigureAwait?").
- **Do** call `search_autocontext_instructions_files_by_metadata` for attribute queries (e.g. "which rules target *.cs files?", "which rules have a Security section?").
- **Do** chain any search hit into `get_autocontext_instructions_file` with the matched section anchors to fetch only the relevant rule text.
- **Don't** rely on conversation-start instruction injection alone — pull fresh applicable rules each time you encounter a new file path or topic.

## Applying the Rules
Each rule file declares its own scope and any MCP-tool obligations it imposes (look for `## MCP Tool Validation` and similar sections within the rule body). Treat the rule file as the source of truth.

- **Do** read every section returned by `get_autocontext_instructions_file` before responding, including any tool-validation directives the rule declares.
- **Do** invoke whichever MCP tools the matched rule files require, on the inputs that rule specifies. The chat host may surface MCP tools under a host-specific prefix (e.g. `mcp_<server>_<tool>`); match by suffix, not by exact string. If a tool a rule references is not currently visible, call `tool_search` with the bare tool name to load it.
- **Do** address every finding the tools report before continuing.
- **Don't** skip rule discovery, rule reading, or rule-mandated tool calls because the change is "small", read-only, or "just a review" — the rules apply to every interaction with a covered artefact.

## Workflow Loop
For every turn that touches or discusses a workspace artefact, follow this sequence.

1. **Do** call `list_autocontext_instructions_files` for the relevant file path, or `search_autocontext_instructions_files_by_content` / `search_autocontext_instructions_files_by_metadata` for a topic.
2. **Do** call `get_autocontext_instructions_file` for any matched sections.
3. **Do** apply the rules to your response (write, edit, review, plan, or explain) and run any MCP tools those rules require.
4. **Do** address every finding before continuing.
