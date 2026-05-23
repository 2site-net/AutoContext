let counter = 0;

/** Generate a unique pipe name suitable for use in tests. */
export function createTestPipeName(): string {
    counter += 1;
    const random = Math.random().toString(36).slice(2, 8);
    return `autocontext-test-${process.pid}-${Date.now()}-${counter}-${random}`;
}
