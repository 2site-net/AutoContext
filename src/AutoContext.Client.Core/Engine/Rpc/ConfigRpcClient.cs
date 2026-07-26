namespace AutoContext.Client.Core.Engine.Rpc;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Serialization;

/// <summary>
/// Typed client for the engine's <c>Config.*</c> RPC family over a
/// live <see cref="EngineConnection"/>. Reads the current
/// <c>.autocontext.json</c> snapshot and flips per-file or per-rule
/// disabled state; every mutation returns the resulting snapshot so
/// callers see the applied state without a follow-up read. The
/// streaming <c>Config.Subscribe</c> channel is a separate consumer
/// (<c>Subscriptions.ConfigSubscription</c>), since it needs a
/// dedicated connection.
/// </summary>
public sealed class ConfigRpcClient
{
    private readonly EngineConnection _connection;

    /// <summary>
    /// Creates a new <see cref="ConfigRpcClient"/> over
    /// <paramref name="connection"/>.
    /// </summary>
    /// <param name="connection">A live, handshaked <c>rpc</c>
    /// connection. Must not be <see langword="null"/>.</param>
    public ConfigRpcClient(EngineConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Reads the current config snapshot the engine holds in memory.
    /// </summary>
    public Task<JsonConfigSnapshot> GetAsync(CancellationToken cancellationToken)
        => _connection.InvokeAsync(
            ConfigMethods.Get,
            parameters: null,
            ProtocolJsonContext.Default.JsonConfigSnapshot,
            cancellationToken);

    /// <summary>
    /// Flips the whole-file disabled state of the instructions file
    /// named <paramref name="name"/> and returns the resulting
    /// snapshot.
    /// </summary>
    /// <param name="name">Instructions file name to toggle. Must not
    /// be <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonConfigSnapshot> ToggleFileAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonConfigToggleFileParams { Name = name },
            ProtocolJsonContext.Default.JsonConfigToggleFileParams);

        return _connection.InvokeAsync(
            ConfigMethods.ToggleFile,
            parameters,
            ProtocolJsonContext.Default.JsonConfigSnapshot,
            cancellationToken);
    }

    /// <summary>
    /// Flips the disabled state of the rule <paramref name="ruleId"/>
    /// within the instructions file <paramref name="name"/> and returns
    /// the resulting snapshot.
    /// </summary>
    /// <param name="name">Owning instructions file name. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="ruleId">Rule id within the file. Must not be
    /// <see langword="null"/> or empty.</param>
    /// <param name="cancellationToken">Cancellation for the call.</param>
    public Task<JsonConfigSnapshot> ToggleRuleAsync(
        string name, string ruleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(ruleId);

        var parameters = JsonSerializer.SerializeToElement(
            new JsonConfigToggleRuleParams { Name = name, RuleId = ruleId },
            ProtocolJsonContext.Default.JsonConfigToggleRuleParams);

        return _connection.InvokeAsync(
            ConfigMethods.ToggleRule,
            parameters,
            ProtocolJsonContext.Default.JsonConfigSnapshot,
            cancellationToken);
    }
}
