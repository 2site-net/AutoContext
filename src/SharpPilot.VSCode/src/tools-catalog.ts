import { tools } from './tool-entry.js';

export function toolSettingsForScope(scope: string): readonly string[] {
    const categoryPrefix: Record<string, string> = {
        dotnet: '.NET Tool',
        git: 'Git Tool',
        editorconfig: 'EditorConfig Tool',
    };
    const cat = categoryPrefix[scope];
    return cat ? tools.filter(t => t.category === cat).map(t => t.settingId) : [];
}
