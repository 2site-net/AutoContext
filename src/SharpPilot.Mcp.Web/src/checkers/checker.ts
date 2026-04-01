export interface Checker {
    readonly toolName: string;
    check(content: string, data?: Record<string, string>): string | Promise<string>;
}

export interface EditorConfigFilter {
    readonly editorConfigKeys: readonly string[];
}
