import * as vscode from 'vscode';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { ContextKeys } from './context-keys.js';
import type { McpServersCatalog } from './mcp-servers-catalog.js';

// --- File-system watcher globs ---

const existenceWatchGlob =
    '**/*.{csproj,fsproj,sln,slnx,razor,xaml,aspx,html,cshtml,css,js,jsx,mjs,cjs,ts,tsx,mts,cts,ps1,psm1,psd1,sh,bash,bat,cmd,java,kt,kts,scala,sc,groovy,gvy,c,cpp,cxx,cc,rs,go,py,lua,php}';

const contentWatchGlob =
    '**/{*.csproj,*.fsproj,package.json,pom.xml,build.gradle,build.sbt,Cargo.toml,go.mod,pyproject.toml,composer.json}';

const overrideWatchGlob =
    '**/.github/{copilot-instructions.md,instructions/*.instructions.md}';

// --- Declarative detection tables ---

interface FileRule {
    readonly flag: string;
    readonly globs: readonly string[];
}

interface ContentRule {
    readonly flag: string;
    readonly pattern: RegExp;
}

const fileRules = [
    { flag: 'hasDotNet', globs: ['**/*.{csproj,fsproj,vbproj,sln,slnx}'] },
    { flag: 'hasCSharp', globs: ['**/*.csproj'] },
    { flag: 'hasFSharp', globs: ['**/*.fsproj'] },
    { flag: 'hasVbNet', globs: ['**/*.vbproj'] },
    { flag: 'hasBlazor', globs: ['**/*.razor'] },
    { flag: 'hasXaml', globs: ['**/*.xaml'] },
    { flag: 'hasWebForms', globs: ['**/*.{aspx,ascx,master}'] },
    { flag: 'hasRazor', globs: ['**/*.cshtml'] },
    { flag: 'hasHtml', globs: ['**/*.{html,cshtml}'] },
    { flag: 'hasCss', globs: ['**/*.css'] },
    { flag: 'hasDart', globs: ['**/*.dart', '**/pubspec.yaml'] },
    { flag: 'hasJavaScript', globs: ['**/*.{js,jsx,mjs,cjs}'] },
    { flag: 'hasTypeScript', globs: ['**/*.{ts,tsx,mts,cts}'] },
    { flag: 'hasUnity', globs: ['**/ProjectSettings/ProjectSettings.asset'] },
    { flag: 'hasDocker', globs: ['**/Dockerfile*'] },
    { flag: 'hasPowerShell', globs: ['**/*.{ps1,psm1,psd1}'] },
    { flag: 'hasBash', globs: ['**/*.{sh,bash}'] },
    { flag: 'hasBatch', globs: ['**/*.{bat,cmd}'] },
    { flag: 'hasYaml', globs: ['**/*.{yml,yaml}'] },
    { flag: 'hasJava', globs: ['**/*.java', '**/{pom.xml,build.gradle}'] },
    { flag: 'hasKotlin', globs: ['**/*.{kt,kts}'] },
    { flag: 'hasScala', globs: ['**/*.{scala,sc}', '**/build.sbt'] },
    { flag: 'hasGroovy', globs: ['**/*.{groovy,gvy}'] },
    { flag: 'hasC', globs: ['**/*.c'] },
    { flag: 'hasCpp', globs: ['**/*.{cpp,cxx,cc}'] },
    { flag: 'hasRust', globs: ['**/*.rs', '**/Cargo.toml'] },
    { flag: 'hasGo', globs: ['**/*.go', '**/go.mod'] },
    { flag: 'hasPython', globs: ['**/*.py', '**/pyproject.toml'] },
    { flag: 'hasLua', globs: ['**/*.lua'] },
    { flag: 'hasPhp', globs: ['**/*.php', '**/composer.json'] },
] as const satisfies readonly FileRule[];

