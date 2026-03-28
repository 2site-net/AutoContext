export interface ServerEntry {
    label: string;
    category: string;
    contextKey?: string;
}

export const servers: readonly ServerEntry[] = [
    { label: 'SharpPilot: DotNet', category: 'dotnet', contextKey: 'hasDotnet' },
    { label: 'SharpPilot: Git', category: 'git', contextKey: 'hasGit' },
    { label: 'SharpPilot: EditorConfig', category: 'editorconfig' },
];
