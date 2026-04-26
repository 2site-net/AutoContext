// ── Extension identifiers ────────────────────────────────────────────

export const EXTENSION_NAME = 'AutoContext';

export const commandIds = {
    AutoConfigure: 'autocontext.auto-configure',
    ShowNotDetected: 'autocontext.show-not-detected',
    HideNotDetected: 'autocontext.hide-not-detected',
    ToggleInstruction: 'autocontext.toggle-instruction',
    ResetInstructions: 'autocontext.reset-instructions',
    EnableInstruction: 'autocontext.enable-instruction',
    DisableInstruction: 'autocontext.disable-instruction',
    DeleteOverride: 'autocontext.delete-override',
    ShowOriginal: 'autocontext.show-original',
    ShowChangelog: 'autocontext.show-changelog',
    ShowWhatsNew: 'autocontext.show-whats-new',
    EnterExportMode: 'autocontext.enter-export-mode',
    ConfirmExport: 'autocontext.confirm-export',
    CancelExport: 'autocontext.cancel-export',
    StartMcpServer: 'autocontext.start-mcp-server',
    StopMcpServer: 'autocontext.stop-mcp-server',
    RestartMcpServer: 'autocontext.restart-mcp-server',
    ShowMcpServerOutput: 'autocontext.show-mcp-server-output',
} as const;

export const viewIds = {
    Instructions: 'autocontext.instructions-view',
    Tools: 'autocontext.mcp-tools-view',
} as const;

export const contextKeys = {
    ExportMode: 'autocontext.export-mode',
    HasWhatsNew: 'autocontext.has-whats-new',
} as const;

export const globalStateKeys = {
    LastSeenVersion: 'autocontext.lastSeenVersion',
} as const;

// ── Tree View Labels ─────────────────────────────────────────────────

export const treeViewLabels = {
    activeSuffix: 'active',
    activeTooltip: 'Active — included in Copilot context',
    disabled: 'disabled',
    disabledTooltip: 'Disabled — turned off in settings',
    enabledTooltip: 'Enabled — available to Copilot',
    tasksEnabledTooltip: 'tasks enabled',
    notDetected: 'not detected',
    notDetectedTooltip: 'Not detected — workspace lacks matching files',
    outdated: 'overridden (outdated)',
    outdatedTooltip: 'Overridden — the local file is outdated, a newer version is available',
    overridden: 'overridden',
    overriddenTooltip: 'Overridden — using a local file instead of AutoContext\'s version',
    contextKeyPrefix: 'Context Key:',
} as const;
