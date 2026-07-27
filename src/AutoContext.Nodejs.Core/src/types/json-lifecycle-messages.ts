// Wire shapes of the Engine.Lifecycle.Subscribe surface.

/** Lifecycle transition broadcast on Engine.Lifecycle.Subscribe. */
export interface JsonLifecycleEvent {
    readonly kind: 'started' | 'reloading' | 'reloaded' | 'shutting-down' | 'dropped';
    readonly instanceId?: string;
    readonly revision?: number;
    readonly reason?: string;
}
