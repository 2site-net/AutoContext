import type { Checker } from '../checker.js';
import { isEditorConfigFilter } from '../editorconfig-filter.js';
import type { ToolsStatusConfig } from '../../../configuration/tools-status-config.js';
import type { EditorConfigReader } from '../../editorconfig/editorconfig-reader.js';
import type { Logger } from '../../../core/logger.js';
import { TypeScriptCodingStyleChecker } from './typescript-coding-style-checker.js';

function hasAnyEditorConfigKey(
    data: Record<string, string> | undefined,
    checker: Checker,
): boolean {
    if (!data || !isEditorConfigFilter(checker)) {
        return false;
    }

    return checker.editorConfigKeys.some(key => key in data);
}

export class TypeScriptChecker implements Checker {
    readonly toolName = 'check_typescript_all';

    constructor(
        private readonly config: ToolsStatusConfig,
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
        const allKeys = checkers
            .filter(isEditorConfigFilter)
            .flatMap(c => c.editorConfigKeys);
        const properties = await this.editorConfig.resolve(editorConfigFilePath, allKeys);

        const mergedData: Record<string, string> | undefined =
            properties ?? Object.keys(restData).length > 0
                ? { ...restData, ...properties }
                : undefined;

        const sections: string[] = [];

        for (const checker of checkers) {
            if (this.config.isEnabled(checker.toolName)) {
                sections.push(await checker.check(content, mergedData));
            } else if (hasAnyEditorConfigKey(mergedData, checker)) {
                const disabledData = { ...mergedData, __disabled: 'true' };
                sections.push(await checker.check(content, disabledData));
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
