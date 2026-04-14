export class TreeViewNodeState {
    static readonly Enabled = new TreeViewNodeState('enabled', 0);
    static readonly Overridden = new TreeViewNodeState('overridden', 1);
    static readonly Disabled = new TreeViewNodeState('disabled', 2);
    static readonly NotDetected = new TreeViewNodeState('notDetected', 3);

    private constructor(
        readonly value: string,
        readonly sortOrder: number,
    ) {}

    isActive(): boolean {
        return this === TreeViewNodeState.Enabled
            || this === TreeViewNodeState.Overridden;
    }

    throwIfUnknown(): never {
        throw new Error(`Unknown state: ${this.value}`);
    }
}
