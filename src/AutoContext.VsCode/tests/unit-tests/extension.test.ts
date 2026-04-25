import { describe, it, expect, vi, beforeEach } from 'vitest';
import { lm, workspace, __emitterInstances } from './_fakes/fake-vscode';

const { callLog } = vi.hoisted(() => ({ callLog: [] as string[] }));

// ── Module mocks (hoisted) ──────────────────────────────────────────

vi.mock('node:fs', () => ({ existsSync: () => false }));

vi.mock('../../src/metadata-loader', () => ({
    MetadataLoader: class {
        getMcpToolsInfo() { return []; }
        getInstructionsInfo() { return []; }
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

vi.mock('../../src/mcp-tools-catalog', () => ({
    McpToolsCatalog: class { all = []; },
}));

vi.mock('../../src/instructions-catalog', () => ({
    InstructionsCatalog: class { all = []; findByFileName() {} },
}));

vi.mock('../../src/instructions-exporter', () => ({
    InstructionsExporter: class {},
}));

vi.mock('../../src/autocontext-config', () => ({
    AutoContextConfigManager: class {
        async read() { return {}; }
        readSync() { return {}; }
        async removeOrphanedIds() {}
        async clearStaleDisabledIds() { return []; }
        onDidChange = () => ({ dispose() {} });
        dispose() {}
    },
}));

vi.mock('../../src/instructions-content-provider', () => ({
    InstructionsContentProvider: class { dispose() {} },
    instructionScheme: 'autocontext-instructions',
}));

vi.mock('../../src/instructions-codelens-provider', () => ({
    InstructionsCodeLensProvider: class { dispose() {} },
}));

vi.mock('../../src/instructions-decoration-manager', () => ({
    InstructionsDecorationManager: class { dispose() {} },
}));

vi.mock('../../src/instructions-config-writer', () => ({
    InstructionsConfigWriter: class {
        async removeOrphanedStagingDirs() {}
        async write() {}
        dispose() {}
    },
}));

vi.mock('../../src/instructions-diagnostics', () => ({
    InstructionsDiagnostics: { log: async () => {} },
}));

vi.mock('../../src/instructions-tree-provider', () => {
    class InstructionsTreeProvider {
        showNotDetected = false;
        dispose() {}
        enableInstruction() {}
        disableInstruction() {}
        static deleteOverride() {}
        static showOriginal() {}
    }
    return { InstructionsTreeProvider };
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

vi.mock('../../src/servers-manifest', () => ({
    ServersManifest: class {
        static load() { return new this(); }
        workers() { return []; }
    },
}));

vi.mock('../../src/worker-manager', () => ({
    WorkerManager: class { start() {} dispose() {} whenWorkspaceReady() { return Promise.resolve(); } getEndpointSuffix() { return 'test'; } },
}));

vi.mock('../../src/auto-configurer', () => ({
    AutoConfigurer: class { async run() {} },
}));

// ── SUT ─────────────────────────────────────────────────────────────

import { activate } from '../../src/extension';
import { createFakeExtensionContext } from './_fakes';

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
