export interface ServerEntry {
    label: string;
    scope: string;
    contextKey?: string;
}

export const servers: readonly ServerEntry[] = [
    { label: 'SharpPilot: DotNet', scope: 'dotnet', contextKey: 'hasDotnet' },
    { label: 'SharpPilot: Git', scope: 'git', contextKey: 'hasGit' },
    { label: 'SharpPilot: EditorConfig', scope: 'editorconfig' },
];
