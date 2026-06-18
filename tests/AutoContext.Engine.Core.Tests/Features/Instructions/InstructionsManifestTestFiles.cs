namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// Shared, valid two-file instruction side-car fixtures for the manifest
/// loader and service tests: a generated <c>instructions-manifest.json</c>
/// fact index covering one always-attached file (no <c>applyTo</c>, a
/// single section) and one filtered file (an <c>applyTo</c>, an extension
/// set, a nested section), plus a hand-authored
/// <c>instructions-catalog.json</c> that declares the always-attached
/// file and catalogs only the filtered one.
/// </summary>
internal static class InstructionsManifestTestFiles
{
    public const string ManifestJson =
        """
        {
          "schemaVersion": "1",
          "instructions": [
            {
              "key": "autocontext",
              "fileName": "autocontext.instructions.md",
              "name": "autocontext (v1.0.0)",
              "version": "1.0.0",
              "description": "Always attached.",
              "hasChangelog": false,
              "contentHash": "sha256:aaa",
              "sections": [
                { "heading": "Intro", "anchor": "intro" }
              ]
            },
            {
              "key": "docker",
              "fileName": "docker.instructions.md",
              "name": "docker (v1.0.0)",
              "version": "1.0.0",
              "description": "Docker rules.",
              "applyTo": "**/Dockerfile*",
              "extensions": [ "yml", "yaml" ],
              "hasChangelog": true,
              "contentHash": "sha256:bbb",
              "sections": [
                { "heading": "Build", "anchor": "build" },
                { "heading": "Stages", "anchor": "build-stages", "parent": "Build" }
              ]
            }
          ]
        }
        """;

    public const string CatalogJson =
        """
        {
          "schemaVersion": "1",
          "alwaysAttached": [ "autocontext.instructions.md" ],
          "categories": [
            { "name": "Tools", "description": "Developer tooling and platform conventions." }
          ],
          "instructions": [
            {
              "label": "Docker",
              "fileName": "docker.instructions.md",
              "category": "Tools",
              "activationFlags": [ "hasDocker" ]
            }
          ]
        }
        """;

    public static void WriteValid(string directory)
    {
        WriteManifest(directory, ManifestJson);
        WriteCatalog(directory, CatalogJson);
    }

    public static void WriteManifest(string directory, string json)
        => File.WriteAllText(
            Path.Combine(directory, InstructionsManifestLoader.ManifestFileName), json);

    public static void WriteCatalog(string directory, string json)
        => File.WriteAllText(
            Path.Combine(directory, InstructionsManifestLoader.CatalogFileName), json);
}
