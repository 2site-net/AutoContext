/**
 * Connect timing for the find-or-spawn resolver: one short warm try
 * before spawning, then a bounded backoff loop while a freshly-spawned
 * engine binds its pipes.
 *
 * Values mirror the C# `EngineConnectBudget` in
 * `AutoContext.Client.Core`.
 */
export interface EngineConnectBudget {
    /** Connect timeout for the single warm try, before any spawn. */
    readonly warmConnectTimeoutMs: number;

    /** Total time the resolver keeps retrying after a spawn. */
    readonly coldConnectBudgetMs: number;

    /** Connect timeout for one attempt inside the cold retry loop. */
    readonly coldConnectAttemptTimeoutMs: number;

    /** Delay before the first cold retry. */
    readonly initialRetryDelayMs: number;

    /** Upper bound on the backoff delay between cold retries. */
    readonly maxRetryDelayMs: number;

    /** Factor applied to the previous delay on each retry. */
    readonly retryDelayMultiplier: number;
}

export const DEFAULT_ENGINE_CONNECT_BUDGET: EngineConnectBudget = {
    warmConnectTimeoutMs: 500,
    coldConnectBudgetMs: 10_000,
    coldConnectAttemptTimeoutMs: 1_000,
    initialRetryDelayMs: 50,
    maxRetryDelayMs: 500,
    retryDelayMultiplier: 2,
};

/** Grows {@link previousDelayMs} by the budget's factor, capped. */
export function nextRetryDelayMs(budget: EngineConnectBudget, previousDelayMs: number): number {
    if (previousDelayMs <= 0) {
        return budget.initialRetryDelayMs;
    }

    const scaled = previousDelayMs * budget.retryDelayMultiplier;
    return scaled > budget.maxRetryDelayMs ? budget.maxRetryDelayMs : scaled;
}
