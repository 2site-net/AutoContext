import { vi } from 'vitest';
import { workspace } from './fake-vscode';
import { fakeUri } from './fake-uri';

export function stubFindFiles(mapping: Record<string, string[]>): void {
    (workspace.findFiles as ReturnType<typeof vi.fn>).mockImplementation(
        async (pattern: unknown) => (mapping[pattern as string] ?? []).map(f => fakeUri(f)),
    );
}

export function stubReadFile(mapping: Record<string, string>): void {
    const encoder = new TextEncoder();
    (workspace.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async (uri: unknown) => {
        const content = mapping[(uri as { path: string }).path];
        return content !== undefined ? encoder.encode(content) : new Uint8Array();
    });
}
