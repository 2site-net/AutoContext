import type { Checker } from '../checker.js';
import { isEditorConfigFilter } from '../editorconfig-filter.js';
import type { EditorConfigReader, McpToolEntry } from '../../editorconfig/editorconfig-reader.js';
import type { Logger } from '../../../core/logger.js';
import { TypeScriptCodingStyleChecker } from './typescript-coding-style-checker.js';

export class TypeScriptChecker implements Checker {
    readonly toolName = 'check_typescript_all';

    constructor(
        private readonly editorConfig: EditorConfigReader,
        private readonly logger: Logger,
    ) {}

    async check(content: string, data?: Record<string, string>): Promise<string> {
        if (!content.trim()) {
            throw new Error('Source code must not be empty or whitespace.');
        }

        this.logger.log('TypeScriptChecker', `Tool invoked: ${this.toolName} | content length: ${content.length}`);

        const checkers: readonly Checker[] = [
            new TypeScriptCodingStyleChecker(),
        ];

        const { editorConfigFilePath, ...restData } = data ?? {};
        const explicitParams = Object.keys(restData).length > 0 ? restData : undefined;

        const entries: McpToolEntry[] = checkers.map(c => {
            const keys = isEditorConfigFilter(c) ? [...c.editorConfigKeys] : undefined;
            return keys ? { name: c.toolName, 'editorconfig-keys': keys } : { name: c.toolName };
        });

        const results = await this.editorConfig.resolveTools(editorConfigFilePath, entries);

        const sections: string[] = [];

        for (const checker of checkers) {
            const result = results?.find(r => r.name === checker.toolName);

            if (result === undefined) {
                sections.push(await checker.check(content, explicitParams));
                continue;
            }

            switch (result.mode) {
                case 'run':
                    sections.push(await checker.check(content, { ...explicitParams, ...result.data }));
                    break;
                case 'editorconfig-only': {
                    const merged = { ...explicitParams, ...result.data, __disabled: 'true' };
                    sections.push(await checker.check(content, merged));
                    break;
                }
                case 'skip':
                    break;
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
