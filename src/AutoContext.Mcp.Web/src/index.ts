import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { readFileSync } from 'node:fs';
import { parseArgs } from 'node:util';
import { z } from 'zod';
import { McpToolsClient } from './features/mcp-tools/mcp-tools-client.js';
import { StderrLogger } from './features/logging/logger.js';
import { TypeScriptChecker } from './tools/typescript/typescript-checker.js';

const { values } = parseArgs({
    options: {
        scope: { type: 'string' },
        'workspace-folder': { type: 'string' },
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

const workspaceFolder = typeof values['workspace-folder'] === 'string' ? values['workspace-folder'] : undefined;
if (workspaceFolder) {
    logger.log('Startup', `workspace-folder=${workspaceFolder}`);
}

const workspaceServer = typeof values['workspace-server'] === 'string' ? values['workspace-server'] : undefined;
const mcpToolsClient = new McpToolsClient(workspaceServer);
if (workspaceServer) {
    logger.log('Startup', `workspace-server=${workspaceServer}`);
}

const { version } = JSON.parse(
    readFileSync(new URL('../package.json', import.meta.url), 'utf8'),
) as { version: string };

const server = new McpServer({
    name: 'AutoContext.Mcp.Web',
    version,
});

if (scope === 'typescript') {
    const checker = new TypeScriptChecker(mcpToolsClient, logger);

    server.registerTool(
        'check_typescript_all',
        {
            description:
                'Runs all enabled TypeScript code quality checks on TypeScript source code and returns a combined report. ' +
                'Covers coding style anti-patterns (any, enum, @ts-ignore, Function/Object types). ' +
                'Prefer this over calling individual check tools unless you only need a specific check. ' +
                'When filePath is provided, resolves its effective .editorconfig properties ' +
                'and uses them to drive checker behavior.',
            inputSchema: {
                content: z.string().describe('The TypeScript source code to check.'),
                filePath: z.string().optional().describe(
                    'Absolute path of the TypeScript source file being checked. ' +
                    'Used to resolve .editorconfig properties.',
                ),
            },
        },
        async ({ content, filePath }) => ({
            content: [{
                type: 'text' as const,
                text: await checker.check(
                    content,
                    filePath ? { filePath } : undefined,
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
