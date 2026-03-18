---
description: "Use when writing MongoDB queries, aggregation pipelines, indexes, or any MongoDB data access code in .NET projects."
---
# MongoDB Guidelines

> These rules target MongoDB used in .NET projects with MongoDB.Driver or MongoDB.EntityFrameworkCore.

## Security

- **Do** use parameterized filters with the MongoDB C# driver's builder API (`Builders<T>.Filter`) instead of raw JSON or string interpolation — string-built queries are vulnerable to NoSQL injection just as SQL queries are vulnerable to SQL injection.
- **Do** enable authentication and use SCRAM-SHA-256 or x.509 certificates — MongoDB ships with authentication disabled by default; an unauthenticated instance is open to anyone who can reach the network port.
- **Do** enable TLS for all connections (`?tls=true` in the connection string) — MongoDB traffic is unencrypted by default; without TLS, credentials and data travel in plaintext.
- **Don't** store secrets (passwords, API keys, tokens) in MongoDB documents or collection metadata — use application-level secrets management; any user with `find` permission on the collection can read the data.

## Schema & Data Modelling

- **Do** embed related data in the same document when the relationship is one-to-few and the embedded data is always read together — embedding avoids extra round-trips and leverages MongoDB's document-level atomicity.
- **Do** use references (store the `_id` of the related document) when the related entity is large, shared across many parents, or updated independently — referencing avoids unbounded document growth and keeps write amplification low.
- **Do** keep documents under the 16 MB BSON limit — if a document can grow without bound (e.g., an array of log entries), move the growing data into a separate collection.
- **Do** use `BsonClassMap` or `[BsonElement]` attributes to control serialisation explicitly — relying on default conventions couples your wire format to C# property names and makes schema evolution fragile.
- **Don't** use unbounded arrays that grow over time (e.g., `comments`, `events`) inside a single document — unbounded arrays cause document migrations when they outgrow their allocated space and eventually hit the 16 MB limit.

## Query Design

- **Do** use the typed driver API (`Find<T>`, `Builders<T>.Filter`, `Builders<T>.Projection`) over raw `BsonDocument` filters — typed queries catch field-name typos at compile time and are easier to refactor.
- **Do** project only the fields you need with `Builders<T>.Projection.Include` — returning entire documents wastes bandwidth and prevents covered-index queries.
- **Do** use the aggregation pipeline (`Aggregate<T>().Match().Group().Project()`) for complex transformations — the pipeline runs server-side and avoids pulling large data sets into the application.
- **Do** use `BulkWriteAsync` for batch inserts, updates, and deletes — `BulkWrite` sends all operations in a single round-trip and is significantly faster than individual calls in a loop.
- **Don't** use `$where` or JavaScript expressions in queries — `$where` disables index use, runs a JavaScript interpreter per document, and is a NoSQL injection vector if the expression contains user input.
- **Don't** use `Find` without a filter or with an empty filter on large collections in production — an unfiltered `Find` returns every document; use pagination with `Limit` and `Skip` or a cursor-based approach.

## Indexing & Performance

- **Do** create indexes on fields used in `Filter`, `Sort`, and `Group` operations — missing indexes force collection scans; use `explain()` or the Atlas Profiler to verify index usage.
- **Do** create compound indexes that match your most common query patterns (equality fields first, sort fields next, range fields last) — MongoDB uses a single index per query; a well-ordered compound index covers the entire filter+sort.
- **Do** build indexes during low-traffic windows or use a rolling index build on replica sets — on MongoDB 4.2+ background builds are the default, but index creation still competes for resources and can degrade query latency on large collections.
- **Do** set a TTL index on time-series or expiring data (`ExpireAfter`) — TTL indexes let MongoDB automatically delete documents after a specified duration, avoiding manual cleanup jobs.
- **Don't** create indexes on fields with very low cardinality (e.g., `boolean`, `status` with 2–3 values) as the sole index — low-selectivity indexes scan a large fraction of the collection and provide little benefit.
- **Don't** ignore the `explain()` output — `executionStats.totalDocsExamined` vs `nReturned` reveals whether a query is scanning far more documents than it returns, indicating a missing or suboptimal index.

## Transactions & Consistency

- **Do** use multi-document transactions (`StartSessionAsync` + `StartTransaction`) only when atomicity across multiple documents or collections is required — single-document operations are already atomic in MongoDB.
- **Do** set an appropriate read concern (`majority`, `local`, `linearizable`) and write concern (`w: "majority"`) based on your consistency requirements — the defaults (`local` read, `w: 1` write) can return stale reads and acknowledge writes before replication.
- **Don't** use transactions for operations that can be modelled as a single-document update — transactions add latency, hold locks, and are unnecessary when MongoDB's single-document atomicity suffices.
