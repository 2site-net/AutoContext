import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { workspace, commands } from './__mocks__/vscode';
import { WorkspaceContextDetector } from '../../src/workspace-context-detector';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { McpServersCatalog } from '../../src/mcp-servers-catalog';
import type { McpServerEntry } from '../../src/types/mcp-server-entry';
import type { InstructionsFileEntry } from '../../src/types/instructions-file-entry';

const fakeUri = (p: string) => ({ path: p, scheme: 'file', fsPath: p, toString: () => `file://${p}` });

const mockOutputChannel = { appendLine: vi.fn() } as unknown as import('vscode').OutputChannel;

const testInstructions: InstructionsFileEntry[] = [
    { key: 'copilot', fileName: 'copilot.instructions.md', label: 'Copilot', category: 'general' },
    { key: 'dotnet.codingStandards', fileName: 'dotnet-coding-standards.instructions.md', label: '.NET Standards', category: 'dotnet', workspaceFlags: ['hasDotNet'] },
];

const testServers: McpServerEntry[] = [
    { label: 'DotNet', scope: 'dotnet', server: 'dotnet', contextKey: 'hasDotNet' },
    { label: 'Git', scope: 'git', server: 'workspace', contextKey: 'hasGit' },
    { label: 'EditorConfig', scope: 'editorconfig', server: 'workspace' },
    { label: 'TypeScript', scope: 'typescript', server: 'web', contextKey: 'hasTypeScript' },
];

function createDetector(): WorkspaceContextDetector {
    return new WorkspaceContextDetector(
        new InstructionsCatalog(testInstructions),
        new McpServersCatalog(testServers),
        mockOutputChannel,
    );
}

function stubFindFiles(mapping: Record<string, string[]>): void {
    (workspace.findFiles as ReturnType<typeof vi.fn>).mockImplementation(
        async (pattern: unknown) => (mapping[pattern as string] ?? []).map(f => fakeUri(f)),
    );
}

function stubReadFile(mapping: Record<string, string>): void {
    const encoder = new TextEncoder();
    (workspace.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async (uri: unknown) => {
        const content = mapping[(uri as { path: string }).path];
        return content !== undefined ? encoder.encode(content) : new Uint8Array();
    });
}

beforeEach(() => {
    vi.clearAllMocks();
    workspace.workspaceFolders = undefined;
    (workspace.findFiles as ReturnType<typeof vi.fn>).mockImplementation(async () => []);
    (workspace.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async () => new Uint8Array());
    (workspace.fs.stat as ReturnType<typeof vi.fn>).mockRejectedValue(new Error('not found'));
});

