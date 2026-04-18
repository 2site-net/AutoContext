import { vi } from 'vitest';

export function createFakeOutputChannel(): import('vscode').OutputChannel {
    return { appendLine: vi.fn(), dispose: vi.fn() } as unknown as import('vscode').OutputChannel;
}
