namespace AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// The terminal frame of a successfully completed stream. Carries
/// no payload — its sole purpose is to mark end-of-stream so the
/// client can stop reading without relying on connection close.
/// </summary>
public sealed record JsonRpcStreamComplete : JsonRpcStreamFrame;
