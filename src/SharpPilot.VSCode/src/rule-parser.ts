import { createHash } from 'node:crypto';

export interface ParsedRule {
    readonly text: string;
    readonly hash: string;
    readonly startLine: number;
    readonly endLine: number;
}

const ruleBulletPattern = /^[-*]\s\*\*(Do|Don't)\*\*/;

export function parseRules(content: string): readonly ParsedRule[] {
    const lines = content.split('\n');
    const rules: ParsedRule[] = [];
    let ruleStart = -1;
    let ruleLines: string[] = [];

    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        if (ruleBulletPattern.test(line)) {
            if (ruleStart >= 0) {
                rules.push(buildRule(ruleLines, ruleStart, i - 1));
            }
            ruleStart = i;
            ruleLines = [line];
        } else if (ruleStart >= 0) {
            // Continuation lines: indented or blank lines within a rule.
            // A non-indented, non-blank line that isn't a new rule ends the current rule.
            if (line === '' || /^\s+/.test(line)) {
                ruleLines.push(line);
            } else {
                rules.push(buildRule(ruleLines, ruleStart, i - 1));
                ruleStart = -1;
                ruleLines = [];
            }
        }
    }

    if (ruleStart >= 0) {
        rules.push(buildRule(ruleLines, ruleStart, lines.length - 1));
    }

    return rules;
}

function buildRule(lines: readonly string[], startLine: number, endLine: number): ParsedRule {
    // Trim trailing blank lines from the rule without mutating the input.
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

export function hashRule(text: string): string {
    const normalized = text.replace(/\s+/g, ' ').trim();
    return createHash('sha256').update(normalized).digest('hex').slice(0, 12);
}
