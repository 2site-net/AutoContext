import type { Checker } from './checker.js';

export interface EditorConfigFilter {
    readonly editorConfigKeys: readonly string[];
}

export function isEditorConfigFilter(checker: Checker): checker is Checker & EditorConfigFilter {
    return 'editorConfigKeys' in checker
        && Array.isArray((checker as Checker & EditorConfigFilter).editorConfigKeys);
}
