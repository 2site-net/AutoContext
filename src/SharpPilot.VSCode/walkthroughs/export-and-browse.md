## Export & Browse Instructions

### Export to your repository

Export instruction files into your workspace at `.github/instructions/`. This is useful for sharing coding guidelines across the team — teammates with VS Code and GitHub Copilot will pick them up automatically, even without SharpPilot installed.

A multi-select menu lets you pick which instructions to export. If a file already exists at the target path, you can choose to **Overwrite**, **Open Existing**, or **Skip**.

[Export Instructions](command:sharppilot.exportInstructions)

### Override detection

Once instruction files exist in `.github/instructions/` that match a built-in instruction by filename, the extension detects them as overrides. Overridden instructions are marked with a $(file-symlink-directory) badge in the Toggle Instructions menu, signaling that a workspace-level version is present.

### Browse and disable individual instructions

Browse any instruction file shipped with the extension — opens it in a virtual document with a **Disable Instruction** / **Enable Instruction** CodeLens above each instruction. Click a CodeLens to toggle that instruction. Disabled instructions are dimmed and tagged `[DISABLED]`, and they are excluded from what Copilot receives.

When any instructions are disabled, a **Reset All Instructions** CodeLens appears at the top of the file to re-enable everything at once. The disable state is stored in `.sharppilot.json` in your workspace root.

[Browse Instructions](command:sharppilot.browseInstructions)
