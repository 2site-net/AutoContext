---
description: "Use when working with Redis caching, session storage, pub/sub, or distributed locking in .NET projects."
applyTo: "**/*.{cs,fs,vb}"
---
# Redis Guidelines

- **Do** reuse a single `ConnectionMultiplexer` instance per application — it is thread-safe and expensive to create; register it as a singleton.
- **Do** use short, structured key names with a colon-separated namespace (e.g., `tenant:42:session:abc`) — keeps keys scannable and avoids collisions.
- **Do** set an explicit TTL on every key unless you have a deliberate reason for it to live forever — unbounded keys are a memory leak.
- **Do** use `SCAN` to iterate keys — never `KEYS *` in production, it blocks the server.
- **Do** use `IDistributedCache` or `IConnectionMultiplexer` via DI — avoid creating connections manually in business logic.
- **Do** use `MGET` / `MSET` for batch operations — round-trips are the primary latency cost.
- **Do** prefer `StringSet` with `When.NotExists` for simple distributed locks — or use RedLock for multi-instance correctness.
- **Don't** store large objects (>100 KB) as single values — consider breaking them up or using Hashes for partial reads.
- **Don't** treat Redis as a primary database — it is a cache and ephemeral store; data must be recoverable from the source of truth.
- **Don't** use `FlushDb` / `FlushAll` outside of test environments.
