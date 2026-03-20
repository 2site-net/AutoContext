import { vi } from 'vitest';

const configStore: Record<string, unknown> = {};

export const workspace = {
    getConfiguration: vi.fn(() => ({
        get: vi.fn(<T>(key: string, defaultValue?: T): T | undefined =>
            key in configStore ? configStore[key] as T : defaultValue),
        update: vi.fn(),
    })),
    createFileSystemWatcher: vi.fn(() => ({
        onDidCreate: vi.fn(),
        onDidChange: vi.fn(),
        onDidDelete: vi.fn(),
        dispose: vi.fn(),
    })),
    findFiles: vi.fn(async () => []),
    workspaceFolders: undefined as unknown[] | undefined,
    fs: {
        readFile: vi.fn(),
        writeFile: vi.fn(),
        stat: vi.fn(),
    },
    onDidChangeConfiguration: vi.fn(),
};

export const window = {
    createStatusBarItem: vi.fn(() => ({
        name: '',
        text: '',
        tooltip: '',
        command: '',
        show: vi.fn(),
        dispose: vi.fn(),
    })),
    showQuickPick: vi.fn(),
    showInformationMessage: vi.fn(),
    showWarningMessage: vi.fn(),
    showErrorMessage: vi.fn(),
    showTextDocument: vi.fn(),
};

export const commands = {
    registerCommand: vi.fn(),
    executeCommand: vi.fn(),
};

export const lm = {
    registerMcpServerDefinitionProvider: vi.fn(),
};

export enum StatusBarAlignment {
    Left = 1,
    Right = 2,
}

export enum ConfigurationTarget {
    Global = 1,
    Workspace = 2,
    WorkspaceFolder = 3,
}

export enum QuickPickItemKind {
    Separator = -1,
    Default = 0,
}

export enum ViewColumn {
    Active = -1,
}

export class EventEmitter {
    event = vi.fn();
    fire = vi.fn();
    dispose = vi.fn();
}

export class McpStdioServerDefinition {
    constructor(
        public label: string,
        public command: string,
        public args?: string[],
        public env?: Record<string, string>,
        public version?: string,
    ) {}
}

export const Uri = {
    file: vi.fn((path: string) => ({ path, scheme: 'file' })),
    joinPath: vi.fn((base: { path: string }, ...segments: string[]) => ({
        path: [base.path, ...segments].join('/'),
        scheme: 'file',
    })),
};

/**
 * Preload config values for `workspace.getConfiguration().get()`.
 */
export function __setConfigStore(values: Record<string, unknown>): void {
    for (const key of Object.keys(configStore)) {
        delete configStore[key];
    }
    Object.assign(configStore, values);
}
