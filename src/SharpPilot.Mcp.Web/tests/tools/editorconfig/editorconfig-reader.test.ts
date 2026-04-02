import { describe, test, expect, beforeAll, afterAll } from 'vitest';
import { type Server } from 'node:net';
import { randomUUID } from 'node:crypto';
import { EditorConfigReader } from '../../../src/tools/editorconfig/editorconfig-reader.js';
import { createFakeWorkspaceServer } from '../../fakes/fake-workspace-server.js';

describe('EditorConfigReader (pipe client)', () => {
    const pipeName = `ec-test-${randomUUID().replace(/-/g, '').slice(0, 12)}`;
    let mockServer: Server;
    let reader: EditorConfigReader;

    beforeAll(async () => {
        mockServer = createFakeWorkspaceServer(pipeName);
        reader = new EditorConfigReader(pipeName);

        await new Promise<void>((resolve) => {
            mockServer.once('listening', resolve);
        });
    });

    afterAll(async () => {
        await new Promise<void>((resolve, reject) => {
            mockServer.close((err) => (err ? reject(err) : resolve()));
        });
    });

    describe('read', () => {
        test('should return formatted properties for matching file', async () => {
            const result = await reader.read('/repo/src/index.ts');

            expect(result).toContain('indent_style = space');
            expect(result).toContain('indent_size = 2');
        });

        test('should return warning for non-matching file', async () => {
            const result = await reader.read('/repo/src/file.py');

            expect(result).toMatch(/^⚠️/);
            expect(result).toContain('No .editorconfig properties');
        });

        test('should throw on empty path', async () => {
            await expect(reader.read('')).rejects.toThrow(Error);
            await expect(reader.read('   ')).rejects.toThrow(Error);
        });
    });

    describe('resolve', () => {
        test('should return undefined when path is undefined', async () => {
            expect(await reader.resolve(undefined)).toBeUndefined();
        });

        test('should return undefined when path is empty', async () => {
            expect(await reader.resolve('')).toBeUndefined();
        });

        test('should return undefined when no properties apply', async () => {
            expect(await reader.resolve('/repo/file.py')).toBeUndefined();
        });

        test('should return properties as a record', async () => {
            const result = await reader.resolve('/repo/index.ts');

            expect(result).toBeDefined();
            expect(result!['indent_style']).toBe('space');
            expect(result!['indent_size']).toBe('2');
        });

        test('should filter by keys when provided', async () => {
            const result = await reader.resolve('/repo/index.ts', ['indent_style']);

            expect(result).toBeDefined();
            expect(result!['indent_style']).toBe('space');
            expect(result!['indent_size']).toBeUndefined();
        });
    });
});
