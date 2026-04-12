const SEMVER_CORE = String.raw`\d+\.\d+\.\d+(-[a-zA-Z0-9-]+(\.[a-zA-Z0-9-]+)*)?`;
const SEMVER_PATTERN = new RegExp(`^${SEMVER_CORE}$`);
const LABEL_SUFFIX_PATTERN = new RegExp(String.raw`\(v(${SEMVER_CORE})\)\s*$`);

export class SemVer {
    static isValid(version: string): boolean {
        return SEMVER_PATTERN.test(version);
    }

    static fromParentheses(label: string): string | undefined {
        return label.match(LABEL_SUFFIX_PATTERN)?.[1];
    }
}
