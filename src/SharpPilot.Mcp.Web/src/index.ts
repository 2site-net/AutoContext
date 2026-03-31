import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { parseArgs } from 'node:util';
import { z } from 'zod';
import { configure } from './tools-status-config.js';
import { read } from './editorconfig-reader.js';
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
    server.tool(
        'get_editorconfig',
        'Resolves the effective .editorconfig properties for a given file path. ' +
        'Walks up the directory tree, evaluates glob patterns and section cascading, ' +
        'and returns the final resolved key-value pairs that apply to the file. ' +
        'Use this tool to understand the coding style rules (indent style, charset, ' +
        'end-of-line, etc.) that apply to a specific file.',
        {
            path: z.string().describe(
                'Absolute path to the file whose effective .editorconfig properties should be resolved.',
            ),
        },
        ({ path }) => ({
            content: [{ type: 'text' as const, text: read(path) }],
        }),
    );

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
        ({ content, editorConfigFilePath }) => ({
            content: [{
                type: 'text' as const,
                text: checker.check(
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
