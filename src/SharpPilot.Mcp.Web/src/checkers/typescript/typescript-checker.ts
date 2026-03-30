import type { Checker } from '../checker.js';
import { isEnabled } from '../../tools-status-config.js';
import { TypeScriptCodingStyleChecker } from './typescript-coding-style-checker.js';

export class TypeScriptChecker implements Checker {
    readonly toolName = 'check_typescript_all';

    check(content: string): string {
        if (!content.trim()) {
            throw new Error('Source code must not be empty or whitespace.');
        }

        const checkers: readonly Checker[] = [
            new TypeScriptCodingStyleChecker(),
        ];

        const sections: string[] = [];

        for (const checker of checkers) {
            if (isEnabled(checker.toolName)) {
                sections.push(checker.check(content));
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
