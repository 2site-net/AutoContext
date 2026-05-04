export interface InstructionsFileSection {
    readonly heading: string;
    readonly level: 2 | 3;
    readonly anchor: string;
    readonly parent?: string;
    readonly charStart: number;
    readonly charEnd: number;
}
