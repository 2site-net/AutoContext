![SharpPilot](../resources/logo.png)

## Welcome to SharpPilot

SharpPilot is a quality-assurance extension for GitHub Copilot. It ships curated **chat instructions** that guide Copilot's responses and registers **MCP servers** whose tools let Copilot check your code against best practices — all manageable from dedicated sidebar panels.

### What you get

- **Chat Instructions** — Curated Markdown guidelines covering .NET, TypeScript, web frameworks, scripting, Git, and more. Instructions are workspace-aware: only the ones relevant to your project are injected into Copilot's context. Individual rules can be disabled without turning off the entire file.
- **MCP Tool Checks** — Quality checks that Copilot can invoke in Agent mode across multiple categories (DotNet, Git, EditorConfig, TypeScript). Each sub-check can be toggled individually.
- **EditorConfig-Driven Enforcement** — Checkers read `.editorconfig` properties and enforce whichever direction the project specifies rather than just skipping conflicting rules.
- **Auto Configuration** — One command scans your workspace and enables only the instructions and tools that match the detected technologies.
- **Sidebar Panels** — Dedicated Instructions and MCP Tools panels in the SharpPilot activity bar, showing enabled/total counts and offering filters, export, and per-item toggling.
