export class PipeNameTestFactory {
    static #counter = 0;

    public static create(): string {
        PipeNameTestFactory.#counter += 1;
        const random = Math.random().toString(36).slice(2, 8);
        return `autocontext-test-${process.pid}-${Date.now()}-${PipeNameTestFactory.#counter}-${random}`;
    }
}
