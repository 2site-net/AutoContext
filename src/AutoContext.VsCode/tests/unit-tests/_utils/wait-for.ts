/** Poll a condition until it becomes true (or timeout). */
export async function waitFor(condition: () => boolean, ms = 2000, interval = 10): Promise<void> {
    const start = Date.now();
    while (!condition()) {
        if (Date.now() - start > ms) { throw new Error('waitFor timeout'); }
        await new Promise(r => setTimeout(r, interval));
    }
}