describe('WorkspaceContextDetector', () => {
    describe('detect', () => {
        describe('file-based detection', () => {
            it('should set all flags to false for an empty workspace', async () => {
                const det = createDetector();

                await det.detect();

                expect.soft(det.get('hasDotNet')).toBe(false);
                expect.soft(det.get('hasTypeScript')).toBe(false);
                expect.soft(det.get('hasJavaScript')).toBe(false);
                expect.soft(det.get('hasPython')).toBe(false);
                expect.soft(det.get('hasGit')).toBe(false);
            });

            it('should detect TypeScript when .ts files exist', async () => {
                stubFindFiles({ '**/*.{ts,tsx,mts,cts}': ['/src/index.ts'] });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasTypeScript')).toBe(true);
            });

            it('should detect C# and .NET when .csproj files exist', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/app/App.csproj'],
                    '**/*.csproj': ['/app/App.csproj'],
                });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasCSharp')).toBe(true);
                expect.soft(det.get('hasDotNet')).toBe(true);
            });

            it('should detect Python when .py files exist', async () => {
                stubFindFiles({ '**/*.py': ['/main.py'] });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasPython')).toBe(true);
            });

            it('should detect Docker when Dockerfile exists', async () => {
                stubFindFiles({ '**/Dockerfile*': ['/Dockerfile'] });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasDocker')).toBe(true);
            });

            it('should detect PowerShell when .ps1 files exist', async () => {
                stubFindFiles({ '**/*.{ps1,psm1,psd1}': ['/deploy.ps1'] });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasPowerShell')).toBe(true);
            });
        });

        describe('package.json scanning', () => {
            it('should detect React from package.json dependencies', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "dependencies": { "react": "^18" } }' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasReact')).toBe(true);
            });

            it('should detect Angular from package.json', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "dependencies": { "@angular/core": "^17" } }' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasAngular')).toBe(true);
            });

            it('should detect Vitest from package.json', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "devDependencies": { "vitest": "^1" } }' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasVitest')).toBe(true);
            });

            it('should detect GraphQL from package.json', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "dependencies": { "graphql": "^16" } }' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasGraphql')).toBe(true);
            });

            it('should not detect frameworks when package.json has no matching dependencies', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "dependencies": { "lodash": "^4" } }' });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasReact')).toBe(false);
                expect.soft(det.get('hasAngular')).toBe(false);
                expect.soft(det.get('hasVue')).toBe(false);
            });
        });

        describe('csproj scanning', () => {
            it('should detect ASP.NET Core from Web SDK', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/app/App.csproj'],
                    '**/*.csproj': ['/app/App.csproj'],
                    '**/*.{csproj,fsproj,vbproj}': ['/app/App.csproj'],
                });
                stubReadFile({ '/app/App.csproj': '<Project Sdk="Microsoft.NET.Sdk.Web"></Project>' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasAspNetCore')).toBe(true);
            });

            it('should detect xUnit from NuGet reference', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/t/T.csproj'],
                    '**/*.csproj': ['/t/T.csproj'],
                    '**/*.{csproj,fsproj,vbproj}': ['/t/T.csproj'],
                });
                stubReadFile({ '/t/T.csproj': '<PackageReference Include="xunit" Version="2.7" />' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasXunit')).toBe(true);
            });

            it('should detect Entity Framework Core from NuGet reference', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/app/App.csproj'],
                    '**/*.csproj': ['/app/App.csproj'],
                    '**/*.{csproj,fsproj,vbproj}': ['/app/App.csproj'],
                });
                stubReadFile({ '/app/App.csproj': '<PackageReference Include="Microsoft.EntityFrameworkCore" />' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasEntityFrameworkCore')).toBe(true);
            });

            it('should not scan csproj files when no .NET project exists', async () => {
                stubFindFiles({ '**/*.py': ['/main.py'] });

                const det = createDetector();
                await det.detect();

                expect(workspace.findFiles).not.toHaveBeenCalledWith(
                    '**/*.{csproj,fsproj,vbproj}',
                    expect.anything(),
                    expect.anything(),
                );
            });
        });

        describe('implication chains', () => {
            it('should imply TypeScript and JavaScript from Angular', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "dependencies": { "@angular/core": "^17" } }' });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasAngular')).toBe(true);
                expect.soft(det.get('hasTypeScript')).toBe(true);
                expect.soft(det.get('hasJavaScript')).toBe(true);
                expect.soft(det.get('hasNodeJs')).toBe(true);
            });

            it('should imply React from NextJs', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "dependencies": { "next": "^14" } }' });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasNextJs')).toBe(true);
                expect.soft(det.get('hasReact')).toBe(true);
            });

            it('should imply JavaScript from TypeScript', async () => {
                stubFindFiles({ '**/*.{ts,tsx,mts,cts}': ['/app.ts'] });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasTypeScript')).toBe(true);
                expect.soft(det.get('hasJavaScript')).toBe(true);
            });

            it('should imply ASP.NET Core, C#, and .NET from Blazor', async () => {
                stubFindFiles({ '**/*.razor': ['/Counter.razor'] });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasBlazor')).toBe(true);
                expect.soft(det.get('hasAspNetCore')).toBe(true);
                expect.soft(det.get('hasCSharp')).toBe(true);
                expect.soft(det.get('hasDotNet')).toBe(true);
            });

            it('should imply XAML and .NET from WPF', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/app/App.csproj'],
                    '**/*.csproj': ['/app/App.csproj'],
                    '**/*.{csproj,fsproj,vbproj}': ['/app/App.csproj'],
                });
                stubReadFile({ '/app/App.csproj': '<UseWPF>true</UseWPF>' });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasWpf')).toBe(true);
                expect.soft(det.get('hasXaml')).toBe(true);
                expect.soft(det.get('hasDotNet')).toBe(true);
            });

            it('should imply .NET from xUnit', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/t/T.csproj'],
                    '**/*.csproj': ['/t/T.csproj'],
                    '**/*.{csproj,fsproj,vbproj}': ['/t/T.csproj'],
                });
                stubReadFile({ '/t/T.csproj': '<PackageReference Include="xunit" />' });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasXunit')).toBe(true);
                expect.soft(det.get('hasDotNet')).toBe(true);
            });
        });

        describe('composite flags', () => {
            it('should set hasDotNetTesting when xUnit is detected', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/t/T.csproj'],
                    '**/*.csproj': ['/t/T.csproj'],
                    '**/*.{csproj,fsproj,vbproj}': ['/t/T.csproj'],
                });
                stubReadFile({ '/t/T.csproj': '<PackageReference Include="xunit" />' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasDotNetTesting')).toBe(true);
            });

            it('should set hasWebTesting when Vitest is detected', async () => {
                stubFindFiles({ '**/package.json': ['/package.json'] });
                stubReadFile({ '/package.json': '{ "devDependencies": { "vitest": "^1" } }' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasWebTesting')).toBe(true);
            });

            it('should set hasJvm when Kotlin is detected', async () => {
                stubFindFiles({ '**/*.{kt,kts}': ['/Main.kt'] });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasKotlin')).toBe(true);
                expect.soft(det.get('hasJvm')).toBe(true);
            });

            it('should set hasNative when Rust is detected', async () => {
                stubFindFiles({ '**/*.rs': ['/main.rs'] });

                const det = createDetector();
                await det.detect();

                expect.soft(det.get('hasRust')).toBe(true);
                expect.soft(det.get('hasNative')).toBe(true);
            });

            it('should not set hasDotNetTesting when no test framework is detected', async () => {
                stubFindFiles({
                    '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/app/App.csproj'],
                    '**/*.csproj': ['/app/App.csproj'],
                    '**/*.{csproj,fsproj,vbproj}': ['/app/App.csproj'],
                });
                stubReadFile({ '/app/App.csproj': '<Project Sdk="Microsoft.NET.Sdk"></Project>' });

                const det = createDetector();
                await det.detect();

                expect(det.get('hasDotNetTesting')).toBe(false);
            });
        });

        describe('git detection', () => {
            it('should detect git when .git directory exists', async () => {
                workspace.workspaceFolders = [{ uri: { path: '/workspace', fsPath: '/workspace' } }];
                (workspace.fs.stat as ReturnType<typeof vi.fn>).mockResolvedValue({});

                const det = createDetector();
                await det.detect();

                expect(det.get('hasGit')).toBe(true);
            });

            it('should not detect git when .git directory is absent', async () => {
                workspace.workspaceFolders = [{ uri: { path: '/workspace', fsPath: '/workspace' } }];

                const det = createDetector();
                await det.detect();

                expect(det.get('hasGit')).toBe(false);
            });

            it('should not detect git when no workspace folders are open', async () => {
                const det = createDetector();

                await det.detect();

                expect(det.get('hasGit')).toBe(false);
            });
        });

        describe('context key registration', () => {
            it('should prefix workspace keys with autocontext.workspace.', async () => {
                stubFindFiles({ '**/*.py': ['/main.py'] });

                const det = createDetector();
                await det.detect();

                expect.soft(commands.executeCommand).toHaveBeenCalledWith(
                    'setContext', 'autocontext.workspace.hasPython', true,
                );
                expect.soft(commands.executeCommand).toHaveBeenCalledWith(
                    'setContext', 'autocontext.workspace.hasRust', false,
                );
            });

            it('should register override keys with autocontext.override. prefix', async () => {
                stubFindFiles({
                    '.github/instructions/*.instructions.md': [
                        '/.github/instructions/dotnet-coding-standards.instructions.md',
                    ],
                });

                const det = createDetector();
                await det.detect();

                expect(commands.executeCommand).toHaveBeenCalledWith(
                    'setContext', 'autocontext.override.dotnet.codingStandards', true,
                );
            });
        });

        describe('override detection', () => {
            it('should populate overriddenContextKeys for matching instruction files', async () => {
                stubFindFiles({
                    '.github/instructions/*.instructions.md': [
                        '/.github/instructions/dotnet-coding-standards.instructions.md',
                    ],
                });

                const det = createDetector();
                await det.detect();

                expect(det.getOverriddenContextKeys().has('autocontext.instructions.dotnet.codingStandards')).toBe(true);
            });

            it('should not include unknown override files', async () => {
                stubFindFiles({
                    '.github/instructions/*.instructions.md': [
                        '/.github/instructions/unknown.instructions.md',
                    ],
                });

                const det = createDetector();
                await det.detect();

                expect(det.getOverriddenContextKeys().size).toBe(0);
            });

            it('should clear overridden IDs between detections', async () => {
                stubFindFiles({
                    '.github/instructions/*.instructions.md': [
                        '/.github/instructions/dotnet-coding-standards.instructions.md',
                    ],
                });
                const det = createDetector();
                await det.detect();
                expect(det.getOverriddenContextKeys().size).toBe(1);

                stubFindFiles({});
                await det.detect();

                expect(det.getOverriddenContextKeys().size).toBe(0);
            });
        });

        describe('state transitions', () => {
            it('should update state across repeated detections', async () => {
                const det = createDetector();
                await det.detect();
                expect(det.get('hasTypeScript')).toBe(false);

                stubFindFiles({ '**/*.{ts,tsx,mts,cts}': ['/app.ts'] });
                await det.detect();

                expect(det.get('hasTypeScript')).toBe(true);
            });

            it('should reflect removed files in subsequent detections', async () => {
                stubFindFiles({ '**/*.py': ['/main.py'] });
                const det = createDetector();
                await det.detect();
                expect(det.get('hasPython')).toBe(true);

                stubFindFiles({});
                await det.detect();

                expect(det.get('hasPython')).toBe(false);
            });
        });
    });

    describe('get', () => {
        it('should return false for unknown keys', () => {
            const det = createDetector();

            expect(det.get('nonExistentKey')).toBe(false);
        });

        it('should return detected values after detect()', async () => {
            stubFindFiles({ '**/*.{ts,tsx,mts,cts}': ['/app.ts'] });

            const det = createDetector();
            await det.detect();

            expect.soft(det.get('hasTypeScript')).toBe(true);
            expect.soft(det.get('hasDotNet')).toBe(false);
        });
    });

    describe('incremental detection', () => {
        beforeEach(() => vi.useFakeTimers());
        afterEach(() => vi.useRealTimers());

        type WatcherMock = {
            onDidCreate: ReturnType<typeof vi.fn>;
            onDidChange: ReturnType<typeof vi.fn>;
            onDidDelete: ReturnType<typeof vi.fn>;
        };

        function getWatchers(): { existence: WatcherMock; content: WatcherMock; override: WatcherMock } {
            const results = (workspace.createFileSystemWatcher as ReturnType<typeof vi.fn>).mock.results;
            return {
                existence: results[0].value as WatcherMock,
                content: results[1].value as WatcherMock,
                override: results[2].value as WatcherMock,
            };
        }

        function fireEvent(watcher: WatcherMock, event: 'create' | 'change' | 'delete', path: string): void {
            const prop = event === 'create' ? 'onDidCreate' : event === 'change' ? 'onDidChange' : 'onDidDelete';
            const listener = watcher[prop].mock.calls[0]?.[0] as ((uri: unknown) => void) | undefined;
            listener?.(fakeUri(path));
        }

        it('should set flag on existence create without extra globs', async () => {
            const det = createDetector();
            const { existence } = getWatchers();
            await det.detect();
            (workspace.findFiles as ReturnType<typeof vi.fn>).mockClear();

            fireEvent(existence, 'create', '/src/index.ts');
            await vi.advanceTimersByTimeAsync(500);

            expect.soft(det.get('hasTypeScript')).toBe(true);
            expect.soft(det.get('hasJavaScript')).toBe(true); // activation cascade
            expect(workspace.findFiles).not.toHaveBeenCalled();
        });

        it('should re-glob only affected flags on existence delete', async () => {
            stubFindFiles({ '**/*.{ts,tsx,mts,cts}': ['/app.ts'] });
            const det = createDetector();
            const { existence } = getWatchers();
            await det.detect();
            expect(det.get('hasTypeScript')).toBe(true);

            stubFindFiles({});
            (workspace.findFiles as ReturnType<typeof vi.fn>).mockClear();

            fireEvent(existence, 'delete', '/app.ts');
            await vi.advanceTimersByTimeAsync(500);

            expect.soft(det.get('hasTypeScript')).toBe(false);
            // Only the TypeScript glob should have been called, not all 30+
            const calls = (workspace.findFiles as ReturnType<typeof vi.fn>).mock.calls.map(c => c[0]);
            expect(calls).toEqual(['**/*.{ts,tsx,mts,cts}']);
        });

        it('should trigger npm content re-scan on package.json change', async () => {
            stubFindFiles({ '**/package.json': ['/package.json'] });
            stubReadFile({ '/package.json': '{ "dependencies": { "react": "^18" } }' });
            const det = createDetector();
            const { content } = getWatchers();
            await det.detect();
            expect(det.get('hasReact')).toBe(true);

            stubReadFile({ '/package.json': '{ "dependencies": { "vue": "^3" } }' });

            fireEvent(content, 'change', '/package.json');
            await vi.advanceTimersByTimeAsync(500);

            expect.soft(det.get('hasReact')).toBe(false);
            expect.soft(det.get('hasVue')).toBe(true);
            expect.soft(det.get('hasNodeJs')).toBe(true);
        });

        it('should trigger dotnet content re-scan on csproj change', async () => {
            stubFindFiles({
                '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/app/App.csproj'],
                '**/*.csproj': ['/app/App.csproj'],
                '**/*.{csproj,fsproj,vbproj}': ['/app/App.csproj'],
            });
            stubReadFile({ '/app/App.csproj': '<PackageReference Include="xunit" />' });
            const det = createDetector();
            const { content } = getWatchers();
            await det.detect();
            expect(det.get('hasXunit')).toBe(true);

            stubReadFile({ '/app/App.csproj': '<Project Sdk="Microsoft.NET.Sdk.Web"></Project>' });

            fireEvent(content, 'change', '/app/App.csproj');
            await vi.advanceTimersByTimeAsync(500);

            expect.soft(det.get('hasXunit')).toBe(false);
            expect.soft(det.get('hasAspNetCore')).toBe(true);
        });

        it('should re-scan overrides when override watcher fires', async () => {
            const det = createDetector();
            const { override } = getWatchers();
            await det.detect();
            expect(det.getOverriddenContextKeys().size).toBe(0);

            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });

            fireEvent(override, 'create', '/.github/instructions/dotnet-coding-standards.instructions.md');
            await vi.advanceTimersByTimeAsync(500);

            expect(det.getOverriddenContextKeys().has('autocontext.instructions.dotnet.codingStandards')).toBe(true);
        });

        it('should fall back to full detect when no prior scan exists', async () => {
            stubFindFiles({ '**/*.py': ['/main.py'] });
            const det = createDetector();
            const { existence } = getWatchers();

            fireEvent(existence, 'create', '/main.py');
            await vi.advanceTimersByTimeAsync(500);

            // Full detect ran, so all flags are computed from globs
            expect(det.get('hasPython')).toBe(true);
        });

        it('should set manifest file flags on content create', async () => {
            const det = createDetector();
            const { content } = getWatchers();
            await det.detect();

            fireEvent(content, 'create', '/pom.xml');
            await vi.advanceTimersByTimeAsync(500);

            expect.soft(det.get('hasJava')).toBe(true);
            expect.soft(det.get('hasJvm')).toBe(true); // activation cascade
        });

        it('should carry forward git detection from full scan', async () => {
            workspace.workspaceFolders = [{ uri: { path: '/workspace', fsPath: '/workspace' } }];
            (workspace.fs.stat as ReturnType<typeof vi.fn>).mockResolvedValue({});
            const det = createDetector();
            const { existence } = getWatchers();
            await det.detect();
            expect(det.get('hasGit')).toBe(true);

            fireEvent(existence, 'create', '/src/index.ts');
            await vi.advanceTimersByTimeAsync(500);

            expect(det.get('hasGit')).toBe(true);
        });
    });

    describe('resilience', () => {
        it('should update state even when setContext rejects', async () => {
            stubFindFiles({
                '**/*.{csproj,fsproj,vbproj,sln,slnx}': ['/app/App.csproj'],
                '**/*.csproj': ['/app/App.csproj'],
            });
            (commands.executeCommand as ReturnType<typeof vi.fn>).mockRejectedValue(
                new Error('setContext failure'),
            );

            const det = createDetector();
            await det.detect();

            expect.soft(det.get('hasCSharp')).toBe(true);
            expect.soft(det.get('hasDotNet')).toBe(true);
        });

        it('should fire onDidDetect even when setContext rejects', async () => {
            stubFindFiles({ '**/*.py': ['/main.py'] });
            (commands.executeCommand as ReturnType<typeof vi.fn>).mockRejectedValue(
                new Error('setContext failure'),
            );

            const det = createDetector();

            await det.detect();

            // eslint-disable-next-line @typescript-eslint/no-explicit-any -- access private emitter to verify fire was called
            expect((det as any)._onDidDetect.fire).toHaveBeenCalled();
        });

        it('should update overrides even when setContext rejects', async () => {
            stubFindFiles({
                '.github/instructions/*.instructions.md': [
                    '/.github/instructions/dotnet-coding-standards.instructions.md',
                ],
            });
            (commands.executeCommand as ReturnType<typeof vi.fn>).mockRejectedValue(
                new Error('setContext failure'),
            );

            const det = createDetector();
            await det.detect();

            expect(det.getOverriddenContextKeys().has('autocontext.instructions.dotnet.codingStandards')).toBe(true);
        });
    });

});
