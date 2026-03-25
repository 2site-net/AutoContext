---
description: "Use when writing SQL queries: naming conventions, formatting, query design, joins, indexing, security, and performance."
---
# SQL

## Formatting

- [INST0001] **Do** write keywords in uppercase: `SELECT`, `FROM`, `WHERE`, `JOIN`, `GROUP BY`.
- [INST0002] **Do** use consistent indentation — place each major clause on its own line.
- [INST0003] **Do** alias tables with meaningful short names: `FROM Orders o JOIN Customers c ON o.CustomerId = c.Id`.
- [INST0004] **Do** qualify all column references with the table alias when joining multiple tables.
- [INST0005] **Do** end statements with a semicolon.

## Naming

- [INST0006] **Do** use `PascalCase` for table and column names.
- [INST0007] **Do** use singular nouns for table names: `Order`, `Customer`, `Product`.
- [INST0008] **Do** name primary keys `Id` or `<Table>Id` (e.g. `OrderId`).
- [INST0009] **Do** name foreign keys `<ReferencedTable>Id` (e.g. `CustomerId`).
- [INST0010] **Do** use descriptive names for indexes: `IX_Order_CustomerId`, `UQ_Customer_Email`.

## Query Design

- [INST0011] **Do** select only the columns you need; avoid `SELECT *` in production code.
- [INST0012] **Do** use `EXISTS` instead of `COUNT(*) > 0` for existence checks.
- [INST0013] **Do** prefer `JOIN` over correlated subqueries for set-based operations.
- [INST0014] **Do** use `LEFT JOIN` when the related row may be absent; use `INNER JOIN` when it must exist.
- [INST0015] **Do** filter early — place the most selective conditions first in `WHERE`.
- [INST0016] **Do** use `UNION ALL` instead of `UNION` when duplicates are intentionally included or impossible.
- **Do not** use functions on indexed columns in `WHERE` clauses; they prevent index use.

## Parameters and Security

- [INST0017] **Do** always use parameterised queries or prepared statements; never concatenate user input into SQL.
- [INST0018] **Do** validate and allow-list any dynamic column or table names if runtime construction is unavoidable.
- **Do not** expose raw database error messages to clients.

## Transactions

- [INST0019] **Do** wrap related mutations in a transaction to ensure atomicity.
- [INST0020] **Do** keep transactions as short as possible to reduce lock contention.
- [INST0021] **Do** handle rollback explicitly on failure.

## Indexing

- [INST0022] **Do** index foreign key columns.
- [INST0023] **Do** index columns frequently used in `WHERE`, `JOIN`, and `ORDER BY`.
- [INST0024] **Do** use covering indexes (include extra columns) for read-heavy queries.
- **Do not** over-index write-heavy tables; each index adds overhead on `INSERT`/`UPDATE`/`DELETE`.

## NULL Handling

- [INST0025] **Do** use `IS NULL` / `IS NOT NULL`; never compare with `= NULL`.
- [INST0026] **Do** be explicit about nullable columns in schema design — prefer `NOT NULL` with a default where semantically correct.
- [INST0027] **Do** account for `NULL` propagation in expressions and aggregates.

## Performance

- [INST0028] **Do** use `EXPLAIN` / `EXPLAIN ANALYZE` to inspect query plans before optimising.
- [INST0029] **Do** prefer set-based operations over row-by-row cursor loops.
- [INST0030] **Do** paginate large result sets rather than fetching all rows.
- [INST0031] **Do** avoid `SELECT DISTINCT` as a workaround for duplicate rows caused by bad joins; fix the join instead.
