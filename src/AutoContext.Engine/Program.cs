namespace AutoContext.Engine;

using AutoContext.Engine.Core;

/// <summary>
/// <c>autocontext-engine</c> binary entry point. Wires
/// <see cref="EngineCommand"/> to the
/// <see cref="System.CommandLine"/> parser, prefixes every parser
/// or role diagnostic with the <c>autocontext-engine: </c> stderr
/// prefix, hands <c>--version</c>/<c>--help</c> back to the parser's
/// built-in actions, and dispatches successful parses to either
/// <see cref="DaemonHostFactory"/> or
/// <see cref="McpServerHostFactory"/>.
/// </summary>
internal static class Program
{
    private const string DiagnosticPrefix = "autocontext-engine: ";

    public static async Task<int> Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = new EngineCommand();
        var parseResult = command.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            await WriteErrorsAsync(parseResult.Errors.Select(e => e.Message))
                .ConfigureAwait(false);
            return 1;
        }

        // --help / --version (and any other built-in action SCL added)
        // are owned by the parser. Errors-clear-before-action options
        // such as VersionAction set ClearsParseErrors = true, so they
        // arrive here with Errors empty and Action non-null.
        if (parseResult.Action is not null)
        {
            return await parseResult.InvokeAsync().ConfigureAwait(false);
        }

        if (!command.TryBuildOptions(parseResult, out var options, out var error))
        {
            await WriteErrorsAsync([error!]).ConfigureAwait(false);
            return 1;
        }

        return options.McpServerMode == EngineMcpServerMode.WithStdio
            ? await McpServerHostFactory.RunAsync(options).ConfigureAwait(false)
            : await DaemonHostFactory.RunAsync(options).ConfigureAwait(false);
    }

    private static async Task WriteErrorsAsync(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            await Console.Error.WriteLineAsync(DiagnosticPrefix + message).ConfigureAwait(false);
        }
    }
}