const npmContentRules = [
    { flag: 'hasReact', pattern: /"react"\s*:/ },
    { flag: 'hasAngular', pattern: /"@angular\/core"\s*:/ },
    { flag: 'hasVue', pattern: /"vue"\s*:/ },
    { flag: 'hasSvelte', pattern: /"svelte"\s*:/ },
    { flag: 'hasVitest', pattern: /"vitest"\s*:/ },
    { flag: 'hasJest', pattern: /"jest"\s*:/ },
    { flag: 'hasJasmine', pattern: /"jasmine"\s*:/ },
    { flag: 'hasMocha', pattern: /"mocha"\s*:/ },
    { flag: 'hasPlaywright', pattern: /"@playwright\/test"\s*:/ },
    { flag: 'hasCypress', pattern: /"cypress"\s*:/ },
    { flag: 'hasNextJs', pattern: /"next"\s*:/ },
    { flag: 'hasGraphql', pattern: /"graphql"\s*:|"@apollo\/|"graphql-request"\s*:|"urql"\s*:|"HotChocolate/i },
] as const satisfies readonly ContentRule[];

const dotnetContentRules = [
    { flag: 'hasAspNetCore', pattern: /Sdk\s*=\s*["']Microsoft\.NET\.Sdk\.(Web|Razor|BlazorWebAssembly)["']/i },
    { flag: 'hasDapper', pattern: /Dapper/i },
    { flag: 'hasEntityFrameworkCore', pattern: /Microsoft\.EntityFrameworkCore/i },
    { flag: 'hasMaui', pattern: /<UseMaui>\s*true\s*<\/UseMaui>/i },
    { flag: 'hasMongoDb', pattern: /MongoDB\.Driver|MongoDB\.EntityFrameworkCore/i },
    { flag: 'hasXunit', pattern: /xunit/i },
    { flag: 'hasMsTest', pattern: /MSTest|Microsoft\.VisualStudio\.TestPlatform/i },
    { flag: 'hasNUnit', pattern: /NUnit/i },
    { flag: 'hasWpf', pattern: /<UseWPF>\s*true\s*<\/UseWPF>/i },
    { flag: 'hasWinForms', pattern: /<UseWindowsForms>\s*true\s*<\/UseWindowsForms>/i },
    { flag: 'hasMySql', pattern: /MySqlConnector|MySql\.Data|Pomelo\.EntityFrameworkCore\.MySql/i },
    { flag: 'hasOracle', pattern: /Oracle\.ManagedDataAccess|Oracle\.EntityFrameworkCore/i },
    { flag: 'hasPostgres', pattern: /Npgsql/i },
    { flag: 'hasSqlite', pattern: /Microsoft\.Data\.Sqlite|System\.Data\.SQLite|EntityFrameworkCore\.Sqlite/i },
    { flag: 'hasSqlServer', pattern: /Microsoft\.Data\.SqlClient|System\.Data\.SqlClient|EntityFrameworkCore\.SqlServer|EntityFramework\.SqlServer/i },
    { flag: 'hasGrpc', pattern: /Grpc\.|Google\.Protobuf/i },
    { flag: 'hasMediatR', pattern: /MediatR|Mediator\.Abstractions/i },
    { flag: 'hasRedis', pattern: /StackExchange\.Redis|Microsoft\.Extensions\.Caching\.StackExchangeRedis/i },
    { flag: 'hasSignalR', pattern: /Microsoft\.AspNetCore\.SignalR/i },
    { flag: 'hasGraphql', pattern: /HotChocolate|GraphQL\.Server/i },
] as const satisfies readonly ContentRule[];

// [child, parent] — when child is true, parent becomes activated.
const flagActivationRules = [
    // Web: framework → language/runtime
    ['hasNextJs', 'hasReact'],
    ['hasAngular', 'hasTypeScript'],
    ['hasTypeScript', 'hasJavaScript'],
    ['hasReact', 'hasNodeJs'],
    ['hasAngular', 'hasNodeJs'],
    ['hasVue', 'hasNodeJs'],
    ['hasSvelte', 'hasNodeJs'],
    ['hasVitest', 'hasNodeJs'],
    ['hasJest', 'hasNodeJs'],
    ['hasJasmine', 'hasNodeJs'],
    ['hasMocha', 'hasNodeJs'],
    ['hasPlaywright', 'hasNodeJs'],
    ['hasCypress', 'hasNodeJs'],
    ['hasNodeJs', 'hasJavaScript'],

    // .NET: framework → platform
    ['hasBlazor', 'hasAspNetCore'],
    ['hasSignalR', 'hasAspNetCore'],
    ['hasAspNetCore', 'hasRazor'],
    ['hasBlazor', 'hasCSharp'],
    ['hasUnity', 'hasCSharp'],
    ['hasWpf', 'hasXaml'],
    ['hasMaui', 'hasXaml'],
    ['hasAspNetCore', 'hasDotNet'],
    ['hasDapper', 'hasDotNet'],
    ['hasEntityFrameworkCore', 'hasDotNet'],
    ['hasMaui', 'hasDotNet'],
    ['hasWpf', 'hasDotNet'],
    ['hasWinForms', 'hasDotNet'],
    ['hasWebForms', 'hasDotNet'],
    ['hasGrpc', 'hasDotNet'],
    ['hasMediatR', 'hasDotNet'],
    ['hasRedis', 'hasDotNet'],
    ['hasSignalR', 'hasDotNet'],
    ['hasXunit', 'hasDotNet'],
    ['hasMsTest', 'hasDotNet'],
    ['hasNUnit', 'hasDotNet'],
    ['hasMongoDb', 'hasDotNet'],
    ['hasMySql', 'hasDotNet'],
    ['hasOracle', 'hasDotNet'],
    ['hasPostgres', 'hasDotNet'],
    ['hasSqlite', 'hasDotNet'],
    ['hasSqlServer', 'hasDotNet'],
    ['hasUnity', 'hasDotNet'],

    // Markup → style
    ['hasBlazor', 'hasHtml'],
    ['hasHtml', 'hasCss'],

    // JVM ecosystem
    ['hasJava', 'hasJvm'],
    ['hasKotlin', 'hasJvm'],
    ['hasScala', 'hasJvm'],
    ['hasGroovy', 'hasJvm'],

    // Native/systems
    ['hasC', 'hasNative'],
    ['hasCpp', 'hasNative'],
    ['hasRust', 'hasNative'],
    ['hasGo', 'hasNative'],

    // Testing composites
    ['hasXunit', 'hasDotNetTesting'],
    ['hasMsTest', 'hasDotNetTesting'],
    ['hasNUnit', 'hasDotNetTesting'],
    ['hasVitest', 'hasWebTesting'],
    ['hasJest', 'hasWebTesting'],
    ['hasJasmine', 'hasWebTesting'],
    ['hasMocha', 'hasWebTesting'],
    ['hasPlaywright', 'hasWebTesting'],
    ['hasCypress', 'hasWebTesting'],
] as const satisfies readonly (readonly [string, string])[];

type FlagName =
    | typeof fileRules[number]['flag']
    | typeof npmContentRules[number]['flag']
    | typeof dotnetContentRules[number]['flag']
    | typeof flagActivationRules[number][0 | 1]
    | 'hasGit';

const allFlags: ReadonlySet<FlagName> = new Set<FlagName>([
    ...fileRules.map(r => r.flag),
    ...npmContentRules.map(r => r.flag),
    ...dotnetContentRules.map(r => r.flag),
    ...flagActivationRules.flat(),
    'hasGit',
    'hasNodeJs',
]);

// --- Reverse maps for incremental detection ---

const extensionToFileFlags: ReadonlyMap<string, readonly string[]> = (() => {
    const map = new Map<string, string[]>();
    const add = (ext: string, flag: string) => {
        const list = map.get(ext);
        if (list) { list.push(flag); } else { map.set(ext, [flag]); }
    };
    for (const rule of fileRules) {
        for (const glob of rule.globs) {
            const brace = glob.match(/^\*\*\/\*\.\{([^}]+)\}$/);
            if (brace) { for (const e of brace[1].split(',')) add(e, rule.flag); continue; }
            const single = glob.match(/^\*\*\/\*\.(\w+)$/);
            if (single) { add(single[1], rule.flag); }
        }
    }
    return map;
})();

const manifestToFileFlags: ReadonlyMap<string, readonly string[]> = (() => {
    const map = new Map<string, string[]>();
    const add = (name: string, flag: string) => {
        const list = map.get(name);
        if (list) { list.push(flag); } else { map.set(name, [flag]); }
    };
    for (const rule of fileRules) {
        for (const glob of rule.globs) {
            const braceNames = glob.match(/^\*\*\/\{([^}]+)\}$/);
            if (braceNames) { for (const n of braceNames[1].split(',')) add(n, rule.flag); continue; }
            const exact = glob.match(/^\*\*\/([^*{/]+)$/);
            if (exact) { add(exact[1], rule.flag); }
        }
    }
    return map;
})();

type ContentCategory = 'npm' | 'dotnet';

function getContentCategory(filename: string): ContentCategory | undefined {
    if (filename === 'package.json') return 'npm';
    if (/\.(?:csproj|fsproj|vbproj)$/.test(filename)) return 'dotnet';
    return undefined;
}

type WatcherKind = 'existence' | 'content' | 'override';
type EventKind = 'create' | 'change' | 'delete';

interface PendingEvent {
    readonly uri: vscode.Uri;
    readonly watcher: WatcherKind;
    readonly event: EventKind;
}

export class WorkspaceContextDetector implements vscode.Disposable {
    private readonly disposables: vscode.Disposable[] = [];
    private debounceTimer: ReturnType<typeof setTimeout> | undefined;
    private readonly _onDidChange = new vscode.EventEmitter<void>();
    private readonly _onDidDetect = new vscode.EventEmitter<void>();
    private readonly _state = new Map<string, boolean>();
    private readonly _baseFlags = new Map<string, boolean>();
    private readonly _overriddenSettingIds = new Set<string>();
    private _overriddenFileNames = new Set<string>();
    private _pendingEvents: PendingEvent[] = [];

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
        const existenceWatcher = vscode.workspace.createFileSystemWatcher(existenceWatchGlob);

        this.disposables.push(
            existenceWatcher,
            existenceWatcher.onDidCreate(uri => this.scheduleEvent(uri, 'existence', 'create')),
            existenceWatcher.onDidDelete(uri => this.scheduleEvent(uri, 'existence', 'delete')),
        );

        const contentWatcher = vscode.workspace.createFileSystemWatcher(contentWatchGlob);

        this.disposables.push(
            contentWatcher,
            contentWatcher.onDidCreate(uri => this.scheduleEvent(uri, 'content', 'create')),
            contentWatcher.onDidChange(uri => this.scheduleEvent(uri, 'content', 'change')),
            contentWatcher.onDidDelete(uri => this.scheduleEvent(uri, 'content', 'delete')),
        );

        const overrideWatcher = vscode.workspace.createFileSystemWatcher(overrideWatchGlob);

        this.disposables.push(
            overrideWatcher,
            overrideWatcher.onDidCreate(uri => this.scheduleEvent(uri, 'override', 'create')),
            overrideWatcher.onDidDelete(uri => this.scheduleEvent(uri, 'override', 'delete')),
        );
    }

    private scheduleEvent(uri: vscode.Uri, watcher: WatcherKind, event: EventKind): void {
        this._pendingEvents.push({ uri, watcher, event });
        if (this.debounceTimer !== undefined) {
            clearTimeout(this.debounceTimer);
        }
        this.debounceTimer = setTimeout(() => {
            this.debounceTimer = undefined;
            const events = this._pendingEvents;
            this._pendingEvents = [];
            void this.detectIncremental(events);
        }, 500);
    }

    /** Full workspace scan. Called on activation and as a fallback. */
    async detect(): Promise<void> {
        try {
            const flags = {} as Record<string, boolean>;
            for (const f of allFlags) flags[f] = false;

            // File-based detection
            const fileResults = await Promise.all(
                fileRules.flatMap(rule =>
                    rule.globs.map(glob =>
                        vscode.workspace.findFiles(glob, '**/node_modules/**', 1)
                            .then(files => ({ flag: rule.flag, found: files.length > 0 })),
                    ),
                ),
            );

            for (const { flag, found } of fileResults) {
                if (found) flags[flag] = true;
            }

            await WorkspaceContextDetector.scanNpmContent(flags);

            if (flags.hasDotNet) {
                await WorkspaceContextDetector.scanDotNetContent(flags);
            }

            // Store base flags (pre-cascade)
            this._baseFlags.clear();
            for (const f of allFlags) this._baseFlags.set(f, flags[f]);

            WorkspaceContextDetector.applyActivationCascade(flags);
            await WorkspaceContextDetector.detectGit(flags);

            const overriddenFileNames = await this.scanOverrides();
            await this.commitState(flags, overriddenFileNames);
        } catch {
            // Workspace detection is best-effort; failures should not break the extension
        }
    }

    /**
     * Incremental detection from file-system watcher events.
     * Falls back to a full detect() when no prior scan exists.
     */
    private async detectIncremental(events: readonly PendingEvent[]): Promise<void> {
        if (this._baseFlags.size === 0) {
            return this.detect();
        }

        try {
            const flags = {} as Record<string, boolean>;
            for (const [k, v] of this._baseFlags) flags[k] = v;

            let scanNpm = false;
            let scanDotnet = false;
            let scanOverrides = false;
            const flagsToRecheck = new Set<string>();

            for (const ev of events) {
                if (ev.watcher === 'override') {
                    scanOverrides = true;
                    continue;
                }

                const filename = ev.uri.path.split('/').pop() ?? '';
                const dotIdx = filename.lastIndexOf('.');
                const ext = dotIdx >= 0 ? filename.slice(dotIdx + 1) : '';

                // File-flag updates from extension (existence + content watchers)
                const extFlags = extensionToFileFlags.get(ext);
                if (extFlags) {
                    if (ev.event === 'create') {
                        for (const f of extFlags) flags[f] = true;
                    } else if (ev.event === 'delete') {
                        for (const f of extFlags) flagsToRecheck.add(f);
                    }
                }

                // File-flag updates from manifest filename (content watcher)
                if (ev.watcher === 'content') {
                    const nameFlags = manifestToFileFlags.get(filename);
                    if (nameFlags) {
                        if (ev.event === 'create') {
                            for (const f of nameFlags) flags[f] = true;
                        } else if (ev.event === 'delete') {
                            for (const f of nameFlags) flagsToRecheck.add(f);
                        }
                    }

                    const category = getContentCategory(filename);
                    if (category === 'npm') scanNpm = true;
                    if (category === 'dotnet') scanDotnet = true;
                }
            }

            // Re-glob only flags affected by file deletions
            if (flagsToRecheck.size > 0) {
                const results = await Promise.all(
                    [...flagsToRecheck].flatMap(flag => {
                        const rule = fileRules.find(r => r.flag === flag);
                        if (!rule) return [];
                        return rule.globs.map(glob =>
                            vscode.workspace.findFiles(glob, '**/node_modules/**', 1)
                                .then(files => ({ flag, found: files.length > 0 })),
                        );
                    }),
                );

                for (const flag of flagsToRecheck) flags[flag] = false;
                for (const { flag, found } of results) {
                    if (found) flags[flag] = true;
                }
            }

            if (scanNpm) {
                for (const rule of npmContentRules) flags[rule.flag] = false;
                flags.hasNodeJs = false;
                await WorkspaceContextDetector.scanNpmContent(flags);
            }

            if (scanDotnet) {
                for (const rule of dotnetContentRules) flags[rule.flag] = false;
                if (flags.hasDotNet) {
                    await WorkspaceContextDetector.scanDotNetContent(flags);
                }
            }

            // Update base flags
            for (const [k, v] of Object.entries(flags)) this._baseFlags.set(k, v);

            WorkspaceContextDetector.applyActivationCascade(flags);

            // Git detection is unchanged by file events
            flags.hasGit = this._state.get('hasGit') ?? false;

            const overriddenFileNames = scanOverrides
                ? await this.scanOverrides()
                : this._overriddenFileNames;

            await this.commitState(flags, overriddenFileNames);
        } catch {
            // Workspace detection is best-effort; failures should not break the extension
        }
    }

    // --- Shared helpers ---

    private static async scanNpmContent(flags: Record<string, boolean>): Promise<void> {
        const decoder = new TextDecoder();
        const packageFiles = await vscode.workspace.findFiles('**/package.json', '**/node_modules/**', 50);
        if (packageFiles.length > 0) flags.hasNodeJs = true;

        for (const uri of packageFiles) {
            const content = decoder.decode(await vscode.workspace.fs.readFile(uri));

            for (const rule of npmContentRules) {
                if (!flags[rule.flag] && rule.pattern.test(content)) {
                    flags[rule.flag] = true;
                }
            }

            if (npmContentRules.every(r => flags[r.flag])) break;
        }
    }

    private static async scanDotNetContent(flags: Record<string, boolean>): Promise<void> {
        const decoder = new TextDecoder();
        const projFiles = await vscode.workspace.findFiles('**/*.{csproj,fsproj,vbproj}', '**/node_modules/**', 50);

        for (const uri of projFiles) {
            const content = decoder.decode(await vscode.workspace.fs.readFile(uri));

            for (const rule of dotnetContentRules) {
                if (!flags[rule.flag] && rule.pattern.test(content)) {
                    flags[rule.flag] = true;
                }
            }

            if (dotnetContentRules.every(r => flags[r.flag])) break;
        }
    }

    private static applyActivationCascade(flags: Record<string, boolean>): void {
        let changed = true;
        while (changed) {
            changed = false;
            for (const [child, parent] of flagActivationRules) {
                if (flags[child] && !flags[parent]) {
                    flags[parent] = true;
                    changed = true;
                }
            }
        }
    }

    private static async detectGit(flags: Record<string, boolean>): Promise<void> {
        for (const folder of vscode.workspace.workspaceFolders ?? []) {
            try {
                await vscode.workspace.fs.stat(vscode.Uri.joinPath(folder.uri, '.git'));
                flags.hasGit = true;
                return;
            } catch {
                // .git directory not found in this workspace folder
            }
        }
    }

    private async scanOverrides(): Promise<Set<string>> {
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

        return overriddenFileNames;
    }

    private async commitState(
        flags: Record<string, boolean>,
        overriddenFileNames: Set<string>,
    ): Promise<void> {
        const setContext = (key: string, value: boolean): Thenable<unknown> =>
            vscode.commands.executeCommand('setContext', key, value);

        await Promise.all([
            ...Object.entries(flags).map(([key, value]) =>
                setContext(`sharppilot.workspace.${key}`, value),
            ),
            ...this.instructionsCatalog.all.map(i =>
                setContext(ContextKeys.overrideKey(i.settingId), overriddenFileNames.has(i.fileName)),
            ),
        ]);

        const serverChanged = this.serversCatalog.all.some(s =>
            s.contextKey !== undefined && this._state.get(s.contextKey) !== flags[s.contextKey as string],
        );

        for (const [k, v] of Object.entries(flags)) {
            this._state.set(k, v);
        }

        this._overriddenFileNames = overriddenFileNames;
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
