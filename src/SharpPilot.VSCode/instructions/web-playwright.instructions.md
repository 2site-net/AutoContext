---
description: "Use when writing, reviewing, or refactoring Playwright end-to-end tests, page objects, or Playwright-specific APIs."
applyTo: "**/*.{test,spec}.{js,jsx,ts,tsx,mjs,mts}"
---
# Playwright Guidelines

- **Do** use `test` / `expect` from `@playwright/test` — avoid mixing with other assertion libraries.
- **Do** use auto-waiting locators (`page.getByRole`, `page.getByText`, `page.getByLabel`, `page.getByTestId`) instead of CSS/XPath selectors.
- **Do** prefer `getByRole` for interactive elements — it aligns tests with accessible semantics.
- **Do** use `expect(locator).toBeVisible()`, `toHaveText()`, `toHaveValue()` and other web-first assertions that automatically retry.
- **Do** use the Page Object Model to abstract page interactions — keep locators and actions in page classes, assertions in tests.
- **Do** use `test.beforeEach` for navigation and common setup; use fixtures for reusable state (auth, seeded data).
- **Do** use `--project` to run against multiple browsers in CI; test at least Chromium and Firefox.
- **Do** use `page.waitForResponse` or `page.route` when tests depend on network calls — avoid arbitrary `page.waitForTimeout`.
- **Don't** use `page.waitForTimeout` for flaky waits — always wait on a specific condition, locator, or network event.
- **Don't** share page state between tests — each test gets a fresh `BrowserContext` by default; preserve that isolation.
- **Don't** hardcode URLs — use `baseURL` in `playwright.config` and navigate with relative paths.
