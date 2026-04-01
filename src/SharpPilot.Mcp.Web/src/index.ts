import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { parseArgs } from 'node:util';
import { z } from 'zod';
import { configure } from './tools-status-config.js';
import { configurePipe } from './editorconfig-reader.js';
import { TypeScriptChecker } from './checkers/typescript/typescript-checker.js';

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

if (typeof values.workspace === 'string') {
    configure(values.workspace);
}

const workspacePipe = values['workspace-server'];
if (typeof workspacePipe === 'string') {
    configurePipe(workspacePipe);
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
        'Prefer this over calling individual check tools unless you only need a specific check. ' +
        'When editorConfigFilePath is provided (the path of the source file being checked), ' +
        'resolves its effective .editorconfig properties and uses them to drive checker behavior.',
        {
            content: z.string().describe('The TypeScript source code to check.'),
            editorConfigFilePath: z.string().optional().describe(
                'Absolute path of the TypeScript source file being checked. ' +
                'Used to resolve its effective .editorconfig properties. ' +
                'Pass the same path used when calling get_editorconfig.',
            ),
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
