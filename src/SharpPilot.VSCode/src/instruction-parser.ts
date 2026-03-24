import { createHash } from 'node:crypto';

export interface ParsedInstruction {
    readonly text: string;
    readonly hash: string;
    readonly startLine: number;
    readonly endLine: number;
}

const instructionBulletPattern = /^[-*]\s\*\*(Do|Don't)\*\*/;

export function parseInstructions(content: string): readonly ParsedInstruction[] {
    const lines = content.split('\n');
    const instructions: ParsedInstruction[] = [];
    let instructionStart = -1;
    let instructionLines: string[] = [];

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        if (instructionBulletPattern.test(line)) {
            if (instructionStart >= 0) {
                instructions.push(buildInstruction(instructionLines, instructionStart, i - 1));
            }
            instructionStart = i;
            instructionLines = [line];
        } else if (instructionStart >= 0) {
            // Continuation lines: indented or blank lines within an instruction.
            // A non-indented, non-blank line that isn't a new instruction ends the current one.
            if (line === '' || /^\s+/.test(line)) {
                instructionLines.push(line);
            } else {
                instructions.push(buildInstruction(instructionLines, instructionStart, i - 1));
                instructionStart = -1;
                instructionLines = [];
            }
        }
    }

    if (instructionStart >= 0) {
        instructions.push(buildInstruction(instructionLines, instructionStart, lines.length - 1));
    }

    return instructions;
}

function buildInstruction(lines: readonly string[], startLine: number, endLine: number): ParsedInstruction {
    // Trim trailing blank lines from the instruction without mutating the input.
    let end = lines.length;
    while (end > 0 && lines[end - 1].trim() === '') {
        end--;
        endLine--;
    }

    const text = lines.slice(0, end).join('\n');
    const normalized = text.replace(/\s+/g, ' ').trim();
    const hash = createHash('sha256').update(normalized).digest('hex').slice(0, 12);

    return { text, hash, startLine, endLine };
}

export function hashInstruction(text: string): string {
    const normalized = text.replace(/\s+/g, ' ').trim();
    return createHash('sha256').update(normalized).digest('hex').slice(0, 12);
}
