import { describe, it, expect } from 'vitest';
import { platform } from 'node:os';
import { EndpointKind, createInstanceId, formatEndpoint } from '#src/engine/endpoint.js';
import {
    DEFAULT_ENGINE_CONNECT_BUDGET,
    nextRetryDelayMs,
} from '#src/engine/engine-connect-budget.js';
import { resolveEngineBinaryPath } from '#src/engine/engine-locator.js';
import { buildEngineArgv } from '#src/engine/engine-spawner.js';

const HASH = 'E8935F55006A7F3B';
const INSTANCE = '550e8400-e29b-41d4-a716-446655440000';

describe('formatEndpoint', () => {
    it('renders the canonical address', () => {
        expect(formatEndpoint(EndpointKind.Rpc, HASH, INSTANCE))
            .toBe(`autocontext-engine:rpc@${HASH}#${INSTANCE}`);
    });

    it('renders each endpoint kind as its lowercase wire name', () => {
        const kinds = [
            EndpointKind.Rpc,
            EndpointKind.Events,
            EndpointKind.Health,
            EndpointKind.Logs,
        ];

        expect(kinds.map((kind) => formatEndpoint(kind, HASH, INSTANCE).split('@')[0]))
            .toEqual([
                'autocontext-engine:rpc',
                'autocontext-engine:events',
                'autocontext-engine:health',
                'autocontext-engine:logs',
            ]);
    });

    it('rejects a lowercase workspace hash', () => {
        expect(() => formatEndpoint(EndpointKind.Rpc, HASH.toLowerCase(), INSTANCE))
            .toThrow(/workspaceHash/);
    });

    it('rejects a hash of the wrong length', () => {
        expect(() => formatEndpoint(EndpointKind.Rpc, 'ABCD', INSTANCE))
            .toThrow(/workspaceHash/);
    });

    it('rejects an uppercase instance id', () => {
        expect(() => formatEndpoint(EndpointKind.Rpc, HASH, INSTANCE.toUpperCase()))
            .toThrow(/instanceId/);
    });
});

describe('createInstanceId', () => {
    it('mints an id the endpoint formatter accepts', () => {
        expect(() => formatEndpoint(EndpointKind.Rpc, HASH, createInstanceId())).not.toThrow();
    });
});

describe('nextRetryDelayMs', () => {
    it('starts at the initial delay', () => {
        expect(nextRetryDelayMs(DEFAULT_ENGINE_CONNECT_BUDGET, 0))
            .toBe(DEFAULT_ENGINE_CONNECT_BUDGET.initialRetryDelayMs);
    });

    it('grows by the multiplier', () => {
        expect(nextRetryDelayMs(DEFAULT_ENGINE_CONNECT_BUDGET, 50)).toBe(100);
    });

    it('caps at the maximum delay', () => {
        expect(nextRetryDelayMs(DEFAULT_ENGINE_CONNECT_BUDGET, 400))
            .toBe(DEFAULT_ENGINE_CONNECT_BUDGET.maxRetryDelayMs);
    });
});

describe('resolveEngineBinaryPath', () => {
    it('returns an explicit path unchanged', () => {
        expect(resolveEngineBinaryPath({ engineBinaryPath: '/opt/ac/engine-bin' }))
            .toBe('/opt/ac/engine-bin');
    });

    it('resolves through the bundle root with the platform suffix', () => {
        const resolved = resolveEngineBinaryPath({ bundleRoot: '/opt/ac' });
        const expected = platform() === 'win32' ? 'autocontext-engine.exe' : 'autocontext-engine';

        expect(resolved.endsWith(expected)).toBe(true);
        expect(resolved).toContain('engine');
    });
});

describe('buildEngineArgv', () => {
    it('emits the workspace and instance id in order', () => {
        const argv = buildEngineArgv({
            workspacePath: '/w',
            instanceId: INSTANCE,
            engineBinaryPath: '/bin/engine',
        });

        expect(argv).toEqual(['--workspace', '/w', '--instance-id', INSTANCE]);
    });

    it('omits an empty instance label', () => {
        const argv = buildEngineArgv({
            workspacePath: '/w',
            instanceId: INSTANCE,
            instanceLabel: '',
            engineBinaryPath: '/bin/engine',
        });

        expect(argv).not.toContain('--instance-label');
    });

    it('appends the label and idle timeout when supplied', () => {
        const argv = buildEngineArgv({
            workspacePath: '/w',
            instanceId: INSTANCE,
            instanceLabel: 'vscode',
            idleTimeoutSeconds: 90,
            engineBinaryPath: '/bin/engine',
        });

        expect(argv).toEqual([
            '--workspace', '/w',
            '--instance-id', INSTANCE,
            '--instance-label', 'vscode',
            '--idle-timeout', '90',
        ]);
    });

    it('renders the idle timeout as whole seconds', () => {
        const argv = buildEngineArgv({
            workspacePath: '/w',
            instanceId: INSTANCE,
            idleTimeoutSeconds: 12.7,
            engineBinaryPath: '/bin/engine',
        });

        expect(argv.at(-1)).toBe('12');
    });
});
