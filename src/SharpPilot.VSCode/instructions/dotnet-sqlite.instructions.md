---
description: "Use when writing SQLite queries, migrations, or any SQLite data access code in .NET projects."
---
# SQLite Guidelines

> These rules target SQLite used in .NET projects with Microsoft.Data.Sqlite or Microsoft.EntityFrameworkCore.Sqlite.

## Security

- **Do** use parameterized queries with `@param` syntax for all dynamic SQL — never concatenate user input into query strings; even local/embedded databases are vulnerable to SQL injection when they accept external input.
- **Do** set `Mode=ReadOnly` in the connection string when the application only reads data — a read-only connection prevents accidental writes and limits the impact of injection attacks.
- **Do** protect the database file with OS-level permissions — SQLite databases are plain files; anyone with file-system read access can open and query the data without authentication.
- **Don't** store secrets (passwords, API keys, tokens) in SQLite tables unless the database file is encrypted — SQLite has no built-in authentication; the file is readable by any process with file-system access.

## Query Design

- **Do** specify only the columns you need instead of `SELECT *` — `SELECT *` breaks code when schema changes and prevents covering-index-only scans.
- **Do** use `INSERT OR REPLACE` or `INSERT … ON CONFLICT DO UPDATE` for upserts — SQLite supports both forms; prefer `ON CONFLICT` when you need to update specific columns without replacing the entire row.
- **Do** use `EXISTS` instead of `COUNT(*) > 0` to check for row existence — `EXISTS` short-circuits on the first matching row; `COUNT` scans all matching rows.
- **Do** use `COALESCE` or `IFNULL` to provide defaults for `NULL` columns — SQLite uses dynamic typing and does not enforce `NOT NULL` unless explicitly declared; unexpected `NULL` values are a common source of bugs.
- **Don't** use `NOT IN` with a subquery that can return `NULL` — if any row in the subquery is `NULL`, the entire `NOT IN` predicate evaluates to `NULL` and returns no rows; prefer `NOT EXISTS` instead.
- **Don't** rely on type enforcement from column declarations — SQLite uses type affinity, not strict typing; a `TEXT` column happily stores an integer unless `STRICT` mode is enabled (SQLite 3.37+).

## Performance

- **Do** add indexes to columns used in `WHERE`, `JOIN`, `ORDER BY`, and `GROUP BY` clauses — missing indexes force full table scans, which matter even on small databases when queries are frequent.
- **Do** use `EXPLAIN QUERY PLAN` to understand how SQLite executes a query — it shows whether indexes are used, detects full scans, and reveals suboptimal join orders.
- **Do** enable WAL mode (`PRAGMA journal_mode = WAL`) for applications that read and write concurrently — WAL allows concurrent reads during writes; the default rollback journal blocks readers while a write transaction is active.
- **Do** wrap batch inserts in an explicit transaction (`BEGIN … COMMIT`) — without an explicit transaction, SQLite auto-commits after each `INSERT`, and each auto-commit triggers an `fsync`; batching reduces I/O by orders of magnitude.
- **Do** set `PRAGMA synchronous = NORMAL` when using WAL mode — `FULL` is the default and issues an `fsync` on every commit; `NORMAL` in WAL mode is safe against corruption from an application crash and only risks data loss on an OS or power failure.
- **Don't** open multiple long-lived connections from the same process expecting true parallelism — SQLite serialises all writes through a single writer lock; use a pooled connection with `Pooling=true` in the connection string instead.

## Schema & Migrations

- **Do** always include `IF NOT EXISTS` / `IF EXISTS` guards in migration scripts — migrations must be idempotent so they can be re-run safely without duplicate-object errors.
- **Do** use `INTEGER PRIMARY KEY` for rowid aliases — in SQLite, `INTEGER PRIMARY KEY` is an alias for the implicit `rowid` and avoids a redundant B-tree; `INT PRIMARY KEY` does not alias the rowid and creates a separate index.
- **Do** prefer `TEXT` with ISO 8601 format (`YYYY-MM-DDTHH:MM:SS.SSSZ`) for date-time storage — SQLite has no native date-time type; storing as text in a consistent format works with SQLite's built-in date functions and sorts correctly.
- **Do** use `STRICT` tables (SQLite 3.37+) when type safety matters — `STRICT` enforces declared column types and rejects values that do not match, preventing silent type coercion.
- **Don't** use `ALTER TABLE … DROP COLUMN` on SQLite versions before 3.35.0 — it is not supported; use the recreate-table pattern (create new table, copy data, drop old, rename) instead.
- **Don't** use `VACUUM` inside a transaction — `VACUUM` rebuilds the entire database file and cannot run within a transaction; it also requires temporary disk space roughly equal to the database size.
