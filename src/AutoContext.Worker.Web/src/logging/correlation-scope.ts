import { AsyncLocalStorage } from 'node:async_hooks';

/**
 * Worker-process holder for the per-invocation correlation id sent on
 * every task request envelope. Set by `McpTaskDispatcherService` for the
 * duration of a task dispatch and read by the {@link Logger} at
 * log-emission time so every {@link LogEntry} carries the id without
 * requiring callers to thread it through their logging APIs.
 *
 * Implemented over Node's {@link AsyncLocalStorage} so the scope is
 * bound to the async call chain that originated from the dispatch —
 * nested promises and `await`s observe the same id, but unrelated
 * dispatches running concurrently each see their own.
 *
 * TypeScript counterpart of `CorrelationScope` in
 * `AutoContext.Worker.Shared`.
 */
export class CorrelationScope {
    private static readonly storage = new AsyncLocalStorage<string>();

    /**
     * The correlation id active on the current async context, or
     * `undefined` when no {@link run} scope is in effect.
     */
    static current(): string | undefined {
        return CorrelationScope.storage.getStore();
    }

    /**
     * Runs `callback` with `correlationId` set as the active scope.
     * Resolves with the callback's return value. Restores whatever id
     * (if any) was previously in effect once the callback's promise
     * settles.
     */
    static run<T>(correlationId: string, callback: () => Promise<T>): Promise<T> {
        if (correlationId.length === 0) {
            throw new Error('correlationId must be a non-empty string.');
        }
        return CorrelationScope.storage.run(correlationId, callback);
    }
}
