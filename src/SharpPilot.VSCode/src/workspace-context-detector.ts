import * as vscode from 'vscode';

export class WorkspaceContextDetector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;

    constructor() {
        const schedule = () => this.scheduleDetect();

        const existenceWatcher = vscode.workspace.createFileSystemWatcher(
            '**/*.{csproj,fsproj,sln,slnx,razor,html,cshtml,css,js,jsx,mjs,cjs,ts,tsx,mts,cts}',
        );

        this.disposables.push(
            existenceWatcher,
            existenceWatcher.onDidCreate(schedule),
            existenceWatcher.onDidDelete(schedule),
        );

        const contentWatcher = vscode.workspace.createFileSystemWatcher(
            '**/{*.csproj,*.fsproj,package.json}',
        );

        this.disposables.push(
            contentWatcher,
            contentWatcher.onDidCreate(schedule),
            contentWatcher.onDidChange(schedule),
            contentWatcher.onDidDelete(schedule),
        );
    }

    private scheduleDetect(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this.debounceTimer = setTimeout(() => {
            this.debounceTimer = undefined;
            this.detect();
        }, 500);
    }

    async detect(): Promise<void> {
        try {
            const setCtx = (key: string, value: boolean): Thenable<unknown> =>
                vscode.commands.executeCommand('setContext', key, value);

            const decoder = new TextDecoder();

            const [dotnetFiles, fsharpFiles, razorFiles, htmlFiles, cssFiles, jsFiles, tsFiles, unityFiles] = await Promise.all([
                vscode.workspace.findFiles('**/*.{csproj,fsproj,sln,slnx}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.fsproj', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.razor', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{html,cshtml}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.css', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{js,jsx,mjs,cjs}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{ts,tsx,mts,cts}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/ProjectSettings/ProjectSettings.asset', '**/node_modules/**', 1),
            ]);

            const hasDotnet = dotnetFiles.length > 0;
            const hasFsharp = fsharpFiles.length > 0;
            const hasBlazor = razorFiles.length > 0;
            const hasHtml = htmlFiles.length > 0 || hasBlazor;
            const hasCss = cssFiles.length > 0 || hasHtml;
            const hasJavaScript = jsFiles.length > 0;
            const hasTypeScript = tsFiles.length > 0;
            const hasUnity = unityFiles.length > 0;

            let hasReact = false;
            let hasAngular = false;
            let hasVue = false;
            let hasSvelte = false;
            const packageFiles = await vscode.workspace.findFiles('**/package.json', '**/node_modules/**', 50);
            for (const uri of packageFiles) {
                const bytes = await vscode.workspace.fs.readFile(uri);
                const content = decoder.decode(bytes);
                if (!hasReact && /"react"\s*:/.test(content)) {
                    hasReact = true;
                }
                if (!hasAngular && /"@angular\/core"\s*:/.test(content)) {
                    hasAngular = true;
                }
                if (!hasVue && /"vue"\s*:/.test(content)) {
                    hasVue = true;
                }
                if (!hasSvelte && /"svelte"\s*:/.test(content)) {
                    hasSvelte = true;
                }
                if (hasReact && hasAngular && hasVue && hasSvelte) {
                    break;
                }
            }

            let hasAspNetCore = false;
            let hasDapper = false;
            let hasEntityFrameworkCore = false;
            let hasMaui = false;
            let hasMongodb = false;
            let hasMysql = false;
            let hasOracle = false;
            let hasPostgres = false;
            let hasSqlite = false;
            let hasSqlServer = false;
            let hasXunit = false;
            let hasWpf = false;
            let hasWinForms = false;
            if (hasDotnet) {
                const projFiles = await vscode.workspace.findFiles('**/*.{csproj,fsproj}', '**/node_modules/**', 50);
                for (const uri of projFiles) {
                    const bytes = await vscode.workspace.fs.readFile(uri);
                    const content = decoder.decode(bytes);
                    if (!hasAspNetCore && /Sdk\s*=\s*["']Microsoft\.NET\.Sdk\.(Web|Razor)["']/i.test(content)) {
                        hasAspNetCore = true;
                    }
                    if (!hasDapper && /Dapper/i.test(content)) {
                        hasDapper = true;
                    }
                    if (!hasEntityFrameworkCore && /Microsoft\.EntityFrameworkCore/i.test(content)) {
                        hasEntityFrameworkCore = true;
                    }
                    if (!hasMaui && /<UseMaui>\s*true\s*<\/UseMaui>/i.test(content)) {
                        hasMaui = true;
                    }
                    if (!hasMongodb && /MongoDB\.Driver|MongoDB\.EntityFrameworkCore/i.test(content)) {
                        hasMongodb = true;
                    }
                    if (!hasXunit && /xunit/i.test(content)) {
                        hasXunit = true;
                    }
                    if (!hasWpf && /<UseWPF>\s*true\s*<\/UseWPF>/i.test(content)) {
                        hasWpf = true;
                    }
                    if (!hasWinForms && /<UseWindowsForms>\s*true\s*<\/UseWindowsForms>/i.test(content)) {
                        hasWinForms = true;
                    }
                    if (!hasMysql && /MySqlConnector|MySql\.Data|Pomelo\.EntityFrameworkCore\.MySql/i.test(content)) {
                        hasMysql = true;
                    }
                    if (!hasOracle && /Oracle\.ManagedDataAccess|Oracle\.EntityFrameworkCore/i.test(content)) {
                        hasOracle = true;
                    }
                    if (!hasPostgres && /Npgsql/i.test(content)) {
                        hasPostgres = true;
                    }
                    if (!hasSqlite && /Microsoft\.Data\.Sqlite|System\.Data\.SQLite|EntityFrameworkCore\.Sqlite/i.test(content)) {
                        hasSqlite = true;
                    }
                    if (!hasSqlServer && /Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|EntityFrameworkCore\.SqlServer|EntityFramework\.SqlServer/i.test(content)) {
                        hasSqlServer = true;
                    }
                    if (hasAspNetCore && hasDapper && hasEntityFrameworkCore && hasMaui && hasMongodb && hasMysql && hasOracle && hasPostgres && hasSqlite && hasSqlServer && hasXunit && hasWpf && hasWinForms) {
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
                setCtx('sharp-pilot.workspace.hasFsharp', hasFsharp),
                setCtx('sharp-pilot.workspace.hasAspNetCore', hasAspNetCore),
                setCtx('sharp-pilot.workspace.hasDapper', hasDapper),
                setCtx('sharp-pilot.workspace.hasEntityFrameworkCore', hasEntityFrameworkCore),
                setCtx('sharp-pilot.workspace.hasMaui', hasMaui),
                setCtx('sharp-pilot.workspace.hasBlazor', hasBlazor),
                setCtx('sharp-pilot.workspace.hasHtml', hasHtml),
                setCtx('sharp-pilot.workspace.hasCss', hasCss),
                setCtx('sharp-pilot.workspace.hasJavaScript', hasJavaScript),
                setCtx('sharp-pilot.workspace.hasTypeScript', hasTypeScript),
                setCtx('sharp-pilot.workspace.hasReact', hasReact),
                setCtx('sharp-pilot.workspace.hasAngular', hasAngular),
                setCtx('sharp-pilot.workspace.hasVue', hasVue),
                setCtx('sharp-pilot.workspace.hasSvelte', hasSvelte),
                setCtx('sharp-pilot.workspace.hasMysql', hasMysql),
                setCtx('sharp-pilot.workspace.hasMongodb', hasMongodb),
                setCtx('sharp-pilot.workspace.hasOracle', hasOracle),
                setCtx('sharp-pilot.workspace.hasPostgres', hasPostgres),
                setCtx('sharp-pilot.workspace.hasSqlite', hasSqlite),
                setCtx('sharp-pilot.workspace.hasSqlServer', hasSqlServer),
                setCtx('sharp-pilot.workspace.hasXunit', hasXunit),
                setCtx('sharp-pilot.workspace.hasWpf', hasWpf),
                setCtx('sharp-pilot.workspace.hasWinForms', hasWinForms),
                setCtx('sharp-pilot.workspace.hasUnity', hasUnity),
                setCtx('sharp-pilot.workspace.hasGit', hasGit),
            ]);
        } catch {
            // Workspace detection is best-effort; failures should not break the extension
        }
    }

    dispose(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this.disposables.forEach(d => d.dispose());
    }
}
