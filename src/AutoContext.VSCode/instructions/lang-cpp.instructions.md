---
description: "C++ coding style instructions: RAII, smart pointers, move semantics, type design, modern features, templates, error handling, STL usage, and concurrency."
applyTo: "**/*.{cpp,cxx,cc,h,hpp,hxx,hh}"
version: "1.0.0"
---
# C++ Coding Style

## Naming

- [INST0001] **Do** use PascalCase for classes, structs, enums, type aliases, and concepts; use camelCase for functions, methods, variables, and parameters.
- [INST0002] **Do** use SCREAMING_SNAKE_CASE for macros and compile-time constants; prefer `constexpr` variables or `enum class` values over `#define`.
- [INST0003] **Do** use the `snake_case` convention for namespaces — `namespace net::http` — mirroring the standard library style.
- [INST0004] **Don't** use names that start with an underscore followed by an uppercase letter or double underscores — those are reserved by the C++ standard.

## Resource Management

- [INST0005] **Do** acquire resources in constructors and release them in destructors (RAII) — never rely on the caller to remember a separate cleanup step.
- [INST0006] **Do** use `std::unique_ptr` for exclusive ownership and `std::shared_ptr` only when ownership is genuinely shared among multiple owners with independent lifetimes.
- [INST0007] **Do** use `std::make_unique` and `std::make_shared` to create smart pointers — they are exception-safe and avoid repeating the type.
- [INST0008] **Do** pass raw pointers or references for non-owning access — `const Widget&` for observation, `Widget*` when nullability is needed.
- [INST0009] **Do** follow the Rule of Zero — if a class manages no resources directly, do not declare a destructor, copy/move constructors, or copy/move assignment operators.
- [INST0010] **Do** follow the Rule of Five when a class manages a resource — if you declare any of the destructor, copy constructor, copy assignment, move constructor, or move assignment, declare all five explicitly.
- [INST0011] **Don't** use `new` and `delete` directly — wrap every heap allocation in a smart pointer or RAII container.

## Type Design

- [INST0012] **Do** use `enum class` (scoped enumerations) instead of unscoped `enum` — they prevent implicit conversion to `int` and avoid polluting the enclosing namespace.
- [INST0013] **Do** mark classes not designed for inheritance as `final` — it communicates intent and enables compiler devirtualisation.
- [INST0014] **Do** use `struct` for passive data aggregates with all-public members and `class` when the type has invariants enforced by constructors or private state.
- [INST0015] **Do** declare single-argument constructors `explicit` unless implicit conversion is intentional — this prevents surprising narrowing and type conversions.
- [INST0016] **Don't** use C-style casts — use `static_cast`, `dynamic_cast`, `const_cast`, or `reinterpret_cast` to make the cast intent explicit and searchable.

## Modern C++ Features

- [INST0017] **Do** use `auto` for local variable declarations where the type is clear from the initialiser — `auto stream = std::ifstream{"config.json"}`.
- [INST0018] **Do** use range-based `for` loops — `for (const auto& item : collection)` — instead of index-based or iterator-based loops when the index is not needed.
- [INST0019] **Do** use structured bindings to unpack pairs, tuples, and aggregates — `auto [key, value] = *map.begin();` — instead of `.first` / `.second`.
- [INST0020] **Do** use `std::optional<T>` to represent values that may be absent instead of sentinel values, nullable pointers, or out-parameters.
- [INST0021] **Do** use `std::variant<Ts...>` for type-safe tagged unions and `std::visit` with an overload set for exhaustive dispatch.
- [INST0022] **Do** use `std::string_view` for non-owning read-only string parameters instead of `const std::string&` — it avoids unnecessary allocations when called with string literals or substrings.
- [INST0023] **Do** use `std::span<T>` (C++20) for non-owning contiguous range parameters instead of `T* + size` pairs.
- [INST0024] **Do** use `constexpr` for values and functions that can be evaluated at compile time — it enables compile-time validation and eliminates runtime overhead.
- [INST0025] **Do** use `[[nodiscard]]` on functions whose return value must not be silently ignored — error codes, factory functions, and computational results.

## Templates & Generic Programming

- [INST0026] **Do** constrain templates with C++20 concepts or `static_assert` (pre-C++20) to produce clear diagnostics when type requirements are not met.
- [INST0027] **Do** prefer function overloading or `if constexpr` over SFINAE when branching on type traits — the resulting code is clearer and compiles faster.
- [INST0028] **Don't** define template implementations in `.cpp` files — they must be visible at the point of instantiation, so keep them in headers or explicit-instantiation source files.

## Error Handling

- [INST0029] **Do** use exceptions for truly exceptional, unrecoverable failures and `std::expected<T, E>` (C++23) or `std::optional<T>` for anticipated, recoverable failures.
- [INST0030] **Do** throw by value and catch by `const` reference — `throw std::runtime_error{"msg"}; ... catch (const std::exception& ex)`.
- [INST0031] **Do** provide strong exception safety for mutating operations when practical — if an operation throws, the object remains in its original state.
- [INST0032] **Do** mark functions that never throw as `noexcept` — it enables move optimisations in standard containers and documents the contract.
- [INST0033] **Don't** use exceptions across module or ABI boundaries (e.g., shared libraries, COM, plugin interfaces) — use error codes or result types at those seams.

## STL & Containers

- [INST0034] **Do** prefer `<algorithm>` functions (`std::find_if`, `std::transform`, `std::sort`) over hand-written loops — they express intent, are harder to get wrong, and are optimised by standard library implementations.
- [INST0035] **Do** use `std::vector` as the default sequential container — it has the best cache locality and is the right choice unless you need a different algorithmic guarantee.
- [INST0036] **Do** use `reserve()` when the final size of a vector is known or can be estimated — it avoids repeated reallocations.
- [INST0037] **Do** use `emplace_back` over `push_back` when constructing elements in place avoids a copy or move.

## Concurrency

- [INST0038] **Do** protect shared mutable state with `std::mutex` and `std::lock_guard` / `std::scoped_lock` — never lock and unlock manually.
- [INST0039] **Do** use `std::atomic<T>` for simple shared counters and flags instead of a full mutex.
- [INST0040] **Do** prefer higher-level concurrency abstractions (`std::async`, `std::jthread`, task queues) over raw `std::thread` when the threading model allows it.
- [INST0041] **Don't** access shared data from multiple threads without synchronisation — data races are undefined behaviour in C++.

## Documentation

- [INST0042] **Do** document every public class and function — parameters, return value, ownership transfers, exception guarantees, and thread-safety — in a comment above its declaration.
- [INST0043] **Don't** restate what the code already says — focus comments on *why* a decision was made, invariants, and non-obvious constraints.
