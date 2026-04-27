import { randomUUID } from 'node:crypto';

/**
 * Centralised producer of the random identifiers and pipe names used
 * across the extension. Every value is derived from a fresh UUID and
 * carries 48 bits of entropy — enough that two VS Code windows running
 * side-by-side will never collide on an endpoint suffix or pipe name.
 */
export class IdentifierFactory {
    /**
     * Returns a 12-character lowercase-hex identifier (e.g.
     * `abc123def456`). Used as the per-window endpoint suffix shared
     * between the extension and the workers it spawns.
     */
    static createRandomId(): string {
        return randomUUID().replace(/-/g, '').slice(0, 12);
    }

    /**
     * Returns a pipe name of the form `<prefix>-<12-hex>` (e.g.
     * `autocontext-log-abc123def456`). The 12-hex suffix is generated
     * fresh on every call.
     */
    static createRandomName(prefix: string): string {
        return `${prefix}-${IdentifierFactory.createRandomId()}`;
    }
}
