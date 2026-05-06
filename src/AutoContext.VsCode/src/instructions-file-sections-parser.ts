import type { InstructionsFileSectionWithOffsets } from '#types/instructions-file-section-with-offsets.js';

const HEADING_PATTERN = /^(#{2,3}) +(.+?)\s*$/;
const FENCE_PATTERN = /^```/;

/**
 * Parses the section index from a normalized instructions-file body
 * (frontmatter already stripped). Pure: no I/O, no validation, no throws.
 *
 * Recognizes only `##` and `###` headings; deeper levels and headings inside
 * fenced code blocks are ignored. Anchors are GitHub-slug style; `###`
 * anchors are prefixed with the parent `##` slug. Duplicate anchors are
 * **not** disambiguated here — callers that care (e.g. the build-time
 * generator) detect duplicates against the returned array.
 *
 * Each returned section carries body-relative offsets `[charStart, charEnd)`.
 * `level` is intentionally omitted; callers derive it as `parent ? 3 : 2`.
 */
export class InstructionsFileSectionsParser {
    static parse(body: string): readonly InstructionsFileSectionWithOffsets[] {
        interface RawHeading {
            level: 2 | 3;
            heading: string;
            charStart: number;
            parent?: string;
        }

        const lines = body.split('\n');
        const raw: RawHeading[] = [];
        let offset = 0;
        let inFence = false;
        let lastH2: string | undefined;

        for (const line of lines) {
            if (FENCE_PATTERN.test(line)) {
                inFence = !inFence;
                offset += line.length + 1;
                continue;
            }
            if (!inFence) {
                const match = HEADING_PATTERN.exec(line);
                if (match) {
                    const level = match[1].length as 2 | 3;
                    const heading = match[2].trim();
                    if (level === 2) {
                        raw.push({ level, heading, charStart: offset });
                        lastH2 = heading;
                    } else {
                        raw.push({ level, heading, charStart: offset, parent: lastH2 });
                    }
                }
            }
            offset += line.length + 1;
        }

        const sections: InstructionsFileSectionWithOffsets[] = [];
        for (let i = 0; i < raw.length; i++) {
            const r = raw[i];
            const charEnd = InstructionsFileSectionsParser.computeCharEnd(raw, i, body.length);
            const baseSlug = InstructionsFileSectionsParser.slugify(r.heading);
            const anchor = r.parent
                ? `${InstructionsFileSectionsParser.slugify(r.parent)}-${baseSlug}`
                : baseSlug;

            sections.push({
                heading: r.heading,
                anchor,
                ...(r.parent !== undefined ? { parent: r.parent } : {}),
                charStart: r.charStart,
                charEnd,
            });
        }
        return sections;
    }

    private static computeCharEnd(
        raw: ReadonlyArray<{ readonly level: 2 | 3; readonly charStart: number }>,
        index: number,
        bodyLength: number,
    ): number {
        const current = raw[index];
        for (let j = index + 1; j < raw.length; j++) {
            if (raw[j].level <= current.level) {
                return raw[j].charStart;
            }
        }
        return bodyLength;
    }

    private static slugify(heading: string): string {
        return heading
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '');
    }
}
