import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Base class for loaders that read a JSON manifest from the
 * extension's `resources/` folder and project it into a domain
 * manifest. Subclasses supply the file name, the JSON shape via
 * {@link TJson}, and the projection logic in {@link project}.
 *
 * Path resolution, UTF-8 read, JSON parse, and uniform error
 * contextualisation (`<fileName>: ...`) are owned by the base.
 */
export abstract class ResourceManifestLoader<TJson, TManifest> {
    protected constructor(
        private readonly extensionPath: string,
        private readonly fileName: string,
    ) {}

    /**
     * Reads the JSON manifest synchronously and returns the projected
     * domain manifest. Throws an `Error` prefixed with the file name
     * for parse failures or any structural violation reported by
     * {@link project} via {@link fail}.
     */
    load(): TManifest {
        const path = join(this.extensionPath, 'resources', this.fileName);
        const json = this.readJson(path);
        return this.project(json);
    }

    /**
     * Validates `json` and converts it into the domain manifest.
     * Implementations should call {@link fail} for any structural
     * violation rather than throwing directly, so error messages stay
     * consistent.
     */
    protected abstract project(json: TJson): TManifest;

    /**
     * Throws an `Error` prefixed with the manifest's file name. Use
     * for structural violations detected during {@link project}.
     */
    protected fail(reason: string): never {
        throw new Error(`${this.fileName}: ${reason}`);
    }

    private readJson(path: string): TJson {
        const raw = readFileSync(path, 'utf-8');
        try {
            return JSON.parse(raw) as TJson;
        } catch (err) {
            const detail = err instanceof Error ? err.message : String(err);
            throw new Error(`${this.fileName}: failed to parse JSON from '${path}': ${detail}`);
        }
    }
}
