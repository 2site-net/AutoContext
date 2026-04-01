namespace SharpPilot.WorkspaceServer;

/// <summary>
/// JSON-serializable request sent to the workspace service over the named pipe.
/// </summary>
/// <param name="FilePath">Absolute path to the file whose effective .editorconfig properties should be resolved.</param>
/// <param name="Keys">Optional subset of property keys to return. When empty or null, all properties are returned.</param>
internal sealed record EditorConfigRequest(string FilePath, string[]? Keys = null);

/// <summary>
/// JSON-serializable response returned by the workspace service over the named pipe.
/// </summary>
/// <param name="Properties">The resolved key-value pairs that apply to the requested file.</param>
internal sealed record EditorConfigResponse(Dictionary<string, string> Properties);
