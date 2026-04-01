# Expand EditorConfig Property Coverage

## Summary

Extend the EditorConfig integration beyond the two currently resolved
properties (`csharp_prefer_braces`, `csharp_style_namespace_declarations`) to
cover a wider set of standard C# EditorConfig properties — turning checkers
that today apply fixed, hardcoded opinions into checkers that enforce whatever
the project's `.editorconfig` specifies.

## Current Approach

`check_csharp_all` accepts an optional `editorConfigFilePath` argument. When
provided, it queries the `SharpPilot.WorkspaceServer` named-pipe service to
resolve the effective properties for that file, then passes the key-value
dictionary to each checker via its `IReadOnlyDictionary<string, string>? data`
parameter.

Two checkers currently implement `IEditorConfigFilter` and declare which keys
they consume:

- `CSharpCodingStyleChecker` reads `csharp_prefer_braces` to determine
  whether to flag missing braces, unnecessary braces, or both;
  `dotnet_sort_system_directives_first` to enforce using-directive ordering;
  and `csharp_style_expression_bodied_methods` /
  `csharp_style_expression_bodied_properties` to validate expression-body
  usage per member type.
- `CSharpProjectStructureChecker` reads `csharp_style_namespace_declarations`
  to enforce file-scoped or block-scoped namespaces.

All other checkers ignore the `data` parameter entirely — their rules are
hardcoded regardless of what the project's `.editorconfig` says.

## The Problem

Hardcoded rules contradict the core design principle: *EditorConfig wins*.
When `check_csharp_naming_conventions` flags `_camelCase` private fields
unconditionally, it silently disagrees with a project that has configured
`dotnet_naming_rule.*` to require a different convention. The checker produces
violations that aren't actually violations for that project.

The same friction applies to using-directive ordering, expression-body
preferences, formatting rules, and more — checkers enforce one opinion while
the project may have explicitly chosen another.

## Proposed Additions

### `dotnet_naming_rule.*` — Naming Conventions

The naming conventions checker currently hardcodes the following:

- Interfaces → `I`-prefix
- Extension classes → `Extensions`-suffix
- Async methods → `Async`-suffix
- Private instance fields → `_camelCase`
- Types, methods, properties, events → PascalCase
- Parameters → camelCase

The `.editorconfig` `dotnet_naming_rule.*` / `dotnet_naming_style.*` /
`dotnet_naming_symbols.*` family allows teams to define their own conventions.
A project might omit the `_` prefix for private fields, use `s_` for statics,
or specify different casing for local functions.

**Proposed change:** Read the relevant `dotnet_naming_*` keys and use them to
drive the naming checker instead of the hardcoded defaults. Where a naming rule
is not configured in `.editorconfig`, fall back to the current hardcoded
behaviour.

### `dotnet_sort_system_directives_first` — Using Directive Ordering

> **Implemented.** `CSharpCodingStyleChecker` now reads this property and
> flags out-of-order `System.*` directives when the value is `true`.

### `csharp_style_expression_bodied_*` — Expression-Body Usage

> **Implemented.** `CSharpCodingStyleChecker` now reads
> `csharp_style_expression_bodied_methods` and
> `csharp_style_expression_bodied_properties`. When set to
> `when_on_single_line` or `true`, it flags multi-line members that could be
> expression-bodied. When set to `never` or `false`, it flags
> expression-bodied members that should use block bodies.

### `csharp_new_line_*` / `csharp_indent_*` — Formatting

Properties such as `csharp_new_line_before_open_brace`,
`csharp_new_line_before_else`, and `csharp_indent_case_contents` push brace
and indentation style into territory currently handled by the style checker's
fixed rules. Aligning enforcement with these properties would make the checker
consistent with projects that use Allman or K&R brace style.

## Implementation Notes

Each checker that gains EditorConfig awareness should:

1. Implement `IEditorConfigFilter` and declare the keys it consumes in
   `EditorConfigKeys`. This is used to filter and pass only the relevant
   subset of the resolved properties.
2. Treat a missing or unrecognised key value as "use the hardcoded default"
   so existing behaviour is preserved when `editorConfigFilePath` is not
   provided or the property is absent from the `.editorconfig`.
3. Add test cases for each new property path: the EditorConfig value present
   and matching, present and conflicting, and absent.

The `dotnet_naming_rule.*` group is the most complex because it uses a
three-table structure (`naming_rule`, `naming_symbols`, `naming_style`) that
requires cross-referencing multiple keys. This should be tackled separately
from the simpler boolean/enum properties.
