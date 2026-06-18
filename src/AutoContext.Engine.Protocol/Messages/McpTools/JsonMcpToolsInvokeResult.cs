namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union response of the
/// <see cref="McpToolsMethods.Invoke"/> request. The discriminator is
/// the <c>kind</c> JSON property: <c>ok</c>
/// (<see cref="JsonMcpToolsInvokeOkResult"/>) and <c>tool-error</c>
/// (<see cref="JsonMcpToolsInvokeToolErrorResult"/>) both carry the
/// worker's content blocks; <c>schema-error</c>
/// (<see cref="JsonMcpToolsInvokeSchemaErrorResult"/>) carries the
/// argument-validation failures; <c>disabled</c>
/// (<see cref="JsonMcpToolsInvokeDisabledResult"/>) and <c>not-found</c>
/// (<see cref="JsonMcpToolsInvokeNotFoundResult"/>) are identity-only.
/// </summary>
/// <remarks>
/// <para>
/// The shape mirrors <c>Instructions.Get</c>'s envelope (<c>design §
/// P2</c>): <c>tool-error</c> ("the tool ran and reported failure") is
/// strictly distinct from <c>disabled</c> / <c>not-found</c> ("the
/// engine refused to dispatch"), and the identity-only arms leak
/// nothing beyond the queried name so a model cannot route around the
/// user's mute. See <c>design § RPC surface (McpTools.*)</c>.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonMcpToolsInvokeOkResult), typeDiscriminator: "ok")]
[JsonDerivedType(typeof(JsonMcpToolsInvokeToolErrorResult), typeDiscriminator: "tool-error")]
[JsonDerivedType(typeof(JsonMcpToolsInvokeSchemaErrorResult), typeDiscriminator: "schema-error")]
[JsonDerivedType(typeof(JsonMcpToolsInvokeDisabledResult), typeDiscriminator: "disabled")]
[JsonDerivedType(typeof(JsonMcpToolsInvokeNotFoundResult), typeDiscriminator: "not-found")]
public abstract record JsonMcpToolsInvokeResult;
