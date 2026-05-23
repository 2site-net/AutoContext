export class PromiseTestUtils {
    public static async until(
        predicate: () => boolean,
        timeoutMs = 2000,
        stepMs = 10,
    ): Promise<void> {
        const start = Date.now();

        while (!predicate()) {
            if (Date.now() - start > timeoutMs) {
                throw new Error('until: timed out');
            }
            await new Promise<void>((resolve) => setTimeout(resolve, stepMs));
        }
    }
}
