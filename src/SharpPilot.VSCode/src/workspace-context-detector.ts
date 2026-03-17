import * as vscode from 'vscode';

export class WorkspaceContextDetector implements vscode.Disposable {
    private readonly watcher: vscode.FileSystemWatcher;

    constructor() {
        this.watcher = vscode.workspace.createFileSystemWatcher(
            '**/*.{csproj,fsproj,sln,slnx,razor,html,cshtml,css,js,mjs,cjs}',
        );
        this.watcher.onDidCreate(() => this.detect());
        this.watcher.onDidDelete(() => this.detect());
    }

    async detect(): Promise<void> {
        try {
            const setCtx = (key: string, value: boolean): Thenable<unknown> =>
                vscode.commands.executeCommand('setContext', key, value);

            const [dotnetFiles, razorFiles, htmlFiles, cssFiles, jsFiles] = await Promise.all([
                vscode.workspace.findFiles('**/*.{csproj,fsproj,sln,slnx}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.razor', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{html,cshtml}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.css', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{js,mjs,cjs}', '**/node_modules/**', 1),
            ]);

            const hasDotnet = dotnetFiles.length > 0;
            const hasBlazor = razorFiles.length > 0;
            const hasHtml = htmlFiles.length > 0 || hasBlazor;
            const hasCss = cssFiles.length > 0 || hasHtml;
            const hasJavaScript = jsFiles.length > 0;

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
                setCtx('sharp-pilot.workspace.hasDotnet', hasDotnet),
                setCtx('sharp-pilot.workspace.hasBlazor', hasBlazor),
                setCtx('sharp-pilot.workspace.hasHtml', hasHtml),
                setCtx('sharp-pilot.workspace.hasCss', hasCss),
                setCtx('sharp-pilot.workspace.hasJavaScript', hasJavaScript),
                setCtx('sharp-pilot.workspace.hasXunit', hasXunit),
                setCtx('sharp-pilot.workspace.hasGit', hasGit),
            ]);
        } catch {
            // Workspace detection is best-effort; failures should not break the extension
        }
    }

    dispose(): void {
        this.watcher.dispose();
    }
}
