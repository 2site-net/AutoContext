namespace AutoContext.Framework.Logging.Tests.Support;

using System.Text.Json.Nodes;

using static AutoContext.Framework.Tests.Support.Encodings.TestEncodings;

/// <summary>
/// Reads a <see cref="LoggingClient"/>'s JSON-lines wire output —
/// one greeting line followed by N record lines — back into parsed
/// <see cref="JsonNode"/> trees for assertion. Caps the wait at
/// 5 seconds so a silent client cannot hang the test.
/// </summary>
internal static class LoggingClientTestReader
{
    public static async Task<(JsonNode? Greeting, List<JsonNode?> Records)> ReadLinesAsync(
        Stream stream,
        int expected,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Utf8NoBom, leaveOpen: true);
        var lines = new List<string>();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        for (var i = 0; i < expected; i++)
        {
            var line = await reader.ReadLineAsync(timeoutCts.Token).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }
            lines.Add(line);
        }

        Assert.Equal(expected, lines.Count);

        var greeting = JsonNode.Parse(lines[0]);
        var records = lines.Skip(1).Select(line => JsonNode.Parse(line)).ToList();
        return (greeting, records);
    }
}
