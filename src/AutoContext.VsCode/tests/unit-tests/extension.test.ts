import { describe, it, expect, vi, beforeEach } from 'vitest';
import { lm, workspace, __emitterInstances } from '#testing/fakes/fake-vscode';

const { callLog } = vi.hoisted(() => ({ callLog: [] as string[] }));

// ── Module mocks (hoisted) ──────────────────────────────────────────

vi.mock('node:fs', () => ({ existsSync: () => false }));

vi.mock('../../src/metadata-loader', () => ({
    MetadataLoader: class {
        getInstructionsInfo() { return new Map(); }
    },
}));

vi.mock('../../src/mcp-tools-manifest-loader', () => ({
    McpToolsManifestLoader: class {
        load() {
            return {
                tools: [],
                categories: [],
                topCategories: [],
                subCategories: [],
                toolByName: () => undefined,
            };
        }
    },
}));

vi.mock('../../src/workspace-context-detector', () => ({
    WorkspaceContextDetector: class {
        async detect() { callLog.push('detect'); }
        onDidChange = () => ({ dispose() {} });
        get() { return false; }
        dispose() {}
    },
}));

vi.mock('../../src/instructions-files-manifest-loader', () => ({
    InstructionsFilesManifestLoader: class {
        load() {
            return { instructions: [], categories: [], count: 0, findByName: () => undefined };
        }
    },
}));

vi.mock('../../src/instructions-files-exporter', () => ({
    InstructionsFilesExporter: class {},
}));

vi.mock('../../src/autocontext-config-manager', () => ({
    AutoContextConfigManager: class {
        async read() { return {}; }
        readSync() { return {}; }
        async removeOrphanedIds() {}
        async clearStaleDisabledIds() { return []; }
        onDidChange = () => ({ dispose() {} });
        dispose() {}
    },
}));

vi.mock('../../src/instructions-viewer-document-provider', () => ({
    InstructionsViewerDocumentProvider: class { dispose() {} },
    instructionScheme: 'autocontext-instructions',
}));

vi.mock('../../src/instructions-viewer-codelens-provider', () => ({
    InstructionsViewerCodeLensProvider: class { dispose() {} },
}));

vi.mock('../../src/instructions-viewer-decoration-manager', () => ({
    InstructionsViewerDecorationManager: class { dispose() {} },
}));

vi.mock('../../src/instructions-files-manager', () => ({
    InstructionsFilesManager: class {
        async removeOrphanedStagingDirs() {}
        async write() {}
        dispose() {}
    },
}));

vi.mock('../../src/instructions-files-diagnostics-reporter', () => ({
    InstructionsFilesDiagnosticsReporter: class { async report() {} },
}));

vi.mock('../../src/instructions-files-tree-provider', () => {
    class InstructionsFilesTreeProvider {
        showNotDetected = false;
        dispose() {}
        enableInstruction() {}
        disableInstruction() {}
        static deleteOverride() {}
        static showOriginal() {}
    }
    return { InstructionsFilesTreeProvider };
});

vi.mock('../../src/mcp-tools-tree-provider', () => ({
    McpToolsTreeProvider: class { showNotDetected = false; dispose() {} },
}));

vi.mock('../../src/tree-view-state-resolver', () => ({
    TreeViewStateResolver: class {},
}));

vi.mock('../../src/tree-view-tooltip', () => ({
    TreeViewTooltip: class {},
}));

vi.mock('../../src/mcp-server-provider', () => ({
    McpServerProvider: class { dispose() {} },
}));

vi.mock('../../src/servers-manifest-loader', () => ({
    ServersManifestLoader: class {
        load() {
            return {
                servers: [],
                byId: () => undefined,
                get mcpServer() {
                    return { id: 'mcp-server', name: 'AutoContext.Mcp.Server', type: 'dotnet' };
                },
            };
        }
    },
}));

vi.mock('../../src/worker-manager', () => ({
    WorkerManager: class { start() {} dispose() {} whenWorkspaceReady() { return Promise.resolve(); } getInstanceId() { return 'test'; } },
}));

vi.mock('../../src/auto-configurer', () => ({
    AutoConfigurer: class { async run() {} },
}));

// ── SUT ─────────────────────────────────────────────────────────────

import { activate } from '#src/extension';
import { createFakeExtensionContext } from '#testing/fakes';

// ── Tests ───────────────────────────────────────────────────────────

beforeEach(() => {
    callLog.length = 0;
    __emitterInstances.length = 0;
    vi.clearAllMocks();
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
    lm.registerMcpServerDefinitionProvider.mockImplementation(() => {
        callLog.push('registerProvider');
        return { dispose() {} };
    });
});

describe('activate — MCP provider registration ordering', () => {
    it('should register MCP provider before running detect()', async () => {
        await activate(createFakeExtensionContext());

        const providerIdx = callLog.indexOf('registerProvider');
        const detectIdx = callLog.indexOf('detect');

        expect(providerIdx).toBeGreaterThanOrEqual(0);
        expect(detectIdx).toBeGreaterThanOrEqual(0);
        expect(providerIdx).toBeLessThan(detectIdx);
    });

    it('should notify VS Code after workspace detection completes', async () => {
        await activate(createFakeExtensionContext());

        // activate() creates one EventEmitter (didChangeEmitter) and fires it
        // after detect() to prompt VS Code to re-query the MCP provider.
        const didChangeEmitter = __emitterInstances[0];
        expect(didChangeEmitter).toBeDefined();
        expect(didChangeEmitter.fire).toHaveBeenCalledTimes(1);
    });
});
