import type { Checker, EditorConfigFilter } from '../checker.js';
import { isEnabled } from '../../tools-status-config.js';
import { resolve } from '../../editorconfig-reader.js';
import { TypeScriptCodingStyleChecker } from './typescript-coding-style-checker.js';

export class TypeScriptChecker implements Checker {
    readonly toolName = 'check_typescript_all';

    async check(content: string, data?: Record<string, string>): Promise<string> {
        if (!content.trim()) {
            throw new Error('Source code must not be empty or whitespace.');
        }

        const checkers: readonly Checker[] = [
            new TypeScriptCodingStyleChecker(),
        ];

        const { editorConfigFilePath, ...restData } = data ?? {};
        const allKeys = checkers
            .filter((c): c is Checker & EditorConfigFilter => 'editorConfigKeys' in c)
            .flatMap(c => c.editorConfigKeys);
        const properties = await resolve(editorConfigFilePath, allKeys);

        const mergedData: Record<string, string> | undefined =
            properties ?? Object.keys(restData).length > 0
                ? { ...restData, ...properties }
                : undefined;

        const sections: string[] = [];

        for (const checker of checkers) {
            if (isEnabled(checker.toolName)) {
                sections.push(await checker.check(content, mergedData));
            }
        }

        if (sections.length === 0) {
            return '⚠️ All TypeScript checks are disabled.';
        }

        const failures = sections.filter(s => s.startsWith('❌'));

        if (failures.length === 0) {
            return '✅ All enabled TypeScript checks passed.';
        }

        return failures.join('\n\n');
    }
}
