export interface ServerEntry {
    label: string;
    category: string;
    server: 'dotnet' | 'web' | 'workspace';
    contextKey?: string;
}

export const servers: readonly ServerEntry[] = [
    { label: 'SharpPilot: DotNet', category: 'dotnet', server: 'dotnet', contextKey: 'hasDotNet' },
    { label: 'SharpPilot: Git', category: 'git', server: 'dotnet', contextKey: 'hasGit' },
    { label: 'SharpPilot: EditorConfig', category: 'editorconfig', server: 'workspace' },
    { label: 'SharpPilot: TypeScript', category: 'typescript', server: 'web', contextKey: 'hasTypeScript' },
];
