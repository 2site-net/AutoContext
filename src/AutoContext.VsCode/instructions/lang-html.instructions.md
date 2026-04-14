---
name: "lang-html (v1.0.0)"
description: "Use when generating or editing HTML markup, structuring documents, adding accessibility attributes, or working with forms and semantic elements."
applyTo: "**/*.{html,razor,cshtml}"
---
# HTML Guidelines

> These instructions target frontend HTML markup — document structure, accessibility, SEO, and client-side security.

## Structure & Semantics

- [INST0001] **Do** use semantic elements (`<nav>`, `<main>`, `<article>`, `<section>`, `<header>`, `<footer>`, `<aside>`) for page structure instead of generic `<div>`s — assistive tech uses these as landmarks for navigation.
- [INST0002] **Do** use heading levels (`<h1>`–`<h6>`) in logical descending order without skipping levels — screen readers build a document outline from headings.
- [INST0003] **Do** use `<button>` for clickable actions and `<a>` for navigation — never use `<div>` or `<span>` with click handlers as interactive elements.
- [INST0004] **Do** set `lang` on the `<html>` element to the document's primary language (e.g., `lang="en"`) — screen readers use this to select correct pronunciation.
- [INST0005] **Do** include `<meta charset="utf-8">` and `<meta name="viewport" content="width=device-width, initial-scale=1">` in the `<head>`.
- [INST0006] **Do** include `<meta name="description">` with a concise page summary in full HTML documents — search engines use it for snippets and it improves discoverability.
- [INST0007] **Do** use `<table>` with `<thead>`, `<th>`, and `scope="col"`/`scope="row"` for tabular data — screen readers use header associations to describe each cell.
- [INST0008] **Don't** use CSS grid or `<div>`s to fake tables, and don't use `<table>` for layout — tables are for data, CSS is for layout.
- [INST0009] **Don't** use `type="text/css"` on `<link>` or `type="text/javascript"` on `<script>` — they are unnecessary in HTML5.

## Forms

- [INST0010] **Do** associate every form input with a `<label>` using `for`/`id` matching or nesting — don't rely on `placeholder` as a substitute for labels.
- [INST0011] **Do** group related form controls with `<fieldset>` and provide a `<legend>` — especially for radio button groups and checkbox sets. Screen readers announce the legend as context for each control.
- [INST0012] **Don't** place interactive elements (links, buttons) inside a `<label>` — it confuses assistive technology and makes the associated input hard to activate.
- [INST0013] **Don't** use placeholder text as the only label for form fields — it disappears on input, has low contrast, and is often ignored by screen readers.

## Accessibility

- [INST0014] **Do** provide descriptive `alt` text on `<img>` elements; use `alt=""` (empty) only for purely decorative images so screen readers skip them.
- [INST0015] **Do** use `aria-label` or `aria-labelledby` only when a visible text label isn't possible — prefer native HTML labeling over ARIA.
- [INST0016] **Do** give `<iframe>` elements a descriptive `title` attribute so assistive tech can identify their content without navigating into them.
- [INST0017] **Do** ensure all interactive elements are keyboard-reachable and operable — every action available via mouse must also be reachable by `Tab` and activated by `Enter`/`Space`.
- [INST0018] **Do** maintain a visible focus indicator on all focusable elements — never suppress the `:focus` outline with `outline: none` unless you replace it with an equally visible custom style.
- [INST0019] **Do** meet WCAG AA color-contrast ratios: 4.5:1 for normal text, 3:1 for large text (≥18pt or ≥14pt bold), and 3:1 for UI components and graphical elements.
- [INST0020] **Do** use `aria-live="polite"` (or `"assertive"` for urgent alerts) on regions that update dynamically so screen readers announce changes without a page reload.
- [INST0021] **Do** set `aria-expanded`, `aria-selected`, `aria-checked`, and `aria-disabled` on custom widgets to reflect their current state — static markup alone isn't enough for interactive patterns (accordions, tabs, dropdowns).
- [INST0022] **Don't** convey meaning through color alone — always pair color cues with text, icons, or patterns so the information remains accessible to color-blind users.
- [INST0023] **Don't** set `aria-hidden="true"` on an element that is focusable or contains focusable children — it removes the element from the accessibility tree while leaving it in the tab order, creating an invisible keyboard trap.

## Security

- [INST0024] **Do** set a Content Security Policy via `<meta http-equiv="Content-Security-Policy">` or server headers — at minimum restrict `script-src` and `object-src` to mitigate XSS and injection attacks.
- [INST0025] **Do** add `rel="noopener noreferrer"` on `<a target="_blank">` links to external sites — without `noopener` the opened page can access `window.opener` and redirect the original tab.
- [INST0026] **Do** add a `sandbox` attribute on `<iframe>` elements embedding third-party content — only allowlist capabilities you actually need (e.g., `sandbox="allow-scripts"`).
- [INST0027] **Do** add `integrity` and `crossorigin` attributes on `<script>` and `<link>` tags loading from external CDNs (Subresource Integrity) — ensures the fetched resource matches an expected hash.
