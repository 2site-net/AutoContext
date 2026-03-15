import * as vscode from 'vscode';

export class WorkspaceContextDetector implements vscode.Disposable {
    private readonly watcher: vscode.FileSystemWatcher;

    constructor() {
        this.watcher = vscode.workspace.createFileSystemWatcher(
            '**/*.{csproj,fsproj,sln,slnx,razor}',
        );
        this.watcher.onDidCreate(() => this.detect());
        this.watcher.onDidDelete(() => this.detect());
    }

    async detect(): Promise<void> {
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

    dispose(): void {
        this.watcher.dispose();
    }
}
