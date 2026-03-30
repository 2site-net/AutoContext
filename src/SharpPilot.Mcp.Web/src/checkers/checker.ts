export interface Checker {
    readonly toolName: string;
    check(content: string): string;
}
