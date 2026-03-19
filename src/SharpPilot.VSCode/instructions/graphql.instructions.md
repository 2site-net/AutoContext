---
description: "Use when designing GraphQL schemas, resolvers, queries, mutations, or subscriptions."
---
# GraphQL Guidelines

## Schema Design

- **Do** design schemas around client needs, not database tables — expose the shape consumers want, not your internal model.
- **Do** use descriptive, domain-specific type names (`OrderLineItem`, not `Item`) — the schema is a public API contract.
- **Do** make mutations return the affected object (or a payload type with the result and errors) — avoids a follow-up query after every write.
- **Do** use `input` types for mutation arguments — keep argument lists clean and enable reuse.
- **Do** use `ID` scalar for identifiers and custom scalars (e.g., `DateTime`, `URI`) when a plain `String` would lose semantic meaning.
- **Don't** expose internal IDs or implementation details (database column names, sequence numbers) in the schema.

## Queries & Performance

- **Do** implement pagination with cursor-based connections (`first` / `after` / `edges` / `pageInfo`) for list fields — offset pagination performs poorly at scale.
- **Do** use DataLoader (or equivalent batching) to solve N+1 problems — never issue a database query per resolver invocation.
- **Do** enforce query depth and complexity limits — unbounded nested queries can exhaust server resources.
- **Don't** return unbounded lists — always require a `first` or `last` argument, or set a server-side default limit.

## Mutations & Error Handling

- **Do** name mutations as verb phrases describing the action: `createUser`, `cancelOrder`, `assignRole`.
- **Do** use union return types or a `UserError` field in the payload for domain-level errors — reserve transport-level errors for infrastructure failures.
- **Don't** use mutations for read-only operations — if it doesn't change state, it's a query.

## Subscriptions

- **Do** scope subscriptions to a specific entity or event — avoid broadcasting the entire state on every change.
- **Don't** use subscriptions for data that changes infrequently — polling or cache invalidation is simpler and cheaper.
