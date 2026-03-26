import * as vscode from 'vscode';
import { instructions, instructionByFileName } from './instructions-catalog.js';
import { overrideContextKey } from './toggle-context-keys.js';

export { overrideContextKey };

export class WorkspaceContextDetector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;
    private readonly _onDidChange = new vscode.EventEmitter<void>();
    private readonly _state = new Map<string, boolean>();
    private readonly _overriddenSettingIds = new Set<string>();

    readonly onDidChange = this._onDidChange.event;

    get(key: string): boolean {
        return this._state.get(key) ?? false;
    }

    getOverriddenSettingIds(): ReadonlySet<string> {
        return this._overriddenSettingIds;
    }

    constructor() {
        const schedule = () => this.scheduleDetect();

        const existenceWatcher = vscode.workspace.createFileSystemWatcher(
            '**/*.{csproj,fsproj,sln,slnx,razor,html,cshtml,css,js,jsx,mjs,cjs,ts,tsx,mts,cts,ps1,psm1,psd1,sh,bash,bat,cmd}',
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

        const overrideWatcher = vscode.workspace.createFileSystemWatcher(
            '**/.github/{copilot-instructions.md,instructions/*.instructions.md}',
        );

        this.disposables.push(
            overrideWatcher,
            overrideWatcher.onDidCreate(schedule),
            overrideWatcher.onDidDelete(schedule),
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
            const setContext = (key: string, value: boolean): Thenable<unknown> =>
                vscode.commands.executeCommand('setContext', key, value);

            const decoder = new TextDecoder();

            const [dotnetFiles, fsharpFiles, vbnetFiles, razorFiles, htmlFiles, cssFiles, jsFiles, tsFiles, unityFiles, dockerFiles, psFiles, shFiles, batFiles] = await Promise.all([
                vscode.workspace.findFiles('**/*.{csproj,fsproj,vbproj,sln,slnx}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.fsproj', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.vbproj', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.razor', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{html,cshtml}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.css', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{js,jsx,mjs,cjs}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{ts,tsx,mts,cts}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/ProjectSettings/ProjectSettings.asset', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/Dockerfile*', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{ps1,psm1,psd1}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{sh,bash}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{bat,cmd}', '**/node_modules/**', 1),
            ]);

            const hasDotnet = dotnetFiles.length > 0;
            const hasFsharp = fsharpFiles.length > 0;
            const hasVbnet = vbnetFiles.length > 0;
            const hasBlazor = razorFiles.length > 0;
            const hasHtml = htmlFiles.length > 0 || hasBlazor;
            const hasCss = cssFiles.length > 0 || hasHtml;
            const hasJavaScript = jsFiles.length > 0;
            const hasTypeScript = tsFiles.length > 0;
            const hasUnity = unityFiles.length > 0;
            const hasDocker = dockerFiles.length > 0;
            const hasPowerShell = psFiles.length > 0;
            const hasBash = shFiles.length > 0;
            const hasBatch = batFiles.length > 0;

            let hasReact = false;
            let hasAngular = false;
            let hasVue = false;
            let hasSvelte = false;
            let hasVitest = false;
            let hasJest = false;
            let hasJasmine = false;
            let hasMocha = false;
            let hasPlaywright = false;
            let hasCypress = false;
            let hasNextJs = false;
            let hasGraphql = false;
            const packageFiles = await vscode.workspace.findFiles('**/package.json', '**/node_modules/**', 50);
            const hasNodeJs = packageFiles.length > 0;

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
                if (!hasVitest && /"vitest"\s*:/.test(content)) {
                    hasVitest = true;
                }
                if (!hasJest && /"jest"\s*:/.test(content)) {
                    hasJest = true;
                }
                if (!hasJasmine && /"jasmine"\s*:/.test(content)) {
                    hasJasmine = true;
                }
                if (!hasMocha && /"mocha"\s*:/.test(content)) {
                    hasMocha = true;
                }
                if (!hasPlaywright && /"@playwright\/test"\s*:/.test(content)) {
                    hasPlaywright = true;
                }
                if (!hasCypress && /"cypress"\s*:/.test(content)) {
                    hasCypress = true;
                }
                if (!hasNextJs && /"next"\s*:/.test(content)) {
                    hasNextJs = true;
                }
                if (!hasGraphql && /"graphql"\s*:|"@apollo\/|"graphql-request"\s*:|"urql"\s*:|"HotChocolate/i.test(content)) {
                    hasGraphql = true;
                }
                if (hasReact && hasAngular && hasVue && hasSvelte && hasVitest && hasJest && hasJasmine && hasMocha && hasPlaywright && hasCypress && hasNextJs && hasGraphql) {
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
            let hasMstest = false;
            let hasNunit = false;
            let hasWpf = false;
            let hasWinForms = false;
            let hasGrpc = false;
            let hasMediatR = false;
            let hasRedis = false;
            let hasSignalR = false;

            if (hasDotnet) {
                const projFiles = await vscode.workspace.findFiles('**/*.{csproj,fsproj,vbproj}', '**/node_modules/**', 50);

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
                    if (!hasMstest && /MSTest|Microsoft\.VisualStudio\.TestPlatform/i.test(content)) {
                        hasMstest = true;
                    }
                    if (!hasNunit && /NUnit/i.test(content)) {
                        hasNunit = true;
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
                    if (!hasGrpc && /Grpc\.|Google\.Protobuf/i.test(content)) {
                        hasGrpc = true;
                    }
                    if (!hasMediatR && /MediatR|Mediator\.Abstractions/i.test(content)) {
                        hasMediatR = true;
                    }
                    if (!hasRedis && /StackExchange\.Redis|Microsoft\.Extensions\.Caching\.StackExchangeRedis/i.test(content)) {
                        hasRedis = true;
                    }
                    if (!hasSignalR && /Microsoft\.AspNetCore\.SignalR/i.test(content)) {
                        hasSignalR = true;
                    }
                    if (!hasGraphql && /HotChocolate|GraphQL\.Server/i.test(content)) {
                        hasGraphql = true;
                    }
                    if (hasAspNetCore && hasDapper && hasEntityFrameworkCore && hasMaui && hasMongodb && hasMysql && hasOracle && hasPostgres && hasSqlite && hasSqlServer && hasXunit && hasMstest && hasNunit && hasWpf && hasWinForms && hasGrpc && hasMediatR && hasRedis && hasSignalR && hasGraphql) {
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

            const overrideFiles = await vscode.workspace.findFiles(
                '.github/instructions/*.instructions.md', undefined, 50,
            );

            const overriddenFileNames = new Set<string>();

            for (const uri of overrideFiles) {
                const segments = uri.path.split('/');
                const matchName = segments[segments.length - 1];

                if (instructionByFileName(matchName)) {
                    overriddenFileNames.add(matchName);
                }
            }

            await Promise.all([
                setContext('sharppilot.workspace.hasDotnet', hasDotnet),
                setContext('sharppilot.workspace.hasFsharp', hasFsharp),
                setContext('sharppilot.workspace.hasVbnet', hasVbnet),
                setContext('sharppilot.workspace.hasAspNetCore', hasAspNetCore),
                setContext('sharppilot.workspace.hasDapper', hasDapper),
                setContext('sharppilot.workspace.hasEntityFrameworkCore', hasEntityFrameworkCore),
                setContext('sharppilot.workspace.hasMaui', hasMaui),
                setContext('sharppilot.workspace.hasBlazor', hasBlazor),
                setContext('sharppilot.workspace.hasHtml', hasHtml),
                setContext('sharppilot.workspace.hasCss', hasCss),
                setContext('sharppilot.workspace.hasJavaScript', hasJavaScript),
                setContext('sharppilot.workspace.hasTypeScript', hasTypeScript),
                setContext('sharppilot.workspace.hasReact', hasReact),
                setContext('sharppilot.workspace.hasAngular', hasAngular),
                setContext('sharppilot.workspace.hasVue', hasVue),
                setContext('sharppilot.workspace.hasSvelte', hasSvelte),
                setContext('sharppilot.workspace.hasMysql', hasMysql),
                setContext('sharppilot.workspace.hasMongodb', hasMongodb),
                setContext('sharppilot.workspace.hasOracle', hasOracle),
                setContext('sharppilot.workspace.hasPostgres', hasPostgres),
                setContext('sharppilot.workspace.hasSqlite', hasSqlite),
                setContext('sharppilot.workspace.hasSqlServer', hasSqlServer),
                setContext('sharppilot.workspace.hasXunit', hasXunit),
                setContext('sharppilot.workspace.hasMstest', hasMstest),
                setContext('sharppilot.workspace.hasNunit', hasNunit),
                setContext('sharppilot.workspace.hasWpf', hasWpf),
                setContext('sharppilot.workspace.hasWinForms', hasWinForms),
                setContext('sharppilot.workspace.hasGrpc', hasGrpc),
                setContext('sharppilot.workspace.hasMediatR', hasMediatR),
                setContext('sharppilot.workspace.hasRedis', hasRedis),
                setContext('sharppilot.workspace.hasSignalR', hasSignalR),
                setContext('sharppilot.workspace.hasUnity', hasUnity),
                setContext('sharppilot.workspace.hasDocker', hasDocker),
                setContext('sharppilot.workspace.hasGraphql', hasGraphql),
                setContext('sharppilot.workspace.hasNextJs', hasNextJs),
                setContext('sharppilot.workspace.hasNodeJs', hasNodeJs),
                setContext('sharppilot.workspace.hasPowerShell', hasPowerShell),
                setContext('sharppilot.workspace.hasBash', hasBash),
                setContext('sharppilot.workspace.hasBatch', hasBatch),
                setContext('sharppilot.workspace.hasVitest', hasVitest),
                setContext('sharppilot.workspace.hasJest', hasJest),
                setContext('sharppilot.workspace.hasJasmine', hasJasmine),
                setContext('sharppilot.workspace.hasMocha', hasMocha),
                setContext('sharppilot.workspace.hasPlaywright', hasPlaywright),
                setContext('sharppilot.workspace.hasCypress', hasCypress),
                setContext('sharppilot.workspace.hasWebTesting', hasVitest || hasJest || hasJasmine || hasMocha || hasPlaywright || hasCypress),
                setContext('sharppilot.workspace.hasGit', hasGit),
                ...instructions.map(i =>
                    setContext(overrideContextKey(i.settingId), overriddenFileNames.has(i.fileName)),
                ),
            ]);

            const serverChanged = this._state.get('hasDotnet') !== hasDotnet
                || this._state.get('hasGit') !== hasGit;

            const contextState: Record<string, boolean> = {
                hasDotnet, hasFsharp, hasVbnet, hasAspNetCore, hasDapper, hasEntityFrameworkCore,
                hasMaui, hasBlazor, hasHtml, hasCss, hasJavaScript, hasTypeScript,
                hasReact, hasAngular, hasVue, hasSvelte, hasMysql, hasMongodb,
                hasOracle, hasPostgres, hasSqlite, hasSqlServer, hasXunit, hasMstest,
                hasNunit, hasWpf, hasWinForms, hasGrpc, hasMediatR, hasRedis, hasSignalR,
                hasUnity, hasDocker, hasGraphql, hasNextJs, hasNodeJs, hasPowerShell, hasBash, hasBatch,
                hasVitest, hasJest, hasJasmine, hasMocha, hasPlaywright, hasCypress,
                hasWebTesting: hasVitest || hasJest || hasJasmine || hasMocha || hasPlaywright || hasCypress,
                hasGit,
            };

            for (const [k, v] of Object.entries(contextState)) {
                this._state.set(k, v);
            }

            this._overriddenSettingIds.clear();
            for (const i of instructions) {
                if (overriddenFileNames.has(i.fileName)) {
                    this._overriddenSettingIds.add(i.settingId);
                }
            }

            if (serverChanged) {
                this._onDidChange.fire();
            }
        } catch {
            // Workspace detection is best-effort; failures should not break the extension
        }
    }

    dispose(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this._onDidChange.dispose();
        this.disposables.forEach(d => d.dispose());
    }
}
