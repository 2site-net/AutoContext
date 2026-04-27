import { vi } from 'vitest';
import { commands } from '#testing/fakes/fake-vscode';

export function findSetContextCall(key: string): [string, string, boolean] | undefined {
    return vi.mocked(commands.executeCommand).mock.calls
        .find(c => c[0] === 'setContext' && c[1] === key) as [string, string, boolean] | undefined;
}
