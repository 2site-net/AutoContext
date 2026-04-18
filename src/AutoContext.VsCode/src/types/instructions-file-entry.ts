export interface InstructionsFileEntry {
    key: string;
    fileName: string;
    label: string;
    category: string;
    workspaceFlags?: readonly string[];
}
