## Export & Browse Instructions

### Export to your repository

Export instruction files into your workspace at `.github/instructions/` (or `.github/copilot-instructions.md` for the main Copilot instructions file). This is useful for sharing coding guidelines across the team or using them without the extension installed.

A multi-select menu lets you pick which instructions to export. If a file already exists at the target path, you can choose to **Overwrite**, **Open Existing**, or **Skip**.

[Export Instructions](command:sharppilot.exportInstructions)

### Version tracking

Each export records a SHA-256 hash of the file in a manifest (`.github/.sharppilot-exports.json`). When the extension is updated with new instruction content, the version checker compares hashes and notifies you if any exported files are outdated. You can **View Details** to see a side-by-side diff of your exported file versus the latest version, **Acknowledge** to update the manifest hashes, or **Dismiss** to ignore.

### Override detection

Once instruction files exist in `.github/instructions/` or `.github/copilot-instructions.md`, the extension detects them as overrides. Overridden instructions are marked with a $(file-symlink-directory) badge in the Toggle Instructions menu, signaling that a workspace-level version is present.

### Browse instructions

Preview any instruction file shipped with the extension — opens it read-only in the editor so you can review its content before exporting.

[Browse Instructions](command:sharppilot.browseInstructions)
