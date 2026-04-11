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
    textDocuments: [] as unknown[],
    openTextDocument: vi.fn(async (uri: unknown) => ({ uri })),
    fs: {
        readFile: vi.fn(),
        writeFile: vi.fn(),
        stat: vi.fn(),
        delete: vi.fn(),
    },
    onDidChangeConfiguration: vi.fn(() => ({ dispose: vi.fn() })),
    onDidChangeWorkspaceFolders: vi.fn(() => ({ dispose: vi.fn() })),
    onDidGrantWorkspaceTrust: vi.fn(() => ({ dispose: vi.fn() })),
    registerTextDocumentContentProvider: vi.fn(() => ({ dispose: vi.fn() })),
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
    createQuickPick: vi.fn(() => createMockQuickPick()),
    createTextEditorDecorationType: vi.fn(() => ({ dispose: vi.fn() })),
    showQuickPick: vi.fn(),
    showInformationMessage: vi.fn(),
    showWarningMessage: vi.fn(),
    showErrorMessage: vi.fn(),
    showTextDocument: vi.fn(),
    onDidChangeActiveTextEditor: vi.fn(() => ({ dispose: vi.fn() })),
    registerTreeDataProvider: vi.fn(() => ({ dispose: vi.fn() })),
    createTreeView: vi.fn(() => ({
        description: undefined as string | undefined,
        onDidChangeCheckboxState: vi.fn(() => ({ dispose: vi.fn() })),
        dispose: vi.fn(),
    })),
    visibleTextEditors: [] as unknown[],
    tabGroups: {
        all: [] as { tabs: unknown[] }[],
        close: vi.fn(),
    },
};

export const commands = {
    registerCommand: vi.fn(),
    executeCommand: vi.fn(),
};

export const languages = {
    registerCodeLensProvider: vi.fn(() => ({ dispose: vi.fn() })),
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
    event = vi.fn(() => ({ dispose: vi.fn() }));
    fire = vi.fn();
    dispose = vi.fn();
}

export class ThemeIcon {
    constructor(public readonly id: string, public readonly color?: ThemeColor) {}
}

export class ThemeColor {
    constructor(public readonly id: string) {}
}

export enum TreeItemCollapsibleState {
    None = 0,
    Collapsed = 1,
    Expanded = 2,
}

export enum TreeItemCheckboxState {
    Unchecked = 0,
    Checked = 1,
}

export class TreeItem {
    label?: string;
    collapsibleState?: TreeItemCollapsibleState;
    iconPath?: unknown;
    description?: string;
    tooltip?: string;
    contextValue?: string;
    command?: { command: string; title: string; arguments?: unknown[] };
    checkboxState?: TreeItemCheckboxState;

    constructor(label: string, collapsibleState?: TreeItemCollapsibleState) {
        this.label = label;
        this.collapsibleState = collapsibleState;
    }
}

export class Range {
    constructor(
        public readonly startLine: number,
        public readonly startCharacter: number,
        public readonly endLine: number,
        public readonly endCharacter: number,
    ) {}
}

export class CodeLens {
    constructor(
        public readonly range: Range,
        public readonly command?: unknown,
    ) {}
}

export interface MockQuickPick {
    title: string;
    placeholder: string;
    canSelectMany: boolean;
    items: unknown[];
    selectedItems: unknown[];
    buttons: unknown[];
    onDidTriggerButton: ReturnType<typeof vi.fn>;
    onDidAccept: ReturnType<typeof vi.fn>;
    onDidHide: ReturnType<typeof vi.fn>;
    onDidChangeSelection: ReturnType<typeof vi.fn>;
    show: ReturnType<typeof vi.fn>;
    dispose: ReturnType<typeof vi.fn>;
    __accept(): void;
    __hide(): void;
    __triggerButton(button: unknown): void;
}

export function createMockQuickPick(): MockQuickPick {
    const acceptListeners: (() => void)[] = [];
    const hideListeners: (() => void)[] = [];
    const buttonListeners: ((b: unknown) => void)[] = [];
    const selectionListeners: ((items: unknown[]) => void)[] = [];

    const qp: MockQuickPick = {
        title: '',
        placeholder: '',
        canSelectMany: false,
        items: [],
        selectedItems: [],
        buttons: [],
        onDidTriggerButton: vi.fn((cb: (b: unknown) => void) => { buttonListeners.push(cb); }),
        onDidAccept: vi.fn((cb: () => void) => { acceptListeners.push(cb); }),
        onDidHide: vi.fn((cb: () => void) => { hideListeners.push(cb); }),
        onDidChangeSelection: vi.fn((cb: (items: unknown[]) => void) => { selectionListeners.push(cb); }),
        show: vi.fn(),
        dispose: vi.fn(),
        __accept() { for (const cb of acceptListeners) cb(); },
        __hide() { for (const cb of hideListeners) cb(); },
        __triggerButton(button: unknown) { for (const cb of buttonListeners) cb(button); },
    };

    // Fire selection listeners when selectedItems is assigned.
    let currentSelection = qp.selectedItems;
    Object.defineProperty(qp, 'selectedItems', {
        get: () => currentSelection,
        set: (value: unknown[]) => {
            currentSelection = value;
            for (const cb of selectionListeners) cb(value);
        },
    });

    return qp;
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
    file: vi.fn((path: string) => ({ path, scheme: 'file', toString: () => `file://${path}` })),
    from: vi.fn((components: { scheme: string; path: string }) => ({
        scheme: components.scheme,
        path: components.path,
        toString: () => `${components.scheme}://${components.path}`,
    })),
    joinPath: vi.fn((base: { path: string }, ...segments: string[]) => {
        const path = [base.path, ...segments].join('/');
        return {
            path,
            scheme: 'file',
            toString: () => `file://${path}`,
        };
    }),
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
