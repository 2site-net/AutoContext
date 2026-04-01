# Expand EditorConfig Property Coverage

## Summary

Extend the EditorConfig integration beyond the two currently resolved
properties (`csharp_prefer_braces`, `csharp_style_namespace_declarations`) to
cover a wider set of standard C# EditorConfig properties — turning checkers
that today apply fixed, hardcoded opinions into checkers that enforce whatever
the project's `.editorconfig` specifies.

## Current Approach

`check_csharp_all` accepts an optional `editorConfigFilePath` argument. When
provided, it queries the `SharpPilot.EditorConfig` named-pipe service to
resolve the effective properties for that file, then passes the key-value
dictionary to each checker via its `IReadOnlyDictionary<string, string>? data`
parameter.

Two checkers currently implement `IEditorConfigFilter` and declare which keys
they consume:

- `CSharpCodingStyleChecker` reads `csharp_prefer_braces` to determine
  whether to flag missing braces, unnecessary braces, or both.
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

No checker currently validates `using` directive ordering. The coding style
checker could flag out-of-order `System.*` directives when
`dotnet_sort_system_directives_first = true` is set.

### `csharp_style_expression_bodied_*` — Expression-Body Usage

The coding style checker flags `=>` arrow placement (arrow must be on a new
line) but does not validate *whether* expression bodies should be used at all.
The `csharp_style_expression_bodied_methods`,
`csharp_style_expression_bodied_properties`, and related properties control
this per member type.

**Proposed change:** When these properties are set to `when_on_single_line`
or `always`, flag multi-line members that could be expression-bodied. When
set to `never`, flag expression-bodied members that should use block bodies.

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
