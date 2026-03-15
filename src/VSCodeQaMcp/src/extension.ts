import * as vscode from 'vscode';
import { join } from 'node:path';
import { writeFileSync, mkdirSync } from 'node:fs';

interface ServerEntry {
    label: string;
    scope: string;
}

const servers: readonly ServerEntry[] = [
    { label: 'DotNet QA MCP', scope: 'dotnet' },
    { label: 'Git QA MCP', scope: 'git' },
];

interface InstructionEntry {
    settingId: string;
    label: string;
    category: string;
}

const instructions: readonly InstructionEntry[] = [
    { settingId: 'qa-mcp.instructions.copilot', label: 'Copilot Rules', category: 'General' },
    { settingId: 'qa-mcp.instructions.dotnet.asyncAwait', label: 'Async/Await', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.blazor', label: 'Blazor', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.codeQuality', label: 'Code Quality', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.codeStyle', label: 'Code Style', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.codingStandards', label: 'Coding Standards', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.debugging', label: 'Debugging', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.designPrinciples', label: 'Design Principles', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.nuget', label: 'NuGet', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.performanceMemory', label: 'Performance & Memory', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.testing', label: 'Testing', category: '.NET' },
    { settingId: 'qa-mcp.instructions.dotnet.xunit', label: 'xUnit', category: '.NET' },
    { settingId: 'qa-mcp.instructions.git.commitFormat', label: 'Commit Format', category: 'Git' },
];

interface ToolEntry {
    settingId: string;
    toolName: string;
    label: string;
    category: string;
}

const tools: readonly ToolEntry[] = [
    { settingId: 'qa-mcp.tools.check_code_style', toolName: 'check_code_style', label: 'Code Style', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_member_ordering', toolName: 'check_member_ordering', label: 'Member Ordering', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_naming_conventions', toolName: 'check_naming_conventions', label: 'Naming Conventions', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_async_patterns', toolName: 'check_async_patterns', label: 'Async Patterns', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_nullable_context', toolName: 'check_nullable_context', label: 'Nullable Context', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_project_structure', toolName: 'check_project_structure', label: 'Project Structure', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_nuget_hygiene', toolName: 'check_nuget_hygiene', label: 'NuGet Hygiene', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.check_tests_style', toolName: 'check_tests_style', label: 'Test Style', category: '.NET Tool' },
    { settingId: 'qa-mcp.tools.validate_commit_format', toolName: 'validate_commit_format', label: 'Commit Format', category: 'Git Tool' },
    { settingId: 'qa-mcp.tools.validate_commit_content', toolName: 'validate_commit_content', label: 'Commit Content', category: 'Git Tool' },
];

function getEnabledCount(): number {
    const config = vscode.workspace.getConfiguration();
    const enabledInstructions = instructions.filter(i => config.get<boolean>(i.settingId, true)).length;
    const enabledTools = tools.filter(t => config.get<boolean>(t.settingId, true)).length;
    return enabledInstructions + enabledTools;
}

function getTotalCount(): number {
    return instructions.length + tools.length;
}

function updateStatusBar(item: vscode.StatusBarItem): void {
    const enabled = getEnabledCount();
    const total = getTotalCount();
    item.text = `$(checklist) QA-MCP: ${enabled}/${total}`;
    item.tooltip = `${enabled} of ${total} items enabled — click to toggle`;
}

async function detectWorkspaceContext(): Promise<void> {
    try {
        const setCtx = (key: string, value: boolean): Thenable<unknown> =>
            vscode.commands.executeCommand('setContext', key, value);

        const [dotnetFiles, razorFiles] = await Promise.all([
            vscode.workspace.findFiles('**/*.{csproj,fsproj,sln,slnx}', '**/node_modules/**', 1),
            vscode.workspace.findFiles('**/*.razor', '**/node_modules/**', 1),
        ]);

        const hasDotnet = dotnetFiles.length > 0;
        const hasBlazor = razorFiles.length > 0;

        let hasXunit = false;
        if (hasDotnet) {
            const projFiles = await vscode.workspace.findFiles('**/*.csproj', '**/node_modules/**', 50);
            for (const uri of projFiles) {
                const bytes = await vscode.workspace.fs.readFile(uri);
                const content = new TextDecoder().decode(bytes);
                if (/xunit/i.test(content)) {
                    hasXunit = true;
                    break;
                }
            }
        }

        let hasGit = false;
        for (const folder of vscode.workspace.workspaceFolders ?? []) {
            try {
                await vscode.workspace.fs.stat(vscode.Uri.joinPath(folder.uri, '.git'));
                hasGit = true;
                break;
            } catch {
                // .git directory not found in this workspace folder
            }
        }

        await Promise.all([
            setCtx('qa-mcp.workspace.hasDotnet', hasDotnet),
            setCtx('qa-mcp.workspace.hasBlazor', hasBlazor),
            setCtx('qa-mcp.workspace.hasXunit', hasXunit),
            setCtx('qa-mcp.workspace.hasGit', hasGit),
        ]);
    } catch {
        // Workspace detection is best-effort; failures should not break the extension
    }
}

async function toggleInstructions(): Promise<void> {
    const config = vscode.workspace.getConfiguration();

    const allEntries = [
        ...instructions.map(entry => ({
            label: entry.label,
            description: entry.category,
            picked: config.get<boolean>(entry.settingId, true),
            settingId: entry.settingId,
        })),
        ...tools.map(entry => ({
            label: entry.label,
            description: entry.category,
            picked: config.get<boolean>(entry.settingId, true),
            settingId: entry.settingId,
        })),
    ];

    const selected = await vscode.window.showQuickPick(allEntries, {
        canPickMany: true,
        title: 'QA-MCP: Toggle Instructions & Tools',
        placeHolder: 'Select instructions and tools to enable',
    });

    if (!selected) {
        return;
    }

    const selectedIds = new Set(selected.map(s => s.settingId));

    for (const entry of allEntries) {
        const shouldBeEnabled = selectedIds.has(entry.settingId);
        const isEnabled = config.get<boolean>(entry.settingId, true);

        if (shouldBeEnabled !== isEnabled) {
            await config.update(entry.settingId, shouldBeEnabled, vscode.ConfigurationTarget.Global);
        }
    }
}

export function activate(context: vscode.ExtensionContext): void {
    const serversPath = join(context.extensionPath, 'servers');
    const ext = process.platform === 'win32' ? '.exe' : '';
    const version = context.extension.packageJSON.version as string;
    const didChangeEmitter = new vscode.EventEmitter<void>();

    const ToolsStatusWriter = {
        write(): void {
            const config = vscode.workspace.getConfiguration();
            const status: Record<string, boolean> = {};

            for (const tool of tools) {
                status[tool.toolName] = config.get<boolean>(tool.settingId, true);
            }

            const dir = join(serversPath, 'QaMcp');
            mkdirSync(dir, { recursive: true });
            writeFileSync(join(dir, 'tools-status.json'), JSON.stringify(status, null, 2));
        },
    };

    // Write initial tools-status.json
    ToolsStatusWriter.write();

    // Status bar — shows enabled instruction count, click to toggle
    const statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBar.name = 'QA-MCP Instructions';
    statusBar.command = 'qa-mcp.toggleInstructions';
    updateStatusBar(statusBar);
    statusBar.show();

    // Set workspace context keys for smart chatInstruction activation
    detectWorkspaceContext();

    // Re-detect when .NET or Blazor project files are added/removed
    const fileWatcher = vscode.workspace.createFileSystemWatcher(
        '**/*.{csproj,fsproj,sln,slnx,razor}',
    );
    fileWatcher.onDidCreate(() => detectWorkspaceContext());
    fileWatcher.onDidDelete(() => detectWorkspaceContext());

    context.subscriptions.push(
        didChangeEmitter,
        statusBar,
        fileWatcher,
        vscode.commands.registerCommand('qa-mcp.toggleInstructions', toggleInstructions),
        vscode.workspace.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration('qa-mcp.instructions') || e.affectsConfiguration('qa-mcp.tools')) {
                updateStatusBar(statusBar);
            }

            if (e.affectsConfiguration('qa-mcp.tools')) {
                ToolsStatusWriter.write();
            }
        }),
        vscode.lm.registerMcpServerDefinitionProvider('qaMcpProvider', {
            onDidChangeMcpServerDefinitions: didChangeEmitter.event,
            provideMcpServerDefinitions: async () =>
                servers.map(
                    s =>
                        new vscode.McpStdioServerDefinition(
                            s.label,
                            join(serversPath, 'QaMcp', `QaMcp${ext}`),
                            ['--scope', s.scope],
                            undefined,
                            version,
                        ),
                ),
            resolveMcpServerDefinition: async (server) => server,
        }),
    );
}

export function deactivate(): void {}
