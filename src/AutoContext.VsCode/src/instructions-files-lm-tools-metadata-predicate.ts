import type { InstructionsFilesLmToolsApplyToMatcher } from './types/instructions-files-lm-tools-apply-to-matcher.js';
import type { InstructionsFilesLmToolsMetadataMatchResult } from './types/instructions-files-lm-tools-metadata-match-result.js';
import type {
    InstructionsFilesLmToolsMetadataPredicateError,
    InstructionsFilesLmToolsMetadataPredicateResult,
} from './types/instructions-files-lm-tools-metadata-predicate-result.js';
import type { InstructionsFilesLmToolsMetadataView } from './types/instructions-files-lm-tools-metadata-view.js';
import type { InstructionsFileSection } from './types/instructions-file-section.js';

type PredicateValue = string | number | boolean;
type PredicateInput = Readonly<Record<string, PredicateValue>>;
type FieldKind = 'string-regex' | 'string-glob' | 'string-array-regex' | 'boolean' | 'number';

/**
 * Generic predicate evaluator that powers
 * `search_autocontext_instructions_files_by_metadata` and (via
 * delegation in `ListInstructionsFilesLmToolHandler`)
 * `list_autocontext_instructions_files`. Operates on flattened
 * `InstructionsFilesLmToolsMetadataView` rows assembled by handlers
 * from the manifest + build-time metadata.
 *
 * Semantics:
 * - String fields → case-insensitive regex (pattern source capped at
 *   256 chars).
 * - `applyTo` → glob, never regex; dispatched to
 *   `InstructionsFilesLmToolsApplyToMatcher` (Step 5).
 * - `categories` → regex against any element ("any element matches").
 * - `hasChangelog` → boolean exact equality.
 * - `sections.level` → number exact equality; `sections.heading` /
 *   `sections.anchor` / `sections.parent` → regex.
 * - AND across keys; empty predicate ⇒ full input passes through.
 *
 * `sections.*` clauses are intersected per view: a single section
 * must satisfy all `sections.*` clauses, but any one such section is
 * enough for the view to pass. The anchors of those sections become
 * the row's `matchedAnchors`.
 *
 * Type mismatches between predicate value and field kind (e.g.
 * `{ hasChangelog: "true" }` or `{ description: 42 }`) return a
 * structured `type-mismatch` error rather than silently failing — the
 * LLM caller gets actionable feedback.
 */
export class InstructionsFilesLmToolsMetadataPredicate {
    private static readonly maxRegexPatternLength = 256;

    private static readonly fieldKinds: ReadonlyMap<string, FieldKind> = new Map<string, FieldKind>([
        ['name', 'string-regex'],
        ['key', 'string-regex'],
        ['fileName', 'string-regex'],
        ['description', 'string-regex'],
        ['version', 'string-regex'],
        ['applyTo', 'string-glob'],
        ['categories', 'string-array-regex'],
        ['hasChangelog', 'boolean'],
        ['sections.heading', 'string-regex'],
        ['sections.anchor', 'string-regex'],
        ['sections.parent', 'string-regex'],
        ['sections.level', 'number'],
    ]);

    constructor(private readonly applyToMatcher: InstructionsFilesLmToolsApplyToMatcher) {}

    async evaluate(
        predicate: PredicateInput,
        views: readonly InstructionsFilesLmToolsMetadataView[],
    ): Promise<InstructionsFilesLmToolsMetadataPredicateResult> {
        const entries = Object.entries(predicate);

        const validation = this.validate(entries);
        if (validation) {
            return validation;
        }

        // Precompile every regex predicate once. Predicate keys are
        // unique by construction (came from `Object.entries`), so a flat
        // Map suffices.
        const regexByField = new Map<string, RegExp>();
        for (const [field, value] of entries) {
            const kind = InstructionsFilesLmToolsMetadataPredicate.fieldKinds.get(field);
            if ((kind === 'string-regex' || kind === 'string-array-regex') && typeof value === 'string') {
                regexByField.set(field, new RegExp(value, 'i'));
            }
        }

        const sectionsClauses = entries.filter(([k]) => k.startsWith('sections.'));
        const nonApplyToScalarClauses = entries.filter(([k]) => !k.startsWith('sections.') && k !== 'applyTo');
        const applyToClause = entries.find(([k]) => k === 'applyTo');
        const touchesSections = sectionsClauses.length > 0;

        // Evaluate views in parallel — `applyTo` is I/O-bound on
        // `vscode.workspace.findFiles`, and the cheap sync gate inside
        // each task short-circuits before that I/O.
        const tasks = views.map(async (view): Promise<InstructionsFilesLmToolsMetadataMatchResult | undefined> => {
            // 1. Cheap sync scalar reject before any async work.
            if (!this.matchesScalarClauses(view, nonApplyToScalarClauses, regexByField)) {
                return undefined;
            }

            // 2. Async applyTo (skipped when the clause is absent).
            if (applyToClause && !await this.matchesApplyToClause(view, applyToClause[1] as string)) {
                return undefined;
            }

            // 3. Sections AND-intersection.
            if (!touchesSections) {
                return { view };
            }
            const matchedSections = this.intersectSectionClauses(view.sections, sectionsClauses, regexByField);
            if (matchedSections.length === 0) {
                return undefined;
            }
            return { view, matchedAnchors: matchedSections.map(s => s.anchor) };
        });

        const settled = await Promise.all(tasks);
        const results = settled.filter((r): r is InstructionsFilesLmToolsMetadataMatchResult => r !== undefined);
        return { kind: 'ok', results };
    }

