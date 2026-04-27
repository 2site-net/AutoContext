import { readFile, stat } from 'node:fs/promises';
import type { InstructionsFileParsedSpan } from '#types/instructions-file-parsed-span.js';
import type { InstructionsFileParserDiagnostic } from '#types/instructions-file-parser-diagnostic.js';
import type { InstructionsFileParsedResult, InstructionsFileParsedFrontmatter } from '#types/instructions-file-parsed-result.js';
import type { InstructionsFileParsedCachedResult } from '#types/instructions-file-parsed-cached-result.js';
import { SemVer } from './semver.js';

const instructionBulletPattern = /^[-*]\s(?:\[(INST\d{4})\]\s*)?\*\*(Do|Don't)\*\*/;
const malformedIdPattern = /^[-*]\s\[(?!INST\d{4}\])[^\]]*\]\s*\*\*(Do|Don't)\*\*/;
const frontmatterPattern = /^---\r?\n([\s\S]*?)\r?\n---/;
const frontmatterFieldPattern = /^(\w+):\s*"?([^"\r\n]*)"?\s*$/;

export class InstructionsFileParser {
    private static readonly fileCache = new Map<string, { mtimeMs: number; content: string; result: InstructionsFileParsedResult }>();

    static async fromFile(filePath: string): Promise<InstructionsFileParsedCachedResult> {
        const mtimeMs = (await stat(filePath)).mtimeMs;
        const cached = this.fileCache.get(filePath);
        if (cached && cached.mtimeMs === mtimeMs) {
            return { content: cached.content, result: cached.result };
        }
        const content = await readFile(filePath, 'utf-8');
        const result = this.parse(content);
        this.fileCache.set(filePath, { mtimeMs, content, result });
        return { content, result };
    }

    static parse(content: string): InstructionsFileParsedResult {
        const frontmatter = this.parseFrontmatter(content);
        const lines = content.split('\n');
        const instructions: InstructionsFileParsedSpan[] = [];
        const diagnostics: InstructionsFileParserDiagnostic[] = [];
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

        return { frontmatter, instructions, diagnostics };
    }

    static parseFrontmatter(content: string): InstructionsFileParsedFrontmatter {
        const match = frontmatterPattern.exec(content);
        if (!match) {
            return {};
        }

        const result: Record<string, string> = {};
        for (const line of match[1].split('\n')) {
            const fieldMatch = frontmatterFieldPattern.exec(line.trim());
            if (fieldMatch) {
                result[fieldMatch[1]] = fieldMatch[2];
            }
        }

        // Version is embedded in the name field as "(vX.Y.Z)" suffix to avoid
        // VS Code's "unsupported attribute" diagnostic on a bare version field.
        const version = result['name'] ? SemVer.fromParentheses(result['name']) : undefined;

        return {
            description: result['description'],
            version,
        };
    }

    private static buildInstruction(
        id: string | undefined,
        lines: readonly string[],
        startLine: number,
        endLine: number,
    ): InstructionsFileParsedSpan {
        let end = lines.length;

        while (end > 0 && lines[end - 1].trim() === '') {
            end--;
            endLine--;
        }

        const text = lines.slice(0, end).join('\n');

        return { id, text, startLine, endLine };
    }
}
