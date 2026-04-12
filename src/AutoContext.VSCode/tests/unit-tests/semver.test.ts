import { describe, it, expect } from 'vitest';
import { SemVer } from '../../src/semver';

describe('SemVer.isValid', () => {
    it.each`
        version              | label
        ${'1.0.0'}           | ${'stable release'}
        ${'0.0.1'}           | ${'patch-only'}
        ${'10.20.30'}        | ${'multi-digit'}
        ${'1.0.0-beta.1'}   | ${'pre-release with dot'}
        ${'1.0.0-rc.2'}     | ${'release candidate'}
        ${'1.0.0-alpha-1'}  | ${'pre-release with hyphen'}
        ${'2.0.0-0.3.7'}    | ${'numeric pre-release'}
    `('should accept $label ($version)', ({ version }) => {
        expect.soft(SemVer.isValid(version)).toBe(true);
    });

    it.each`
        version              | label
        ${'1.0'}             | ${'missing patch'}
        ${'1'}               | ${'major only'}
        ${'1.0.0.0'}         | ${'four segments'}
        ${'v1.0.0'}          | ${'leading v'}
        ${'1.0.0-'}          | ${'trailing hyphen'}
        ${''}                | ${'empty string'}
        ${'abc'}             | ${'non-numeric'}
        ${'1.0.0-beta..1'}  | ${'double dot in pre-release'}
    `('should reject $label ($version)', ({ version }) => {
        expect.soft(SemVer.isValid(version)).toBe(false);
    });
});

describe('SemVer.fromParentheses', () => {
    it.each`
        label                                  | expected
        ${'my-tool (v1.0.0)'}                  | ${'1.0.0'}
        ${'some name (v2.3.4-beta.1)'}         | ${'2.3.4-beta.1'}
        ${'tool (v0.1.0-alpha-2)'}             | ${'0.1.0-alpha-2'}
        ${'spaced (v1.0.0)  '}                 | ${'1.0.0'}
    `('should extract version from "$label"', ({ label, expected }) => {
        expect.soft(SemVer.fromParentheses(label)).toBe(expected);
    });

    it.each`
        label                        | reason
        ${'no version here'}         | ${'no parenthesised suffix'}
        ${'tool (1.0.0)'}            | ${'missing v prefix'}
        ${'tool (v1.0)'}             | ${'incomplete semver'}
        ${''}                        | ${'empty string'}
    `('should return undefined for $reason ("$label")', ({ label }) => {
        expect.soft(SemVer.fromParentheses(label)).toBeUndefined();
    });
});
