---
description: "Use when building Next.js applications: App Router, Server/Client Components, data fetching, routing, middleware, and rendering strategies."
applyTo: "**/*.{js,jsx,ts,tsx,mjs,mts}"
---
# Next.js Guidelines

## Server & Client Components

- **Do** default to Server Components — only add `'use client'` when the component needs browser APIs, event handlers, or React hooks that require client state.
- **Do** push `'use client'` boundaries as far down the tree as possible — wrap only the interactive leaf, not the entire page.
- **Do** pass serializable props from Server to Client Components — functions, classes, and non-plain objects cannot cross the boundary.
- **Don't** import server-only code (database clients, secrets, `fs`) in Client Components — use the `server-only` package to enforce this at build time.

## Data Fetching

- **Do** fetch data in Server Components or `generateStaticParams` — avoid `useEffect` for initial data loading.
- **Do** use `fetch` with Next.js caching options (`cache: 'force-cache'`, `next: { revalidate: N }`) to control static vs. dynamic rendering.
- **Do** co-locate data fetching with the component that uses it — avoid prop-drilling from layout to deeply nested children; let each `page.tsx` or `layout.tsx` own its own data.
- **Don't** call Route Handlers (`/api/...`) from Server Components — call the underlying logic directly to avoid an unnecessary network round-trip.

## Routing & Middleware

- **Do** use the App Router's file conventions (`page.tsx`, `layout.tsx`, `loading.tsx`, `error.tsx`, `not-found.tsx`) to define routes and UI states.
- **Do** use `loading.tsx` for streaming / suspense fallbacks and `error.tsx` for error boundaries — they integrate with React Suspense automatically.
- **Do** keep middleware (`middleware.ts`) lightweight — it runs on every matching request; heavy logic belongs in Route Handlers or server actions.
- **Don't** use middleware for data fetching or complex auth checks — use it for redirects, rewrites, and header manipulation.

## Rendering Strategies

- **Do** choose the right rendering strategy per route: static (default), dynamic (`export const dynamic = 'force-dynamic'`), or ISR (`revalidate` option).
- **Do** use `generateStaticParams` for static generation of dynamic routes — avoids runtime rendering for known paths.
- **Don't** mark an entire route as dynamic when only one data source is uncacheable — isolate the dynamic part with Suspense boundaries.
