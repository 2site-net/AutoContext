---
description: "Lua coding style instructions: naming conventions, tables, error handling, functions, modules, metatables, performance, and documentation."
applyTo: "**/*.lua"
---
# Lua Coding Style

## Naming

- [INST0001] **Do** use `snake_case` for local variables, functions, and module-level names; use `PascalCase` for classes and constructors (e.g., `Player.new()`); use `SCREAMING_SNAKE_CASE` for constants.
- [INST0002] **Do** prefix private module members with an underscore — `local function _validate(input)` — to signal they are not part of the public API.
- [INST0003] **Don't** use single-character variable names outside of short loops (`for i = 1, n`) or mathematical formulas — descriptive names improve maintainability.

## Tables

- [INST0004] **Do** use tables as the primary data structure — they serve as arrays, dictionaries, objects, and namespaces in Lua.
- [INST0005] **Do** use the array part of a table (integer keys starting at 1) for ordered sequences and the hash part for named fields — mixing both in one table is fine for argument/config tables, but avoid inserting arbitrary integer keys into a table that also serves as a sequence.
- [INST0006] **Do** initialise tables with constructor syntax `{ field = value }` rather than creating an empty table and assigning fields one by one.
- [INST0007] **Do** use `#t` for the length of sequence tables — be aware it is only reliable for arrays without gaps (nil holes).
- [INST0008] **Don't** use `table.getn` or `table.setn` — they are deprecated since Lua 5.1; use the `#` operator and direct assignment instead.

## Error Handling

- [INST0009] **Do** use `pcall` or `xpcall` to catch errors at API boundaries — Lua errors propagate by unwinding the stack, so unprotected calls crash the program.
- [INST0010] **Do** return `nil, err_message` from functions that can fail in expected ways — this is the idiomatic Lua pattern for recoverable errors.
- [INST0011] **Do** use `error()` with a descriptive message (and optional level argument) for programming errors and violated preconditions — `error("expected string, got " .. type(value), 2)`.
- [INST0012] **Do** use `xpcall` with a message handler to capture stack traces on error — `pcall` alone discards traceback information.
- [INST0013] **Don't** use `error()` for expected runtime failures like missing files or network timeouts — return `nil, err` so callers can handle them without `pcall`.

## Functions

- [INST0014] **Do** use `local function name()` instead of `local name = function()` for named functions — it enables self-referencing and produces clearer stack traces.
- [INST0015] **Do** keep functions short and focused — if a function exceeds ~50 lines, break it into smaller local helper functions.
- [INST0016] **Do** use multiple return values for functions that produce a result and a status — `return value, nil` on success, `return nil, err` on failure.
- [INST0017] **Do** use varargs (`...`) sparingly and document expected arguments — prefer explicit parameter names for clarity.
- [INST0018] **Don't** rely on global function definitions — always use `local function` and pass dependencies explicitly or through module tables.

## Modules

- [INST0019] **Do** structure modules as a local table with functions, returned at the end — `local M = {} ... return M` — this is the standard Lua module pattern.
- [INST0020] **Do** declare all module-internal helpers as `local` above the module table — they stay private and are not accessible to consumers.
- [INST0021] **Do** use `require` for loading dependencies and cache the result in a local — `local json = require("cjson")`.
- [INST0022] **Don't** use `module()` (deprecated since Lua 5.2) or pollute the global environment — always return the module table explicitly.
- [INST0023] **Don't** modify other modules' tables or the global table from within a module — side-effects make dependency tracking impossible.

## Metatables & OOP

- [INST0024] **Do** use metatables with `__index` for prototype-based OOP — define a class table, set its `__index` to itself, and provide a `new` constructor that calls `setmetatable`.
- [INST0025] **Do** use `__tostring` to provide a human-readable representation for custom types — it is used by `tostring()` and `print()`.
- [INST0026] **Do** use `__gc` (Lua 5.2+ for tables) or weak tables for cleanup of resources held by Lua objects — ensure file handles, sockets, and native resources are released.
- [INST0027] **Don't** overload metamethods (`__add`, `__eq`, `__lt`, etc.) unless the type genuinely models a mathematical or comparable concept — surprising operator behaviour harms readability.

## Performance & Idioms

- [INST0028] **Do** localise frequently used global functions — `local insert = table.insert` — local lookups are significantly faster than global/table chain lookups in hot paths.
- [INST0029] **Do** pre-size tables with `table.new(narray, nhash)` (LuaJIT) or `table.create(n, value)` (Luau) or by initialising with known elements — it avoids rehashing during growth.
- [INST0030] **Do** use `table.concat` for building strings from many parts — repeated `..` concatenation creates intermediate strings and pressures the garbage collector.
- [INST0031] **Do** use `ipairs` for iterating array-part sequences and `pairs` for iterating all key-value entries — `ipairs` stops at the first nil, `pairs` visits every entry.
- [INST0032] **Don't** create closures or tables inside tight loops when the same object can be reused — each allocation adds GC pressure.

## Documentation

- [INST0033] **Do** write a header comment at the top of each module file describing its purpose and public API.
- [INST0034] **Do** document function parameters, return values, and error conditions with `---` (LDoc/EmmyLua) annotations — they enable IDE tooling and auto-completion.
- [INST0035] **Don't** restate what the code already says — focus comments on *why* a decision was made, non-obvious behaviour, and module contracts.
