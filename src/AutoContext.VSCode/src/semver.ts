const SEMVER_CORE = String.raw`\d+\.\d+\.\d+(-[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*)?`;
const SEMVER_PATTERN = new RegExp(`^${SEMVER_CORE}$`);
const LABEL_SUFFIX_PATTERN = new RegExp(String.raw`\(v(${SEMVER_CORE})\)\s*$`);
const MAJOR_MINOR_PATTERN = /^(\d+)\.(\d+)\./;

export class SemVer {
    static isValid(version: string): boolean {
        return SEMVER_PATTERN.test(version);
    }

    static fromParentheses(label: string): string | undefined {
        return label.match(LABEL_SUFFIX_PATTERN)?.[1];
    }

    /** Returns true when both versions share the same MAJOR.MINOR components. */
    static equalsMajorMinor(a: string, b: string): boolean {
        const am = a.match(MAJOR_MINOR_PATTERN);
        const bm = b.match(MAJOR_MINOR_PATTERN);
        if (!am || !bm) {
            return false;
        }
        return am[1] === bm[1] && am[2] === bm[2];
    }
}
