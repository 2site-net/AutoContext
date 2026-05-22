export function createFakeExtensionContext(): import('vscode').ExtensionContext {
    return {
        extensionPath: '/ext',
        extension: { id: '2site-net.autocontext', packageJSON: { version: '0.0.0-test' } },
        subscriptions: [],
        globalState: { get: () => undefined, update: async () => {} },
    } as unknown as import('vscode').ExtensionContext;
}
