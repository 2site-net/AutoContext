import { vi } from 'vitest';

export function makeDocument(scheme: string, path: string): import('vscode').TextDocument {
    return { uri: { scheme, path } } as unknown as import('vscode').TextDocument;
}

export function makeEditor(scheme: string, path: string): import('vscode').TextEditor {
    return {
        document: { uri: { scheme, path } },
        setDecorations: vi.fn(),
    } as unknown as import('vscode').TextEditor;
}
