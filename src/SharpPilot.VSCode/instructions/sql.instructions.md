---
description: "Use when writing SQL queries: naming conventions, formatting, query design, joins, indexing, security, and performance."
---
# SQL

## Formatting

- **Do** write keywords in uppercase: `SELECT`, `FROM`, `WHERE`, `JOIN`, `GROUP BY`.
- **Do** use consistent indentation — place each major clause on its own line.
- **Do** alias tables with meaningful short names: `FROM Orders o JOIN Customers c ON o.CustomerId = c.Id`.
- **Do** qualify all column references with the table alias when joining multiple tables.
- **Do** end statements with a semicolon.

## Naming

- **Do** use `PascalCase` for table and column names.
- **Do** use singular nouns for table names: `Order`, `Customer`, `Product`.
- **Do** name primary keys `Id` or `<Table>Id` (e.g. `OrderId`).
- **Do** name foreign keys `<ReferencedTable>Id` (e.g. `CustomerId`).
- **Do** use descriptive names for indexes: `IX_Order_CustomerId`, `UQ_Customer_Email`.

## Query Design

- **Do** select only the columns you need; avoid `SELECT *` in production code.
- **Do** use `EXISTS` instead of `COUNT(*) > 0` for existence checks.
- **Do** prefer `JOIN` over correlated subqueries for set-based operations.
- **Do** use `LEFT JOIN` when the related row may be absent; use `INNER JOIN` when it must exist.
- **Do** filter early — place the most selective conditions first in `WHERE`.
- **Do** use `UNION ALL` instead of `UNION` when duplicates are intentionally included or impossible.
- **Do not** use functions on indexed columns in `WHERE` clauses; they prevent index use.

## Parameters and Security

- **Do** always use parameterised queries or prepared statements; never concatenate user input into SQL.
- **Do** validate and allow-list any dynamic column or table names if runtime construction is unavoidable.
- **Do not** expose raw database error messages to clients.

## Transactions

- **Do** wrap related mutations in a transaction to ensure atomicity.
- **Do** keep transactions as short as possible to reduce lock contention.
- **Do** handle rollback explicitly on failure.

## Indexing

- **Do** index foreign key columns.
- **Do** index columns frequently used in `WHERE`, `JOIN`, and `ORDER BY`.
- **Do** use covering indexes (include extra columns) for read-heavy queries.
- **Do not** over-index write-heavy tables; each index adds overhead on `INSERT`/`UPDATE`/`DELETE`.

## NULL Handling

- **Do** use `IS NULL` / `IS NOT NULL`; never compare with `= NULL`.
- **Do** be explicit about nullable columns in schema design — prefer `NOT NULL` with a default where semantically correct.
- **Do** account for `NULL` propagation in expressions and aggregates.

## Performance

- **Do** use `EXPLAIN` / `EXPLAIN ANALYZE` to inspect query plans before optimising.
- **Do** prefer set-based operations over row-by-row cursor loops.
- **Do** paginate large result sets rather than fetching all rows.
- **Do** avoid `SELECT DISTINCT` as a workaround for duplicate rows caused by bad joins; fix the join instead.
