namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union response of the
/// <see cref="InstructionsMethods.GetRaw"/> request, mirroring
/// <see cref="JsonInstructionsGetResult"/> but with no <c>disabled</c>
/// arm — disabled state is irrelevant to a source-file read. The
/// discriminator is the <c>kind</c> JSON property: <c>ok</c>
/// (<see cref="JsonInstructionsGetRawOkResult"/>) carries the
/// source-faithful bytes; <c>not-found</c>
/// (<see cref="JsonInstructionsGetRawNotFoundResult"/>) carries just
/// the name.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonInstructionsGetRawOkResult), typeDiscriminator: "ok")]
[JsonDerivedType(typeof(JsonInstructionsGetRawNotFoundResult), typeDiscriminator: "not-found")]
public abstract record JsonInstructionsGetRawResult;
