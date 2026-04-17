import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { parseArgs } from 'node:util';
import { z } from 'zod';
import { VERSION } from './version.js';
import { WorkspaceServerClient } from './features/workspace-server/workspace-server-client.js';
import { StderrLogger } from './features/logging/logger.js';
import { HealthMonitorClient } from './features/health-monitor/health-monitor-client.js';
import { TypeScriptChecker } from './tools/typescript/typescript-checker.js';

const { values } = parseArgs({
    options: {
        scope: { type: 'string' },
        'workspace-folder': { type: 'string' },
        'workspace-server': { type: 'string' },
        'health-monitor': { type: 'string' },
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
const workspaceServerClient = new WorkspaceServerClient(workspaceServer, 'TypeScript');
if (workspaceServer) {
    logger.log('Startup', `workspace-server=${workspaceServer}`);
}

// Connect to the extension's health monitor pipe.  The connection stays
// alive for the lifetime of the process; when the process exits the OS
// closes the socket and the extension detects the disconnect.
const healthPipe = typeof values['health-monitor'] === 'string' ? values['health-monitor'] : undefined;
if (healthPipe) {
    new HealthMonitorClient(logger).connect(healthPipe, String(scope));
}

const server = new McpServer({
    name: 'AutoContext.Mcp.Web',
    version: VERSION,
});

if (scope === 'typescript') {
    const checker = new TypeScriptChecker(workspaceServerClient);

    server.registerTool(
        'check_typescript_all',
        {
            description:
                'Runs all enabled TypeScript code quality checks and returns a combined report. ' +
                'Covers coding style (any, enum, @ts-ignore, type assertions, unconstrained generics, Function/Object types). ' +
                'Pass the source to check as content and its file path as originalPath.',
            inputSchema: {
                content: z.string().describe('The TypeScript source code to check.'),
                originalPath: z.string().optional().describe(
                    'Absolute path of the file whose source is in content.',
                ),
            },
        },
        async ({ content, originalPath }) => ({
            content: [{
                type: 'text' as const,
                text: await checker.check(
                    content,
                    originalPath ? { filePath: originalPath } : undefined,
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
