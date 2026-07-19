namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union response of the
/// <see cref="InstructionsMethods.SearchByMetadata"/> request. The
/// discriminator is the <c>kind</c> JSON property: <c>ok</c>
/// (<see cref="JsonInstructionsSearchByMetadataOkResult"/>) carries the matched
/// rows; <c>error</c>
/// (<see cref="JsonInstructionsSearchByMetadataErrorResult"/>) carries the
/// structured predicate fault plus the recognised-field schema so the model
/// caller can correct the predicate without an extra round-trip.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonInstructionsSearchByMetadataOkResult), typeDiscriminator: "ok")]
[JsonDerivedType(typeof(JsonInstructionsSearchByMetadataErrorResult), typeDiscriminator: "error")]
public abstract record JsonInstructionsSearchByMetadataResult;
