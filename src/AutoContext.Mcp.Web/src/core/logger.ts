export interface Logger {
    log(category: string, message: string): void;
}

export class StderrLogger implements Logger {
    log(category: string, message: string): void {
        const timestamp = new Date().toISOString();
        process.stderr.write(`[${timestamp}] [${category}] ${message}\n`);
    }
}

export const NullLogger: Logger = {
    log(): void { /* no-op */ },
};
