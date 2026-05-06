import { describe, it, expect } from 'vitest';
import { McpToolRuntimeInfo } from '#src/mcp-tool-runtime-info';

describe('McpToolRuntimeInfo', () => {
    it('should expose the tool name verbatim', () => {
        expect(new McpToolRuntimeInfo('analyze_csharp_code').name).toBe('analyze_csharp_code');
    });

    it('should derive autocontext.mcpTools.<name> as the context key', () => {
        expect(new McpToolRuntimeInfo('analyze_csharp_code').contextKey)
            .toBe('autocontext.mcpTools.analyze_csharp_code');
    });

    it('should preserve names with underscores and digits in the context key', () => {
        expect(new McpToolRuntimeInfo('read_editorconfig_v2').contextKey)
            .toBe('autocontext.mcpTools.read_editorconfig_v2');
    });
});
