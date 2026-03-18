---
description: "Use when generating or editing React components, managing state, writing Hooks, handling effects, rendering lists, or structuring component trees."
applyTo: "**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}"
---
# React Guidelines

> These rules target React code — component purity, Hooks, state design, effects, list rendering, and DOM access. All rules are derived from official React documentation (react.dev).

## Rules of React

- **Do** keep components and custom Hooks pure — given the same props, state, and context a component must return the same JSX; run side effects in event handlers or `useEffect`, never during render.
- **Do** treat props and state as read-only snapshots — never mutate them directly; use setter functions and create new objects or arrays when updating.
- **Do** treat Hook return values and arguments as immutable — don't modify a value after it is returned from a Hook or before it is passed to one.
- **Do** call Hooks only at the top level of a function component or custom Hook — never inside loops, conditions, nested functions, or `try`/`catch`/`finally` blocks so that React can preserve Hook state between renders.
- **Do** call Hooks only from React function components or custom Hooks — never from regular JavaScript functions or class components.
- **Do** render components through JSX (`<Component />`) — never call a component function directly (e.g., `Component()`) because React cannot track its Hooks or lifecycle.
- **Don't** pass Hooks as regular values, use higher-order hooks, or dynamically mutate a Hook — Hooks must always be called directly and statically.

## State Design

- **Do** derive computed values during render from existing props and state instead of syncing them with `useEffect` and a state setter — eliminates an extra render cycle and keeps logic colocated.
- **Do** use `useMemo` to cache expensive render-time calculations rather than `useEffect` with a state setter — avoids the unnecessary render caused by a second state update.
- **Do** use the `key` prop to reset a component's internal state when a meaningful prop changes, instead of a `useEffect` that watches the prop and calls a setter.
- **Do** place event-specific logic (form submissions, clicks, navigation) in event handlers, not in Effects — Effects are for synchronizing with external systems, not for reacting to user actions.
- **Do** group related state variables that always change together into a single state variable or object — reduces the chance of forgetting to update one of them.
- **Do** model state to avoid impossible combinations — prefer a single status variable (e.g., `'idle' | 'loading' | 'error'`) over multiple independent booleans that can contradict each other.
- **Do** store IDs or indexes in state instead of duplicated objects — derive the full object from the source collection during render to keep data in sync.
- **Do** flatten deeply nested state into normalized (flat) structures that use ID references — nested state is hard to update immutably and easy to desynchronize.
- **Do** lift shared state to the closest common parent component — maintain a single source of truth for each piece of state rather than syncing duplicate state across siblings.

## Effects

- **Do** return a cleanup function from every `useEffect` that allocates resources — tear down subscriptions, timers, or connections to prevent leaks and stale callbacks.
- **Do** include every reactive value (props, state, values derived from them) used inside `useEffect` in its dependency array — never suppress the `react-hooks/exhaustive-deps` lint rule.
- **Do** create objects and functions inside `useEffect` when they are only used by that Effect — avoids adding them to the dependency array and triggering unnecessary re-runs.
- **Do** use state updater functions (e.g., `setCount(prev => prev + 1)`) inside Effects and callbacks instead of reading state directly — removes the state variable from the dependency array.

## Lists and Keys

- **Do** provide a stable, unique `key` from your data (e.g., a database ID) for every element rendered in a list — keys must be unique among siblings and stable across re-renders.
- **Don't** use array index as a `key` when items can be reordered, inserted, or deleted — index keys cause incorrect state mapping and subtle rendering bugs.
- **Don't** generate keys at render time (e.g., `Math.random()` or `crypto.randomUUID()`) — unstable keys force React to recreate the DOM and lose component state on every render.

## Custom Hooks and Refs

- **Do** prefix custom Hook names with `use` followed by a capital letter (e.g., `useOnlineStatus`) — this naming convention enables the linter to enforce the Rules of Hooks inside the Hook.
- **Do** extract repeated `useEffect` logic into custom Hooks to communicate intent, make data flow explicit, and simplify future migration when React provides better built-in APIs.
- **Do** use refs for non-destructive DOM operations only (focus, scroll, measure) — avoid modifying, adding children to, or removing children from DOM nodes that React manages, as it causes inconsistent state and crashes.
