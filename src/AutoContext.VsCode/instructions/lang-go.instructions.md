---
name: "lang-go (v1.0.0)"
description: "Go coding style instructions: naming conventions, error handling, interfaces, concurrency, packages, testing, and documentation."
applyTo: "**/*.go"
---

# Go Coding Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Naming

- [INST0001] **Do** use `MixedCaps` (exported) and `mixedCaps` (unexported) for all identifiers — Go does not use underscores in names except for test functions (`Test_`) and generated code.
- [INST0002] **Do** keep names short and contextual — `r` for a reader in a small scope, `db` for a database handle, `srv` for a server — Go favours brevity when the type or context makes the meaning clear.
- [INST0003] **Do** name interfaces after the method they declare with an `-er` suffix — `Reader`, `Writer`, `Closer`, `Stringer` — or use a descriptive noun when a single-method name does not fit.
- [INST0004] **Do** name packages as short, lowercase, singular nouns — `http`, `json`, `user` — that describe what the package provides, not what it contains.
- [INST0005] **Don't** stutter package and type names — write `http.Client`, not `http.HTTPClient`; the package name already provides context.

### Error Handling

- [INST0006] **Do** return `error` as the last return value and check it immediately — `if err != nil { return fmt.Errorf("opening config: %w", err) }`.
- [INST0007] **Do** wrap errors with `fmt.Errorf("context: %w", err)` to build a chain of context — callers can unwrap with `errors.Is` and `errors.As`.
- [INST0008] **Do** define sentinel errors with `var ErrNotFound = errors.New("not found")` and custom error types with the `Error() string` method when callers need to inspect error details.
- [INST0009] **Do** handle errors at the point where you can add meaningful context or take action — don't log-and-return or swallow errors silently.
- [INST0010] **Don't** use `panic` for expected failures — reserve it for truly unrecoverable programmer errors; use `log.Fatal` in `main` for startup failures.

### Interfaces

- [INST0011] **Do** define interfaces at the point of consumption (the calling package), not at the point of implementation — this follows the Go proverb "accept interfaces, return structs."
- [INST0012] **Do** keep interfaces small — one or two methods is ideal; compose larger behaviours by embedding small interfaces.
- [INST0013] **Don't** define interfaces preemptively — wait until you have at least two concrete implementations or a clear testing need before introducing an interface.

### Type Design

- [INST0014] **Do** use struct embedding for composition — embed an `io.Reader` in a struct to reuse its methods rather than delegating manually.
- [INST0015] **Do** use the zero value as a useful default — design structs so that `var cfg Config` or `Config{}` is immediately usable without extra initialisation.
- [INST0016] **Do** use a constructor function (`NewClient(...)`) when a type requires validation, non-trivial setup, or unexported fields that the caller cannot set directly.
- [INST0017] **Do** use pointer receivers when the method mutates the receiver or the struct is large; use value receivers for small, immutable types — be consistent within a type.
- [INST0018] **Don't** export struct fields unless they are part of the public API or needed for serialisation — use accessor methods to protect invariants.

### Concurrency

- [INST0019] **Do** use goroutines and channels for concurrent workflows — prefer channels for communication and `sync.Mutex` or `sync.RWMutex` for protecting shared state.
- [INST0020] **Do** pass a `context.Context` as the first parameter to any function that performs I/O, blocks, or may be cancelled — respect cancellation by selecting on `ctx.Done()`.
- [INST0021] **Do** use `sync.WaitGroup` to wait for a known set of goroutines to finish and `errgroup.Group` when any goroutine failure should cancel the rest.
- [INST0022] **Do** use `sync.Once` for lazy one-time initialisation instead of a `bool` + `Mutex` pattern.
- [INST0023] **Don't** start goroutines without a clear shutdown path — every goroutine must be joinable or cancellable to prevent leaks.

### Packages & Imports

- [INST0024] **Do** group imports in three blocks separated by blank lines — standard library, external dependencies, internal packages — `goimports` enforces this automatically.
- [INST0025] **Do** avoid circular imports by pushing shared types into a separate package or using interfaces at package boundaries.
- [INST0026] **Don't** use the dot import (`. "pkg"`) or blank import (`_ "pkg"`) except for side-effect registration (e.g., `_ "github.com/lib/pq"` for database drivers) and test helpers.

### Testing

- [INST0027] **Do** name test functions `TestXxx` and use `t.Run("subtest name", ...)` for table-driven tests — each row should be a self-contained case with a descriptive name.
- [INST0028] **Do** use `t.Helper()` in test helper functions so failure messages report the caller's line number, not the helper's.
- [INST0029] **Do** use `t.Parallel()` for tests that have no shared mutable state — it enables concurrent test execution and surfaces data races.
- [INST0030] **Do** write clear assertion messages — the standard `testing` package with formatted `t.Errorf`/`t.Fatalf` messages works well; if using `testify`, prefer `require` over `assert` when a failure should stop the test immediately to avoid cascading nil-pointer panics.
- [INST0031] **Don't** test unexported functions directly — test through the public API; if an unexported function is complex enough to need its own tests, consider extracting it into its own package.

### Performance & Idioms

- [INST0032] **Do** use `make([]T, 0, n)` to pre-allocate slices when the capacity is known — it avoids repeated grow-and-copy reallocations.
- [INST0033] **Do** use `strings.Builder` for building strings in a loop instead of repeated concatenation — it avoids O(n²) allocations.
- [INST0034] **Do** use `defer` for cleanup (closing files, unlocking mutexes, flushing buffers) — it keeps the cleanup close to the acquisition and executes even on early returns.
- [INST0035] **Don't** use `init()` functions except for driver or codec registration — they make testing and dependency injection harder by introducing implicit global state.

### Documentation

- [INST0036] **Do** write a doc comment on every exported function, type, and package — start with the name of the element: `// Client represents an HTTP client that...`.
- [INST0037] **Don't** restate what the code already says — focus comments on *why* a decision was made, non-obvious behaviour, and concurrency contracts.
