---
description: "Use when writing, reviewing, or refactoring Cypress end-to-end tests, custom commands, or Cypress-specific APIs."
applyTo: "**/*.{test,spec,cy}.{js,jsx,ts,tsx,mjs,mts}"
---
# Cypress Guidelines

- [INST0001] **Do** use `cy.get('[data-testid="…"]')` or `cy.findByRole` (with `@testing-library/cypress`) — avoid brittle CSS class or structure-based selectors.
- [INST0002] **Do** rely on Cypress's built-in retry-ability — commands like `cy.get`, `cy.contains`, and `.should()` automatically retry until the assertion passes or times out.
- [INST0003] **Do** use `.should('be.visible')`, `.should('have.text', …)`, `.should('have.value', …)` for assertions — chain them directly on commands.
- [INST0004] **Do** use `cy.intercept` to stub or wait on network requests — avoid `cy.wait(ms)` for timing-based waits.
- [INST0005] **Do** use `beforeEach` to seed data and navigate — keep each test independent; never rely on the state of a previous test.
- [INST0006] **Do** use custom commands (`Cypress.Commands.add`) for repeated multi-step interactions (e.g., login, form fill) — keep them in `cypress/support/commands`.
- [INST0007] **Do** use `cy.session` for caching authenticated state across tests to speed up the suite.
- [INST0008] **Do** set `baseUrl` in `cypress.config` and use relative paths with `cy.visit('/')`.
- [INST0009] **Don't** use `cy.wait(ms)` for arbitrary delays — always wait on a specific alias, intercept, or assertion.
- [INST0010] **Don't** assign Cypress command results to variables (`const el = cy.get(…)`) — Cypress commands are asynchronous and chainable, not synchronous.
- [INST0011] **Don't** use `async/await` with Cypress commands — they return Chainable objects, not Promises.
