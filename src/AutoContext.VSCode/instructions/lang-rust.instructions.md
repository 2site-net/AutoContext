---
name: "lang-rust (v1.0.0)"
description: "Rust coding style instructions: naming conventions, ownership, borrowing, error handling, type design, traits, concurrency, and documentation."
applyTo: "**/*.rs"
---
# Rust Coding Style

## Naming

- [INST0001] **Do** use `snake_case` for functions, methods, variables, modules, and crate names; use `PascalCase` for types (structs, enums, traits, type aliases); use `SCREAMING_SNAKE_CASE` for constants and statics.
- [INST0002] **Do** use descriptive lifetime names (`'buf`, `'conn`, `'src`) when there are multiple lifetimes or the meaning is not obvious from context — `'a` is fine for single-lifetime generics.
- [INST0003] **Don't** encode types or ownership in variable names — write `let timeout = Duration::from_secs(30)`, not `let timeout_dur`; Rust's type system and IDE tooling make such suffixes redundant.

## Ownership & Borrowing

- [INST0004] **Do** prefer borrowing (`&T` or `&mut T`) over cloning or moving when the caller retains ownership — pass by reference unless ownership transfer is semantically meaningful.
- [INST0005] **Do** clone only when you need independent ownership — use `Rc<T>` or `Arc<T>` for shared ownership rather than cloning shared data repeatedly.
- [INST0006] **Do** use `Cow<str>` (or `Cow<[T]>`) for functions that sometimes need to return owned data and sometimes a borrow — it avoids unconditional allocation.
- [INST0007] **Don't** use `unsafe` to work around the borrow checker — redesign the data structure or use interior mutability (`Cell<T>`, `RefCell<T>`, `Mutex<T>`) instead.

## Error Handling

- [INST0008] **Do** use `Result<T, E>` for all recoverable errors — never use `unwrap()` or `expect()` in library code or on any code path reachable in production.
- [INST0009] **Do** use `?` to propagate errors rather than manually matching on `Err` — it is terser and composes well with `From`/`Into` conversions.
- [INST0010] **Do** define a crate-level error type (manually or with `thiserror`) that implements `std::error::Error` — use `anyhow` in application (binary) crates for convenient error context and propagation.
- [INST0011] **Do** attach context to errors before propagating — use `anyhow::Context` in binaries (`.with_context(|| format!("reading {path}"))`) or wrap with a descriptive variant in library error enums.
- [INST0012] **Don't** use `panic!` for expected failures — reserve panics for invariant violations that represent programmer error, not runtime conditions.

## Type Design

- [INST0013] **Do** use enums with data-carrying variants to model sum types — `enum Request { Get { url: String }, Post { url: String, body: Bytes } }` — instead of struct + discriminant field.
- [INST0014] **Do** use the newtype pattern (`struct Metres(f64)`) to give distinct types to values with the same underlying representation — it prevents confusing one unit or identifier for another.
- [INST0015] **Do** use builder structs for types with many optional fields — implement `Default` for the builder and return `Self` from each setter for method chaining.
- [INST0016] **Do** derive `Debug` on all public types and `Clone`, `PartialEq`, `Eq`, `Hash` on value types where it makes semantic sense.
- [INST0017] **Don't** make fields `pub` unless they are part of the stable API — expose mutation through methods so invariants can be enforced.

## Traits

- [INST0018] **Do** implement standard traits (`Display`, `From`, `Iterator`, `Default`) when they fit the type's semantics — implement `From` (not `Into` directly) so the reciprocal `Into` is derived automatically.
- [INST0019] **Do** prefer trait objects (`Box<dyn Trait>` or `&dyn Trait`) for runtime-polymorphic collections and `impl Trait` (or generics) for static dispatch.
- [INST0020] **Do** use `impl Trait` in return position for simple cases where the concrete type is an implementation detail — switch to an explicit generic or trait object once the API needs to be named or stored.
- [INST0021] **Don't** implement `Deref` for non-pointer types purely for convenience — it makes method resolution surprising and obscures ownership.

## Lifetimes

- [INST0022] **Do** let the compiler elide lifetimes wherever the elision rules apply — only annotate explicitly when the compiler requires it or when annotations materially aid readability.
- [INST0023] **Do** prefer owning types in structs over borrowed references when the struct is long-lived or passed across thread boundaries — `String` over `&str`, `Vec<T>` over `&[T]`.
- [INST0024] **Don't** fight the borrow checker by introducing unnecessary lifetime parameters — if the relationships are too complex, restructure the data ownership.

## Concurrency

- [INST0025] **Do** use `Arc<Mutex<T>>` or `Arc<RwLock<T>>` for shared mutable state across threads — prefer `RwLock` when reads vastly outnumber writes.
- [INST0026] **Do** use channels (`std::sync::mpsc` or `crossbeam`) for message-passing between threads — prefer channels over shared state when the ownership model allows it.
- [INST0027] **Do** use async/await (with `tokio` or `async-std`) for I/O-bound concurrency — avoid blocking the async executor with synchronous calls; use `spawn_blocking` for CPU-bound work.
- [INST0028] **Don't** use `std::thread::sleep` or blocking I/O inside async functions — it starves the executor's thread pool.

## Performance & Idioms

- [INST0029] **Do** use iterators and iterator adapters (`map`, `filter`, `flat_map`, `fold`) over hand-written loops — they express intent clearly and are optimised by the compiler.
- [INST0030] **Do** use `String::with_capacity` and `Vec::with_capacity` when the final size is known or can be estimated — it avoids repeated reallocations.
- [INST0031] **Do** use `#[inline]` sparingly and only after profiling — the compiler's inlining heuristics are usually correct; manual annotations are for hot-path functions that cross crate boundaries.
- [INST0032] **Don't** use `.clone()` to silence borrow-checker errors without considering whether a reference or restructuring would serve better.

## Documentation

- [INST0033] **Do** write doc comments (`///`) for all public items — include a one-sentence summary, parameter descriptions, return value, panics, and errors sections where applicable.
- [INST0034] **Do** include at least one `# Examples` section in doc comments for public functions — doctests are compiled and run by `cargo test`.
- [INST0035] **Don't** restate what the code already says — focus doc comments on *why* a decision was made, invariants, and non-obvious behaviour.
