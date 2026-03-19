---
description: "Use when generating or editing HTML markup, structuring documents, adding accessibility attributes, or working with forms and semantic elements."
applyTo: "**/*.{html,razor,cshtml}"
---
# HTML Guidelines

> These rules target frontend HTML markup — document structure, accessibility, SEO, and client-side security.

## Structure & Semantics

* **Do** use semantic elements (`<nav>`, `<main>`, `<article>`, `<section>`, `<header>`, `<footer>`, `<aside>`) for page structure instead of generic `<div>`s — assistive tech uses these as landmarks for navigation.
* **Do** use heading levels (`<h1>`–`<h6>`) in logical descending order without skipping levels — screen readers build a document outline from headings.
* **Do** use `<button>` for clickable actions and `<a>` for navigation — never use `<div>` or `<span>` with click handlers as interactive elements.
* **Do** set `lang` on the `<html>` element to the document's primary language (e.g., `lang="en"`) — screen readers use this to select correct pronunciation.
* **Do** include `<meta charset="utf-8">` and `<meta name="viewport" content="width=device-width, initial-scale=1">` in the `<head>`.
* **Do** include `<meta name="description">` with a concise page summary in full HTML documents — search engines use it for snippets and it improves discoverability.
* **Do** use `<table>` with `<thead>`, `<th>`, and `scope="col"`/`scope="row"` for tabular data — screen readers use header associations to describe each cell.
* **Don't** use CSS grid or `<div>`s to fake tables, and don't use `<table>` for layout — tables are for data, CSS is for layout.
* **Don't** use `type="text/css"` on `<link>` or `type="text/javascript"` on `<script>` — they are unnecessary in HTML5.

## Forms

* **Do** associate every form input with a `<label>` using `for`/`id` matching or nesting — don't rely on `placeholder` as a substitute for labels.
* **Do** group related form controls with `<fieldset>` and provide a `<legend>` — especially for radio button groups and checkbox sets. Screen readers announce the legend as context for each control.
* **Don't** place interactive elements (links, buttons) inside a `<label>` — it confuses assistive technology and makes the associated input hard to activate.
* **Don't** use placeholder text as the only label for form fields — it disappears on input, has low contrast, and is often ignored by screen readers.

## Accessibility

* **Do** provide descriptive `alt` text on `<img>` elements; use `alt=""` (empty) only for purely decorative images so screen readers skip them.
* **Do** use `aria-label` or `aria-labelledby` only when a visible text label isn't possible — prefer native HTML labeling over ARIA.
* **Do** give `<iframe>` elements a descriptive `title` attribute so assistive tech can identify their content without navigating into them.
* **Do** ensure all interactive elements are keyboard-reachable and operable — every action available via mouse must also be reachable by `Tab` and activated by `Enter`/`Space`.
* **Do** maintain a visible focus indicator on all focusable elements — never suppress the `:focus` outline with `outline: none` unless you replace it with an equally visible custom style.
* **Do** meet WCAG AA color-contrast ratios: 4.5:1 for normal text, 3:1 for large text (≥18pt or ≥14pt bold), and 3:1 for UI components and graphical elements.
* **Do** use `aria-live="polite"` (or `"assertive"` for urgent alerts) on regions that update dynamically so screen readers announce changes without a page reload.
* **Do** set `aria-expanded`, `aria-selected`, `aria-checked`, and `aria-disabled` on custom widgets to reflect their current state — static markup alone isn't enough for interactive patterns (accordions, tabs, dropdowns).
* **Don't** convey meaning through color alone — always pair color cues with text, icons, or patterns so the information remains accessible to color-blind users.
* **Don't** set `aria-hidden="true"` on an element that is focusable or contains focusable children — it removes the element from the accessibility tree while leaving it in the tab order, creating an invisible keyboard trap.

## Security

* **Do** set a Content Security Policy via `<meta http-equiv="Content-Security-Policy">` or server headers — at minimum restrict `script-src` and `object-src` to mitigate XSS and injection attacks.
* **Do** add `rel="noopener noreferrer"` on `<a target="_blank">` links to external sites — without `noopener` the opened page can access `window.opener` and redirect the original tab.
* **Do** add a `sandbox` attribute on `<iframe>` elements embedding third-party content — only allowlist capabilities you actually need (e.g., `sandbox="allow-scripts"`).
* **Do** add `integrity` and `crossorigin` attributes on `<script>` and `<link>` tags loading from external CDNs (Subresource Integrity) — ensures the fetched resource matches an expected hash.
