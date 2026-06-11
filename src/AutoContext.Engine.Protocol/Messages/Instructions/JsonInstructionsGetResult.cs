namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union response of the
/// <see cref="InstructionsMethods.Get"/> request. The discriminator is
/// the <c>kind</c> JSON property: <c>ok</c>
/// (<see cref="JsonInstructionsGetOkResult"/>) carries the projected
/// body; <c>disabled</c>
/// (<see cref="JsonInstructionsGetDisabledResult"/>) is identity-only;
/// <c>not-found</c> (<see cref="JsonInstructionsGetNotFoundResult"/>)
/// carries just the name.
/// </summary>
/// <remarks>
/// <para>
/// The <c>disabled</c> arm is the reason <c>Get</c> is not a nullable
/// string: LM tools need to tell the model "this rule exists but the
/// user muted it" without leaking the body, description, or version —
/// otherwise the model could quote the muted rule back and route
/// around the user's choice. <c>not-found</c> is strictly distinct
/// (the name was never in the corpus, no user policy involved). See
/// <c>design § P2</c> and <c>§ Instructions.Get distinguishes disabled
/// from not-found</c>.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonInstructionsGetOkResult), typeDiscriminator: "ok")]
[JsonDerivedType(typeof(JsonInstructionsGetDisabledResult), typeDiscriminator: "disabled")]
[JsonDerivedType(typeof(JsonInstructionsGetNotFoundResult), typeDiscriminator: "not-found")]
public abstract record JsonInstructionsGetResult;
