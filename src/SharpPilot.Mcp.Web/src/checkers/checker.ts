export interface Checker {
    readonly toolName: string;
    check(content: string, data?: Record<string, string>): string;
}
