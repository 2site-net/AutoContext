---
description: "Use when writing T-SQL queries, stored procedures, migrations, or any SQL Server data access code in .NET projects."
---
# T-SQL / SQL Server Guidelines

> These rules target T-SQL as used with SQL Server, Azure SQL Database, and Azure SQL Managed Instance. Rules are derived from [Microsoft SQL Server documentation](https://learn.microsoft.com/en-us/sql/sql-server/) and SQL Server security and performance best practices.

## Security

- **Do** use parameterized queries or `sp_executesql` with parameters for all dynamic SQL — concatenating user input directly into SQL strings is the primary cause of SQL injection vulnerabilities.
- **Do** grant the minimum permissions needed — prefer `EXECUTE` on stored procedures over direct `SELECT`/`INSERT`/`UPDATE`/`DELETE` on tables; never grant `db_owner` to application accounts.
- **Do** use schema-qualified object names (e.g., `dbo.Users`, not just `Users`) — unqualified names force SQL Server to check multiple schemas, can cause ambiguity, and bypass plan caching.
- **Don't** store secrets (passwords, API keys, connection strings) in SQL scripts, stored procedures, or database objects — use application-level secrets management or SQL Server features like Always Encrypted.

## Query Design

- **Do** specify only the columns you need instead of `SELECT *` — `SELECT *` breaks code when schema changes, prevents covering index use, and wastes bandwidth.
- **Do** use set-based operations over row-by-row cursors or `WHILE` loops — set-based queries leverage SQL Server's query optimizer; loops serialize work and scale poorly.
- **Do** use CTEs (`WITH cte AS (…)`) to break complex queries into readable, named steps — prefer CTEs over deeply nested subqueries for readability; use temp tables or `#table` when the intermediate result set is large and reused multiple times.
- **Do** filter with `WHERE`, `JOIN … ON`, and `HAVING` on indexed columns — push filters as early as possible to reduce rows processed; avoid applying functions to indexed columns in `WHERE` clauses as this prevents index seeks.
- **Do** handle `NULL` explicitly — use `IS NULL` / `IS NOT NULL`; comparisons using `=` or `<>` against `NULL` always return `UNKNOWN`, not `TRUE` or `FALSE`, and `NULL` propagates through arithmetic and string operations.
- **Don't** use `SELECT DISTINCT` as a workaround for duplicate rows caused by a bad join — fix the join instead; distinct forces a sort or hash operation over the entire result set.
- **Don't** use `NOT IN` with a subquery that can return `NULL` — if any row in the subquery is `NULL`, the entire `NOT IN` predicate evaluates to `UNKNOWN` and returns no rows; prefer `NOT EXISTS` instead.

## Performance

- **Do** add indexes to columns used in `WHERE`, `JOIN`, `ORDER BY`, and `GROUP BY` clauses — missing indexes are the most common cause of table scans and slow queries.
- **Do** use `EXISTS` instead of `COUNT(*) > 0` to check for row existence — `EXISTS` short-circuits on the first matching row; `COUNT` scans all matching rows.
- **Do** use `TOP (n)` with `ORDER BY` to return a limited, deterministic result — without `ORDER BY`, `TOP` returns an arbitrary subset.
- **Do** set `SET NOCOUNT ON` at the top of stored procedures and triggers — suppresses the "n rows affected" message, reducing network traffic and avoiding interference with `@@ROWCOUNT` in calling code.
- **Don't** use `WITH (NOLOCK)` / `READ UNCOMMITTED` to cure blocking — it causes dirty reads, phantom reads, and rows that silently disappear or appear twice; address root-cause blocking with proper indexing and shorter transactions instead.
- **Don't** call scalar user-defined functions (UDFs) in `WHERE` clauses or on large result sets — scalar UDFs execute once per row, inhibit parallelism, and are a common source of performance regressions; prefer inline table-valued functions or computed columns.

## Stored Procedures & Batches

- **Do** wrap multi-statement stored procedures in `BEGIN TRY … END TRY BEGIN CATCH … END CATCH` — unhandled errors in T-SQL silently continue to the next statement; `TRY/CATCH` lets you log errors, roll back transactions, and re-throw with `THROW`.
- **Do** use explicit transactions (`BEGIN TRANSACTION … COMMIT / ROLLBACK`) for any operation that must be atomic — never rely on implicit transaction behaviour for multi-step mutations.
- **Do** check `@@TRANCOUNT` before committing or rolling back in nested transaction scenarios — committing an inner `BEGIN TRANSACTION` does not commit to disk; only the outermost `COMMIT` does.
- **Do** use `OUTPUT` clause to return inserted, updated, or deleted rows in a single round-trip — avoids a separate `SELECT` after a DML statement and is safe under concurrent access.
- **Don't** use `RAISERROR` in new code — use `THROW` instead; `THROW` re-throws the original error number and is consistent with `TRY/CATCH` semantics.

## Schema & Migrations

- **Do** always include `IF NOT EXISTS` / `IF EXISTS` guards in migration scripts — migrations must be idempotent so they can be re-run safely without duplicate-object errors.
- **Do** use `ALTER TABLE … ADD column … NULL` when adding columns to existing tables — adding a `NOT NULL` column without a default to a populated table requires a full table rewrite in older SQL Server versions and acquires a schema lock.
- **Do** name constraints explicitly (e.g., `CONSTRAINT PK_Users PRIMARY KEY`, `CONSTRAINT UQ_Users_Email UNIQUE`) — auto-generated constraint names differ across environments and make migrations and rollbacks harder to script.
