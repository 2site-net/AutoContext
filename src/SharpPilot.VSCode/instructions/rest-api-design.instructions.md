---
description: "Use when designing or implementing REST APIs: resource naming, HTTP methods, status codes, versioning, pagination, error responses, and caching."
---
# REST API Design

## Resource Naming

- **Do** use plural nouns for collection endpoints: `/orders`, `/users`.
- **Do** use path segments for hierarchy: `/users/{id}/orders`.
- **Do** use lowercase with hyphens for multi-word resources: `/line-items`.
- **Do not** use verbs in URIs; let HTTP methods convey the action.
- **Do not** expose internal implementation details (table names, auto-increment IDs) in URIs.

## HTTP Methods

- **Do** use `GET` for safe, idempotent reads.
- **Do** use `POST` to create a new resource in a collection.
- **Do** use `PUT` to fully replace an existing resource.
- **Do** use `PATCH` for partial updates.
- **Do** use `DELETE` to remove a resource.
- **Do** keep `GET`, `PUT`, `PATCH`, and `DELETE` idempotent.

## Status Codes

- **Do** return `200 OK` for successful `GET`, `PUT`, `PATCH`, and `DELETE` with a body.
- **Do** return `201 Created` for successful `POST` with a `Location` header.
- **Do** return `204 No Content` for successful operations with no response body.
- **Do** return `400 Bad Request` for malformed or invalid input.
- **Do** return `401 Unauthorized` when authentication is missing or invalid.
- **Do** return `403 Forbidden` when the caller lacks permission.
- **Do** return `404 Not Found` when the resource does not exist.
- **Do** return `409 Conflict` for state conflicts (duplicate creation, concurrent edit).
- **Do** return `422 Unprocessable Entity` for well-formed but semantically invalid input.
- **Do** return `429 Too Many Requests` when rate-limiting, include a `Retry-After` header.
- **Do not** return `200 OK` with an error payload; use the appropriate 4xx/5xx code.

## Error Responses

- **Do** return a consistent error body with `type`, `title`, `status`, and `detail` fields (RFC 9457 Problem Details).
- **Do** include `errors` for field-level validation failures.
- **Do not** expose stack traces, internal exception messages, or server paths in production error responses.

## Pagination

- **Do** paginate collection endpoints that can return large result sets.
- **Do** use query parameters for pagination: `?page=2&pageSize=25` or `?cursor=<token>&limit=25`.
- **Do** include total count and navigation links in the response when using offset pagination.
- **Prefer** cursor-based pagination for large or frequently changing data sets.

## Filtering, Sorting, and Search

- **Do** use query parameters for filters: `?status=active&createdAfter=2025-01-01`.
- **Do** use a `sort` parameter with field and direction: `?sort=createdAt:desc`.
- **Do** use a `q` parameter for free-text search.
- **Do not** allow arbitrary field filtering without validation and allow-listing.

## Versioning

- **Prefer** URL path versioning (`/v1/orders`) for simplicity and discoverability.
- **Do** support at most two major versions concurrently.
- **Do** communicate deprecation timelines in documentation and response headers.

## Request and Response Bodies

- **Do** use `camelCase` for JSON property names.
- **Do** use ISO 8601 for dates and times: `2025-03-19T14:30:00Z`.
- **Do** return the created or updated resource in `POST`, `PUT`, and `PATCH` responses.
- **Do** accept and return `application/json` by default; support `Content-Type` negotiation when needed.
- **Do not** wrap single-resource responses in unnecessary envelopes.

## Security

- **Do** require HTTPS for all endpoints.
- **Do** authenticate with short-lived tokens (OAuth 2.0 / JWT); avoid API keys in query strings.
- **Do** validate and sanitise all input on the server side regardless of client validation.
- **Do** apply rate limiting and throttling.
- **Do** set appropriate CORS policies; do not use `Access-Control-Allow-Origin: *` in production.

## Caching

- **Do** set `Cache-Control` headers on `GET` responses where appropriate.
- **Do** use `ETag` or `Last-Modified` for conditional requests (`If-None-Match`, `If-Modified-Since`).
- **Do not** cache responses that contain user-specific or sensitive data without proper `Vary` headers.
