import { describe, it, expect } from 'vitest';
import { TreeViewTooltip } from '#src/tree-view-tooltip';
import { TreeViewNodeState } from '#src/tree-view-node-state';
import { treeViewLabels } from '#src/ui-constants';

describe('TreeViewTooltip.container', () => {
    it('emits "name\\ndescription\\nactive/total active" for instructions', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.container('Languages', 3, 5, 'Lang group');

        expect(result).toBe(`Languages\nLang group\n3/5 ${treeViewLabels.activeSuffix}`);
    });

    it('emits "name v<version>\\nactive/total tasks enabled" for tools', () => {
        const tooltip = new TreeViewTooltip('tools');

        const result = tooltip.container('analyze_csharp_code', 4, 7, undefined, '1.0.0');

        expect(result).toBe(`analyze_csharp_code v1.0.0\n4/7 ${treeViewLabels.tasksEnabledTooltip}`);
    });

    it('omits the description line when none is supplied', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.container('General', 1, 1);

        expect(result.split('\n')).toHaveLength(2);
    });
});

describe('TreeViewTooltip.leaf', () => {
    it('renders enabled instruction state with the active tooltip', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.leaf('lang-csharp', TreeViewNodeState.Enabled, 'autocontext.instructions.lang-csharp');

        expect(result).toBe(
            `lang-csharp (${treeViewLabels.activeTooltip})\n`
            + `${treeViewLabels.contextKeyPrefix} autocontext.instructions.lang-csharp`,
        );
    });

    it('renders enabled tool state with the enabled tooltip (different from instructions)', () => {
        const tooltip = new TreeViewTooltip('tools');

        const result = tooltip.leaf('analyze_csharp_code', TreeViewNodeState.Enabled, 'autocontext.mcpTools.analyze_csharp_code');

        expect(result).toContain(treeViewLabels.enabledTooltip);
        expect(result).not.toContain(treeViewLabels.activeTooltip);
    });

    it('renders disabled state with the shared disabled tooltip', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.leaf('lang-csharp', TreeViewNodeState.Disabled, 'autocontext.instructions.lang-csharp');

        expect(result).toContain(treeViewLabels.disabledTooltip);
    });

    it('renders not-detected state with the not-detected tooltip', () => {
        const tooltip = new TreeViewTooltip('tools');

        const result = tooltip.leaf('analyze_csharp_code', TreeViewNodeState.NotDetected, 'autocontext.mcpTools.analyze_csharp_code');

        expect(result).toContain(treeViewLabels.notDetectedTooltip);
    });

    it('renders overridden state with the overridden tooltip', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.leaf('lang-csharp', TreeViewNodeState.Overridden, 'autocontext.instructions.lang-csharp');

        expect(result).toContain(treeViewLabels.overriddenTooltip);
    });

    it('prefixes the heading with v<version> when supplied', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.leaf('lang-csharp', TreeViewNodeState.Enabled, 'autocontext.instructions.lang-csharp', undefined, '1.2.0');

        expect(result.split('\n')[0]).toMatch(/^lang-csharp v1\.2\.0/);
    });

    it('uses the supplied stateLabel override instead of the default tooltip', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.leaf('lang-csharp', TreeViewNodeState.Overridden, 'autocontext.instructions.lang-csharp', undefined, undefined, treeViewLabels.outdatedTooltip);

        expect(result).toContain(treeViewLabels.outdatedTooltip);
        expect(result).not.toContain(treeViewLabels.overriddenTooltip);
    });

    it('inserts the description line between heading and context key', () => {
        const tooltip = new TreeViewTooltip('instructions');

        const result = tooltip.leaf('lang-csharp', TreeViewNodeState.Enabled, 'autocontext.instructions.lang-csharp', 'C# style');

        const lines = result.split('\n');
        expect(lines[1]).toBe('C# style');
        expect(lines[2]).toBe(`${treeViewLabels.contextKeyPrefix} autocontext.instructions.lang-csharp`);
    });
});

describe('TreeViewTooltip.description', () => {
    it('formats <active>/<total> regardless of kind', () => {
        expect(new TreeViewTooltip('instructions').description(2, 5)).toBe('2/5');
        expect(new TreeViewTooltip('tools').description(0, 7)).toBe('0/7');
    });
});
