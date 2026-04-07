import * as vscode from 'vscode';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { ContextKeys } from './context-keys.js';
import type { McpServersCatalog } from './mcp-servers-catalog.js';

export class WorkspaceContextDetector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;
    private readonly _onDidChange = new vscode.EventEmitter<void>();
    private readonly _onDidDetect = new vscode.EventEmitter<void>();
    private readonly _state = new Map<string, boolean>();
    private readonly _overriddenSettingIds = new Set<string>();

    readonly onDidChange = this._onDidChange.event;
    readonly onDidDetect = this._onDidDetect.event;

    get(key: string): boolean {
        return this._state.get(key) ?? false;
    }

    getOverriddenSettingIds(): ReadonlySet<string> {
        return this._overriddenSettingIds;
    }

    constructor(
        private readonly instructionsCatalog: InstructionsCatalog,
        private readonly serversCatalog: McpServersCatalog,
    ) {
        const schedule = () => this.scheduleDetect();

        const existenceWatcher = vscode.workspace.createFileSystemWatcher(
            '**/*.{csproj,fsproj,sln,slnx,razor,xaml,aspx,html,cshtml,css,js,jsx,mjs,cjs,ts,tsx,mts,cts,ps1,psm1,psd1,sh,bash,bat,cmd,java,kt,kts,scala,sc,groovy,gvy,c,cpp,cxx,cc,rs,go,py,lua,php}',
        );

        this.disposables.push(
            existenceWatcher,
            existenceWatcher.onDidCreate(schedule),
            existenceWatcher.onDidDelete(schedule),
        );

        const contentWatcher = vscode.workspace.createFileSystemWatcher(
            '**/{*.csproj,*.fsproj,package.json,pom.xml,build.gradle,build.sbt,Cargo.toml,go.mod,pyproject.toml,composer.json}',
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

            const [dotnetFiles, csharpFiles, fsharpFiles, vbnetFiles, razorFiles, xamlFiles, aspxFiles, cshtmlFiles, htmlFiles, cssFiles, jsFiles, tsFiles, unityFiles, dockerFiles, psFiles, shFiles, batFiles, yamlFiles, javaFiles, javaProjectFiles, ktFiles, scalaFiles, scalaProjectFiles, groovyFiles, cFiles, cppFiles, rustFiles, rustProjectFiles, goFiles, goProjectFiles, pythonFiles, pythonProjectFiles, luaFiles, phpFiles, phpProjectFiles] = await Promise.all([
                vscode.workspace.findFiles('**/*.{csproj,fsproj,vbproj,sln,slnx}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.csproj', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.fsproj', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.vbproj', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.razor', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.xaml', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{aspx,ascx,master}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.cshtml', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{html,cshtml}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.css', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{js,jsx,mjs,cjs}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{ts,tsx,mts,cts}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/ProjectSettings/ProjectSettings.asset', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/Dockerfile*', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{ps1,psm1,psd1}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{sh,bash}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{bat,cmd}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{yml,yaml}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.java', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/{pom.xml,build.gradle}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{kt,kts}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{scala,sc}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/build.sbt', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{groovy,gvy}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.c', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.{cpp,cxx,cc}', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.rs', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/Cargo.toml', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.go', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/go.mod', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.py', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/pyproject.toml', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.lua', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/*.php', '**/node_modules/**', 1),
                vscode.workspace.findFiles('**/composer.json', '**/node_modules/**', 1),
            ]);

            let hasJavaScript = jsFiles.length > 0;
            let hasTypeScript = tsFiles.length > 0;
            let hasDotNet = dotnetFiles.length > 0;
            let hasCSharp = csharpFiles.length > 0;

            const hasFSharp = fsharpFiles.length > 0;
            const hasVbNet = vbnetFiles.length > 0;
            const hasBlazor = razorFiles.length > 0;
            let hasXaml = xamlFiles.length > 0;
            const hasWebForms = aspxFiles.length > 0;
            let hasRazor = razorFiles.length > 0 || cshtmlFiles.length > 0;
            const hasHtml = htmlFiles.length > 0 || hasBlazor;
            const hasCss = cssFiles.length > 0 || hasHtml;
            const hasUnity = unityFiles.length > 0;
            const hasDocker = dockerFiles.length > 0;
            const hasYaml = yamlFiles.length > 0;
            const hasPowerShell = psFiles.length > 0;
            const hasBash = shFiles.length > 0;
            const hasBatch = batFiles.length > 0;
            const hasJava = javaFiles.length > 0 || javaProjectFiles.length > 0;
            const hasKotlin = ktFiles.length > 0;
            const hasScala = scalaFiles.length > 0 || scalaProjectFiles.length > 0;
            const hasGroovy = groovyFiles.length > 0;
            const hasJvm = hasJava || hasKotlin || hasScala || hasGroovy;
            const hasC = cFiles.length > 0;
            const hasCpp = cppFiles.length > 0;
            const hasRust = rustFiles.length > 0 || rustProjectFiles.length > 0;
            const hasGo = goFiles.length > 0 || goProjectFiles.length > 0;
            const hasNative = hasC || hasCpp || hasRust || hasGo;
            const hasPython = pythonFiles.length > 0 || pythonProjectFiles.length > 0;
            const hasLua = luaFiles.length > 0;
            const hasPhp = phpFiles.length > 0 || phpProjectFiles.length > 0;

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
            let hasNodeJs = packageFiles.length > 0;

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
            let hasMongoDb = false;
            let hasMySql = false;
            let hasOracle = false;
            let hasPostgres = false;
            let hasSqlite = false;
            let hasSqlServer = false;
            let hasXunit = false;
            let hasMsTest = false;
            let hasNUnit = false;
            let hasWpf = false;
            let hasWinForms = false;
            let hasGrpc = false;
            let hasMediatR = false;
            let hasRedis = false;
            let hasSignalR = false;

            if (hasDotNet) {
                const projFiles = await vscode.workspace.findFiles('**/*.{csproj,fsproj,vbproj}', '**/node_modules/**', 50);

                for (const uri of projFiles) {
                    const bytes = await vscode.workspace.fs.readFile(uri);
                    const content = decoder.decode(bytes);

                    if (!hasAspNetCore && /Sdk\s*=\s*["']Microsoft\.NET\.Sdk\.(Web|Razor|BlazorWebAssembly)["']/i.test(content)) {
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
                    if (!hasMongoDb && /MongoDB\.Driver|MongoDB\.EntityFrameworkCore/i.test(content)) {
                        hasMongoDb = true;
                    }
                    if (!hasXunit && /xunit/i.test(content)) {
                        hasXunit = true;
                    }
                    if (!hasMsTest && /MSTest|Microsoft\.VisualStudio\.TestPlatform/i.test(content)) {
                        hasMsTest = true;
                    }
                    if (!hasNUnit && /NUnit/i.test(content)) {
                        hasNUnit = true;
                    }
                    if (!hasWpf && /<UseWPF>\s*true\s*<\/UseWPF>/i.test(content)) {
                        hasWpf = true;
                    }
                    if (!hasWinForms && /<UseWindowsForms>\s*true\s*<\/UseWindowsForms>/i.test(content)) {
                        hasWinForms = true;
                    }
                    if (!hasMySql && /MySqlConnector|MySql\.Data|Pomelo\.EntityFrameworkCore\.MySql/i.test(content)) {
                        hasMySql = true;
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
                    if (hasAspNetCore && hasDapper && hasEntityFrameworkCore && hasMaui && hasMongoDb && hasMySql && hasOracle && hasPostgres && hasSqlite && hasSqlServer && hasXunit && hasMsTest && hasNUnit && hasWpf && hasWinForms && hasGrpc && hasMediatR && hasRedis && hasSignalR && hasGraphql) {
                        break;
                    }
                }
            }

            // --- Flag implications ---
            // Sub-flags imply their parent so that the MCP server and
            // cross-cutting instructions activate whenever any child
            // technology is detected. Order: leaves → roots.

            // Web: framework → language/runtime
            if (hasNextJs) {
                hasReact = true;
            }

            if (hasAngular) {
                hasTypeScript = true;
            }

            if (hasTypeScript) {
                hasJavaScript = true;
            }

            if (hasReact || hasAngular || hasVue || hasSvelte
                || hasVitest || hasJest || hasJasmine || hasMocha || hasPlaywright || hasCypress) {
                hasNodeJs = true;
            }

            if (hasNodeJs) {
                hasJavaScript = true;
            }

            // .NET: framework → platform
            if (hasBlazor || hasSignalR) {
                hasAspNetCore = true;
            }

            if (hasAspNetCore) {
                hasRazor = true;
            }

            if (hasBlazor || hasUnity) {
                hasCSharp = true;
            }

            if (hasWpf || hasMaui) {
                hasXaml = true;
            }

            if (hasAspNetCore || hasDapper || hasEntityFrameworkCore
                || hasMaui || hasWpf || hasWinForms || hasWebForms
                || hasGrpc || hasMediatR || hasRedis || hasSignalR
                || hasXunit || hasMsTest || hasNUnit
                || hasMongoDb || hasMySql || hasOracle || hasPostgres || hasSqlite || hasSqlServer
                || hasUnity) {
                hasDotNet = true;
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

                if (this.instructionsCatalog.findByFileName(matchName)) {
                    overriddenFileNames.add(matchName);
                }
            }

            await Promise.all([
                setContext('sharppilot.workspace.hasDotNet', hasDotNet),
                setContext('sharppilot.workspace.hasCSharp', hasCSharp),
                setContext('sharppilot.workspace.hasFSharp', hasFSharp),
                setContext('sharppilot.workspace.hasVbNet', hasVbNet),
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
                setContext('sharppilot.workspace.hasMySql', hasMySql),
                setContext('sharppilot.workspace.hasMongoDb', hasMongoDb),
                setContext('sharppilot.workspace.hasOracle', hasOracle),
                setContext('sharppilot.workspace.hasPostgres', hasPostgres),
                setContext('sharppilot.workspace.hasSqlite', hasSqlite),
                setContext('sharppilot.workspace.hasSqlServer', hasSqlServer),
                setContext('sharppilot.workspace.hasXunit', hasXunit),
                setContext('sharppilot.workspace.hasMsTest', hasMsTest),
                setContext('sharppilot.workspace.hasNUnit', hasNUnit),
                setContext('sharppilot.workspace.hasWpf', hasWpf),
                setContext('sharppilot.workspace.hasWinForms', hasWinForms),
                setContext('sharppilot.workspace.hasGrpc', hasGrpc),
                setContext('sharppilot.workspace.hasMediatR', hasMediatR),
                setContext('sharppilot.workspace.hasRedis', hasRedis),
                setContext('sharppilot.workspace.hasSignalR', hasSignalR),
                setContext('sharppilot.workspace.hasUnity', hasUnity),
                setContext('sharppilot.workspace.hasDocker', hasDocker),
                setContext('sharppilot.workspace.hasYaml', hasYaml),
                setContext('sharppilot.workspace.hasGraphql', hasGraphql),
                setContext('sharppilot.workspace.hasNextJs', hasNextJs),
                setContext('sharppilot.workspace.hasNodeJs', hasNodeJs),
                setContext('sharppilot.workspace.hasPowerShell', hasPowerShell),
                setContext('sharppilot.workspace.hasBash', hasBash),
                setContext('sharppilot.workspace.hasBatch', hasBatch),
                setContext('sharppilot.workspace.hasPython', hasPython),
                setContext('sharppilot.workspace.hasLua', hasLua),
                setContext('sharppilot.workspace.hasPhp', hasPhp),
                setContext('sharppilot.workspace.hasJava', hasJava),
                setContext('sharppilot.workspace.hasKotlin', hasKotlin),
                setContext('sharppilot.workspace.hasScala', hasScala),
                setContext('sharppilot.workspace.hasGroovy', hasGroovy),
                setContext('sharppilot.workspace.hasJvm', hasJvm),
                setContext('sharppilot.workspace.hasC', hasC),
                setContext('sharppilot.workspace.hasCpp', hasCpp),
                setContext('sharppilot.workspace.hasRust', hasRust),
                setContext('sharppilot.workspace.hasGo', hasGo),
                setContext('sharppilot.workspace.hasNative', hasNative),
                setContext('sharppilot.workspace.hasVitest', hasVitest),
                setContext('sharppilot.workspace.hasJest', hasJest),
                setContext('sharppilot.workspace.hasJasmine', hasJasmine),
                setContext('sharppilot.workspace.hasMocha', hasMocha),
                setContext('sharppilot.workspace.hasPlaywright', hasPlaywright),
                setContext('sharppilot.workspace.hasCypress', hasCypress),
                setContext('sharppilot.workspace.hasXaml', hasXaml),
                setContext('sharppilot.workspace.hasRazor', hasRazor),
                setContext('sharppilot.workspace.hasWebForms', hasWebForms),
                setContext('sharppilot.workspace.hasDotNetTesting', hasXunit || hasMsTest || hasNUnit),
                setContext('sharppilot.workspace.hasWebTesting', hasVitest || hasJest || hasJasmine || hasMocha || hasPlaywright || hasCypress),
                setContext('sharppilot.workspace.hasGit', hasGit),
                ...this.instructionsCatalog.all.map(i =>
                    setContext(ContextKeys.overrideKey(i.settingId), overriddenFileNames.has(i.fileName)),
                ),
            ]);

            const contextState: Record<string, boolean> = {
                hasDotNet, hasCSharp, hasFSharp, hasVbNet, hasAspNetCore, hasDapper, hasEntityFrameworkCore,
                hasMaui, hasBlazor, hasHtml, hasCss, hasJavaScript, hasTypeScript,
                hasReact, hasAngular, hasVue, hasSvelte, hasMySql, hasMongoDb,
                hasOracle, hasPostgres, hasSqlite, hasSqlServer, hasXunit, hasMsTest,
                hasNUnit, hasWpf, hasWinForms, hasGrpc, hasMediatR, hasRedis, hasSignalR,
                hasUnity, hasDocker, hasYaml, hasGraphql, hasNextJs, hasNodeJs, hasPowerShell, hasBash, hasBatch, hasPython, hasLua, hasPhp,
                hasVitest, hasJest, hasJasmine, hasMocha, hasPlaywright, hasCypress,
                hasJava,
                hasKotlin,
                hasScala,
                hasGroovy,
                hasJvm,
                hasC,
                hasCpp,
                hasRust,
                hasGo,
                hasNative,
                hasXaml,
                hasRazor,
                hasWebForms,
                hasDotNetTesting: hasXunit || hasMsTest || hasNUnit,
                hasWebTesting: hasVitest || hasJest || hasJasmine || hasMocha || hasPlaywright || hasCypress,
                hasGit,
            };

            const serverChanged = this.serversCatalog.all.some(s =>
                s.contextKey !== undefined && this._state.get(s.contextKey) !== contextState[s.contextKey],
            );

            for (const [k, v] of Object.entries(contextState)) {
                this._state.set(k, v);
            }

            this._overriddenSettingIds.clear();
            for (const i of this.instructionsCatalog.all) {
                if (overriddenFileNames.has(i.fileName)) {
                    this._overriddenSettingIds.add(i.settingId);
                }
            }

            if (serverChanged) {
                this._onDidChange.fire();
            }

            this._onDidDetect.fire();
        } catch {
            // Workspace detection is best-effort; failures should not break the extension
        }
    }

    dispose(): void {
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this._onDidChange.dispose();
        this._onDidDetect.dispose();
        this.disposables.forEach(d => d.dispose());
    }
}
