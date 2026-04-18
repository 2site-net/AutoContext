export interface CatalogEntry {
    contextKey: string;
    label: string;
    category: string;
    workspaceFlags?: readonly string[];
}
