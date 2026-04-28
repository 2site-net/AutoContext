---
name: "rest-api-design (v1.0.0)"
description: "Use when designing or implementing REST APIs: resource naming, HTTP methods, status codes, versioning, pagination, error responses, and caching."
---

# REST API Design Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Resource Naming

- [INST0001] **Do** use plural nouns for collection endpoints: `/orders`, `/users`.
- [INST0002] **Do** use path segments for hierarchy: `/users/{id}/orders`.
- [INST0003] **Do** use lowercase with hyphens for multi-word resources: `/line-items`.
- [INST0004] **Don't** use verbs in URIs; let HTTP methods convey the action.
- [INST0005] **Don't** expose internal implementation details (table names, auto-increment IDs) in URIs.

### HTTP Methods

- [INST0006] **Do** use `GET` for safe, idempotent reads.
- [INST0007] **Do** use `POST` to create a new resource in a collection.
- [INST0008] **Do** use `PUT` to fully replace an existing resource.
- [INST0009] **Do** use `PATCH` for partial updates.
- [INST0010] **Do** use `DELETE` to remove a resource.
- [INST0011] **Do** keep `GET`, `PUT`, `PATCH`, and `DELETE` idempotent.

### Status Codes

- [INST0012] **Do** return `200 OK` for successful `GET`, `PUT`, `PATCH`, and `DELETE` with a body.
- [INST0013] **Do** return `201 Created` for successful `POST` with a `Location` header.
- [INST0014] **Do** return `204 No Content` for successful operations with no response body.
- [INST0015] **Do** return `400 Bad Request` for malformed or invalid input.
- [INST0016] **Do** return `401 Unauthorized` when authentication is missing or invalid.
- [INST0017] **Do** return `403 Forbidden` when the caller lacks permission.
- [INST0018] **Do** return `404 Not Found` when the resource does not exist.
- [INST0019] **Do** return `409 Conflict` for state conflicts (duplicate creation, concurrent edit).
- [INST0020] **Do** return `422 Unprocessable Entity` for well-formed but semantically invalid input.
- [INST0021] **Do** return `429 Too Many Requests` when rate-limiting, include a `Retry-After` header.
- [INST0022] **Don't** return `200 OK` with an error payload; use the appropriate 4xx/5xx code.

### Error Responses

- [INST0023] **Do** return a consistent error body with `type`, `title`, `status`, and `detail` fields (RFC 9457 Problem Details).
- [INST0024] **Do** include `errors` for field-level validation failures.
- [INST0025] **Don't** expose stack traces, internal exception messages, or server paths in production error responses.

### Pagination

- [INST0026] **Do** paginate collection endpoints that can return large result sets.
- [INST0027] **Do** use query parameters for pagination: `?page=2&pageSize=25` or `?cursor=<token>&limit=25`.
- [INST0028] **Do** include total count and navigation links in the response when using offset pagination.
- [INST0029] **Do** prefer cursor-based pagination for large or frequently changing data sets.

### Filtering, Sorting, and Search

- [INST0030] **Do** use query parameters for filters: `?status=active&createdAfter=2025-01-01`.
- [INST0031] **Do** use a `sort` parameter with field and direction: `?sort=createdAt:desc`.
- [INST0032] **Do** use a `q` parameter for free-text search.
- [INST0033] **Don't** allow arbitrary field filtering without validation and allow-listing.

### Versioning

- [INST0034] **Do** prefer URL path versioning (`/v1/orders`) for simplicity and discoverability.
- [INST0035] **Do** support at most two major versions concurrently.
- [INST0036] **Do** communicate deprecation timelines in documentation and response headers.

### Request and Response Bodies

- [INST0037] **Do** use `camelCase` for JSON property names.
- [INST0038] **Do** use ISO 8601 for dates and times: `2025-03-19T14:30:00Z`.
- [INST0039] **Do** return the created or updated resource in `POST`, `PUT`, and `PATCH` responses.
- [INST0040] **Do** accept and return `application/json` by default; support `Content-Type` negotiation when needed.
- [INST0041] **Don't** wrap single-resource responses in unnecessary envelopes.

### Security

- [INST0042] **Do** require HTTPS for all endpoints.
- [INST0043] **Do** authenticate with short-lived tokens (OAuth 2.0 / JWT); avoid API keys in query strings.
- [INST0044] **Do** validate and sanitise all input on the server side regardless of client validation.
- [INST0045] **Do** apply rate limiting and throttling.
- [INST0046] **Do** set appropriate CORS policies; do not use `Access-Control-Allow-Origin: *` in production.

### Caching

- [INST0047] **Do** set `Cache-Control` headers on `GET` responses where appropriate.
- [INST0048] **Do** use `ETag` or `Last-Modified` for conditional requests (`If-None-Match`, `If-Modified-Since`).
- [INST0049] **Don't** cache responses that contain user-specific or sensitive data without proper `Vary` headers.
