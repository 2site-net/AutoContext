/**
 * Optional manifest-derived data the tree provider needs in order to
 * order server-label and category nodes consistently with the manifest's
 * declaration order, and to map a server label back to its owning worker.
 */
export interface McpToolsCatalogOptions {
    readonly descriptions?: ReadonlyMap<string, string>;
    readonly serverLabelOrder?: readonly string[];
    readonly categoryOrder?: readonly string[];
    readonly serverLabelToWorkerIdMap?: ReadonlyMap<string, string>;
}
