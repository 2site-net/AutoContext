---
description: "Use when writing, reviewing, or refactoring Cypress end-to-end tests, custom commands, or Cypress-specific APIs."
applyTo: "**/*.{test,spec,cy}.{js,jsx,ts,tsx,mjs,mts}"
---
# Cypress Guidelines

- **Do** use `cy.get('[data-testid="…"]')` or `cy.findByRole` (with `@testing-library/cypress`) — avoid brittle CSS class or structure-based selectors.
- **Do** rely on Cypress's built-in retry-ability — commands like `cy.get`, `cy.contains`, and `.should()` automatically retry until the assertion passes or times out.
- **Do** use `.should('be.visible')`, `.should('have.text', …)`, `.should('have.value', …)` for assertions — chain them directly on commands.
- **Do** use `cy.intercept` to stub or wait on network requests — avoid `cy.wait(ms)` for timing-based waits.
- **Do** use `beforeEach` to seed data and navigate — keep each test independent; never rely on the state of a previous test.
- **Do** use custom commands (`Cypress.Commands.add`) for repeated multi-step interactions (e.g., login, form fill) — keep them in `cypress/support/commands`.
- **Do** use `cy.session` for caching authenticated state across tests to speed up the suite.
- **Do** set `baseUrl` in `cypress.config` and use relative paths with `cy.visit('/')`.
- **Don't** use `cy.wait(ms)` for arbitrary delays — always wait on a specific alias, intercept, or assertion.
- **Don't** assign Cypress command results to variables (`const el = cy.get(…)`) — Cypress commands are asynchronous and chainable, not synchronous.
- **Don't** use `async/await` with Cypress commands — they return Chainable objects, not Promises.
