import { readFileSync, statSync } from 'node:fs';
import type { InstructionsParsedInstruction } from './instructions-parsed-instruction.js';
import type { InstructionsDiagnostic } from './instructions-diagnostic.js';
import type { InstructionsParsedResult } from './instructions-parsed-result.js';
import type { InstructionsFileParsedResult } from './instructions-file-parsed-result.js';

const instructionBulletPattern = /^[-*]\s(?:\[(INST\d{4})\]\s*)?\*\*(Do|Don't)\*\*/;
const malformedIdPattern = /^[-*]\s\[(?!INST\d{4}\])[^\]]*\]\s*\*\*(Do|Don't)\*\*/;

export class InstructionsParser {
    private static readonly fileCache = new Map<string, { mtimeMs: number; content: string; result: InstructionsParsedResult }>();

    static fromFile(filePath: string): InstructionsFileParsedResult {
        const mtimeMs = statSync(filePath).mtimeMs;
        const cached = this.fileCache.get(filePath);
        if (cached && cached.mtimeMs === mtimeMs) {
            return { content: cached.content, result: cached.result };
        }
        const content = readFileSync(filePath, 'utf-8');
        const result = this.parse(content);
        this.fileCache.set(filePath, { mtimeMs, content, result });
        return { content, result };
    }

    static parse(content: string): InstructionsParsedResult {
        const lines = content.split('\n');
        const instructions: InstructionsParsedInstruction[] = [];
        const diagnostics: InstructionsDiagnostic[] = [];
        const seenIds = new Map<string, number>();
        let instructionStart = -1;
        let instructionLines: string[] = [];
        let instructionId: string | undefined;

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const match = instructionBulletPattern.exec(line);

            if (match) {
                if (instructionStart >= 0) {
                    instructions.push(this.buildInstruction(instructionId, instructionLines, instructionStart, i - 1));
                }

                instructionId = match[1] as string | undefined;
                instructionStart = i;
                instructionLines = [line];

                if (instructionId) {
                    const firstLine = seenIds.get(instructionId);

                    if (firstLine !== undefined) {
                        diagnostics.push({
                            kind: 'duplicate-id',
                            line: i,
                            message: `Duplicate instruction ID: ${instructionId} (first seen at line ${firstLine + 1})`,
                        });
                    } else {
                        seenIds.set(instructionId, i);
                    }
                } else {
                    diagnostics.push({
                        kind: 'missing-id',
                        line: i,
                        message: 'Instruction has no ID (unfilterable)',
                    });
                }
            } else if (malformedIdPattern.test(line)) {
                // Line has a bracket tag but didn't match the instruction pattern —
                // still flag the malformed ID so authors notice typos.
                const bracket = line.match(/\[([^\]]*)\]/)![1];
                diagnostics.push({
                    kind: 'malformed-id',
                    line: i,
                    message: `Malformed instruction ID: [${bracket}]`,
                });
            } else if (instructionStart >= 0) {
                if (line === '' || /^\s+/.test(line)) {
                    instructionLines.push(line);
                } else {
                    instructions.push(this.buildInstruction(instructionId, instructionLines, instructionStart, i - 1));
                    instructionStart = -1;
                    instructionLines = [];
                    instructionId = undefined;
                }
            }
        }

        if (instructionStart >= 0) {
            instructions.push(this.buildInstruction(instructionId, instructionLines, instructionStart, lines.length - 1));
        }

        return { instructions, diagnostics };
    }

    private static buildInstruction(
        id: string | undefined,
        lines: readonly string[],
        startLine: number,
        endLine: number,
    ): InstructionsParsedInstruction {
        let end = lines.length;

        while (end > 0 && lines[end - 1].trim() === '') {
            end--;
            endLine--;
        }

        const text = lines.slice(0, end).join('\n');

        return { id, text, startLine, endLine };
    }
}
