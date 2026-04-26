export interface InstructionsFileParsedRule {
    readonly id: string | undefined;
    readonly text: string;
    readonly startLine: number;
    readonly endLine: number;
}
