import { describe, it, expect } from 'vitest';
import { InstructionsFileSectionsParser } from '#src/instructions-file-sections-parser';

describe('InstructionsFileSectionsParser.parse', () => {
    it('should extract a single ## section spanning the whole body', () => {
        const body = '## Only\n\nbody\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections).toHaveLength(1);
        expect(sections[0]).toMatchObject({ heading: 'Only', anchor: 'only' });
        expect(sections[0].parent).toBeUndefined();
        expect(sections[0].charStart).toBe(0);
        expect(sections[0].charEnd).toBe(body.length);
    });

    it('should attribute ### sections to nearest preceding ## as parent and prefix anchors', () => {
        const body = '## Naming\n\n### Types\n\nbody\n### Members\n\nbody\n## Other\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections.map(s => [s.heading, s.parent, s.anchor])).toEqual([
            ['Naming', undefined, 'naming'],
            ['Types', 'Naming', 'naming-types'],
            ['Members', 'Naming', 'naming-members'],
            ['Other', undefined, 'other'],
        ]);
    });

    it('should ignore #### and deeper headings', () => {
        const body = '## Top\n\n#### Skipped\n\n##### Skipped Too\n\n## After\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections.map(s => s.heading)).toEqual(['Top', 'After']);
    });

    it('should ignore headings inside fenced code blocks', () => {
        const body = '## Real\n\n```md\n## Not A Heading\n```\n\n## After\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections.map(s => s.heading)).toEqual(['Real', 'After']);
    });

    it('should not throw on duplicate anchors (caller validates)', () => {
        const body = '## Same\n\n## Same\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections.map(s => s.anchor)).toEqual(['same', 'same']);
    });

    it('should produce ascending offsets where each section ends where the next begins', () => {
        const body = '## A\n\nalpha\n## B\n\nbeta\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections[0].charStart).toBe(0);
        expect(sections[0].charEnd).toBe(sections[1].charStart);
        expect(sections[1].charEnd).toBe(body.length);
    });

    it('should slugify non-alphanumeric runs and trim dashes', () => {
        const body = '## Hello, World!  Foo-Bar\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections[0].anchor).toBe('hello-world-foo-bar');
    });

    it("should treat an unbalanced fence as 'all subsequent headings inside fence'", () => {
        // Mirrors the existing build-time behavior: an opening fence with no
        // matching close swallows every following heading. Documenting via test.
        const body = '## Real\n\n```\n## Inside Fence\n## Also Inside\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections.map(s => s.heading)).toEqual(['Real']);
    });

    it('should return an empty array for a body with no headings', () => {
        expect(InstructionsFileSectionsParser.parse('just a paragraph\n')).toEqual([]);
    });

    it('should handle ### before any ## by leaving parent undefined', () => {
        const body = '### Orphan\n\n## Later\n';
        const sections = InstructionsFileSectionsParser.parse(body);
        expect(sections[0]).toMatchObject({ heading: 'Orphan', anchor: 'orphan' });
        expect(sections[0].parent).toBeUndefined();
        expect(sections[1]).toMatchObject({ heading: 'Later', anchor: 'later' });
    });
});
