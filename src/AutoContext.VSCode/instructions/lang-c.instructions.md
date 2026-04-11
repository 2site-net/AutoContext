---
description: "C coding style instructions: naming conventions, memory management, pointer safety, const correctness, error handling, header design, and concurrency."
applyTo: "**/*.{c,h}"
---
# C Coding Style

## Naming

- [INST0001] **Do** use `snake_case` for type names (`typedef`, `struct` tags, `enum` tags) consistent with functions and variables; use SCREAMING_SNAKE_CASE for macros and compile-time constants.
- [INST0002] **Do** use snake_case for functions, variables, and struct members.
- [INST0003] **Do** prefix public API symbols with a short module or library tag to avoid name collisions — `json_parse()`, `http_request_new()`.
- [INST0004] **Don't** use names that start with an underscore followed by an uppercase letter or double underscores — those are reserved by the C standard and the implementation.

## Memory Management

- [INST0005] **Do** pair every `malloc`/`calloc`/`realloc` with a corresponding `free` on every code path — including early returns and error branches.
- [INST0006] **Do** set pointers to `NULL` after freeing them to prevent use-after-free and double-free bugs.
- [INST0007] **Do** use `calloc` instead of `malloc` + `memset` when zero-initialized memory is needed — `calloc` also guards against multiplication overflow.
- [INST0008] **Do** check the return value of `malloc`, `calloc`, and `realloc` — they return `NULL` on allocation failure.
- [INST0009] **Do** adopt an ownership convention — every allocated resource should have exactly one owner responsible for freeing it, documented clearly in function contracts.
- [INST0010] **Don't** cast the return value of `malloc` or `calloc` in C — the implicit `void *` conversion is safe and the cast can mask a missing `<stdlib.h>` include.

## Pointers & Const Correctness

- [INST0011] **Do** use `const` on pointer parameters that the function does not modify — `void process(const char *data, size_t len)`.
- [INST0012] **Do** use `const` on local variables and file-scope variables that never change after initialisation.
- [INST0013] **Do** prefer `size_t` for sizes, lengths, and array indices — it is the correct unsigned type for memory-related quantities.
- [INST0014] **Don't** perform arithmetic on `void *` pointers — cast to `char *` or the concrete type first, as `void *` arithmetic is a GCC extension and not standard C.
- [INST0015] **Don't** return pointers to stack-allocated local variables — the memory is invalid once the function returns.

## Error Handling

- [INST0016] **Do** use return codes for error signalling — return `0` or an enum value for success and a negative value or specific error code for failure.
- [INST0017] **Do** check the return value of every function that can fail — including I/O functions (`fopen`, `fread`, `fwrite`), system calls, and library functions.
- [INST0018] **Do** use a consistent cleanup pattern — either a single `goto cleanup` label at the end of the function or early-return with explicit cleanup to avoid duplicated teardown logic.
- [INST0019] **Do** set `errno` or use an output parameter for detailed error information when a return code alone is insufficient.
- [INST0020] **Don't** silently ignore errors — log, propagate, or handle every failure.

## Type Design

- [INST0021] **Do** use `typedef` for opaque types exposed through public APIs — `typedef struct JsonParser JsonParser;` — and keep the struct definition in the `.c` file.
- [INST0022] **Do** use `enum` with an explicit underlying sentinel for state machines and flag sets — e.g., `STATUS_COUNT` as the last value.
- [INST0023] **Do** use fixed-width integer types from `<stdint.h>` (`int32_t`, `uint64_t`) when the exact width matters — for serialisation, hardware registers, or wire protocols.
- [INST0024] **Do** use `bool` from `<stdbool.h>` for boolean values instead of plain `int` — it communicates intent clearly.
- [INST0025] **Don't** use magic numbers — define named constants with `enum` values or `static const` variables rather than `#define` when possible.

## Header Files

- [INST0026] **Do** use include guards in every header file — `#ifndef MODULE_NAME_H` / `#define MODULE_NAME_H` / `#endif` — or `#pragma once` if portability to exotic compilers is not required.
- [INST0027] **Do** include only what the header itself needs to compile — use forward declarations of structs to minimise transitive includes.
- [INST0028] **Do** wrap header contents in `extern "C" { }` guards (conditioned on `__cplusplus`) so C headers are safely included from C++ translation units.
- [INST0029] **Don't** define non-`inline` functions or mutable variables in header files — this causes multiple-definition linker errors when the header is included from more than one translation unit.

## Language Idioms

- [INST0030] **Do** use `static` for file-scoped functions and variables that are not part of the public API — it limits their linkage to the translation unit and prevents symbol collisions.
- [INST0031] **Do** use compound literals and designated initialisers (C99) for readable struct initialisation — `(Point){ .x = 1, .y = 2 }`.
- [INST0032] **Do** use `snprintf` instead of `sprintf` and `strncpy` or `strlcpy` instead of `strcpy` — always supply a buffer size to prevent overflows.
- [INST0033] **Do** use `sizeof(variable)` rather than `sizeof(Type)` in `malloc` and `memcpy` calls — it stays correct when the variable's type changes.
- [INST0034] **Don't** use variable-length arrays (VLAs) in production code — they can cause stack overflows with untrusted sizes and are optional since C11.

## Concurrency

- [INST0035] **Do** protect shared mutable state with a mutex or other synchronisation primitive — use POSIX `pthread_mutex_t` or C11 `mtx_t`.
- [INST0036] **Do** use `_Atomic` qualifiers or `<stdatomic.h>` operations for lock-free shared counters and flags (C11 and later).
- [INST0037] **Don't** access shared data from multiple threads without synchronisation — even seemingly benign races are undefined behaviour in C.

## Documentation

- [INST0038] **Do** document every public function's contract — parameters, return value, ownership semantics, and thread-safety guarantees — in a comment above its declaration in the header file.
- [INST0039] **Don't** restate what the code already says — focus comments on *why* a decision was made, not *what* the code does.
