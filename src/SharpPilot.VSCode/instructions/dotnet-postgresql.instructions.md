---
description: "Use when writing PostgreSQL queries, PL/pgSQL functions, migrations, or any PostgreSQL data access code in .NET projects."
---
# PostgreSQL Guidelines

> These rules target PostgreSQL used in .NET projects with Npgsql or Npgsql.EntityFrameworkCore.PostgreSQL.

## Security

- **Do** use parameterized queries with Npgsql `@param` syntax for all dynamic SQL — never concatenate user input into query strings; SQL injection is the most common critical vulnerability in database-backed applications.
- **Do** restrict the database user's `search_path` to fixed schemas — a misconfigured or attacker-controlled `search_path` can redirect function and table lookups to a malicious schema, enabling privilege escalation.
- **Do** enable Row-Level Security (RLS) on tables that contain multi-tenant data — RLS enforces access control at the database layer, providing defence-in-depth even if the application layer is compromised.
- **Don't** store secrets (passwords, connection strings, API keys) in PostgreSQL objects — use application-level secrets management; secrets in stored functions or comments are readable by any database user with sufficient privilege.

## Query Design

- **Do** use the `RETURNING` clause after `INSERT`, `UPDATE`, and `DELETE` to retrieve affected rows in a single round-trip — avoids a separate `SELECT` and is safe under concurrent access.
- **Do** use `INSERT … ON CONFLICT DO UPDATE` (upsert) rather than a separate `SELECT` + `INSERT` — the latter is not atomic and fails under concurrent load; `ON CONFLICT` handles the race condition at the database level.
- **Do** use CTEs (`WITH … AS (…)`) for readability; be explicit about materialisation — PostgreSQL 12+ inlines single-reference non-recursive CTEs by default, so add `MATERIALIZED` when you need a plan-fence and `NOT MATERIALIZED` when you want forced inlining.
- **Do** use `LATERAL` joins to reference columns from preceding `FROM` items inside a subquery — `LATERAL` enables per-row subqueries and is the PostgreSQL equivalent of `CROSS APPLY` in T-SQL.
- **Do** use `jsonb` operators (`->`, `->>`, `@>`, `?`) for querying JSON columns — `jsonb` is indexed, binary-stored, and faster to query; prefer it over `json` except when preserving key order or duplicate keys is required.
- **Do** use `ILIKE` for case-insensitive pattern matching — `ILIKE` is native to PostgreSQL and is clearer than `LOWER(col) LIKE LOWER(pattern)`; pair it with a `pg_trgm` GIN index for performance on large tables.
- **Don't** use `NOT IN` with a subquery that can return `NULL` — if any row in the subquery is `NULL`, the entire `NOT IN` predicate evaluates to `UNKNOWN` and returns no rows; prefer `NOT EXISTS` instead.
- **Don't** use `SELECT *` in application queries — list columns explicitly; `SELECT *` breaks when columns are added or reordered and prevents covering-index-only scans.

## Performance

- **Do** run `EXPLAIN ANALYZE` on slow queries to understand the actual execution plan — `EXPLAIN` alone shows the estimated plan; `ANALYZE` executes the query and shows actual row counts and timing, revealing plan misestimates.
- **Do** create partial indexes for queries that filter on a fixed condition (e.g., `WHERE is_active = true`) — partial indexes are smaller, faster to scan, and cheaper to maintain than full-table indexes.
- **Do** create indexes `CONCURRENTLY` in production environments — `CREATE INDEX` without `CONCURRENTLY` acquires an `AccessExclusiveLock` that blocks all reads and writes; `CONCURRENTLY` builds the index without blocking at the cost of a longer build time.
- **Do** use `COPY` (or Npgsql's `BeginBinaryImport`) for bulk inserts — `COPY` bypasses per-row overhead and is orders of magnitude faster than individual `INSERT` statements for large data loads.
- **Don't** apply functions to indexed columns in `WHERE` clauses (e.g., `WHERE LOWER(email) = …`) unless a matching functional index exists — function calls prevent index seeks and force sequential scans; create a functional index or use a `GENERATED ALWAYS AS` generated column instead.
- **Don't** ignore table bloat in high-churn tables — dead tuples from `UPDATE` and `DELETE` accumulate and degrade query performance; `autovacuum` handles most cases, but if you notice progressive slowdowns, check `pg_stat_user_tables.n_dead_tup` and consider tuning autovacuum thresholds with the DBA.

## Functions & Procedures

- **Do** use `$$ … $$` dollar-quoting for PL/pgSQL function bodies — dollar-quoting avoids escaping issues with single quotes inside the body and is the standard PostgreSQL convention.
- **Do** use `RETURNS TABLE (…)` for set-returning functions instead of `SETOF record` — typed return columns make the function self-documenting and allow callers to reference columns by name without a column definition list.
- **Do** handle expected errors in PL/pgSQL with an `EXCEPTION` block and use `RAISE EXCEPTION` with an `ERRCODE` to surface application-level errors — `RAISE` with a specific error code allows callers to catch and handle distinct error conditions.

## Schema & Migrations

- **Do** use `timestamptz` (`TIMESTAMP WITH TIME ZONE`) for all date-time columns — `timestamp without time zone` stores and returns values as given with no conversion; `timestamptz` stores UTC and converts to the session time zone, preventing off-by-one-hour bugs across DST boundaries.
- **Do** use `gen_random_uuid()` (built-in since PostgreSQL 13) for UUID primary keys — avoids a dependency on the `uuid-ossp` extension; pair with a `uuid` column type for type safety.
- **Do** always include `IF NOT EXISTS` / `IF EXISTS` guards in migration scripts — migrations must be idempotent so they can be re-run safely without duplicate-object errors.
- **Do** prefer `text` over `varchar(n)` unless you need to enforce a maximum length at the database level — in PostgreSQL, `text` and `varchar` have identical storage and performance; `varchar(n)` adds only a length-check constraint with no other benefit.
- **Don't** use `SERIAL` or `BIGSERIAL` in new tables — use `GENERATED ALWAYS AS IDENTITY` (SQL standard, PostgreSQL 10+) instead; it is cleaner, prevents accidental manual inserts into the sequence column, and integrates better with ORMs.
