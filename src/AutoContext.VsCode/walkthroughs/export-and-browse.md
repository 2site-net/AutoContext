## Export & Browse Instructions

### Export to your repository

Export instruction files into your workspace at `.github/instructions/`. This is useful for sharing coding guidelines across the team — teammates using VS Code will pick them up automatically, even without AutoContext installed.

Open the Instructions Panel, click the export icon in the panel header to enter export mode, check the instructions you want to export, then confirm. If a file already exists at the target path, you can choose to **Overwrite** or **Skip**.

[Open Instructions Panel](command:autocontext.instructions-view.focus)

### Exported instructions

Once an instruction file exists in `.github/instructions/`, it appears as **overridden** in the panel with a distinct icon. Clicking an overridden item opens the workspace-level file for editing. Use the **Show Original** inline action to view the built-in version, or **Delete Override** to remove the workspace file and revert to the built-in version.

When a local override is older than the bundled version, it is flagged as **overridden (outdated)**. Use **Show Changelog** to review what changed between the override and the latest built-in version, then either update the override manually or delete it to restore the latest built-in version.

### Browse and disable individual instructions

Click any instruction in the Instructions Panel to open it for reading. Each instruction has a **Disable Instruction** / **Enable Instruction** action above it — click to toggle. Disabled instructions are dimmed, tagged `[DISABLED]`, and left out of what your agent receives.

When any instructions are disabled, a **Reset All Instructions** action appears at the top of the file to re-enable everything at once. Your choices are saved in `.autocontext.json` in your workspace root.
