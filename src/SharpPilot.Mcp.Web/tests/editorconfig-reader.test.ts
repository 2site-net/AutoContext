import { describe, test, expect, beforeEach, afterEach } from 'vitest';
import { writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { read, resolve } from '../src/editorconfig-reader.js';

describe('EditorConfigReader', () => {
    let tempRoot: string;

    beforeEach(() => {
        tempRoot = join(tmpdir(), `ec-test-${Date.now()}-${Math.random().toString(36).slice(2)}`);
        mkdirSync(tempRoot, { recursive: true });
    });

    afterEach(() => {
        rmSync(tempRoot, { recursive: true, force: true });
    });

    describe('read', () => {
        test('should return warning when no editorconfig exists', () => {
            writeFileSync(join(tempRoot, '.editorconfig'), 'root = true');

            const result = read(join(tempRoot, 'file.ts'));

            expect(result).toMatch(/^⚠️/);
            expect(result).toContain('No .editorconfig properties');
        });

        test('should resolve properties from matching section', () => {
            writeFileSync(
                join(tempRoot, '.editorconfig'),
                [
                    'root = true',
                    '',
                    '[*.ts]',
                    'indent_style = space',
                    'indent_size = 4',
                ].join('\n'),
            );

            const result = read(join(tempRoot, 'index.ts'));

            expect(result).toContain('indent_style = space');
            expect(result).toContain('indent_size = 4');
        });

        test('should not include properties from non-matching section', () => {
            writeFileSync(
                join(tempRoot, '.editorconfig'),
                [
                    'root = true',
                    '',
                    '[*.py]',
                    'indent_style = tab',
                ].join('\n'),
            );

            const result = read(join(tempRoot, 'file.ts'));

            expect(result).toMatch(/^⚠️/);
        });

        test('should cascade child over parent', () => {
            const child = join(tempRoot, 'src');
            mkdirSync(child, { recursive: true });

            writeFileSync(
                join(tempRoot, '.editorconfig'),
                [
                    'root = true',
                    '',
                    '[*.ts]',
                    'indent_size = 4',
                    'charset = utf-8',
                ].join('\n'),
            );

            writeFileSync(
                join(child, '.editorconfig'),
                [
                    '[*.ts]',
                    'indent_size = 2',
                ].join('\n'),
            );

            const result = read(join(child, 'app.ts'));

            expect(result).toContain('indent_size = 2');
            expect(result).toContain('charset = utf-8');
        });

        test('should throw on empty path', () => {
            expect(() => read('')).toThrow(Error);
            expect(() => read('   ')).toThrow(Error);
        });
    });

    describe('resolve', () => {
        test('should return undefined when path is undefined', () => {
            expect(resolve(undefined)).toBeUndefined();
        });

        test('should return undefined when path is empty', () => {
            expect(resolve('')).toBeUndefined();
        });

        test('should return undefined when no properties apply', () => {
            writeFileSync(join(tempRoot, '.editorconfig'), 'root = true');

            expect(resolve(join(tempRoot, 'file.ts'))).toBeUndefined();
        });

        test('should return properties as a record', () => {
            writeFileSync(
                join(tempRoot, '.editorconfig'),
                [
                    'root = true',
                    '',
                    '[*.ts]',
                    'indent_style = space',
                    'indent_size = 2',
                ].join('\n'),
            );

            const result = resolve(join(tempRoot, 'index.ts'));

            expect(result).toBeDefined();
            expect(result!['indent_style']).toBe('space');
            expect(result!['indent_size']).toBe('2');
        });
    });
});
