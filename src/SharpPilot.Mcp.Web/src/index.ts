import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { parseArgs } from 'node:util';
import { z } from 'zod';
import { configure } from './tools-status-config.js';
import { TypeScriptChecker } from './checkers/typescript/typescript-checker.js';

const { values } = parseArgs({
    options: {
        scope: { type: 'string' },
        workspace: { type: 'string' },
    },
    strict: false,
});

const scope = values.scope;
if (!scope) {
    throw new Error('Missing required argument: --scope (typescript)');
}

if (typeof values.workspace === 'string') {
    configure(values.workspace);
}

const server = new McpServer({
    name: 'SharpPilot.Mcp.Web',
    version: '0.5.0',
});

if (scope === 'typescript') {
    const checker = new TypeScriptChecker();

    server.tool(
        'check_typescript_all',
        'Runs all enabled TypeScript code quality checks on TypeScript source code and returns a combined report. ' +
        'Covers coding style anti-patterns (any, enum, @ts-ignore, Function/Object types). ' +
        'Prefer this over calling individual check tools unless you only need a specific check.',
        {
            content: z.string().describe('The TypeScript source code to check.'),
        },
        ({ content }) => ({
            content: [{ type: 'text' as const, text: checker.check(content) }],
        }),
    );
} else {
    throw new Error(`Unknown scope '${scope}'. Valid values: typescript.`);
}

const transport = new StdioServerTransport();
await server.connect(transport);
