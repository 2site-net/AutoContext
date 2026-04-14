import type { Checker } from '../../features/checkers/checker.js';
import { CompositeChecker } from '../../features/checkers/composite-checker.js';
import { TypeScriptCodingStyleChecker } from './typescript-coding-style-checker.js';

export class TypeScriptChecker extends CompositeChecker {
    readonly toolName = 'check_typescript_all';

    protected readonly toolLabel = 'TypeScript';

    protected createCheckers(): readonly Checker[] {
        return [new TypeScriptCodingStyleChecker()];
    }
}
