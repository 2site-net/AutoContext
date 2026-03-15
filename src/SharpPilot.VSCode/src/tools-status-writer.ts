import { join } from 'node:path';
import { writeFileSync, mkdirSync } from 'node:fs';
import * as vscode from 'vscode';
import { tools } from './config';

export class ToolsStatusWriter {
    private readonly dir: string;
    private readonly filePath: string;

    constructor(serversPath: string) {
        this.dir = join(serversPath, 'SharpPilot');
        this.filePath = join(this.dir, 'tools-status.json');
    }

    write(): void {
        const config = vscode.workspace.getConfiguration();
        const status: Record<string, boolean> = {};

        for (const tool of tools) {
            status[tool.toolName] = config.get<boolean>(tool.settingId, true);
        }

        mkdirSync(this.dir, { recursive: true });
        writeFileSync(this.filePath, JSON.stringify(status, null, 2));
    }
}
