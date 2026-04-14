import { describe, it, expect } from 'vitest';
import { CompositeChecker } from '../../../src/features/checkers/composite-checker.js';
import type { Checker } from '../../../src/features/checkers/checker.js';
import type { EditorConfigFilter } from '../../../src/features/checkers/editorconfig-filter.js';
import type { WorkspaceServerClient } from '../../../src/features/workspace-server/workspace-server-client.js';
import type { McpToolsResponse } from '../../../src/features/workspace-server/mcp-tools/mcp-tools-response.js';

describe('CompositeChecker', () => {
    it('should let editorconfig override explicit params on conflict', async () => {
        const spy = createSpyChecker(['indent_size']);

        const checker = createCompositeChecker([spy], {
            tools: { [spy.toolName]: true },
            editorconfig: { indent_size: '4' },
        });

        // Explicit param says indent_size = 2, editorconfig says 4
        await checker.check('class C {}', {
            filePath: '/src/file.cs',
            indent_size: '2',
        });

        expect.soft(spy.receivedData).toBeDefined();
        expect.soft(spy.receivedData!['indent_size']).toBe('4');
    });

    it('should merge editorconfig with explicit params when no conflict', async () => {
        const spy = createSpyChecker(['indent_size']);

        const checker = createCompositeChecker([spy], {
            tools: { [spy.toolName]: true },
            editorconfig: { indent_size: '4' },
        });

        await checker.check('class C {}', {
            filePath: '/src/file.cs',
            custom_param: 'hello',
        });

        expect.soft(spy.receivedData).toBeDefined();
        expect.soft(spy.receivedData!['indent_size']).toBe('4');
        expect.soft(spy.receivedData!['custom_param']).toBe('hello');
    });
});

interface SpyChecker extends Checker, EditorConfigFilter {
    receivedData: Record<string, string> | undefined;
}

function createSpyChecker(editorConfigKeys: string[]): SpyChecker {
    return {
        toolName: 'spy_checker',
        editorConfigKeys,
        receivedData: undefined,
        check(_content: string, data?: Record<string, string>): string {
            this.receivedData = data;
            return '✅ Spy passed.';
        },
    };
}

function createCompositeChecker(
    checkers: readonly Checker[],
    resolved: McpToolsResponse,
): CompositeChecker {
    const fakeClient = {
        resolveTools: async () => resolved,
        sendLog: async () => {},
    } as unknown as WorkspaceServerClient;

    return new (class extends CompositeChecker {
        readonly toolName = 'test_composite';
        protected readonly toolLabel = 'Test';
        protected createCheckers() { return checkers; }
    })(fakeClient);
}
