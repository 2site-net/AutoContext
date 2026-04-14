import { TreeViewNodeState } from './tree-view-node-state.js';
import { treeViewLabels } from './ui-constants.js';

export type TreeViewKind = 'instructions' | 'tools';

export class TreeViewTooltip {
    constructor(private readonly kind: TreeViewKind) {}

    container(name: string, active: number, total: number, description?: string, version?: string): string {
        const heading = version ? `${name} v${version}` : name;
        const lines = [heading];
        if (description) { lines.push(description); }
        lines.push(`${active}/${total} ${this.containerSuffix}`);
        return lines.join('\n');
    }

    leaf(label: string, state: TreeViewNodeState, settingId: string, description?: string, version?: string, stateLabel?: string): string {
        const heading = version ? `${label} v${version}` : label;
        const lines = [`${heading} (${stateLabel ?? this.stateDescription(state)})`];
        if (description) { lines.push(description); }
        lines.push(`${treeViewLabels.settingPrefix} ${settingId}`);
        return lines.join('\n');
    }

    description(active: number, total: number): string {
        return `${active}/${total}`;
    }

    private get containerSuffix(): string {
        return this.kind === 'instructions'
            ? treeViewLabels.activeSuffix
            : treeViewLabels.featuresEnabledTooltip;
    }

    private stateDescription(state: TreeViewNodeState): string {
        switch (state) {
            case TreeViewNodeState.Enabled:
                return this.kind === 'instructions'
                    ? treeViewLabels.activeTooltip
                    : treeViewLabels.enabledTooltip;
            case TreeViewNodeState.Disabled:
                return treeViewLabels.disabledTooltip;
            case TreeViewNodeState.NotDetected:
                return treeViewLabels.notDetectedTooltip;
            case TreeViewNodeState.Overridden:
                return treeViewLabels.overriddenTooltip;
            default:
                state.throwIfUnknown();
        }
    }
}
