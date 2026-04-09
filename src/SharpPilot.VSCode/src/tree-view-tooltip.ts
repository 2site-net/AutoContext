import { TreeViewNodeState, treeViewLabels } from './ui-constants.js';

export type TreeViewKind = 'instructions' | 'tools';

export class TreeViewTooltip {
    constructor(private readonly kind: TreeViewKind) {}

    container(name: string, active: number, total: number): string {
        return `${name}\n${active}/${total} ${this.containerSuffix}`;
    }

    leaf(label: string, state: TreeViewNodeState, settingId: string): string {
        const lines = [label, this.stateDescription(state)];
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
        }
    }
}
