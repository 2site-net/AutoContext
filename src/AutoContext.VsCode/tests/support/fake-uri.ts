export function fakeUri(p: string) {
    return { path: p, scheme: 'file', fsPath: p, toString: () => `file://${p}` };
}
