---
name: "lang-python (v1.0.0)"
description: "Python coding style instructions: naming conventions, type hints, error handling, functions, classes, modules, concurrency, and documentation."
applyTo: "**/*.py"
---

# Python Coding Instructions

## MCP Tool Validation

No corresponding MCP tool is currently available to automatically
validate this instructions file — apply it manually.

## Rules

### Naming

- [INST0001] **Do** use `snake_case` for functions, methods, variables, and module names; use `PascalCase` for class names; use `SCREAMING_SNAKE_CASE` for module-level constants.
- [INST0002] **Do** prefix private attributes and methods with a single underscore (`_internal_method`) — use double underscores (`__name`) only when name-mangling is needed to avoid collisions in subclasses.
- [INST0003] **Don't** use single-character variable names outside of comprehensions, lambdas, or very short scopes — descriptive names improve readability.

### Type Hints

- [INST0004] **Do** annotate all function signatures (parameters and return types) with type hints — use `-> None` for functions that return nothing.
- [INST0005] **Do** use built-in generic types (`list[str]`, `dict[str, int]`, `tuple[int, ...]`) instead of their `typing` equivalents (`List`, `Dict`, `Tuple`) on Python 3.9+.
- [INST0006] **Do** use `X | Y` union syntax instead of `Union[X, Y]` and `X | None` instead of `Optional[X]` on Python 3.10+.
- [INST0007] **Don't** annotate every local variable — let type inference handle locals unless the type is non-obvious or the variable is initialised to `None`.

### Error Handling

- [INST0008] **Do** catch specific exception types — `except ValueError:` or `except (KeyError, IndexError):` — never bare `except:` or `except Exception:` without a compelling reason.
- [INST0009] **Do** use `raise ... from err` to chain exceptions and preserve the original traceback — never silently swallow the cause.
- [INST0010] **Do** define custom exception classes that inherit from a project-specific base exception (itself inheriting `Exception`) for domain errors that callers need to distinguish.
- [INST0011] **Do** use context managers (`with` statements) for resource management — files, locks, database connections, and network sessions should always be released deterministically.
- [INST0012] **Don't** catch `KeyError`, `AttributeError`, or `IndexError` when a direct accessor with a default exists — prefer `dict.get(key, default)`, `getattr(obj, name, default)`, or `collections.defaultdict`; EAFP is idiomatic Python for operations without a clean look-before-you-leap alternative, not for cases where one is readily available.

### Functions & Methods

- [INST0013] **Do** keep functions short and focused on a single task — if a function needs a comment to separate logical sections, extract those sections into helper functions.
- [INST0014] **Do** use keyword-only arguments (after `*`) for functions with more than two or three parameters — it prevents positional-argument mistakes.
- [INST0015] **Do** return early to avoid deep nesting — guard clauses at the top of a function improve readability.
- [INST0016] **Don't** use mutable default arguments (`def f(items=[])`) — use `None` as the default and create the mutable object inside the function body.
- [INST0017] **Don't** write functions that return different types depending on a flag — use separate functions or overloads instead.

### Classes & OOP

- [INST0018] **Do** use `@dataclass` (or `@dataclass(frozen=True)` for immutable values) for classes that are primarily data containers — it generates `__init__`, `__repr__`, and `__eq__` automatically.
- [INST0019] **Do** prefer composition over inheritance — use protocols (`typing.Protocol`) for structural subtyping rather than deep class hierarchies.
- [INST0020] **Do** use `@property` for computed attributes that should look like simple attribute access — avoid getter/setter methods that add no validation or logic.
- [INST0021] **Do** use `__slots__` on performance-critical classes with fixed attributes — it reduces memory usage and speeds up attribute access.
- [INST0022] **Don't** use multiple inheritance except for mixin classes — keep the MRO simple and predictable.

### Modules & Imports

- [INST0023] **Do** group imports in three blocks separated by blank lines — standard library, third-party packages, local/project imports — and sort alphabetically within each block.
- [INST0024] **Do** use absolute imports (`from mypackage.utils import helper`) over relative imports — relative imports are acceptable only within a package's internal modules.
- [INST0025] **Do** import modules rather than individual names when the module provides namespace clarity — `import os.path` reads better than `from os.path import join, exists, dirname`.
- [INST0026] **Don't** use wildcard imports (`from module import *`) — they pollute the namespace and make it impossible to trace where names come from.
- [INST0027] **Don't** put import statements inside functions unless lazy loading is genuinely needed to avoid circular imports or reduce startup time.

### Concurrency

- [INST0028] **Do** use `asyncio` for I/O-bound concurrency — `async`/`await` composes cleanly and avoids callback spaghetti.
- [INST0029] **Do** use `concurrent.futures.ThreadPoolExecutor` for blocking I/O in otherwise synchronous code and `ProcessPoolExecutor` for CPU-bound parallelism.
- [INST0030] **Do** use `asyncio.gather` or `asyncio.TaskGroup` (Python 3.11+) to run independent coroutines concurrently — await them together rather than sequentially.
- [INST0031] **Don't** mix synchronous blocking calls inside `async` functions — use `asyncio.to_thread` or `loop.run_in_executor` to bridge synchronous and asynchronous code.

### Performance & Idioms

- [INST0032] **Do** use comprehensions (`[x for x in items if x > 0]`) over `map`/`filter` with lambdas — they are more readable and often faster.
- [INST0033] **Do** use generators (`yield`) and generator expressions for large sequences — they avoid materialising the entire collection in memory.
- [INST0034] **Do** use `str.join` for concatenating many strings — `"".join(parts)` is O(n) while repeated `+=` is O(n²).
- [INST0035] **Do** use `collections` module types (`defaultdict`, `Counter`, `deque`) when they fit the use case — they are optimised for their purpose.
- [INST0036] **Don't** use `type()` for type checking — use `isinstance()` which respects inheritance and supports union checks.

### Documentation

- [INST0037] **Do** write docstrings for all public modules, classes, and functions — use a consistent style (Google, NumPy, or Sphinx) across the project.
- [INST0038] **Do** include parameter descriptions, return values, and raised exceptions in docstrings for public functions.
- [INST0039] **Don't** restate what the code already says — focus docstrings on *why* a decision was made, invariants, and non-obvious behaviour.