    private validate(
        entries: ReadonlyArray<readonly [string, PredicateValue]>,
    ): InstructionsFilesLmToolsMetadataPredicateError | undefined {
        for (const [field, value] of entries) {
            const kind = InstructionsFilesLmToolsMetadataPredicate.fieldKinds.get(field);
            if (!kind) {
                return {
                    kind: 'error', error: 'unknown-field', field,
                    reason: `Unknown predicate field '${field}'.`,
                };
            }

            const typeError = this.checkValueType(field, value, kind);
            if (typeError) {
                return typeError;
            }

            if (kind === 'string-regex' || kind === 'string-array-regex') {
                const regexError = this.validateRegex(field, value as string);
                if (regexError) {
                    return regexError;
                }
            }
        }
        return undefined;
    }

    private checkValueType(
        field: string,
        value: PredicateValue,
        kind: FieldKind,
    ): InstructionsFilesLmToolsMetadataPredicateError | undefined {
        const expected = this.expectedJsTypeFor(kind);
        const actual = typeof value;
        if (actual !== expected) {
            return {
                kind: 'error', error: 'type-mismatch', field,
                reason: `Field '${field}' expects ${expected}, got ${actual}.`,
            };
        }
        return undefined;
    }

    private expectedJsTypeFor(kind: FieldKind): 'string' | 'number' | 'boolean' {
        switch (kind) {
            case 'string-regex':
            case 'string-glob':
            case 'string-array-regex':
                return 'string';
            case 'number':
                return 'number';
            case 'boolean':
                return 'boolean';
        }
    }

    private validateRegex(
        field: string,
        pattern: string,
    ): InstructionsFilesLmToolsMetadataPredicateError | undefined {
        if (pattern.length > InstructionsFilesLmToolsMetadataPredicate.maxRegexPatternLength) {
            return {
                kind: 'error', error: 'pattern-too-long', field,
                reason: `Pattern length ${pattern.length} exceeds cap of `
                    + `${InstructionsFilesLmToolsMetadataPredicate.maxRegexPatternLength} characters.`,
            };
        }
        try {
            new RegExp(pattern, 'i');
        } catch (err) {
            return {
                kind: 'error', error: 'invalid-regex', field,
                reason: err instanceof Error ? err.message : String(err),
            };
        }
        return undefined;
    }

    private matchesScalarClauses(
        view: InstructionsFilesLmToolsMetadataView,
        clauses: ReadonlyArray<readonly [string, PredicateValue]>,
        regexByField: ReadonlyMap<string, RegExp>,
    ): boolean {
        for (const [field, expected] of clauses) {
            if (!this.matchesScalar(view, field, expected, regexByField.get(field))) {
                return false;
            }
        }
        return true;
    }

    private matchesScalar(
        view: InstructionsFilesLmToolsMetadataView,
        field: string,
        expected: PredicateValue,
        regex: RegExp | undefined,
    ): boolean {
        if (field === 'categories') {
            if (!regex) {
                return false;
            }
            return view.categories.some(c => regex.test(c));
        }

        const value = this.readScalar(view, field);
        if (value === undefined) {
            return false;
        }

        if (regex) {
            return typeof value === 'string' && regex.test(value);
        }
        return value === expected;
    }

    private readScalar(
        view: InstructionsFilesLmToolsMetadataView,
        field: string,
    ): string | number | boolean | undefined {
        switch (field) {
            case 'name': return view.name;
            case 'key': return view.key;
            case 'fileName': return view.fileName;
            case 'description': return view.description;
            case 'version': return view.version;
            case 'hasChangelog': return view.hasChangelog;
            default: return undefined;
        }
    }

    private async matchesApplyToClause(
        view: InstructionsFilesLmToolsMetadataView,
        userInputGlob: string,
    ): Promise<boolean> {
        if (!view.applyTo) {
            return false;
        }
        return this.applyToMatcher.matches(userInputGlob, view.applyTo);
    }

    private intersectSectionClauses(
        sections: readonly InstructionsFileSection[],
        clauses: ReadonlyArray<readonly [string, PredicateValue]>,
        regexByField: ReadonlyMap<string, RegExp>,
    ): readonly InstructionsFileSection[] {
        if (clauses.length === 0) {
            return [];
        }
        return sections.filter(section =>
            clauses.every(([field, expected]) =>
                this.matchesSectionClause(section, field, expected, regexByField.get(field)),
            ),
        );
    }

    private matchesSectionClause(
        section: InstructionsFileSection,
        field: string,
        expected: PredicateValue,
        regex: RegExp | undefined,
    ): boolean {
        const subField = field.slice('sections.'.length);
        let value: string | number | undefined;
        switch (subField) {
            case 'heading': value = section.heading; break;
            case 'anchor': value = section.anchor; break;
            case 'parent': value = section.parent; break;
            case 'level': value = section.parent ? 3 : 2; break;
            default: return false;
        }
        if (value === undefined) {
            return false;
        }
        if (regex) {
            return typeof value === 'string' && regex.test(value);
        }
        return value === expected;
    }
}
