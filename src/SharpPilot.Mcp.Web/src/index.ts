import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { readFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { z } from 'zod';
import { ToolsStatusConfig } from './configuration/tools-status-config.js';
import { EditorConfigReader } from './tools/editorconfig/editorconfig-reader.js';
import { StderrLogger } from './core/logger.js';
import { TypeScriptChecker } from './tools/checkers/typescript/typescript-checker.js';

const { values } = parseArgs({
    options: {
        scope: { type: 'string' },
        workspace: { type: 'string' },
        'workspace-server': { type: 'string' },
    },
    strict: false,
});

const scope = values.scope;
if (!scope) {
    throw new Error('Missing required argument: --scope (typescript)');
}

const logger = new StderrLogger();

logger.log('Startup', `scope=${scope}`);

const workspace = typeof values.workspace === 'string' ? values.workspace : undefined;
const config = new ToolsStatusConfig(workspace);
if (workspace) {
    logger.log('Startup', `workspace=${workspace}`);
}

const workspacePipe = typeof values['workspace-server'] === 'string' ? values['workspace-server'] : undefined;
const editorConfig = new EditorConfigReader(workspacePipe);
if (workspacePipe) {
    logger.log('Startup', `workspace-server=${workspacePipe}`);
}

const { version } = JSON.parse(
    readFileSync(new URL('../package.json', import.meta.url), 'utf8'),
) as { version: string };

const server = new McpServer({
    name: 'SharpPilot.Mcp.Web',
    version,
});

if (scope === 'typescript') {
    const checker = new TypeScriptChecker(config, editorConfig, logger);

    server.registerTool(
        'check_typescript_all',
        {
            description:
                'Runs all enabled TypeScript code quality checks on TypeScript source code and returns a combined report. ' +
                'Covers coding style anti-patterns (any, enum, @ts-ignore, Function/Object types). ' +
                'Prefer this over calling individual check tools unless you only need a specific check. ' +
                'When editorConfigFilePath is provided (the path of the source file being checked), ' +
                'resolves its effective .editorconfig properties and uses them to drive checker behavior.',
            inputSchema: {
                content: z.string().describe('The TypeScript source code to check.'),
                editorConfigFilePath: z.string().optional().describe(
                    'Absolute path of the TypeScript source file being checked. ' +
                    'Used to resolve its effective .editorconfig properties. ' +
                    'Pass the same path used when calling get_editorconfig.',
                ),
            },
        },
        async ({ content, editorConfigFilePath }) => ({
            content: [{
                type: 'text' as const,
                text: await checker.check(
                    content,
                    editorConfigFilePath ? { editorConfigFilePath } : undefined,
                ),
            }],
        }),
    );
} else {
    throw new Error(`Unknown scope '${scope}'. Valid values: typescript.`);
}

const transport = new StdioServerTransport();
await server.connect(transport);

logger.log('Startup', 'MCP server connected');
