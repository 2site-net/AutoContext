## Export & Browse Instructions

### Export to your repository

Export instruction files into your workspace at `.github/instructions/`. This is useful for sharing coding guidelines across the team — teammates with VS Code and GitHub Copilot will pick them up automatically, even without SharpPilot installed.

Open the Instructions Panel, click the export icon in the panel header to enter export mode, check the instructions you want to export, then confirm. If a file already exists at the target path, you can choose to **Overwrite** or **Skip**.

[Open Instructions Panel](command:sharppilot.instructionsView.focus)

### Exported instructions

Once an instruction file exists in `.github/instructions/`, it is automatically hidden from the panel. The exported workspace-level file takes precedence. Delete the exported file to bring the built-in instruction back.

### Browse and disable individual instructions

Click any instruction in the Instructions Panel to open it in a virtual document with a **Disable Instruction** / **Enable Instruction** CodeLens above each instruction. Click a CodeLens to toggle that instruction. Disabled instructions are dimmed and tagged `[DISABLED]`, and they are excluded from what Copilot receives.

When any instructions are disabled, a **Reset All Instructions** CodeLens appears at the top of the file to re-enable everything at once. The disable state is stored in `.sharppilot.json` in your workspace root.
