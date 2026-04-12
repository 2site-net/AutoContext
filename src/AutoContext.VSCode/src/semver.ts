const SEMVER_CORE = String.raw`\d+\.\d+\.\d+(-[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*)?`;
const SEMVER_PATTERN = new RegExp(`^${SEMVER_CORE}$`);
const LABEL_SUFFIX_PATTERN = new RegExp(String.raw`\(v(${SEMVER_CORE})\)\s*$`);
const VERSION_PATTERN = /^(\d+)\.(\d+)\.(\d+)/;

export class SemVer {
    static isValid(version: string): boolean {
        return SEMVER_PATTERN.test(version);
    }

    static fromParentheses(label: string): string | undefined {
        return label.match(LABEL_SUFFIX_PATTERN)?.[1];
    }

    /** Returns true when both versions share the same MAJOR.MINOR components. */
    static equalsMajorMinor(a: string, b: string): boolean {
        const [lhs, rhs] = this.parsePair(a, b);
        if (!lhs || !rhs) { return false; }
        return lhs[0] === rhs[0] && lhs[1] === rhs[1];
    }

    /** Returns true when `a` is strictly greater than `b` in semver ordering. */
    static isGreaterThan(a: string, b: string): boolean {
        const [lhs, rhs] = this.parsePair(a, b);
        if (!lhs || !rhs) { return false; }

        for (let i = 0; i < 3; i++) {
            if (lhs[i] !== rhs[i]) { return lhs[i] > rhs[i]; }
        }
        return false;
    }

    private static parse(version: string): [number, number, number] | undefined {
        const m = version.match(VERSION_PATTERN);
        return m ? [Number(m[1]), Number(m[2]), Number(m[3])] : undefined;
    }

    private static parsePair(a: string, b: string): [ReturnType<typeof SemVer.parse>, ReturnType<typeof SemVer.parse>] {
        return [this.parse(a), this.parse(b)];
    }
}
