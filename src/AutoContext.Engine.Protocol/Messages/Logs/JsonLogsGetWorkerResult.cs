namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union response of the
/// <see cref="LogsMethods.GetWorker"/> request. The discriminator is
/// the <c>kind</c> JSON property: <c>ok</c>
/// (<see cref="JsonLogsGetWorkerOkResult"/>) carries the bounded
/// snapshot; <c>not-found</c>
/// (<see cref="JsonLogsGetWorkerNotFoundResult"/>) carries just the
/// worker id.
/// </summary>
/// <remarks>
/// <para>
/// <c>not-found</c> distinguishes "this <c>workerId</c> is not a
/// worker the current engine has ever spawned" from an <c>ok</c>
/// result with an empty <c>records</c> array ("a real worker that
/// simply hasn't logged yet") — a CLI subcommand or tree-view
/// tooltip needs to tell the two apart. The engine variant
/// (<see cref="JsonLogsGetEngineResult"/>) has no <c>not-found</c>
/// arm because the engine's own log file always exists for the
/// current process. See <c>design § P2</c> and
/// <c>§ RPC surface</c> (<c>Logs.*</c>).
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonLogsGetWorkerOkResult), typeDiscriminator: "ok")]
[JsonDerivedType(typeof(JsonLogsGetWorkerNotFoundResult), typeDiscriminator: "not-found")]
public abstract record JsonLogsGetWorkerResult;
