export interface InstructionsFileParsedSpan {
    readonly id: string | undefined;
    readonly text: string;
    readonly startLine: number;
    readonly endLine: number;
}
