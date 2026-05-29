namespace AutoContext.Engine.Core.Tests.Support.Rpc;

using System.Text.Json;

/// <summary>
/// Converts synchronous sequences into <see cref="IAsyncEnumerable{T}"/>
/// for tests that drive streaming RPC handlers.
/// </summary>
internal static class AsyncEnumerableConverter
{
    /// <summary>
    /// Wraps a synchronous <see cref="IEnumerable{T}"/> as an
    /// <see cref="IAsyncEnumerable{T}"/> that yields each element
    /// after a <see cref="Task.Yield"/>, forcing an asynchronous
    /// hop between items so the consumer sees one frame at a time.
    /// </summary>
    public static async IAsyncEnumerable<JsonElement> FromEnumerable(IEnumerable<JsonElement> source)
    {
        foreach (var item in source)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
