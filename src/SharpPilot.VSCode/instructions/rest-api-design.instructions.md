---
description: "Use when designing or implementing REST APIs: resource naming, HTTP methods, status codes, versioning, pagination, error responses, and caching."
---
# REST API Design

## Resource Naming

- [INST0001] **Do** use plural nouns for collection endpoints: `/orders`, `/users`.
- [INST0002] **Do** use path segments for hierarchy: `/users/{id}/orders`.
- [INST0003] **Do** use lowercase with hyphens for multi-word resources: `/line-items`.
- **Do not** use verbs in URIs; let HTTP methods convey the action.
- **Do not** expose internal implementation details (table names, auto-increment IDs) in URIs.

## HTTP Methods

- [INST0004] **Do** use `GET` for safe, idempotent reads.
- [INST0005] **Do** use `POST` to create a new resource in a collection.
- [INST0006] **Do** use `PUT` to fully replace an existing resource.
- [INST0007] **Do** use `PATCH` for partial updates.
- [INST0008] **Do** use `DELETE` to remove a resource.
- [INST0009] **Do** keep `GET`, `PUT`, `PATCH`, and `DELETE` idempotent.

## Status Codes

- [INST0010] **Do** return `200 OK` for successful `GET`, `PUT`, `PATCH`, and `DELETE` with a body.
- [INST0011] **Do** return `201 Created` for successful `POST` with a `Location` header.
- [INST0012] **Do** return `204 No Content` for successful operations with no response body.
- [INST0013] **Do** return `400 Bad Request` for malformed or invalid input.
- [INST0014] **Do** return `401 Unauthorized` when authentication is missing or invalid.
- [INST0015] **Do** return `403 Forbidden` when the caller lacks permission.
- [INST0016] **Do** return `404 Not Found` when the resource does not exist.
- [INST0017] **Do** return `409 Conflict` for state conflicts (duplicate creation, concurrent edit).
- [INST0018] **Do** return `422 Unprocessable Entity` for well-formed but semantically invalid input.
- [INST0019] **Do** return `429 Too Many Requests` when rate-limiting, include a `Retry-After` header.
- **Do not** return `200 OK` with an error payload; use the appropriate 4xx/5xx code.

## Error Responses

- [INST0020] **Do** return a consistent error body with `type`, `title`, `status`, and `detail` fields (RFC 9457 Problem Details).
- [INST0021] **Do** include `errors` for field-level validation failures.
- **Do not** expose stack traces, internal exception messages, or server paths in production error responses.

## Pagination

- [INST0022] **Do** paginate collection endpoints that can return large result sets.
- [INST0023] **Do** use query parameters for pagination: `?page=2&pageSize=25` or `?cursor=<token>&limit=25`.
- [INST0024] **Do** include total count and navigation links in the response when using offset pagination.
- **Prefer** cursor-based pagination for large or frequently changing data sets.

## Filtering, Sorting, and Search

- [INST0025] **Do** use query parameters for filters: `?status=active&createdAfter=2025-01-01`.
- [INST0026] **Do** use a `sort` parameter with field and direction: `?sort=createdAt:desc`.
- [INST0027] **Do** use a `q` parameter for free-text search.
- **Do not** allow arbitrary field filtering without validation and allow-listing.

## Versioning

- **Prefer** URL path versioning (`/v1/orders`) for simplicity and discoverability.
- [INST0028] **Do** support at most two major versions concurrently.
- [INST0029] **Do** communicate deprecation timelines in documentation and response headers.

## Request and Response Bodies

- [INST0030] **Do** use `camelCase` for JSON property names.
- [INST0031] **Do** use ISO 8601 for dates and times: `2025-03-19T14:30:00Z`.
- [INST0032] **Do** return the created or updated resource in `POST`, `PUT`, and `PATCH` responses.
- [INST0033] **Do** accept and return `application/json` by default; support `Content-Type` negotiation when needed.
- **Do not** wrap single-resource responses in unnecessary envelopes.

## Security

- [INST0034] **Do** require HTTPS for all endpoints.
- [INST0035] **Do** authenticate with short-lived tokens (OAuth 2.0 / JWT); avoid API keys in query strings.
- [INST0036] **Do** validate and sanitise all input on the server side regardless of client validation.
- [INST0037] **Do** apply rate limiting and throttling.
- [INST0038] **Do** set appropriate CORS policies; do not use `Access-Control-Allow-Origin: *` in production.

## Caching

- [INST0039] **Do** set `Cache-Control` headers on `GET` responses where appropriate.
- [INST0040] **Do** use `ETag` or `Last-Modified` for conditional requests (`If-None-Match`, `If-Modified-Since`).
- **Do not** cache responses that contain user-specific or sensitive data without proper `Vary` headers.
