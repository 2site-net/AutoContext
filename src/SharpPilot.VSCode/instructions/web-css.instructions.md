---
description: "Use when generating or editing CSS stylesheets, working with selectors, custom properties, layout, responsive design, or accessibility-related styles."
applyTo: "**/*.css"
---
# CSS Guidelines

> These rules target CSS stylesheets — layout, theming, selectors, responsive design, and accessibility.

- **Do** use CSS custom properties (`var(--*)`) for colors, spacing, and typography tokens — centralizes theming and eases consistency across the stylesheet.
- **Do** use `rem` for font sizes and relative units for spacing — respects user font-size preferences and scales consistently.
- **Do** use CSS logical properties (`margin-block`, `padding-inline`, `inset-inline-start`) instead of physical directional properties (`margin-top`, `padding-left`, `left`) — adapts automatically to different writing modes and text directions.
- **Do** use `flexbox` or `grid` for layout — avoid floats and positioning hacks for page structure.
- **Do** use mobile-first `min-width` media queries for responsive design — start with the smallest viewport and progressively enhance for larger screens.
- **Do** provide a visible `:focus-visible` style on all interactive elements — never remove the browser's default focus outline without providing an equivalent replacement (WCAG 2.4.7).
- **Do** wrap non-essential animations and transitions in a `@media (prefers-reduced-motion: no-preference)` block, or disable them under `prefers-reduced-motion: reduce` — respects users with vestibular sensitivities (WCAG 2.3.3).
- **Do** ensure text has a minimum 4.5 : 1 contrast ratio against its background (3 : 1 for large text ≥ 18pt / bold ≥ 14pt) — verify with a contrast checker, not by eye (WCAG 1.4.3).
- **Do** order properties consistently within each declaration block — e.g., positioning → box model → typography → visual — to make scanning and reviewing easier.
- **Do** prefer flat, single-class selectors over deeply nested descendant chains — keeps specificity low and overrides predictable.
- **Do** use shorthand properties (`margin`, `padding`, `background`, `font`) when setting all sub-values — reduces redundancy and keeps declarations concise.
- **Do** place each selector on its own line in multi-selector rules — improves readability and produces cleaner diffs.
- **Do** respect `prefers-color-scheme` when the project has a dark/light theme — detect the OS default with the media query and layer a manual toggle override on top.
- **Do** use `gap` for spacing between flex/grid children instead of margin combined with negative-margin hacks — `gap` doesn't affect outer spacing and avoids collapsing-margin surprises.
- **Do** use `aspect-ratio` for fixed aspect ratios instead of the `padding-top` percentage hack — clearer intent and works on any element.
- **Do** use `clamp()` for fluid typography (e.g., `font-size: clamp(1rem, 2.5vw, 2rem)`) instead of separate breakpoint-based font-size overrides — scales smoothly without extra media queries.
- **Do** set `font-display: swap` (or `optional`) on `@font-face` declarations — prevents invisible text (FOIT) while custom fonts load.
- **Don't** set a fixed `height` on containers that hold text — use `min-height` or let content define height so text doesn't overflow when users zoom or resize fonts (WCAG 1.4.4).
- **Don't** use `!important` unless overriding third-party styles you cannot otherwise control — it breaks the natural cascade and makes debugging specificity issues harder.
- **Don't** use ID selectors (`#id`) for styling — their high specificity makes them difficult to override; use class selectors instead.
- **Don't** rely on color alone to convey information (e.g., red for errors) — pair with text labels, icons, or patterns so color-blind users are not excluded (WCAG 1.4.1).
- **Don't** use overqualified selectors like `div.classname` — the element prefix adds specificity without benefit and couples styles to the markup structure.
