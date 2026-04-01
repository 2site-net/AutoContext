namespace SharpPilot.WorkspaceServer;

using System.Text.Json;

internal sealed class Program
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task Main(string[] args)
    {
        var pipeName = GetRequiredArg(args, "--pipe");

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Signal readiness — the extension reads this to know the pipe is active.
        var readyMessage = JsonSerializer.Serialize(new { pipe = pipeName }, s_jsonOptions);
        Console.WriteLine(readyMessage);

        var service = new WorkspaceService(pipeName, cts.Token);

        await service.RunAsync().ConfigureAwait(false);
    }

    private static string GetRequiredArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        throw new ArgumentException($"Missing required argument: {name}");
    }
}
